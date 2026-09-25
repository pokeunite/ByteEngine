using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics.ThreeD;

public sealed partial class SkeletalMeshRenderer
{
    private AnimationProfile? _poseProfile;
    private AnimationBlendSpace? _activeBlendSpace;
    private AnimationPose? _workingPose;
    private AnimationPose? _samplePose;
    private AnimationPose? _referencePose;
    private AnimationPose? _blendPose;
    private AnimationPose? _lastBasePose;
    private AnimationPose? _sourceTransitionPose;
    private AnimationPose? _transitionWorkPose;
    private bool _hasEvaluatedBasePose;
    private bool _sourceTransitionActive;
    private float _sourceTransitionElapsed;
    private float _sourceTransitionDuration;
    private Vector3 _sourceTransitionRootTravel;
    private Matrix4x4[] _localPoseMatrices = Array.Empty<Matrix4x4>();
    private Matrix4x4[] _globalPoseMatrices = Array.Empty<Matrix4x4>();
    private byte[] _globalPoseStates = Array.Empty<byte>();
    private float[] _blendWeights = Array.Empty<float>();
    private float[] _layerWeights = Array.Empty<float>();
    private float[] _layerTargetWeights = Array.Empty<float>();
    private float[][] _layerMasks = Array.Empty<float[]>();
    private ImportedAnimation?[] _layerClips = Array.Empty<ImportedAnimation?>();
    private ImportedAnimation?[] _spaceClips = Array.Empty<ImportedAnimation?>();
    private float[] _spaceDurations = Array.Empty<float>();
    private readonly Dictionary<string, int> _poseNodeIndices = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<ImportedAnimation, ImportedAnimationChannel?[]> _poseChannels = new();
    private readonly Dictionary<string, (Vector3 Target, Vector3 Pole, float Weight)> _runtimeIkTargets =
        new(StringComparer.OrdinalIgnoreCase);
    private GameObject?[] _ikTargetObjects = Array.Empty<GameObject?>();
    private readonly List<AnimationIkChainProfile> _effectiveIkChains = new();
    private (int Root, int Mid, int End)[] _ikJointIndices = Array.Empty<(int, int, int)>();
    private bool[] _ikJointValid = Array.Empty<bool>();
    private Vector3[] _footTargets = Array.Empty<Vector3>();
    private Vector3[] _footNormals = Array.Empty<Vector3>();
    private bool[] _footGrounded = Array.Empty<bool>();
    private float[] _footClearances = Array.Empty<float>();
    private float[] _footContactWeights = Array.Empty<float>();
    private GameObject?[] _footSurfaces = Array.Empty<GameObject?>();
    private Vector3[] _footPoleDirections = Array.Empty<Vector3>();
    private bool[] _footPoleReady = Array.Empty<bool>();
    private int[] _footToeIndices = Array.Empty<int>();
    private float _pelvisOffset;
    private DateTime _lastFootIkTraceUtc;
    private GameObject? _lookAtObject;
    private GameObject? _footRayIgnore;
    private CharacterController3D? _footOwnerController;
    private float _blendX, _blendY, _aimYaw, _aimPitch, _smoothedAimYaw, _smoothedAimPitch;
    private float _smoothedLookYaw, _smoothedLookPitch;
    private Vector3? _lookAtWorldTarget;

    public string ActiveBlendSpaceName => _activeBlendSpace?.Name ?? string.Empty;
    public IReadOnlyList<float> ActiveLayerWeights => _layerWeights;
    public IReadOnlyList<float> ActiveBlendWeights => _blendWeights;
    public float BlendParameterX => _blendX;
    public float BlendParameterY => _blendY;
    public float BlendPlaybackScale => _activeBlendSpace != null && _currentAnimation != null
        ? AnimationBlendWeights.PlaybackScale(_currentAnimation.Duration, _blendWeights, _spaceDurations)
        : 1f;
    public float ActiveAimWeight => _poseProfile?.Procedural.AimEnabled == true
        ? _poseProfile.Procedural.AimWeight : 0f;

    public void ConfigurePoseProfile(AnimationProfile? profile)
    {
        _poseProfile = profile;
        RebindAdvancedPose();
        _poseDirty = true;
    }

    public void AlignPlaybackPhase(float phase)
    {
        if (_currentAnimation == null || !float.IsFinite(phase)) return;
        _currentTime = Math.Clamp(phase, 0f, 1f) * _currentAnimation.Duration;
        _poseDirty = true;
    }

    public float NormalizedPlaybackPhase => _currentAnimation is { Duration: > 1e-6f }
        ? Math.Clamp(_currentTime / _currentAnimation.Duration, 0f, 1f) : 0f;

    public void SetBlendSpace(AnimationBlendSpace? space, float x, float y, float transitionDuration = .15f)
    {
        bool changedSpace = !ReferenceEquals(_activeBlendSpace, space);
        if (changedSpace)
        {
            if (!_hasEvaluatedBasePose) CaptureCurrentBasePose();
            _sourceTransitionActive = _hasEvaluatedBasePose && transitionDuration > 1e-6f &&
                _sourceTransitionPose != null && _lastBasePose != null;
            if (_sourceTransitionActive)
            {
                _sourceTransitionPose!.CopyFrom(_lastBasePose!);
                _sourceTransitionRootTravel = _modelSpaceRootTravel;
                _sourceTransitionElapsed = 0f;
                _sourceTransitionDuration = transitionDuration;
            }
            _activeBlendSpace = space;
            RebindBlendSpace();
        }
        x = float.IsFinite(x) ? x : 0f;
        y = float.IsFinite(y) ? y : 0f;
        float response = 3f / MathF.Max(.05f, transitionDuration);
        float alpha = changedSpace ? 1f : AnimationPoseMath.ExponentialAlpha(
            response, (float)ByteEngine.Core.Time.DeltaTime);
        _blendX += (x - _blendX) * alpha;
        _blendY += (y - _blendY) * alpha;
        if (_activeBlendSpace != null)
            AnimationBlendWeights.Evaluate(_activeBlendSpace, _blendX, _blendY, _blendWeights);
        _poseDirty = true;
    }

