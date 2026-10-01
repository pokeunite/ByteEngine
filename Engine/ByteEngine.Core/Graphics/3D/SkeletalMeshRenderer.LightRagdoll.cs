using System.Numerics;

namespace ByteEngine.Core.Graphics.ThreeD;

public sealed partial class SkeletalMeshRenderer
{
    // A small, bounded angular-spring solver. It keeps the current animated
    // pose as its rest pose and reuses the normal CPU skinning/socket pipeline.
    // Whole-character collision and translation remain the owner's Rigidbody's
    // responsibility; this is deliberately not a per-bone collision rig.
    private bool _lightRagdollActive;
    private Matrix4x4[] _lightRagdollRestPose = Array.Empty<Matrix4x4>();
    private LightRagdollBone[] _lightRagdollBones = Array.Empty<LightRagdollBone>();
    private Matrix4x4 _lightRagdollCorrection = Matrix4x4.Identity;
    private float _hitReaction;
    private Matrix4x4[] _recoveryPose = Array.Empty<Matrix4x4>();
    private float _recoveryDuration;
    private float _recoveryElapsed;

    public bool LightRagdollActive => _lightRagdollActive;
    public bool LightRagdollRecovering => _recoveryDuration > 0f;

    /// <summary>Blend the simulated pose back into the currently configured animation.</summary>
    public void EndLightRagdoll(float blendSeconds)
    {
        if (!_lightRagdollActive) return;
        _recoveryPose = (Matrix4x4[])BuildLightRagdollPose().Clone();
        _lightRagdollActive = false;
        _lightRagdollBones = Array.Empty<LightRagdollBone>();
        _lightRagdollRestPose = Array.Empty<Matrix4x4>();
        _recoveryDuration = Math.Clamp(blendSeconds, .05f, 2f);
        _recoveryElapsed = 0f;
        IsPlaying = true;
        _poseDirty = true;
    }

    public void ApplyHitReaction(float strength)
    {
        if (_lightRagdollActive || !float.IsFinite(strength) || strength <= 0f)
            return;
        _hitReaction = Math.Clamp(_hitReaction + strength / 30f, 0f, .65f);
        _poseDirty = true;
    }

    public bool BeginLightRagdoll(float strength = 1f)
    {
        if (_lightRagdollActive) return true;
        if (!_resolved && !ResolveRuntimeResources()) return false;
        if (_skeleton == null || _nodes.Length == 0 || _boneNodeIndices.Length == 0)
            return false;
        if (_poseDirty) UpdatePoseAndMeshes();

        // Freeze precisely the last visible pose, including blends and IK.
        _lightRagdollRestPose = (Matrix4x4[])BuildLocalPose().Clone();
        _lightRagdollCorrection = _currentRootMotionCorrection;
        var bones = new List<LightRagdollBone>(_skeleton.Bones.Count);
        for (int i = 0; i < _boneNodeIndices.Length; i++)
        {
            int node = _boneNodeIndices[i];
            if (node < 0 || node >= _lightRagdollRestPose.Length ||
                bones.Any(existing => existing.NodeIndex == node))
                continue;
            Vector3 target = RagdollTarget(_skeleton.Bones[i].Name,
                node == _rootMotionNodeIndex);
            bones.Add(new LightRagdollBone(node, target,
                new Vector3(.12f * (i % 3 - 1), .08f * (i % 2 == 0 ? 1 : -1),
                    .18f * Math.Clamp(strength, 0f, 3f))));
        }
        if (bones.Count == 0) return false;
        _lightRagdollBones = bones.ToArray();
        _lightRagdollActive = true;
        _recoveryDuration = 0f;
        _recoveryPose = Array.Empty<Matrix4x4>();
        _hitReaction = 0f;
        IsPlaying = false;
        _poseDirty = true;
        UpdatePoseAndMeshes();
        return true;
    }

    private void AdvanceLightRagdoll(float deltaTime)
    {
        float dt = Math.Clamp(float.IsFinite(deltaTime) ? deltaTime : 0f, 0f, .05f);
        if (dt <= 0f) return;
        foreach (LightRagdollBone bone in _lightRagdollBones)
        {
            bone.Velocity += (bone.Target - bone.Angle) * (34f * dt);
            bone.Velocity *= MathF.Exp(-7f * dt);
            bone.Angle += bone.Velocity * dt;
            bone.Angle = Vector3.Clamp(bone.Angle,
                new Vector3(-1.5f), new Vector3(1.5f));
        }
    }

