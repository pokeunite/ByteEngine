using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Core.Animation;

public sealed partial class AnimationController
{
    private AnimationProfile? _loadedPoseProfile;
    private AnimationStateRuntime? _poseGraph;
    private readonly Dictionary<string, AnimationBlendSpace> _blendSpaceByName =
        new(StringComparer.OrdinalIgnoreCase);
    private AnimationBlendSpace? _selectedBlendSpace;

    public IReadOnlyList<SkeletalMeshRenderer> AnimationRenderers => _renderers;
    public string CurrentSyncGroup => _loadedPoseProfile?.Locomotion.SyncGroup ?? string.Empty;
    public float CurrentSyncPhase => _renderers.Count > 0 ? _renderers[0].NormalizedPlaybackPhase : 0f;
    public string CurrentGraphState => _poseGraph?.CurrentState ?? string.Empty;
    public float GraphTransitionWeight => _poseGraph?.BlendAlpha ?? 1f;
    public string CurrentBlendSpace => _selectedBlendSpace?.Name ?? string.Empty;
    public bool SetAnimationFloat(string name, float value) => _poseGraph?.SetFloat(name, value) ?? false;
    public bool SetAnimationBool(string name, bool value) => _poseGraph?.SetBool(name, value) ?? false;
    public bool FireAnimationTrigger(string name) => _poseGraph?.Trigger(name) ?? false;

    public void SetAim(float yawDegrees, float pitchDegrees)
    {
        foreach (SkeletalMeshRenderer renderer in _renderers) renderer.SetAim(yawDegrees, pitchDegrees);
    }

    public void SetLookAtTarget(Vector3? worldTarget)
    {
        foreach (SkeletalMeshRenderer renderer in _renderers) renderer.SetLookAtTarget(worldTarget);
    }

    public void SetIkTarget(string chain, Vector3 worldTarget, Vector3 worldPole, float weight = 1f)
    {
        foreach (SkeletalMeshRenderer renderer in _renderers)
            renderer.SetIkTarget(chain, worldTarget, worldPole, weight);
    }

    public void ClearIkTarget(string chain)
    {
        foreach (SkeletalMeshRenderer renderer in _renderers) renderer.ClearIkTarget(chain);
    }

    private void ConfigureAdvancedProfile(AnimationProfile profile)
    {
        _loadedPoseProfile = profile;
        _blendSpaceByName.Clear();
        foreach (AnimationBlendSpace space in profile.BlendSpaces)
            if (!string.IsNullOrWhiteSpace(space.Name))
                _blendSpaceByName[space.Name] = space;
        _poseGraph = profile.StateGraph.Enabled && profile.StateGraph.States.Count != 0
            ? new AnimationStateRuntime(profile.StateGraph) : null;
        foreach (SkeletalMeshRenderer renderer in _renderers)
            renderer.ConfigurePoseProfile(profile);
    }

    private void UpdateAdvancedPoseControls()
    {
        if (_loadedPoseProfile == null) return;
        CharacterController3D? motor = GameObject.GetComponent<CharacterController3D>();
        if (_poseGraph != null)
        {
            _poseGraph.SetFloat("Speed", motor?.Speed ?? 0f);
            _poseGraph.SetBool("Grounded", motor?.IsGrounded ?? true);
            float phase = _renderers.Count != 0 ? _renderers[0].NormalizedPlaybackPhase : 0f;
            _poseGraph.Step((float)ByteEngine.Core.Time.DeltaTime, phase);
            AnimationGraphState? graphState = _poseGraph.Current;
            if (graphState != null && !_actionActive)
            {
                _selectedBlendSpace = graphState.SourceKind == AnimationPoseSourceKind.Clip
                    ? null : _blendSpaceByName.GetValueOrDefault(graphState.Source);
                _currentLocomotionClip = _selectedBlendSpace != null
                    ? (_selectedBlendSpace.Samples.Count > 0 ? _selectedBlendSpace.Samples[0].Clip : string.Empty)
                    : graphState.Source;
            }
        }
        else
        {
            string selected = _loadedPoseProfile.Locomotion.BlendSpace;
            _selectedBlendSpace = !_actionActive &&
                (State is LocomotionState.Idle or LocomotionState.Walk or LocomotionState.Run) &&
                _blendSpaceByName.TryGetValue(selected, out AnimationBlendSpace? space)
                    ? space : null;
            if (_selectedBlendSpace != null)
                _currentLocomotionClip = _selectedBlendSpace.Samples.Count > 0
                    ? _selectedBlendSpace.Samples[0].Clip : string.Empty;
        }
        if (_actionActive) _selectedBlendSpace = null;
        Vector3 velocity = motor?.Velocity ?? Vector3.Zero;
        float forward = Vector3.Dot(velocity, Transform.Forward);
        float right = Vector3.Dot(velocity, Transform.Right);
        float x = _selectedBlendSpace == null ? 0f :
            ResolveBlendParameter(_selectedBlendSpace.ParameterX, motor, right, forward);
        float y = _selectedBlendSpace?.TwoDimensional == true
            ? ResolveBlendParameter(_selectedBlendSpace.ParameterY, motor, right, forward) : 0f;
        foreach (SkeletalMeshRenderer renderer in _renderers)
        {
            renderer.SetBlendSpace(_selectedBlendSpace, x, y,
                _poseGraph?.TransitionDuration ?? TransitionDuration);
            for (int i = 0; i < _loadedPoseProfile.Layers.Count; i++)
            {
                string parameter = _loadedPoseProfile.Layers[i].WeightParameter;
                float weight = !string.IsNullOrWhiteSpace(parameter) &&
                    _poseGraph?.TryGetFloat(parameter, out float graphValue) == true ? graphValue : 1f;
                renderer.SetLayerParameterWeight(i, weight);
            }
        }
    }

    private float ResolveBlendParameter(string name, CharacterController3D? motor,
        float right, float forward)
    {
        if (_poseGraph?.TryGetFloat(name, out float authored) == true) return authored;
        if (string.Equals(name, "Right", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Strafe", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Lateral", StringComparison.OrdinalIgnoreCase)) return right;
        if (string.Equals(name, "Forward", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "Direction", StringComparison.OrdinalIgnoreCase)) return forward;
        return motor?.Speed ?? 0f;
    }

    private void AlignSynchronizedClip(SkeletalMeshRenderer renderer, string previousClip,
        float previousPhase, string nextClip)
    {
        if (_loadedPoseProfile == null || string.IsNullOrWhiteSpace(previousClip)) return;
        foreach (AnimationSyncGroup group in _loadedPoseProfile.SyncGroups)
        {
            bool sourceInGroup = false, targetInGroup = false;
            foreach (string clip in group.Clips)
            {
                if (string.Equals(clip, previousClip, StringComparison.OrdinalIgnoreCase)) sourceInGroup = true;
                if (string.Equals(clip, nextClip, StringComparison.OrdinalIgnoreCase)) targetInGroup = true;
            }
            if (!sourceInGroup || !targetInGroup) continue;
            group.Markers.TryGetValue(previousClip, out List<AnimationSyncMarker>? sourceMarkers);
            group.Markers.TryGetValue(nextClip, out List<AnimationSyncMarker>? targetMarkers);
            renderer.AlignPlaybackPhase(AnimationSyncMath.MapPhase(previousPhase, sourceMarkers, targetMarkers));
            return;
        }
    }
}