    public void SetLayerParameterWeight(int index, float weight)
    {
        if (index < 0 || index >= _layerTargetWeights.Length) return;
        float clamped = Math.Clamp(float.IsFinite(weight) ? weight : 0f, 0f, 1f);
        if (_layerTargetWeights[index] == clamped) return;
        _layerTargetWeights[index] = clamped;
        _poseDirty = true;
    }

    public void SetAim(float yawDegrees, float pitchDegrees)
    {
        if (!float.IsFinite(yawDegrees) || !float.IsFinite(pitchDegrees)) return;
        _aimYaw = yawDegrees;
        _aimPitch = pitchDegrees;
        _poseDirty = true;
    }

    public void SetLookAtTarget(Vector3? worldTarget)
    {
        _lookAtWorldTarget = worldTarget;
        _poseDirty = true;
    }

    public void SetIkTarget(string chain, Vector3 worldTarget, Vector3 worldPole, float weight = 1f)
    {
        if (string.IsNullOrWhiteSpace(chain)) return;
        _runtimeIkTargets[chain] = (worldTarget, worldPole, Math.Clamp(weight, 0f, 1f));
        _poseDirty = true;
    }

    public void ClearIkTarget(string chain)
    {
        if (_runtimeIkTargets.Remove(chain)) _poseDirty = true;
    }

    private bool HasAdvancedPose => _poseProfile != null &&
        (_activeBlendSpace != null || _sourceTransitionActive || _poseProfile.Layers.Count != 0 ||
         _poseProfile.Procedural.AimEnabled || _poseProfile.Procedural.LookAtEnabled ||
         _poseProfile.Procedural.HandIkEnabled || _poseProfile.Procedural.FootIkEnabled);

    private void CaptureCurrentBasePose()
    {
        if (_lastBasePose == null || _nodes.Length == 0) return;
        for (int i = 0; i < _lastBasePose.Count; i++)
        {
            PoseTransform value = SampleNode(i, _currentAnimation, _currentTime);
            _lastBasePose[i] = new AnimationBoneTransform(value.Position, value.Rotation, value.Scale);
        }
        _hasEvaluatedBasePose = true;
    }

    private void RebindAdvancedPose()
    {
        if (_model == null || _nodes.Length == 0) return;
        int count = _nodes.Length;
        _workingPose = new AnimationPose(count);
        _samplePose = new AnimationPose(count);
        _blendPose = new AnimationPose(count);
        _lastBasePose = new AnimationPose(count);
        _sourceTransitionPose = new AnimationPose(count);
        _transitionWorkPose = new AnimationPose(count);
        _hasEvaluatedBasePose = false;
        _sourceTransitionActive = false;
        _referencePose = new AnimationPose(count);
        _poseNodeIndices.Clear();
        _poseChannels.Clear();
        for (int i = 0; i < count; i++)
        {
            _poseNodeIndices.TryAdd(_nodes[i].Name, i);
            _referencePose[i] = new AnimationBoneTransform(_basePositions[i], _baseRotations[i], _baseScales[i]);
        }
        foreach (ImportedAnimation animation in _model.Animations)
        {
            var channels = new ImportedAnimationChannel?[count];
            for (int i = 0; i < count; i++)
                channels[i] = animation.FindChannel(_nodes[i].Name);
            _poseChannels[animation] = channels;
        }
        int layerCount = _poseProfile?.Layers.Count ?? 0;
        _layerWeights = new float[layerCount];
        _layerTargetWeights = new float[layerCount];
        Array.Fill(_layerTargetWeights, 1f);
        _layerMasks = new float[layerCount][];
        _layerClips = new ImportedAnimation?[layerCount];
        for (int i = 0; i < layerCount; i++)
        {
            AnimationLayerProfile layer = _poseProfile!.Layers[i];
            _layerClips[i] = FindAnimation(layer.Clip);
            _layerMasks[i] = new float[count];
            BuildMask(layer.Mask, _layerMasks[i]);
        }
        _effectiveIkChains.Clear();
        if (_poseProfile != null)
        {
            _effectiveIkChains.AddRange(_poseProfile.Procedural.IkChains);
            if (_poseProfile.Procedural.FootIkEnabled)
            {
                AddMappedFootChain("LeftFoot", HumanoidBone.LeftUpperLeg, HumanoidBone.LeftLowerLeg, HumanoidBone.LeftFoot);
                AddMappedFootChain("RightFoot", HumanoidBone.RightUpperLeg, HumanoidBone.RightLowerLeg, HumanoidBone.RightFoot);
            }
        }
        _ikTargetObjects = new GameObject?[_effectiveIkChains.Count];
        _ikJointIndices = new (int, int, int)[_effectiveIkChains.Count];
        _ikJointValid = new bool[_effectiveIkChains.Count];
        _footTargets = new Vector3[_effectiveIkChains.Count];
        _footNormals = new Vector3[_effectiveIkChains.Count];
        _footGrounded = new bool[_effectiveIkChains.Count];
        _footClearances = new float[_effectiveIkChains.Count];
        _footSurfaces = new GameObject?[_effectiveIkChains.Count];
        _footPoleDirections = new Vector3[_effectiveIkChains.Count];
        _footPoleReady = new bool[_effectiveIkChains.Count];
        _footContactWeights = new float[_effectiveIkChains.Count];
        _footToeIndices = new int[_effectiveIkChains.Count];
        Array.Fill(_footToeIndices, -1);
        int leftFoot = ResolveNode(_model?.HumanoidMapping.GetBoneName(HumanoidBone.LeftFoot));
        int rightFoot = ResolveNode(_model?.HumanoidMapping.GetBoneName(HumanoidBone.RightFoot));
        int leftToe = ResolveNode(_model?.HumanoidMapping.GetBoneName(HumanoidBone.LeftToes));
        int rightToe = ResolveNode(_model?.HumanoidMapping.GetBoneName(HumanoidBone.RightToes));
        for (int i = 0; i < _ikTargetObjects.Length; i++)
        {
            AnimationIkChainProfile chain = _effectiveIkChains[i];
            _ikJointIndices[i] = (ResolveNode(chain.RootBone), ResolveNode(chain.MidBone), ResolveNode(chain.EndBone));
            (int root, int mid, int end) = _ikJointIndices[i];
            _ikJointValid[i] = root >= 0 && mid >= 0 && end >= 0 &&
                IsDescendantOf(mid, root) && IsDescendantOf(end, mid);
            int toe = end == leftFoot ? leftToe : end == rightFoot ? rightToe : -1;
            if (chain.FootGrounding && toe >= 0 && IsDescendantOf(toe, end))
                _footToeIndices[i] = toe;
            if (!string.IsNullOrWhiteSpace(chain.TargetObject))
                _ikTargetObjects[i] = GameObject.Scene?.FindGameObject(chain.TargetObject);
        }
        _footRayIgnore = GameObject;
        _footOwnerController = null;
        for (GameObject? ancestor = GameObject; ancestor != null; ancestor = ancestor.Parent)
            if (ancestor.GetComponent<CharacterController3D>() is { } controller)
            { _footRayIgnore = ancestor; _footOwnerController = controller; break; }
        string lookAtName = _poseProfile?.Procedural.LookAtTarget ?? string.Empty;
        _lookAtObject = !string.IsNullOrWhiteSpace(lookAtName)
            ? GameObject.Scene?.FindGameObject(lookAtName) : null;
        RebindBlendSpace();
    }