    private Matrix4x4[] BuildLightRagdollPose()
    {
        Matrix4x4[] locals = _localPoseMatrices.Length == _lightRagdollRestPose.Length
            ? _localPoseMatrices
            : _localPoseMatrices = new Matrix4x4[_lightRagdollRestPose.Length];
        Array.Copy(_lightRagdollRestPose, locals, locals.Length);
        foreach (LightRagdollBone bone in _lightRagdollBones)
            RotateLocal(locals, bone.NodeIndex, bone.Angle);
        return locals;
    }

    private void ApplyRecoveryPose(Matrix4x4[] locals)
    {
        if (_recoveryDuration <= 0f || _recoveryPose.Length != locals.Length) return;
        float alpha = Math.Clamp(_recoveryElapsed / _recoveryDuration, 0f, 1f);
        alpha = alpha * alpha * (3f - 2f * alpha);
        for (int i = 0; i < locals.Length; i++)
        {
            if (!Matrix4x4.Decompose(_recoveryPose[i], out Vector3 fromScale,
                    out Quaternion fromRotation, out Vector3 fromPosition) ||
                !Matrix4x4.Decompose(locals[i], out Vector3 toScale,
                    out Quaternion toRotation, out Vector3 toPosition)) continue;
            locals[i] = Matrix4x4.CreateScale(Vector3.Lerp(fromScale, toScale, alpha)) *
                Matrix4x4.CreateFromQuaternion(Quaternion.Slerp(fromRotation, toRotation, alpha)) *
                Matrix4x4.CreateTranslation(Vector3.Lerp(fromPosition, toPosition, alpha));
        }
    }

    private void ApplyHitReactionPose(Matrix4x4[] locals)
    {
        int node = _rootMotionNodeIndex;
        if (_skeleton != null)
            for (int i = 0; i < _skeleton.Bones.Count; i++)
                if (_skeleton.Bones[i].Name.Contains("spine", StringComparison.OrdinalIgnoreCase) &&
                    _boneNodeIndices[i] >= 0)
                {
                    node = _boneNodeIndices[i];
                    break;
                }
        if (node >= 0 && node < locals.Length)
            RotateLocal(locals, node, new Vector3(-_hitReaction, 0f, _hitReaction * .3f));
    }

    private static void RotateLocal(Matrix4x4[] locals, int node, Vector3 angles)
    {
        Matrix4x4 source = locals[node];
        if (!Matrix4x4.Decompose(source, out Vector3 scale,
                out Quaternion rotation, out Vector3 position))
            return;
        Quaternion swing = Quaternion.CreateFromYawPitchRoll(angles.Y, angles.X, angles.Z);
        locals[node] = Matrix4x4.CreateScale(scale) *
            Matrix4x4.CreateFromQuaternion(Quaternion.Normalize(rotation * swing)) *
            Matrix4x4.CreateTranslation(position);
    }

    private static Vector3 RagdollTarget(string name, bool isRoot)
    {
        string lower = name.ToLowerInvariant();
        float side = lower.Contains("left") || lower.EndsWith("_l") ? -1f : 1f;
        if (isRoot || lower.Contains("hips") || lower.Contains("pelvis"))
            return new Vector3(.75f, .08f, .12f);
        if (lower.Contains("spine") || lower.Contains("chest"))
            return new Vector3(.45f, 0f, .1f);
        if (lower.Contains("arm") || lower.Contains("shoulder") || lower.Contains("clavicle"))
            return new Vector3(-.35f, 0f, side * .95f);
        if (lower.Contains("hand") || lower.Contains("wrist"))
            return new Vector3(-.2f, 0f, side * .45f);
        if (lower.Contains("leg") || lower.Contains("thigh") || lower.Contains("calf"))
            return new Vector3(-.48f, 0f, side * .18f);
        if (lower.Contains("head") || lower.Contains("neck"))
            return new Vector3(-.28f, 0f, .1f);
        return new Vector3(.08f, 0f, side * .06f);
    }

    private sealed class LightRagdollBone(int nodeIndex, Vector3 target, Vector3 velocity)
    {
        public int NodeIndex { get; } = nodeIndex;
        public Vector3 Target { get; } = target;
        public Vector3 Angle;
        public Vector3 Velocity = velocity;
    }
}
