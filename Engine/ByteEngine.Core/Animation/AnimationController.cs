using System.Numerics;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Animation;

public enum LocomotionState
{
    Idle,
    Walk,
    Run,
    Jump,
    Fall,
    Land
}

public enum LocomotionDirection
{
    None,
    Forward,
    Backward,
    Left,
    Right
}

internal enum NamedActionRequestDecision
{
    KeepCurrent,
    RestartSame,
    InterruptCurrent,
    QueueRequested,
    Reject
}
/// <summary>
/// Character animation playback driver.
///
/// C8C keeps all existing C5-C7 playback/root-motion behaviour but adds one
/// optional unified Animation Profile asset. When assigned, the profile is the
/// source of truth for locomotion settings while gameplay continues to talk to
/// this one AnimationController.
/// </summary>
public sealed partial class AnimationController : Component
{
    private readonly List<SkeletalMeshRenderer> _renderers = new();

    private readonly HashSet<SkeletalMeshRenderer> _actionRenderers =
        new();

    private bool _stateInitialized;
    private bool _actionActive;
    private bool _actionPaused;
    private string _currentAction = string.Empty;
    private string _queuedAction = string.Empty;
    private AnimationActionProfile? _currentNamedAction;
    private readonly Dictionary<string, AnimationActionProfile> _namedActions = new(StringComparer.OrdinalIgnoreCase);
    private string _currentLocomotionClip = string.Empty;
    private float _actionPlaybackRate = 1.0f;

    private SkeletalMeshRenderer? _rootMotionSource;
    private string _rootMotionAnimation = string.Empty;
    private Vector3 _lastRootMotionTravel;
    private Vector3 _lastAppliedRootMotionDelta;
    private float _lastRootMotionTime;
    private bool _rootMotionSampleValid;

    /*
     * C10 runtime event cursor.
     *
     * One renderer is the event authority for the controller so characters with
     * several synchronized skeletal renderers never emit duplicate gameplay
     * events. Event snapshots are rebuilt only when the active clip changes.
     */
    private SkeletalMeshRenderer? _animationEventSource;
    private ImportedAnimation? _animationEventClip;
    private string _animationEventName = string.Empty;
    private AnimationEventMarker[] _animationEventSnapshot =
        Array.Empty<AnimationEventMarker>();
    private readonly List<AnimationEventCrossingHit> _animationEventCrossings =
        new(8);
    private float _animationEventPreviousTime;
    private long _animationEventLoopIndex;
    private bool _animationEventSampleValid;
    private AnimationWindowBoundary[] _animationWindowSnapshot =
        Array.Empty<AnimationWindowBoundary>();
    private readonly List<AnimationWindowCrossingHit> _animationWindowCrossings =
        new(8);
    private readonly HashSet<Guid> _activeAnimationWindows = new();

    /// <summary>
    /// Optional unified .byteanim profile. If empty, all legacy controller
    /// properties continue to work exactly as before.
    /// </summary>
    public AssetReference AnimationProfile { get; set; } =
        AssetReference.Empty;

    public string Idle { get; set; } = "Idle";
    public string Walk { get; set; } = "Walk";
    public string Run { get; set; } = "Run";
    public string Jump { get; set; } = "Jump";
    public string Fall { get; set; } = "Fall";
    public string Land { get; set; } = "Land";

    public float RunThreshold { get; set; } = 4.0f;
    public float MoveThreshold { get; set; } = 0.05f;
    public float StateHysteresis { get; set; } = 0.15f;
    public bool DirectionalMovement { get; set; }
    public string WalkForward { get; set; } = string.Empty;
    public string WalkBackward { get; set; } = string.Empty;
    public string WalkLeft { get; set; } = string.Empty;
    public string WalkRight { get; set; } = string.Empty;
    public string RunForward { get; set; } = string.Empty;
    public string RunBackward { get; set; } = string.Empty;
    public string RunLeft { get; set; } = string.Empty;
    public string RunRight { get; set; } = string.Empty;
    public float DirectionHysteresis { get; set; } = 0.10f;
    public bool MatchPlaybackToSpeed { get; set; }
    public float WalkReferenceSpeed { get; set; } = 2.0f;
    public float RunReferenceSpeed { get; set; } = 5.0f;
    public float MinimumPlaybackRate { get; set; } = 0.70f;
    public float MaximumPlaybackRate { get; set; } = 1.40f;

    public bool DriveLocomotion { get; set; } = true;

    public float TransitionDuration { get; set; } = 0.15f;

    public float PlaybackSpeed { get; set; } = 1.0f;

    public RootMotionMode RootMotionMode { get; set; } =
        RootMotionMode.InPlace;

    public Vector3 RootMotionDelta { get; private set; }

    public LocomotionState State { get; private set; }
    public LocomotionDirection Direction { get; private set; }
    public float LocomotionSpeed { get; private set; }
    public float LocomotionPlaybackRate { get; private set; } = 1.0f;
    public string CurrentLocomotionClip => _currentLocomotionClip;

    /// <summary>
    /// Fired once for every event marker crossed by the authoritative animation
    /// renderer. Large frame advances and loop wraparound preserve marker order.
    /// </summary>
    public event Action<AnimationEventOccurrence>? AnimationEventFired;

    public event Action<AnimationWindowOccurrence>? WindowEntered;

    public event Action<AnimationWindowOccurrence>? WindowExited;

    public bool IsWindowActive(Guid windowId) =>
        windowId != Guid.Empty &&
        _activeAnimationWindows.Contains(windowId);