    private void AddMappedFootChain(string name, HumanoidBone root, HumanoidBone mid, HumanoidBone end)
    {
        if (_model == null || _effectiveIkChains.Any(chain =>
            chain.FootGrounding && string.Equals(chain.Name, name, StringComparison.OrdinalIgnoreCase)))
            return;
        string? rootName = _model.HumanoidMapping.GetBoneName(root);
        string? midName = _model.HumanoidMapping.GetBoneName(mid);
        string? endName = _model.HumanoidMapping.GetBoneName(end);
        if (ResolveNode(rootName) < 0 || ResolveNode(midName) < 0 || ResolveNode(endName) < 0) return;
        _effectiveIkChains.Add(new AnimationIkChainProfile
        {
            Name = name, RootBone = rootName!, MidBone = midName!, EndBone = endName!,
            FootGrounding = true, PoleOffset = Vector3.Zero
        });
    }

    private void RebindBlendSpace()
    {
        int count = _activeBlendSpace?.Samples.Count ?? 0;
        _blendWeights = new float[count];
        _spaceClips = new ImportedAnimation?[count];
        _spaceDurations = new float[count];
        for (int i = 0; i < count; i++)
        {
            _spaceClips[i] = FindAnimation(_activeBlendSpace!.Samples[i].Clip);
            _spaceDurations[i] = _spaceClips[i]?.Duration ?? 0f;
        }
    }

    private void BuildMask(AnimationBoneMask mask, float[] result)
    {
        int root = ResolveNode(mask.RootBone);
        if (root < 0 && _model != null && mask.Kind is AnimationBoneMaskKind.UpperBody or AnimationBoneMaskKind.LowerBody)
            root = ResolveNode(_model.HumanoidMapping.GetBoneName(HumanoidBone.Spine));
        for (int i = 0; i < result.Length; i++)
        {
            bool descendant = root >= 0 && IsDescendantOf(i, root);
            result[i] = mask.Kind switch
            {
                AnimationBoneMaskKind.FullBody => 1f,
                AnimationBoneMaskKind.UpperBody => descendant ? 1f : 0f,
                AnimationBoneMaskKind.LowerBody => descendant ? 0f : 1f,
                _ => descendant || (root < 0 && mask.BoneWeights.Count == 0) ? 1f : 0f
            };
        }
        foreach (string excluded in mask.ExcludedBones)
        {
            int excludedRoot = ResolveNode(excluded);
            if (excludedRoot >= 0)
                for (int i = 0; i < result.Length; i++)
                    if (IsDescendantOf(i, excludedRoot)) result[i] = 0f;
        }
        foreach (var pair in mask.BoneWeights)
        {
            int index = ResolveNode(pair.Key);
            if (index >= 0) result[index] = Math.Clamp(pair.Value, 0f, 1f);
        }
    }

    private bool IsDescendantOf(int node, int root)
    {
        for (int current = node; current >= 0; current = _parentIndices[current])
            if (current == root) return true;
        return false;
    }

