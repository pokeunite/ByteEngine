using System.Numerics;

using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Animation;

/// <summary>
/// Attaches this GameObject to a live skeletal-animation bone.
///
/// Put the attachment object underneath the animated character in the
/// hierarchy, add BoneSocket3D, choose a bone, then use the explicit offset
/// fields to position/rotate/scale the attachment relative to that bone.
/// </summary>
public sealed class BoneSocket3D : Component
{
    public string BoneName { get; set; } = string.Empty;

    /// <summary>
    /// Local-space offset from the selected bone, in engine units.
    /// </summary>
    public Vector3 PositionOffset { get; set; } = Vector3.Zero;

    /// <summary>
    /// Local-space Euler rotation offset in degrees.
    /// </summary>
    public Vector3 RotationOffsetDegrees { get; set; } = Vector3.Zero;

    /// <summary>
    /// Additional scale applied after the bone transform.
    /// </summary>
    public Vector3 ScaleMultiplier { get; set; } = Vector3.One;

    /// <summary>
    /// When true, animated/model bone scale is inherited by the attachment.
    /// Disable this for attachments that should keep only their authored scale.
    /// </summary>
    public bool InheritBoneScale { get; set; } = true;

    public bool IsBound { get; private set; }

    public string SourceRendererName { get; private set; } = string.Empty;

    public override int UpdateOrder => 1000;

    protected override void OnUpdate()
    {
        ApplySocketTransform();
    }

    protected override void OnRender(RenderContext context)
    {
        // Editor scene rendering does not call OnUpdate(), so also resolve the
        // socket here. At runtime this keeps the final rendered attachment on
        // the exact pose that the skeletal mesh submits this frame.
        ApplySocketTransform();
    }

    /// <summary>
    /// Finds the nearest skeletal renderer associated with this attachment's
    /// parent character. A direct renderer on the same object is also allowed.
    /// </summary>
    public SkeletalMeshRenderer? ResolveSourceRenderer()
    {
        SkeletalMeshRenderer? direct =
            GameObject.GetComponent<SkeletalMeshRenderer>();

        if (direct != null)
        {
            return direct;
        }

        GameObject branchToSkip = GameObject;

        for (GameObject? ancestor = GameObject.Parent;
             ancestor != null;
             ancestor = ancestor.Parent)
        {
            direct =
                ancestor.GetComponent<SkeletalMeshRenderer>();

            if (direct != null)
            {
                return direct;
            }

            SkeletalMeshRenderer? nested =
                FindRendererInChildren(
                    ancestor,
                    branchToSkip);

            if (nested != null)
            {
                return nested;
            }

            branchToSkip =
                ancestor;
        }

        return null;
    }

    public bool ApplySocketTransform()
    {
        IsBound = false;
        SourceRendererName = string.Empty;

        if (string.IsNullOrWhiteSpace(BoneName))
        {
            return false;
        }

        SkeletalMeshRenderer? renderer =
            ResolveSourceRenderer();

        if (renderer == null)
        {
            return false;
        }

        SourceRendererName =
            renderer.GameObject.Name;

        if (!renderer.TryGetBoneWorldMatrix(
                BoneName,
                out Matrix4x4 boneWorld))
        {
            return false;
        }

        if (!InheritBoneScale &&
            Matrix4x4.Decompose(
                boneWorld,
                out _,
                out Quaternion boneRotation,
                out Vector3 bonePosition))
        {
            boneWorld =
                Matrix4x4.CreateFromQuaternion(
                    NormalizeSafe(boneRotation)) *
                Matrix4x4.CreateTranslation(
                    bonePosition);
        }

        Quaternion offsetRotation =
            Quaternion.CreateFromYawPitchRoll(
                Radians(RotationOffsetDegrees.Y),
                Radians(RotationOffsetDegrees.X),
                Radians(RotationOffsetDegrees.Z));

        Matrix4x4 offset =
            Matrix4x4.CreateScale(
                SanitizeScale(ScaleMultiplier)) *
            Matrix4x4.CreateFromQuaternion(
                offsetRotation) *
            Matrix4x4.CreateTranslation(
                PositionOffset);

        Matrix4x4 socketWorld =
            offset *
            boneWorld;

        if (!Matrix4x4.Decompose(
                socketWorld,
                out Vector3 worldScale,
                out Quaternion worldRotation,
                out Vector3 worldPosition))
        {
            return false;
        }

        Transform.WorldPosition =
            worldPosition;

        Transform.WorldRotation =
            NormalizeSafe(worldRotation);

        Transform.WorldScale =
            SanitizeScale(worldScale);

        IsBound = true;
        return true;
    }

    private static SkeletalMeshRenderer? FindRendererInChildren(
        GameObject root,
        GameObject branchToSkip)
    {
        foreach (GameObject child in root.Children)
        {
            if (ReferenceEquals(
                    child,
                    branchToSkip))
            {
                continue;
            }

            SkeletalMeshRenderer? renderer =
                child.GetComponent<SkeletalMeshRenderer>();

            if (renderer != null)
            {
                return renderer;
            }

            renderer =
                FindRendererInChildren(
                    child,
                    branchToSkip);

            if (renderer != null)
            {
                return renderer;
            }
        }

        return null;
    }

    private static float Radians(float degrees) =>
        degrees *
        MathF.PI /
        180.0f;

    private static Quaternion NormalizeSafe(Quaternion value) =>
        value.LengthSquared() > 0.000001f
            ? Quaternion.Normalize(value)
            : Quaternion.Identity;

    private static Vector3 SanitizeScale(Vector3 value) =>
        new(
            NonZero(value.X),
            NonZero(value.Y),
            NonZero(value.Z));

    private static float NonZero(float value) =>
        MathF.Abs(value) < 0.0001f
            ? MathF.CopySign(
                0.0001f,
                value == 0.0f
                    ? 1.0f
                    : value)
            : value;
}