    public bool IsWindowActive(string windowName)
    {
        if (string.IsNullOrWhiteSpace(windowName)) return false;

        foreach (AnimationWindowBoundary boundary in _animationWindowSnapshot)
        {
            if (boundary.Kind == AnimationWindowBoundaryKind.Enter &&
                string.Equals(
                    boundary.Window.Name,
                    windowName,
                    StringComparison.OrdinalIgnoreCase) &&
                _activeAnimationWindows.Contains(boundary.Window.Id))
            {
                return true;
            }
        }

        return false;
    }

    public string CurrentAnimation =>
        _renderers
            .Select(renderer => renderer.CurrentAnimation)
            .FirstOrDefault(
                name => !string.IsNullOrWhiteSpace(name))
        ?? string.Empty;

    public bool IsPlaying =>
        _renderers.Any(renderer => renderer.IsPlaying);

    public bool IsActionPlaying =>
        _actionActive;

    public string CurrentAction =>
        _actionActive
            ? _currentAction
            : string.Empty;

    public string QueuedAction => _queuedAction;

    protected override void OnStart()
    {
        ApplyAnimationProfile();
        RefreshRenderers();
        ResetRootMotionTracking();
    }

    protected override void OnUpdate()
    {
        if (_renderers.Count == 0) RefreshRenderers();

        foreach (SkeletalMeshRenderer renderer in _renderers)
        {
            renderer.TransitionDuration = Math.Max(TransitionDuration, 0.0f);
        }

        if (DriveLocomotion) ObserveLocomotion();
        UpdateAdvancedPoseControls();
        ApplyPlaybackRate();
        UpdateRootMotion();

        if (_actionActive)
        {
            if (_actionPaused || _actionRenderers.Any(renderer => renderer.IsPlaying)) return;

            float blendOut = _currentNamedAction?.BlendOut ?? TransitionDuration;
            FinishAction();

            if (!string.IsNullOrWhiteSpace(_queuedAction))
            {
                string queued = _queuedAction;
                _queuedAction = string.Empty;
                if (StartNamedAction(queued)) return;
            }

            if (DriveLocomotion || _poseGraph != null) ApplyObservedLocomotion(blendOut, true);
            return;
        }

        if (DriveLocomotion || _poseGraph != null) ApplyObservedLocomotion(TransitionDuration, false);
    }
    protected override void OnLateUpdate()
    {
        /*
         * SkeletalMeshRenderer advances during the normal scene-wide Update
         * pass. Reading it in LateUpdate removes dependency on hierarchy/object
         * update order and gives event crossing the final playback time for the
         * frame.
         */
        UpdateAnimationEvents();
    }

    protected override void OnDestroy()
    {
        ResetAnimationEventTracking();
    }

    /// <summary>
    /// Pull the unified profile into the existing proven C5-C7 controller
    /// fields. Leaving AnimationProfile empty preserves the legacy workflow.
    /// </summary>
    public bool ApplyAnimationProfile()
    {
        if (AnimationProfile == null ||
            AnimationProfile.IsEmpty)
        {
            return false;
        }

        if (!AnimationRuntimeAssets.TryGet(out AssetManager? assets) ||
            assets == null)
        {
            return false;
        }

        AnimationProfile profile;

        try
        {
            profile =
                assets.LoadAnimationProfile(
                    AnimationProfile);
        }
        catch
        {
            return false;
        }

        profile.Normalize();

        AnimationLocomotionProfile locomotion =
            profile.Locomotion;

        Idle = locomotion.Idle;
        Walk = locomotion.Walk;
        Run = locomotion.Run;
        Jump = locomotion.Jump;
        Fall = locomotion.Fall;
        Land = locomotion.Land;
        RunThreshold = locomotion.RunThreshold;
        DriveLocomotion =
            locomotion.Enabled &&
            locomotion.DriveFromCharacterController;
        TransitionDuration = locomotion.TransitionDuration;
        PlaybackSpeed = locomotion.PlaybackSpeed;
        RootMotionMode = locomotion.RootMotionMode;
        MoveThreshold = locomotion.MoveThreshold;
        StateHysteresis = locomotion.StateHysteresis;
        DirectionalMovement = locomotion.DirectionalMovement;
        WalkForward = locomotion.WalkForward;
        WalkBackward = locomotion.WalkBackward;
        WalkLeft = locomotion.WalkLeft;
        WalkRight = locomotion.WalkRight;
        RunForward = locomotion.RunForward;
        RunBackward = locomotion.RunBackward;
        RunLeft = locomotion.RunLeft;
        RunRight = locomotion.RunRight;
        DirectionHysteresis = locomotion.DirectionHysteresis;
        MatchPlaybackToSpeed = locomotion.MatchPlaybackToSpeed;
        WalkReferenceSpeed = locomotion.WalkReferenceSpeed;
        RunReferenceSpeed = locomotion.RunReferenceSpeed;
        MinimumPlaybackRate = locomotion.MinimumPlaybackRate;
        MaximumPlaybackRate = locomotion.MaximumPlaybackRate;
        _namedActions.Clear();
        foreach (AnimationActionProfile action in profile.Actions)
            if (!string.IsNullOrWhiteSpace(action.Name)) _namedActions[action.Name] = action;
        ConfigureAdvancedProfile(profile);

        return true;
    }