    private int ResolveNode(string? name) =>
        !string.IsNullOrWhiteSpace(name) && _poseNodeIndices.TryGetValue(name, out int index)
            ? index : -1;

    private ImportedAnimationChannel? CachedChannel(ImportedAnimation animation, int nodeIndex) =>
        _poseChannels.TryGetValue(animation, out ImportedAnimationChannel?[]? channels)
            ? channels[nodeIndex] : animation.FindChannel(_nodes[nodeIndex].Name);

    private Matrix4x4[] BuildAdvancedLocalPose()
    {
        if (_workingPose == null || _samplePose == null || _referencePose == null || _blendPose == null)
            RebindAdvancedPose();
        AnimationPose pose = _workingPose!;
        AnimationPose sample = _samplePose!;
        AnimationPose blendPose = _blendPose!;
        AnimationPose referencePose = _referencePose!;
        float phase = _currentAnimation is { Duration: > 1e-6f }
            ? Math.Clamp(_currentTime / _currentAnimation.Duration, 0f, 1f) : 0f;
        float crossfade = _previousAnimation == null || _activeTransitionDuration <= 1e-6f
            ? 1f : Math.Clamp(_transitionElapsed / _activeTransitionDuration, 0f, 1f);

        for (int i = 0; i < pose.Count; i++)
        {
            PoseTransform current = SampleNode(i, _currentAnimation, _currentTime);
            if (_previousAnimation != null && crossfade < 1f)
                current = PoseTransform.Lerp(SampleNode(i, _previousAnimation, _previousTime), current, crossfade);
            pose[i] = new AnimationBoneTransform(current.Position, current.Rotation, current.Scale);
        }

        if (_activeBlendSpace != null && _spaceClips.Length != 0)
        {
            AnimationBlendWeights.Evaluate(_activeBlendSpace, _blendX, _blendY, _blendWeights);
            float total = 0f;
            for (int s = 0; s < _spaceClips.Length; s++)
            {
                ImportedAnimation? clip = _spaceClips[s];
                float weight = _blendWeights[s];
                if (clip == null || weight <= 1e-6f) continue;
                SamplePose(clip, phase * clip.Duration, sample);
                if (total == 0f) blendPose.CopyFrom(sample);
                else blendPose.Blend(sample, weight / (total + weight));
                total += weight;
            }
            if (total > 0f) pose.CopyFrom(blendPose);
        }

        if (_sourceTransitionActive && _sourceTransitionPose != null && _transitionWorkPose != null)
        {
            _sourceTransitionElapsed += Math.Max((float)ByteEngine.Core.Time.DeltaTime, 0f);
            float alpha = Math.Clamp(_sourceTransitionElapsed / _sourceTransitionDuration, 0f, 1f);
            _transitionWorkPose.CopyFrom(_sourceTransitionPose);
            _transitionWorkPose.Blend(pose, alpha);
            pose.CopyFrom(_transitionWorkPose);
            if (alpha >= 1f) _sourceTransitionActive = false;
        }
        _lastBasePose?.CopyFrom(pose);
        _hasEvaluatedBasePose = true;

        for (int i = 0; i < _layerClips.Length; i++)
        {
            AnimationLayerProfile layer = _poseProfile!.Layers[i];
            ImportedAnimation? clip = _layerClips[i];
            float goal = layer.Enabled && clip != null ? layer.Weight * _layerTargetWeights[i] : 0f;
            float seconds = goal > _layerWeights[i] ? layer.BlendIn : layer.BlendOut;
            float step = seconds <= 1e-6f ? 1f :
                Math.Clamp((float)ByteEngine.Core.Time.DeltaTime / seconds, 0f, 1f);
            _layerWeights[i] += (goal - _layerWeights[i]) * step;
            if (clip == null || _layerWeights[i] <= 1e-6f) continue;
            SamplePose(clip, phase * clip.Duration, sample);
            if (layer.BlendMode == AnimationLayerBlendMode.Additive)
                pose.Additive(sample, referencePose, _layerWeights[i], _layerMasks[i]);
            else
                pose.Blend(sample, _layerWeights[i], _layerMasks[i]);
        }

        _currentRootMotionCorrection = BuildModelSpaceRootMotionCorrection();
        ApplyAimAndLookAt(pose);
        ApplyIk(pose);
        Matrix4x4[] locals = _localPoseMatrices.Length == pose.Count
            ? _localPoseMatrices : _localPoseMatrices = new Matrix4x4[pose.Count];
        for (int i = 0; i < pose.Count; i++)
        {
            AnimationBoneTransform bone = pose[i];
            locals[i] = Matrix4x4.CreateScale(bone.Scale) *
                Matrix4x4.CreateFromQuaternion(bone.Rotation) *
                Matrix4x4.CreateTranslation(bone.Position);
        }
        return locals;
    }

    private void SamplePose(ImportedAnimation clip, float time, AnimationPose destination)
    {
        for (int i = 0; i < destination.Count; i++)
        {
            PoseTransform value = SampleNode(i, clip, time);
            destination[i] = new AnimationBoneTransform(value.Position, value.Rotation, value.Scale);
        }
    }

