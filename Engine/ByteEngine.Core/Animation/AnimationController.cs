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
/// </summary>
public sealed class AnimationController : Component
{
    private readonly List<SkeletalMeshRenderer> _renderers = new();

    private bool _stateInitialized;
    private bool _actionActive;
    private bool _actionPaused;
    private string _currentAction = string.Empty;

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
    }

    public void Stop()
    {
        CancelActionOverride();

        foreach (SkeletalMeshRenderer renderer in _renderers)
        {
            renderer.Stop();
        }
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

        return played;
    }

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