    private void ObserveLocomotion()
    {
        CharacterController3D? controller = GameObject.GetComponent<CharacterController3D>();
        float speed = controller?.Speed ?? 0.0f;
        LocomotionSpeed = speed;
        State = ResolveLocomotionStateStable(controller, State, _stateInitialized, MoveThreshold, RunThreshold, StateHysteresis);
        _stateInitialized = true;

        if (DirectionalMovement && controller != null &&
            (State == LocomotionState.Walk || State == LocomotionState.Run))
        {
            Direction = ResolveDirection(Direction, controller.Velocity, Transform.Forward, Transform.Right, DirectionHysteresis);
        }
        else
        {
            Direction = LocomotionDirection.None;
        }

        _currentLocomotionClip = GetClipName(State, Direction);
        LocomotionPlaybackRate = ResolvePlaybackRate(State, speed, MatchPlaybackToSpeed,
            WalkReferenceSpeed, RunReferenceSpeed, MinimumPlaybackRate, MaximumPlaybackRate);
    }

    private void ApplyObservedLocomotion(float transition, bool force)
    {
        if (string.IsNullOrWhiteSpace(_currentLocomotionClip)) return;
        bool loop = _poseGraph?.Current?.Loop ?? State != LocomotionState.Land;
        bool changed = force || !_renderers.Any(renderer =>
            string.Equals(renderer.CurrentAnimation, _currentLocomotionClip, StringComparison.OrdinalIgnoreCase));
        if (changed) PlayInternal(_currentLocomotionClip, loop, null,
            _poseGraph?.TransitionDuration ?? transition,
            _poseGraph?.Current?.PlaybackSpeed ?? LocomotionPlaybackRate);
    }

    private void ApplyPlaybackRate()
    {
        float multiplier = _actionActive ? _actionPlaybackRate :
            _poseGraph?.Current?.PlaybackSpeed ?? LocomotionPlaybackRate;
        foreach (SkeletalMeshRenderer renderer in _renderers)
            renderer.Speed = Math.Max(PlaybackSpeed * multiplier * renderer.BlendPlaybackScale, 0.0f);
    }
    public void RefreshRenderers()
    {
        ResetAnimationEventTracking();

        _renderers.Clear();

        foreach (GameObject gameObject in SelfAndDescendants(GameObject))
        {
            SkeletalMeshRenderer? existing =
                gameObject.GetComponent<SkeletalMeshRenderer>();

            if (existing != null)
            {
                existing.ResolveRuntimeResources();
                AddRenderer(existing);
            }

            ModelHierarchyInstance? instance =
                gameObject.GetComponent<ModelHierarchyInstance>();

            if (instance == null ||
                instance.Model.IsEmpty)
            {
                continue;
            }

            if (!AnimationRuntimeAssets.TryGet(out AssetManager? assets) ||
                assets == null)
            {
                continue;
            }

            ModelAsset model;

            try
            {
                model =
                    assets.LoadModel(instance.Model);
            }
            catch
            {
                continue;
            }

            if (model.Skeleton == null)
            {
                continue;
            }

            HashSet<string> skinnedMeshKeys =
                model.Meshes
                    .Where(
                        mesh =>
                            mesh.JointIndices.Length > 0 &&
                            mesh.JointWeights.Length ==
                            mesh.JointIndices.Length)
                    .Select(mesh => mesh.Key)
                    .ToHashSet(StringComparer.Ordinal);

            if (skinnedMeshKeys.Count == 0)
            {
                continue;
            }

            SkeletalMeshRenderer renderer =
                existing
                ?? gameObject.AddComponent(
                    new SkeletalMeshRenderer
                    {
                        Model = instance.Model,
                        SkeletonKey = model.Skeleton.Key,
                        PlayOnStart = false,
                        TransitionDuration =
                            Math.Max(TransitionDuration, 0.0f),
                        Speed =
                            Math.Max(PlaybackSpeed, 0.0f)
                    });

            renderer.ResolveRuntimeResources();
            AddRenderer(renderer);

            DisableBindPoseMeshRenderers(
                gameObject,
                instance.Model,
                skinnedMeshKeys);
        }

        ResetRootMotionTracking();
        ResetAnimationEventTracking();
    }

    public bool Play(
        string clipName,
        bool loop = true)
    {
        CancelActionOverride();

        return PlayInternal(
            clipName,
            loop);
    }

    public bool PlayAction(
        string clipName,
        bool retrigger = false,
        bool interruptCurrent = true)
    {
        if (string.IsNullOrWhiteSpace(clipName))
        {
            return false;
        }

        if (_renderers.Count == 0)
        {
            RefreshRenderers();
        }

        if (_actionActive)
        {
            bool sameAction =
                string.Equals(
                    _currentAction,
                    clipName,
                    StringComparison.OrdinalIgnoreCase);

            if (sameAction &&
                !retrigger)
            {
                return true;
            }

            if (!sameAction &&
                !interruptCurrent)
            {
                return false;
            }

            if (sameAction &&
                retrigger)
            {
                foreach (SkeletalMeshRenderer renderer in _renderers)
                {
                    renderer.Stop();
                }

                /*
                 * Same-name retriggers rewind the renderer to time zero, so the
                 * event cursor must also rewind. A normal Play() of the same
                 * clip only resumes and therefore intentionally does not reset.
                 */
                ResetAnimationEventTracking();
            }
        }

        _actionRenderers.Clear();

        bool played =
            PlayInternal(
                clipName,
                false,
                _actionRenderers);

        if (!played)
        {
            return false;
        }

        _currentAction = clipName;
        _actionActive = true;
        _actionPaused = false;

        ResetRootMotionTracking();

        return true;
    }