    private void ApplyAimAndLookAt(AnimationPose pose)
    {
        AnimationProceduralProfile settings = _poseProfile!.Procedural;
        float smooth = settings.AimSmoothing <= 0f ? 1f :
            1f - MathF.Exp(-settings.AimSmoothing * Math.Max((float)ByteEngine.Core.Time.DeltaTime, 0f));
        _smoothedAimYaw += (_aimYaw - _smoothedAimYaw) * smooth;
        _smoothedAimPitch += (_aimPitch - _smoothedAimPitch) * smooth;
        if (settings.AimEnabled)
        {
            int explicitBone = ResolveNode(settings.AimBone);
            if (explicitBone >= 0)
            {
                ApplyLocalAim(pose, explicitBone, settings, settings.AimWeight);
            }
            else
            {
                int spine = ResolveNode(_model?.HumanoidMapping.GetBoneName(HumanoidBone.Spine));
                int chest = ResolveNode(_model?.HumanoidMapping.GetBoneName(HumanoidBone.Chest));
                int aimHead = ResolveVisibleHeadNode();
                float total = (spine >= 0 ? .25f : 0f) + (chest >= 0 && chest != spine ? .55f : 0f) +
                    (aimHead >= 0 && aimHead != chest && aimHead != spine ? .20f : 0f);
                if (total > 0f)
                {
                    if (spine >= 0) ApplyLocalAim(pose, spine, settings, settings.AimWeight * .25f / total);
                    if (chest >= 0 && chest != spine)
                        ApplyLocalAim(pose, chest, settings, settings.AimWeight * .55f / total);
                    if (aimHead >= 0 && aimHead != chest && aimHead != spine)
                        ApplyLocalAim(pose, aimHead, settings, settings.AimWeight * .20f / total);
                }
            }
        }
        Vector3? target = _lookAtWorldTarget ?? (_lookAtObject != null ? _lookAtObject.Transform.WorldMatrix.Translation : null);
        if (!settings.LookAtEnabled || !target.HasValue) return;
        int head = ResolveNode(settings.LookAtBone);
        if (head < 0) head = ResolveVisibleHeadNode();
        if (head < 0) return;
        Matrix4x4[] globals = PoseGlobals(pose);
        if (!Matrix4x4.Invert(Transform.WorldMatrix, out Matrix4x4 inverse)) return;
        Vector3 localTarget = Vector3.Transform(target.Value, inverse);
        Vector3 direction = localTarget - globals[head].Translation;
        if (direction.LengthSquared() < 1e-8f) return;
        direction = Vector3.Normalize(direction);
        if (!Matrix4x4.Decompose(globals[head], out _, out Quaternion headRotation, out _)) return;
        Vector3 headLocalDirection = Vector3.Transform(direction, Quaternion.Inverse(headRotation));
        float yaw = MathF.Atan2(headLocalDirection.X, -headLocalDirection.Z) * 180f / MathF.PI;
        float pitch = MathF.Asin(Math.Clamp(headLocalDirection.Y, -1f, 1f)) * 180f / MathF.PI;
        _smoothedLookYaw += (yaw - _smoothedLookYaw) * smooth;
        _smoothedLookPitch += (pitch - _smoothedLookPitch) * smooth;
        Quaternion correction = AnimationPoseMath.ClampAim(_smoothedLookYaw, _smoothedLookPitch,
            settings.AimYawLimit, settings.AimPitchLimit, settings.LookAtWeight);
        AnimationBoneTransform headPose = pose[head];
        pose[head] = headPose with { Rotation = Quaternion.Normalize(headPose.Rotation * correction) };
    }

    private void ApplyLocalAim(AnimationPose pose, int bone, AnimationProceduralProfile settings, float weight)
    {
        AnimationBoneTransform value = pose[bone];
        Quaternion offset = AnimationPoseMath.ClampAim(_smoothedAimYaw, _smoothedAimPitch,
            settings.AimYawLimit, settings.AimPitchLimit, weight);
        pose[bone] = value with { Rotation = Quaternion.Normalize(value.Rotation * offset) };
    }

    private int ResolveVisibleHeadNode()
    {
        int mappedHead = ResolveNode(_model?.HumanoidMapping.GetBoneName(HumanoidBone.Head));
        if (mappedHead >= 0 &&
            !_nodes[mappedHead].Name.EndsWith("End", StringComparison.OrdinalIgnoreCase))
            return mappedHead;
        int mappedNeck = ResolveNode(_model?.HumanoidMapping.GetBoneName(HumanoidBone.Neck));
        if (mappedNeck >= 0) return mappedNeck;
        return mappedHead >= 0 ? _parentIndices[mappedHead] : -1;
    }

