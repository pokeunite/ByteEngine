using System.Numerics;

using ByteEngine.Core.Assets;
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

/// <summary>
/// Character locomotion animation controller.
///
/// v0.11-C1 upgrades the old state-only component into a playback driver. It
/// discovers imported skinned model hierarchies beneath the character, creates
/// their runtime SkeletalMeshRenderer and drives clips from CharacterController3D.
///
/// v0.11-C5B adds temporary one-shot/action overrides. Action clips suspend
/// locomotion playback while they are active and automatically return to the
/// current locomotion state when the one-shot completes.
///
/// v0.11-C7 adds an explicit root-motion policy. SkeletalMeshRenderer continues
/// to extract/remove model-space locomotion travel from the rendered pose; this
/// controller can optionally apply that extracted horizontal travel to the
/// character GameObject.
/// </summary>
public sealed class AnimationController : Component
{
    private readonly List<SkeletalMeshRenderer> _renderers = new();

    private bool _stateInitialized;
    private bool _actionActive;
    private bool _actionPaused;
    private string _currentAction = string.Empty;

    /*
     * C7 root-motion state is controller-owned deliberately. The renderer's
     * existing C2 extraction remains the single source of truth, while the
     * character controller decides whether that extracted travel should become
     * world movement. Only the first active skeletal renderer is used so
     * multi-mesh characters cannot apply movement twice.
     */
    private SkeletalMeshRenderer? _rootMotionSource;
    private string _rootMotionAnimation = string.Empty;
    private Vector3 _lastRootMotionTravel;
    private Vector3 _lastAppliedRootMotionDelta;
    private float _lastRootMotionTime;
    private bool _rootMotionSampleValid;

    public string Idle { get; set; } = "Idle";
    public string Walk { get; set; } = "Walk";
    public string Run { get; set; } = "Run";
    public string Jump { get; set; } = "Jump";
    public string Fall { get; set; } = "Fall";
    public string Land { get; set; } = "Land";

    public float RunThreshold { get; set; } = 4.0f;

    public bool DriveLocomotion { get; set; } = true;

    public float TransitionDuration { get; set; } = 0.15f;

    public float PlaybackSpeed { get; set; } = 1.0f;

    /// <summary>
    /// Controls whether extracted locomotion travel stays in-place or moves the
    /// owning character. InPlace preserves the pre-C7 behaviour.
    /// </summary>
    public RootMotionMode RootMotionMode { get; set; } =
        RootMotionMode.InPlace;

    /// <summary>
    /// World-space root-motion delta applied during the most recent controller
    /// update. Zero while InPlace, paused, or before a valid sample exists.
    /// </summary>
    public Vector3 RootMotionDelta { get; private set; }

    public LocomotionState State { get; private set; }

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

    protected override void OnStart()
    {
        /*
         * Imported model roots can live below this character. Building the
         * runtime renderer here is safe for normal Character Blueprints and
         * means locomotion can begin immediately on the first update.
         */
        RefreshRenderers();
        ResetRootMotionTracking();
    }

    protected override void OnUpdate()
    {
        if (_renderers.Count == 0)
        {
            RefreshRenderers();
        }

        foreach (SkeletalMeshRenderer renderer in _renderers)
        {
            renderer.Speed =
                Math.Max(PlaybackSpeed, 0.0f);

            renderer.TransitionDuration =
                Math.Max(TransitionDuration, 0.0f);
        }

        /*
         * Skeletal renderers beneath the character normally update after the
         * controller in scene order, so this consumes the most recently
         * evaluated pose. That intentionally makes root motion one animation
         * sample behind rather than duplicating animation evaluation here.
         */
        UpdateRootMotion();

        if (_actionActive)
        {
            if (_actionPaused)
            {
                return;
            }

            bool actionStillPlaying =
                _renderers.Any(
                    renderer =>
                        renderer.IsPlaying &&
                        string.Equals(
                            renderer.CurrentAnimation,
                            _currentAction,
                            StringComparison.OrdinalIgnoreCase));

            if (actionStillPlaying)
            {
                return;
            }

            _actionActive = false;
            _actionPaused = false;
            _currentAction = string.Empty;

            /*
             * Force locomotion to re-apply even when the state itself did not
             * change during the action (for example Idle -> Attack -> Idle).
             */
            _stateInitialized = false;
        }

        if (!DriveLocomotion)
        {
            return;
        }

        UpdateLocomotion();
    }