    public bool PlayNamedAction(string actionName, bool retrigger = false, bool queueIfBlocked = false, bool forceInterrupt = false)
    {
        if (!_namedActions.TryGetValue(actionName ?? string.Empty, out AnimationActionProfile? requested)) return false;

        if (_actionActive)
        {
            bool same = string.Equals(_currentAction, requested.Name, StringComparison.OrdinalIgnoreCase);
            NamedActionRequestDecision decision = ResolveNamedActionRequest(
                same, retrigger, forceInterrupt, _currentNamedAction?.Interruptible == true,
                _currentNamedAction?.Priority ?? int.MaxValue, requested.Priority, queueIfBlocked);

            if (decision == NamedActionRequestDecision.KeepCurrent) return true;
            if (decision == NamedActionRequestDecision.QueueRequested)
            {
                _queuedAction = requested.Name;
                return false;
            }
            if (decision == NamedActionRequestDecision.Reject) return false;

            if (decision == NamedActionRequestDecision.RestartSame)
            {
                foreach (SkeletalMeshRenderer renderer in _actionRenderers) renderer.Stop();
                ResetAnimationEventTracking();
                _activeAnimationWindows.Clear();
            }

            _queuedAction = string.Empty;
        }

        return StartNamedAction(requested.Name);
    }

    internal static NamedActionRequestDecision ResolveNamedActionRequest(
        bool sameAction, bool retrigger, bool forceInterrupt, bool currentInterruptible,
        int currentPriority, int requestedPriority, bool queueIfBlocked)
    {
        if (sameAction) return retrigger ? NamedActionRequestDecision.RestartSame : NamedActionRequestDecision.KeepCurrent;
        if (forceInterrupt || (currentInterruptible && requestedPriority >= currentPriority))
            return NamedActionRequestDecision.InterruptCurrent;
        return queueIfBlocked ? NamedActionRequestDecision.QueueRequested : NamedActionRequestDecision.Reject;
    }
    public bool TriggerAction(string actionName)
    {
        string requestedAction = actionName ?? string.Empty;
        if (!_namedActions.ContainsKey(requestedAction)) return false;
        if (!_actionActive) return PlayNamedAction(requestedAction);
        if (_currentNamedAction == null || !IsActionInChain(requestedAction, _currentNamedAction.Name)) return false;
        return QueueCombo();
    }

    internal bool IsActionInChain(string startAction, string candidateAction)
    {
        if (string.IsNullOrWhiteSpace(startAction) || string.IsNullOrWhiteSpace(candidateAction)) return false;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string current = startAction;
        int remaining = Math.Min(Math.Max(_namedActions.Count, 1), 128);

        while (remaining-- > 0 && _namedActions.TryGetValue(current, out AnimationActionProfile? action) && visited.Add(current))
        {
            if (string.Equals(action.Name, candidateAction, StringComparison.OrdinalIgnoreCase)) return true;
            if (string.IsNullOrWhiteSpace(action.NextAction)) return false;
            current = action.NextAction;
        }

        return false;
    }
    public bool QueueNamedAction(string actionName)
    {
        if (!_namedActions.TryGetValue(actionName ?? string.Empty, out AnimationActionProfile? action)) return false;
        _queuedAction = action.Name;
        return true;
    }

    public bool QueueCombo()
    {
        if (_currentNamedAction == null || string.IsNullOrWhiteSpace(_currentNamedAction.NextAction) ||
            !_namedActions.TryGetValue(_currentNamedAction.NextAction, out AnimationActionProfile? next)) return false;
        if (!string.IsNullOrWhiteSpace(_currentNamedAction.ComboWindow) && !IsWindowActive(_currentNamedAction.ComboWindow)) return false;
        _queuedAction = next.Name;
        return true;
    }

    public void CancelCurrentAction()
    {
        float blendOut = _currentNamedAction?.BlendOut ?? TransitionDuration;
        FinishAction();
        _queuedAction = string.Empty;
        if (DriveLocomotion)
        {
            ObserveLocomotion();
            ApplyObservedLocomotion(blendOut, true);
        }
    }

    private bool StartNamedAction(string actionName)
    {
        if (!_namedActions.TryGetValue(actionName, out AnimationActionProfile? action) || string.IsNullOrWhiteSpace(action.Clip)) return false;
        _actionRenderers.Clear();
        bool played = PlayInternal(action.Clip, action.Loop, _actionRenderers, action.BlendIn, action.PlaybackSpeed);
        if (!played) return false;
        _currentNamedAction = action;
        _currentAction = action.Name;
        _actionPlaybackRate = action.PlaybackSpeed;
        _actionActive = true;
        _actionPaused = false;
        ResetRootMotionTracking();
        return true;
    }

    private void FinishAction()
    {
        _actionActive = false;
        _actionPaused = false;
        _currentAction = string.Empty;
        _currentNamedAction = null;
        _actionPlaybackRate = 1.0f;
        _actionRenderers.Clear();
        ResetRootMotionTracking();
    }
    public void Pause()
    {
        foreach (SkeletalMeshRenderer renderer in _renderers)
        {
            renderer.Pause();
        }

        if (_actionActive)
        {
            _actionPaused = true;
        }

        RootMotionDelta =
            Vector3.Zero;
    }

    public void Resume()
    {
        foreach (SkeletalMeshRenderer renderer in _renderers)
        {
            renderer.Resume();
        }

        if (_actionActive)
        {
            _actionPaused = false;
        }

        ResetRootMotionTracking();
    }