    private void ApplyIk(AnimationPose pose)
    {
        if (_poseProfile == null) return;
        AnimationProceduralProfile settings = _poseProfile.Procedural;
        if (!settings.HandIkEnabled && !settings.FootIkEnabled) return;
        Array.Clear(_footGrounded);
        Array.Clear(_footContactWeights);
        Array.Clear(_footSurfaces);
        Matrix4x4 poseWorld = _currentRootMotionCorrection * Transform.WorldMatrix;
        bool traceFootIk = settings.FootIkEnabled && RuntimeDiagnostics.DebugFootIk &&
            (DateTime.UtcNow - _lastFootIkTraceUtc).TotalMilliseconds >= 100;
        if (traceFootIk)
        {
            _lastFootIkTraceUtc = DateTime.UtcNow;
            RuntimeDiagnostics.RecordFootIk(
                $"frame object={GameObject.Name} model={_model?.Name ?? "<none>"} " +
                $"grounded={_footOwnerController?.IsGrounded.ToString() ?? "<no controller>"} " +
                $"scale={FootIkVector(Transform.WorldScale)} pelvisOffset={_pelvisOffset:0.###} " +
                $"rayDistance={settings.FootRayDistance:0.###} footOffset={settings.FootOffset:0.###}");
        }

        // Find ground from the unmodified animation pose, then lower the hips
        // before solving both legs so neither chain is forced beyond its reach.
        float desiredPelvis = 0f;
        if (settings.FootIkEnabled && GameObject.Scene != null &&
            (_footOwnerController?.IsGrounded ?? true))
        {
            Matrix4x4[] initialGlobals = PoseGlobals(pose);
            for (int c = 0; c < _effectiveIkChains.Count; c++)
            {
                AnimationIkChainProfile chain = _effectiveIkChains[c];
                int end = _ikJointIndices[c].End;
                if (!chain.FootGrounding || end < 0 || _runtimeIkTargets.ContainsKey(chain.Name) ||
                    _ikTargetObjects[c] != null) continue;
                Vector3 footWorld = Vector3.Transform(initialGlobals[end].Translation, poseWorld);
                Vector3 origin = footWorld + Vector3.UnitY * settings.FootRayDistance;
                if (!GameplayQuery3D.Raycast(GameObject.Scene, origin, -Vector3.UnitY,
                    out RaycastHit3D hit, settings.FootRayDistance * 2f, _footRayIgnore ?? GameObject))
                {
                    if (traceFootIk) RuntimeDiagnostics.RecordFootIk(
                        $"ray chain={chain.Name} root={chain.RootBone} mid={chain.MidBone} end={chain.EndBone} " +
                        $"miss footWorld={FootIkVector(footWorld)} origin={FootIkVector(origin)}");
                    continue;
                }
                if (!AnimationPoseMath.IsUsableFootGroundHit(hit.Distance, hit.Normal,
                    footWorld.Y, hit.Point.Y, settings.FootRayDistance))
                {
                    if (traceFootIk) RuntimeDiagnostics.RecordFootIk(
                        $"ray chain={chain.Name} rejected hit={hit.GameObject.Name} " +
                        $"point={FootIkVector(hit.Point)} normal={FootIkVector(hit.Normal)} " +
                        $"distance={hit.Distance:0.###} footWorld={FootIkVector(footWorld)}");
                    continue;
                }
                float soleOffset = settings.FootOffset;
                int toe = _footToeIndices[c];
                if (toe >= 0)
                {
                    Vector3 toeWorld = Vector3.Transform(initialGlobals[toe].Translation, poseWorld);
                    soleOffset = AnimationPoseMath.FootSoleOffset(footWorld, toeWorld,
                        hit.Normal, settings.FootOffset);
                }
                soleOffset = MathF.Max(0f, soleOffset);
                _footGrounded[c] = true;
                _footSurfaces[c] = hit.GameObject;
                _footTargets[c] = hit.Point + hit.Normal * soleOffset;
                _footNormals[c] = hit.Normal;
                _footClearances[c] = Vector3.Dot(footWorld - hit.Point, hit.Normal) - soleOffset;
                if (traceFootIk) RuntimeDiagnostics.RecordFootIk(
                    $"ray chain={chain.Name} root={chain.RootBone} mid={chain.MidBone} end={chain.EndBone} " +
                    $"hit={hit.GameObject.Name} footWorld={FootIkVector(footWorld)} " +
                    $"point={FootIkVector(hit.Point)} normal={FootIkVector(hit.Normal)} " +
                    $"targetWorld={FootIkVector(_footTargets[c])} soleOffset={soleOffset:0.###} " +
                    $"clearance={_footClearances[c]:0.###} distance={hit.Distance:0.###}");
            }
            float lowestClearance = float.PositiveInfinity;
            for (int c = 0; c < _footGrounded.Length; c++)
                if (_footGrounded[c]) lowestClearance = MathF.Min(lowestClearance, _footClearances[c]);
            for (int c = 0; c < _footGrounded.Length; c++)
            {
                if (!_footGrounded[c] || !_ikJointValid[c]) continue;
                (int root, int mid, int end) = _ikJointIndices[c];
                Vector3 rootWorld = Vector3.Transform(initialGlobals[root].Translation, poseWorld);
                Vector3 kneeWorld = Vector3.Transform(initialGlobals[mid].Translation, poseWorld);
                Vector3 ankleWorld = Vector3.Transform(initialGlobals[end].Translation, poseWorld);
                float limbLength = Vector3.Distance(rootWorld, kneeWorld) +
                    Vector3.Distance(kneeWorld, ankleWorld);
                float relativeClearance = MathF.Max(0f, _footClearances[c] - lowestClearance);
                float contactWeight = AnimationPoseMath.FootContactWeight(
                    _footClearances[c], relativeClearance, limbLength);
                if (_footOwnerController != null)
                    contactWeight *= AnimationPoseMath.FootMotionWeight(_footOwnerController.Speed);
                _footContactWeights[c] = contactWeight;
                float reachExcess = MathF.Max(0f, Vector3.Distance(rootWorld, _footTargets[c]) -
                    (limbLength - .01f));
                if (_footTargets[c].Y < rootWorld.Y)
                    desiredPelvis = MathF.Min(desiredPelvis,
                        -MathF.Min(reachExcess, limbLength * .12f) * contactWeight);
                if (traceFootIk) RuntimeDiagnostics.RecordFootIk(
                    $"contact chain={_effectiveIkChains[c].Name} " +
                    $"clearance={_footClearances[c]:0.###} relative={relativeClearance:0.###} " +
                    $"weight={contactWeight:0.###} limbLength={limbLength:0.###} " +
                    $"reachExcess={reachExcess:0.###} desiredPelvis={desiredPelvis:0.###}");
            }
            // Use this frame's contact. A filtered world-space target can trail
            // the moving character and pull the legs out of shape.
            for (int c = 0; c < _effectiveIkChains.Count; c++)
                if (!_footGrounded[c]) _footPoleReady[c] = false;
            int hips = ResolveNode(_model?.HumanoidMapping.GetBoneName(HumanoidBone.Hips));
            if (hips >= 0 && settings.PelvisCompensation > 0f)
            {
                float smooth = 1f - MathF.Exp(-12f * Math.Max((float)ByteEngine.Core.Time.DeltaTime, 0f));
                _pelvisOffset += (desiredPelvis - _pelvisOffset) * smooth;
                int parent = _parentIndices[hips];
                Matrix4x4 parentWorld = parent >= 0
                    ? initialGlobals[parent] * poseWorld : poseWorld;
                if (Matrix4x4.Invert(parentWorld, out Matrix4x4 parentInverse))
                {
                    Vector3 localDrop = Vector3.TransformNormal(Vector3.UnitY * _pelvisOffset *
                        settings.PelvisCompensation, parentInverse);
                    AnimationBoneTransform hipPose = pose[hips];
                    pose[hips] = hipPose with { Position = hipPose.Position + localDrop };
                }
            }
        }
        else
        {
            _pelvisOffset = 0f;
            Array.Clear(_footPoleReady);
            if (traceFootIk)
                RuntimeDiagnostics.RecordFootIk(
                    $"ground solve skipped: scene={(GameObject.Scene == null ? "missing" : "loaded")} " +
                    $"characterGrounded={_footOwnerController?.IsGrounded.ToString() ?? "<no controller>"}");
        }

        if (!Matrix4x4.Invert(poseWorld, out Matrix4x4 worldInverse)) return;
        for (int c = 0; c < _effectiveIkChains.Count; c++)
        {
            AnimationIkChainProfile chain = _effectiveIkChains[c];
            if (chain.FootGrounding ? !settings.FootIkEnabled : !settings.HandIkEnabled) continue;
            (int root, int mid, int end) = _ikJointIndices[c];
            if (!_ikJointValid[c])
            {
                if (traceFootIk && chain.FootGrounding)
                    RuntimeDiagnostics.RecordFootIk(
                        $"chain={chain.Name} invalid bone hierarchy root={chain.RootBone} " +
                        $"mid={chain.MidBone} end={chain.EndBone}");
                continue;
            }
            Matrix4x4[] globals = PoseGlobals(pose);
            Vector3 targetWorld, poleWorld;
            float weight = chain.Weight;
            bool groundAligned = false;
            if (_runtimeIkTargets.TryGetValue(chain.Name, out var input))
            {
                targetWorld = input.Target;
                poleWorld = input.Pole;
                weight *= input.Weight;
            }
            else if (_ikTargetObjects[c] != null)
            {
                targetWorld = _ikTargetObjects[c]!.Transform.WorldMatrix.Translation;
                poleWorld = poseWorld.Translation + chain.PoleOffset;
            }
            else if (chain.FootGrounding && _footGrounded[c])
            {
                targetWorld = _footTargets[c];
                Vector3 kneePole = globals[mid].Translation;
                int toe = _footToeIndices[c];
                if (toe >= 0)
                    kneePole = AnimationPoseMath.FootKneePole(
                        globals[root].Translation, globals[mid].Translation,
                        globals[end].Translation, globals[toe].Translation,
                        Vector3.Transform(targetWorld, worldInverse));
                Vector3 hipModel = globals[root].Translation;
                Vector3 targetModel = Vector3.Transform(targetWorld, worldInverse);
                Vector3 axis = targetModel - hipModel;
                Vector3 poleDirection = kneePole - hipModel;
                if (axis.LengthSquared() > 1e-8f)
                    poleDirection -= Vector3.Dot(poleDirection, Vector3.Normalize(axis)) *
                        Vector3.Normalize(axis);
                if (poleDirection.LengthSquared() > 1e-8f)
                {
                    poleDirection = Vector3.Normalize(poleDirection);
                    if (_footPoleReady[c])
                        poleDirection = AnimationPoseMath.SmoothDirection(
                            _footPoleDirections[c], poleDirection,
                            (float)ByteEngine.Core.Time.DeltaTime, 14f);
                    _footPoleDirections[c] = poleDirection;
                    _footPoleReady[c] = true;
                    kneePole = hipModel + poleDirection *
                        (Vector3.Distance(hipModel, globals[mid].Translation) +
                         Vector3.Distance(globals[mid].Translation, globals[end].Translation));
                }
                poleWorld = Vector3.Transform(kneePole, poseWorld) +
                    Vector3.TransformNormal(chain.PoleOffset, poseWorld);
                groundAligned = true;
                weight *= _footContactWeights[c];
            }
            else
            {
                if (traceFootIk && chain.FootGrounding)
                    RuntimeDiagnostics.RecordFootIk(
                        $"chain={chain.Name} skipped: no ground hit or IK target");
                continue;
            }
            if (weight <= .001f)
            {
                if (traceFootIk && chain.FootGrounding)
                    RuntimeDiagnostics.RecordFootIk(
                        $"chain={chain.Name} skipped: swing foot contact weight={weight:0.###}");
                continue;
            }
            Vector3 target = Vector3.Transform(targetWorld, worldInverse);
            Vector3 pole = Vector3.Transform(poleWorld, worldInverse);
            Vector3 rootAt = globals[root].Translation, midAt = globals[mid].Translation, endAt = globals[end].Translation;
            if (!AnimationPoseMath.SolveTwoBone(rootAt, midAt, endAt, target, pole,
                out Vector3 solvedMid, out Vector3 solvedEnd))
            {
                if (traceFootIk && chain.FootGrounding)
                    RuntimeDiagnostics.RecordFootIk(
                        $"chain={chain.Name} solve failed root={FootIkVector(rootAt)} " +
                        $"knee={FootIkVector(midAt)} ankle={FootIkVector(endAt)} " +
                        $"target={FootIkVector(target)} pole={FootIkVector(pole)}");
                continue;
            }
            weight = Math.Clamp(weight, 0f, 1f);
            Quaternion rootDelta = AnimationPoseMath.FromTo(midAt - rootAt, solvedMid - rootAt);
            Vector3 animatedKnee = midAt, animatedAnkle = endAt;
            ApplyModelRotation(pose, globals, root, rootDelta, weight);
            globals = PoseGlobals(pose);
            midAt = globals[mid].Translation;
            endAt = globals[end].Translation;
            Quaternion midDelta = AnimationPoseMath.FromTo(endAt - midAt, solvedEnd - solvedMid);
            ApplyModelRotation(pose, globals, mid, midDelta, weight);
            if (traceFootIk && chain.FootGrounding)
            {
                Matrix4x4[] finalGlobals = PoseGlobals(pose);
                Vector3 finalKnee = finalGlobals[mid].Translation;
                Vector3 finalAnkle = finalGlobals[end].Translation;
                Vector3 axis = target - rootAt;
                axis = axis.LengthSquared() > 1e-8f ? Vector3.Normalize(axis) : Vector3.UnitY;
                Vector3 animatedBend = animatedKnee - rootAt;
                animatedBend -= Vector3.Dot(animatedBend, axis) * axis;
                Vector3 finalBend = finalKnee - rootAt;
                finalBend -= Vector3.Dot(finalBend, axis) * axis;
                float bendDot = animatedBend.LengthSquared() > 1e-8f && finalBend.LengthSquared() > 1e-8f
                    ? Vector3.Dot(Vector3.Normalize(animatedBend), Vector3.Normalize(finalBend)) : 0f;
                RuntimeDiagnostics.RecordFootIk(
                    $"solve chain={chain.Name} weight={weight:0.###} root={FootIkVector(rootAt)} " +
                    $"animatedKnee={FootIkVector(animatedKnee)} animatedAnkle={FootIkVector(animatedAnkle)} " +
                    $"target={FootIkVector(target)} pole={FootIkVector(pole)} " +
                    $"solvedKnee={FootIkVector(solvedMid)} solvedAnkle={FootIkVector(solvedEnd)} " +
                    $"finalKnee={FootIkVector(finalKnee)} finalAnkle={FootIkVector(finalAnkle)} " +
                    $"ankleError={Vector3.Distance(finalAnkle, solvedEnd):0.###} bendDot={bendDot:0.###}");
            }
            if (!groundAligned) continue;
            globals = PoseGlobals(pose);
            // Tilt the animated foot by the surface slope. Aligning the foot bone's
            // local +Y directly to world up destroys imported rigs' authored roll.
            Quaternion inverseWorldRotation = Quaternion.Inverse(Transform.WorldRotation);
            Vector3 modelUp = Vector3.Transform(Vector3.UnitY, inverseWorldRotation);
            Vector3 modelNormal = Vector3.Transform(_footNormals[c], inverseWorldRotation);
            Quaternion footDelta = AnimationPoseMath.FromTo(modelUp, modelNormal);
            ApplyModelRotation(pose, globals, end, footDelta, weight);
        }
    }