    private void UpdateLocomotion()
    {
        CharacterController3D? controller =
            GameObject.GetComponent<CharacterController3D>();

        if (controller == null)
        {
            return;
        }

        LocomotionState nextState =
            controller.JustLanded
                ? LocomotionState.Land
                : controller.IsFalling
                    ? LocomotionState.Fall
                    : !controller.IsGrounded
                        ? LocomotionState.Jump
                        : controller.Speed < 0.05f
                            ? LocomotionState.Idle
                            : controller.Speed >= RunThreshold
                                ? LocomotionState.Run
                                : LocomotionState.Walk;

        if (_stateInitialized &&
            nextState == State)
        {
            return;
        }

        State = nextState;
        _stateInitialized = true;

        string clipName =
            GetClipName(State);

        bool loop =
            State != LocomotionState.Land;

        if (!string.IsNullOrWhiteSpace(clipName))
        {
            PlayInternal(
                clipName,
                loop);
        }
    }

    public void RefreshRenderers()
    {
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

            if (model.Skeleton == null ||
                model.Animations.Count == 0)
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

    /// <summary>
    /// Plays a temporary non-looping action clip and suspends locomotion until
    /// the clip finishes. If the same action is already active, retrigger=false
    /// leaves it running. If another action is active, interruptCurrent=false
    /// rejects the new request.
    /// </summary>
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
                /*
                 * SkeletalMeshRenderer.Play intentionally resumes the same
                 * current clip instead of rewinding it. Stop first when the
                 * caller explicitly asks for a retrigger so the one-shot
                 * restarts at time zero.
                 */
                foreach (SkeletalMeshRenderer renderer in _renderers)
                {
                    renderer.Stop();
                }
            }
        }

        bool played =
            PlayInternal(
                clipName,
                false);

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
    }

    private bool PlayInternal(
        string clipName,
        bool loop)
    {
        if (_renderers.Count == 0)
        {
            RefreshRenderers();
        }

        bool played = false;

        foreach (SkeletalMeshRenderer renderer in _renderers)
        {
            renderer.Speed =
                Math.Max(PlaybackSpeed, 0.0f);

            played |=
                renderer.Play(
                    clipName,
                    loop,
                    Math.Max(TransitionDuration, 0.0f));
        }

        if (played)
        {
            ResetRootMotionTracking();
        }

        return played;
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

        /*
         * Paused clips and duplicate samples must never produce movement.
         */
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
            /*
             * SkeletalMeshRenderer exposes accumulated extracted travel but not
             * a public arbitrary-time sampler. Re-use the previous valid frame
             * delta at a loop boundary instead of interpreting the wrap as a
             * large backwards teleport. On the following sample, exact travel
             * deltas resume normally.
             */
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
            /*
             * Travel is measured in the skeletal renderer's model space. Use
             * its live world matrix as a direction transform so authored model
             * rotation/import scale are respected before moving the character
             * root.
             */
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
            /*
             * Keep a useful local velocity estimate even while InPlace, so a
             * runtime switch to ApplyHorizontal at a loop boundary does not
             * generate a backwards jump.
             */
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
        if (!_actionActive &&
            string.IsNullOrWhiteSpace(_currentAction))
        {
            return;
        }

        _actionActive = false;
        _actionPaused = false;
        _currentAction = string.Empty;
        _stateInitialized = false;

        ResetRootMotionTracking();
    }

    private string GetClipName(
        LocomotionState state) =>
        state switch
        {
            LocomotionState.Idle => Idle,
            LocomotionState.Walk => Walk,
            LocomotionState.Run => Run,
            LocomotionState.Jump => Jump,
            LocomotionState.Fall => Fall,
            LocomotionState.Land => Land,
            _ => string.Empty
        };

    private void AddRenderer(
        SkeletalMeshRenderer renderer)
    {
        if (!_renderers.Contains(renderer))
        {
            _renderers.Add(renderer);
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