    public void Stop()
    {
        CancelActionOverride();

        foreach (SkeletalMeshRenderer renderer in _renderers)
        {
            renderer.Stop();
        }

        ResetRootMotionTracking();
        ResetAnimationEventTracking();
    }

    private bool PlayInternal(
        string clipName,
        bool loop,
        ISet<SkeletalMeshRenderer>? playedRenderers = null,
        float? transitionOverride = null,
        float speedMultiplier = 1.0f)
    {
        if (_renderers.Count == 0)
        {
            RefreshRenderers();
        }

        bool played = false;

        foreach (SkeletalMeshRenderer renderer
                 in _renderers)
        {
            renderer.Speed = Math.Max(PlaybackSpeed * speedMultiplier, 0.0f);

            string previousClip = renderer.CurrentAnimation;
            float previousPhase = renderer.NormalizedPlaybackPhase;
            bool rendererPlayed =
                renderer.Play(
                    clipName,
                    loop,
                    Math.Max(transitionOverride ?? TransitionDuration, 0.0f));

            if (!rendererPlayed)
            {
                continue;
            }

            played =
                true;
            if (playedRenderers == null)
                AlignSynchronizedClip(renderer, previousClip, previousPhase, clipName);

            playedRenderers?.Add(
                renderer);
        }

        if (played)
        {
            ResetRootMotionTracking();

            if (!string.Equals(
                    _animationEventName,
                    clipName,
                    StringComparison.OrdinalIgnoreCase))
            {
                /*
                 * Clear active duration windows immediately when playback
                 * switches clips. The next LateUpdate binds the new cached
                 * timeline; stale windows are never observable in between.
                 */
                ResetAnimationEventTracking();
            }
        }

        return played;
    }

    private void UpdateAnimationEvents()
    {
        SkeletalMeshRenderer? source =
            _renderers.FirstOrDefault(
                renderer =>
                    renderer.ModelLoaded &&
                    !string.IsNullOrWhiteSpace(
                        renderer.CurrentAnimation));

        if (source == null)
        {
            ResetAnimationEventTracking();
            return;
        }

        string animationName =
            source.CurrentAnimation;

        bool sourceChanged =
            !ReferenceEquals(
                _animationEventSource,
                source);

        bool clipChanged =
            !string.Equals(
                _animationEventName,
                animationName,
                StringComparison.OrdinalIgnoreCase);

        if (sourceChanged ||
            clipChanged)
        {
            BindAnimationEventClip(
                source,
                animationName);
        }

        ImportedAnimation? clip =
            _animationEventClip;

        if (clip == null)
        {
            return;
        }

        float duration =
            Math.Max(
                clip.Duration,
                0.0f);

        if (duration <=
            0.000001f)
        {
            _animationEventPreviousTime =
                0.0f;

            _animationEventSampleValid =
                true;

            return;
        }

        float currentTime =
            Math.Clamp(
                source.PlaybackTime,
                0.0f,
                duration);

        bool includeStart =
            !_animationEventSampleValid;

        float fromTime =
            includeStart
                ? 0.0f
                : _animationEventPreviousTime;

        float rawAdvance =
            includeStart
                ? ResolveInitialAnimationAdvance(
                    source,
                    currentTime,
                    duration)
                : ResolveAnimationAdvance(
                    source,
                    _animationEventPreviousTime,
                    currentTime,
                    duration);

        long baseLoopIndex =
            _animationEventLoopIndex;

        AnimationEventCrossing.Collect(
            _animationEventSnapshot,
            duration,
            fromTime,
            rawAdvance,
            source.Loop,
            includeStart,
            _animationEventCrossings,
            out long loopsCrossed);

        AnimationWindowCrossing.Collect(
            _animationWindowSnapshot,
            duration,
            fromTime,
            rawAdvance,
            source.Loop,
            includeStart,
            _animationWindowCrossings,
            out _);

        _animationEventPreviousTime = currentTime;
        _animationEventSampleValid = true;
        _animationEventLoopIndex += loopsCrossed;

        string dispatchAnimation = clip.Name;

        foreach (AnimationEventCrossingHit hit in _animationEventCrossings)
        {
            AnimationEventMarker marker = hit.Marker;

            AnimationEventFired?.Invoke(
                new AnimationEventOccurrence(
                    source.Model.Guid,
                    clip.Key,
                    clip.Name,
                    marker.Id,
                    marker.Name,
                    marker.Payload,
                    marker.Time,
                    baseLoopIndex + hit.LoopOffset));

            if (!string.Equals(
                    source.CurrentAnimation,
                    dispatchAnimation,
                    StringComparison.OrdinalIgnoreCase))
            {
                ResetAnimationEventTracking();
                return;
            }
        }

        foreach (AnimationWindowCrossingHit hit in _animationWindowCrossings)
        {
            AnimationWindowBoundary boundary = hit.Boundary;
            AnimationWindow window = boundary.Window;
            bool entering =
                boundary.Kind == AnimationWindowBoundaryKind.Enter;

            if (entering)
                _activeAnimationWindows.Add(window.Id);
            else
                _activeAnimationWindows.Remove(window.Id);

            var occurrence =
                new AnimationWindowOccurrence(
                    source.Model.Guid,
                    clip.Key,
                    clip.Name,
                    window.Id,
                    window.Name,
                    window.Payload,
                    window.StartTime,
                    window.EndTime,
                    baseLoopIndex + hit.LoopOffset);

            if (entering)
                WindowEntered?.Invoke(occurrence);
            else
                WindowExited?.Invoke(occurrence);

            if (!string.Equals(
                    source.CurrentAnimation,
                    dispatchAnimation,
                    StringComparison.OrdinalIgnoreCase))
            {
                ResetAnimationEventTracking();
                return;
            }
        }
    }
    private void BindAnimationEventClip(
        SkeletalMeshRenderer source,
        string animationName)
    {
        ResetAnimationEventTracking();

        _animationEventSource =
            source;

        _animationEventName =
            animationName;

        if (!AnimationRuntimeAssets.TryGet(
                out AssetManager? assets) ||
            assets == null)
        {
            return;
        }

        try
        {
            ModelAsset model =
                assets.LoadModel(
                    source.Model);

            ImportedAnimation? clip =
                model.Animations.FirstOrDefault(
                    animation =>
                        string.Equals(
                            animation.Name,
                            animationName,
                            StringComparison.Ordinal))
                ?? model.Animations.FirstOrDefault(
                    animation =>
                        string.Equals(
                            animation.Name,
                            animationName,
                            StringComparison.OrdinalIgnoreCase));

            if (clip == null)
            {
                return;
            }

            _animationEventClip =
                clip;

            _animationEventSnapshot =
                AnimationEventCrossing.BuildSnapshot(
                    clip.Events,
                    clip.Duration);

            _animationWindowSnapshot =
                AnimationWindowCrossing.BuildSnapshot(
                    clip.Windows,
                    clip.Duration);
        }
        catch
        {
            _animationEventClip =
                null;

            _animationEventSnapshot =
                Array.Empty<AnimationEventMarker>();

            _animationWindowSnapshot =
                Array.Empty<AnimationWindowBoundary>();
        }
    }