    private static string FootIkVector(Vector3 value) =>
        FormattableString.Invariant($"({value.X:0.###},{value.Y:0.###},{value.Z:0.###})");

    private void ApplyModelRotation(AnimationPose pose, Matrix4x4[] globals, int node,
        Quaternion modelDelta, float weight)
    {
        Quaternion delta = Quaternion.Slerp(Quaternion.Identity, modelDelta, weight);
        int parent = _parentIndices[node];
        Quaternion parentRotation = Quaternion.Identity;
        if (parent >= 0) Matrix4x4.Decompose(globals[parent], out _, out parentRotation, out _);
        Quaternion localDelta = Quaternion.Inverse(parentRotation) * delta * parentRotation;
        AnimationBoneTransform value = pose[node];
        Quaternion rotation = Quaternion.Normalize(localDelta * value.Rotation);
        if (float.IsFinite(rotation.X) && float.IsFinite(rotation.Y) &&
            float.IsFinite(rotation.Z) && float.IsFinite(rotation.W))
            pose[node] = value with { Rotation = rotation };
    }

    private Matrix4x4[] PoseGlobals(AnimationPose pose)
    {
        Matrix4x4[] locals = _localPoseMatrices.Length == pose.Count
            ? _localPoseMatrices : _localPoseMatrices = new Matrix4x4[pose.Count];
        for (int i = 0; i < pose.Count; i++)
        {
            AnimationBoneTransform bone = pose[i];
            locals[i] = Matrix4x4.CreateScale(bone.Scale) *
                Matrix4x4.CreateFromQuaternion(bone.Rotation) *
                Matrix4x4.CreateTranslation(bone.Position);
        }
        return ComputeGlobals(locals);
    }
}