    private static float ResolveInitialAnimationAdvance(
        SkeletalMeshRenderer source,
        float currentTime,
        float duration)
    {
        float expected =
            CurrentFrameAnimationAdvance(
                source);

        if (expected <=
            0.000001f)
        {
            return currentTime;
        }

        float expectedTime =
            PredictAnimationTime(
                0.0f,
                expected,
                duration,
                source.Loop);

        bool expectedMatches =
            NearlySameAnimationTime(
                expectedTime,
                currentTime,
                duration);

        if (expectedMatches &&
            (currentTime >
                 0.000001f ||
             source.IsPlaying &&
             expected >=
                 duration -
                 0.000001f))
        {
            return expected;
        }

        /*
         * If this controller started the renderer after that renderer's Update
         * slot for the frame, playback is still at zero. Do not invent a frame
         * of movement. The zero-time marker still fires through includeStart.
         */
        return currentTime;
    }

    private static float ResolveAnimationAdvance(
        SkeletalMeshRenderer source,
        float previousTime,
        float currentTime,
        float duration)
    {
        float observed;

        if (source.Loop)
        {
            observed =
                currentTime +
                    0.000001f >=
                previousTime
                    ? Math.Max(
                        currentTime -
                        previousTime,
                        0.0f)
                    : Math.Max(
                        duration -
                        previousTime +
                        currentTime,
                        0.0f);
        }
        else
        {
            observed =
                Math.Max(
                    currentTime -
                    previousTime,
                    0.0f);
        }

        float expected =
            CurrentFrameAnimationAdvance(
                source);

        if (expected <=
            0.000001f)
        {
            return observed;
        }

        bool visiblyMoved =
            MathF.Abs(
                currentTime -
                previousTime) >
            0.000001f;

        /*
         * Pause can happen earlier in the same Update pass. If playback time
         * did not move and the renderer is no longer playing, no event time was
         * crossed this frame.
         */
        if (!visiblyMoved &&
            !source.IsPlaying)
        {
            return 0.0f;
        }

        float expectedTime =
            PredictAnimationTime(
                previousTime,
                expected,
                duration,
                source.Loop);

        if (NearlySameAnimationTime(
                expectedTime,
                currentTime,
                duration))
        {
            /*
             * This is the important large-frame path. The observed modulo time
             * cannot reveal that two or more loops were crossed, but the raw
             * frame advance can. Use it only when it predicts the renderer's
             * actual final time.
             */
            return expected;
        }

        return observed;
    }

    private static float CurrentFrameAnimationAdvance(
        SkeletalMeshRenderer source)
    {
        float deltaTime =
            Math.Max(
                (float)ByteEngine.Core.Time.DeltaTime,
                0.0f);

        float speed =
            Math.Max(
                source.Speed,
                0.0f);

        return deltaTime *
               speed;
    }

    private static float PredictAnimationTime(
        float fromTime,
        float advance,
        float duration,
        bool loop)
    {
        if (duration <=
            0.000001f)
        {
            return 0.0f;
        }

        float next =
            Math.Max(
                fromTime,
                0.0f) +
            Math.Max(
                advance,
                0.0f);

        if (!loop)
        {
            return Math.Min(
                next,
                duration);
        }

        float wrapped =
            next %
            duration;

        return wrapped <
            0.0f
                ? wrapped +
                  duration
                : wrapped;
    }

    private static bool NearlySameAnimationTime(
        float left,
        float right,
        float duration)
    {
        float tolerance =
            Math.Max(
                0.0001f,
                duration *
                0.0001f);

        return MathF.Abs(
                left -
                right) <=
            tolerance;
    }

    private void ResetAnimationEventTracking()
    {
        _animationEventSource =
            null;

        _animationEventClip =
            null;

        _animationEventName =
            string.Empty;

        _animationEventSnapshot =
            Array.Empty<AnimationEventMarker>();

        _animationEventCrossings.Clear();

        _animationWindowSnapshot =
            Array.Empty<AnimationWindowBoundary>();

        _animationWindowCrossings.Clear();
        _activeAnimationWindows.Clear();

        _animationEventPreviousTime =
            0.0f;

        _animationEventLoopIndex =
            0;

        _animationEventSampleValid =
            false;
    }

    private void UpdateRootMotion()
    {
        RootMotionDelta =
            Vector3.Zero;

        SkeletalMeshRenderer? source =
            _renderers.FirstOrDefault(
                renderer =>
                    renderer.ModelLoaded &&
                    !string.IsNullOrWhiteSpace(
                        renderer.CurrentAnimation));

        if (source == null)
        {
            ResetRootMotionTracking();
            return;
        }

        string animation =
            source.CurrentAnimation;

        Vector3 travel =
            source.ModelSpaceRootTravel;

        float playbackTime =
            source.PlaybackTime;

        bool sourceChanged =
            !ReferenceEquals(
                _rootMotionSource,
                source);

        bool clipChanged =
            !string.Equals(
                _rootMotionAnimation,
                animation,
                StringComparison.OrdinalIgnoreCase);

        if (!_rootMotionSampleValid ||
            sourceChanged ||
            clipChanged)
        {
            StoreRootMotionSample(
                source,
                animation,
                travel,
                playbackTime);

            return;
        }

        if (MathF.Abs(
                playbackTime -
                _lastRootMotionTime) <=
            0.000001f)
        {
            _lastRootMotionTravel =
                travel;

            return;
        }

        Vector3 localDelta;

        bool wrapped =
            source.Loop &&
            playbackTime + 0.000001f <
            _lastRootMotionTime;

        if (wrapped)
        {
            localDelta =
                _lastAppliedRootMotionDelta;
        }
        else
        {
            localDelta =
                travel -
                _lastRootMotionTravel;
        }

        localDelta.Y =
            0.0f;

        if (RootMotionMode ==
                RootMotionMode.ApplyHorizontal &&
            IsFinite(localDelta) &&
            localDelta.LengthSquared() >
                0.0000000001f)
        {
            Vector3 worldDelta =
                Vector3.TransformNormal(
                    localDelta,
                    source.Transform.WorldMatrix);

            worldDelta.Y =
                0.0f;

            if (IsFinite(worldDelta))
            {
                GameObject.Transform.WorldPosition +=
                    worldDelta;

                RootMotionDelta =
                    worldDelta;

                _lastAppliedRootMotionDelta =
                    localDelta;
            }
        }
        else if (!wrapped)
        {
            _lastAppliedRootMotionDelta =
                localDelta;
        }

        StoreRootMotionSample(
            source,
            animation,
            travel,
            playbackTime);
    }

    private void StoreRootMotionSample(
        SkeletalMeshRenderer source,
        string animation,
        Vector3 travel,
        float playbackTime)
    {
        _rootMotionSource =
            source;

        _rootMotionAnimation =
            animation;

        _lastRootMotionTravel =
            travel;

        _lastRootMotionTime =
            playbackTime;

        _rootMotionSampleValid =
            true;
    }

    private void ResetRootMotionTracking()
    {
        RootMotionDelta =
            Vector3.Zero;

        _rootMotionSource =
            null;

        _rootMotionAnimation =
            string.Empty;

        _lastRootMotionTravel =
            Vector3.Zero;

        _lastAppliedRootMotionDelta =
            Vector3.Zero;

        _lastRootMotionTime =
            0.0f;

        _rootMotionSampleValid =
            false;
    }

    private static bool IsFinite(
        Vector3 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z);

    private void CancelActionOverride()
    {
        if (!_actionActive && string.IsNullOrWhiteSpace(_currentAction)) return;
        FinishAction();
        _queuedAction = string.Empty;
        _stateInitialized = false;
    }

    private static LocomotionState ResolveLocomotionState(CharacterController3D? controller, float runThreshold)
    {
        if (controller == null) return LocomotionState.Idle;
        return controller.JustLanded ? LocomotionState.Land : controller.IsFalling ? LocomotionState.Fall :
            !controller.IsGrounded ? LocomotionState.Jump : controller.Speed < 0.05f ? LocomotionState.Idle :
            controller.Speed >= runThreshold ? LocomotionState.Run : LocomotionState.Walk;
    }
    internal static LocomotionState ResolveLocomotionStateStable(CharacterController3D? controller,
        LocomotionState current, bool initialized, float moveThreshold, float runThreshold, float hysteresis)
    {
        if (controller == null) return LocomotionState.Idle;
        if (controller.JustLanded) return LocomotionState.Land;
        if (controller.IsFalling) return LocomotionState.Fall;
        if (!controller.IsGrounded) return LocomotionState.Jump;
        float speed = controller.Speed;
        moveThreshold = Math.Max(moveThreshold, 0.0f);
        runThreshold = Math.Max(runThreshold, moveThreshold);
        hysteresis = Math.Max(hysteresis, 0.0f);
        if (!initialized || current is LocomotionState.Jump or LocomotionState.Fall or LocomotionState.Land)
            return speed <= moveThreshold ? LocomotionState.Idle : speed >= runThreshold ? LocomotionState.Run : LocomotionState.Walk;
        if (current == LocomotionState.Idle)
            return speed > moveThreshold + hysteresis ? (speed > runThreshold + hysteresis ? LocomotionState.Run : LocomotionState.Walk) : LocomotionState.Idle;
        if (current == LocomotionState.Run)
            return speed < Math.Max(moveThreshold, runThreshold - hysteresis) ?
                (speed <= Math.Max(0.0f, moveThreshold - hysteresis) ? LocomotionState.Idle : LocomotionState.Walk) : LocomotionState.Run;
        return speed <= Math.Max(0.0f, moveThreshold - hysteresis) ? LocomotionState.Idle :
            speed > runThreshold + hysteresis ? LocomotionState.Run : LocomotionState.Walk;
    }

    internal static LocomotionDirection ResolveDirection(LocomotionDirection previous, Vector3 velocity,
        Vector3 forward, Vector3 right, float hysteresis)
    {
        velocity.Y = 0.0f;
        if (velocity.LengthSquared() <= 0.000001f) return LocomotionDirection.None;
        velocity = Vector3.Normalize(velocity);
        forward.Y = 0.0f; right.Y = 0.0f;
        if (forward.LengthSquared() > 0.000001f) forward = Vector3.Normalize(forward);
        if (right.LengthSquared() > 0.000001f) right = Vector3.Normalize(right);
        float f = Vector3.Dot(velocity, forward);
        float r = Vector3.Dot(velocity, right);
        bool previousForwardAxis = previous is LocomotionDirection.Forward or LocomotionDirection.Backward;
        bool useForward = Math.Abs(f) >= Math.Abs(r) + (previousForwardAxis ? -Math.Max(hysteresis, 0.0f) : Math.Max(hysteresis, 0.0f));
        return useForward ? (f >= 0 ? LocomotionDirection.Forward : LocomotionDirection.Backward) :
            (r >= 0 ? LocomotionDirection.Right : LocomotionDirection.Left);
    }

    internal static float ResolvePlaybackRate(LocomotionState state, float speed, bool enabled,
        float walkReference, float runReference, float minimum, float maximum)
    {
        if (!enabled || state is not (LocomotionState.Walk or LocomotionState.Run)) return 1.0f;
        float reference = state == LocomotionState.Run ? runReference : walkReference;
        return Math.Clamp(speed / Math.Max(reference, 0.001f), Math.Max(minimum, 0.0f), Math.Max(maximum, minimum));
    }

    private string GetClipName(LocomotionState state, LocomotionDirection direction)
    {
        string directional = (state, direction) switch
        {
            (LocomotionState.Walk, LocomotionDirection.Forward) => WalkForward,
            (LocomotionState.Walk, LocomotionDirection.Backward) => WalkBackward,
            (LocomotionState.Walk, LocomotionDirection.Left) => WalkLeft,
            (LocomotionState.Walk, LocomotionDirection.Right) => WalkRight,
            (LocomotionState.Run, LocomotionDirection.Forward) => RunForward,
            (LocomotionState.Run, LocomotionDirection.Backward) => RunBackward,
            (LocomotionState.Run, LocomotionDirection.Left) => RunLeft,
            (LocomotionState.Run, LocomotionDirection.Right) => RunRight,
            _ => string.Empty
        };
        if (!string.IsNullOrWhiteSpace(directional)) return directional;
        // Aim/strafe profiles commonly have one backward/strafe set but no
        // separate run variants. Keep those directions instead of playing
        // the forward run clip whenever movement crosses RunThreshold.
        if (state == LocomotionState.Run && DirectionalMovement)
        {
            string walkDirection = direction switch
            {
                LocomotionDirection.Backward => WalkBackward,
                LocomotionDirection.Left => WalkLeft,
                LocomotionDirection.Right => WalkRight,
                _ => string.Empty
            };
            if (!string.IsNullOrWhiteSpace(walkDirection)) return walkDirection;
        }
        return state switch
        {
            LocomotionState.Idle => Idle,
            LocomotionState.Walk => Walk,
            LocomotionState.Run => Run,
            LocomotionState.Jump => Jump,
            LocomotionState.Fall => Fall,
            LocomotionState.Land => Land,
            _ => string.Empty
        };
    }
    private void AddRenderer(
        SkeletalMeshRenderer renderer)
    {
        if (!_renderers.Contains(renderer))
        {
            _renderers.Add(renderer);
            if (_loadedPoseProfile != null) renderer.ConfigurePoseProfile(_loadedPoseProfile);
        }
    }

    private static void DisableBindPoseMeshRenderers(
        GameObject modelRoot,
        AssetReference model,
        IReadOnlySet<string> skinnedMeshKeys)
    {
        foreach (GameObject gameObject in SelfAndDescendants(modelRoot))
        {
            foreach (MeshRenderer meshRenderer
                     in gameObject.Components.OfType<MeshRenderer>())
            {
                ModelMeshReference? reference =
                    meshRenderer.MeshReference;

                if (reference == null ||
                    !SameModel(reference.Model, model) ||
                    !skinnedMeshKeys.Contains(reference.SubAssetKey))
                {
                    continue;
                }

                meshRenderer.Enabled = false;
            }
        }
    }

    private static bool SameModel(
        AssetReference left,
        AssetReference right)
    {
        if (left.Guid != Guid.Empty &&
            right.Guid != Guid.Empty)
        {
            return left.Guid == right.Guid;
        }

        return string.Equals(
            left.CachedProjectPath,
            right.CachedProjectPath,
            StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<GameObject> SelfAndDescendants(
        GameObject root)
    {
        yield return root;

        foreach (GameObject child in root.Children)
        {
            foreach (GameObject descendant in SelfAndDescendants(child))
            {
                yield return descendant;
            }
        }
    }
}
