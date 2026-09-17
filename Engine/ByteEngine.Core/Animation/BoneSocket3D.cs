using System.Numerics;

using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Animation;

/// <summary>
/// Attaches this GameObject to a live skeletal-animation bone.
///
/// C6.2 deliberately treats the animated bone as a position/rotation frame
/// first. Imported FBX hierarchies commonly contain mirrored or unit-conversion
/// scale (for example 0.01 or a negative axis). Copying that world matrix
/// directly onto an attachment can make a primitive microscopic or invert its
/// winding so it appears to vanish. The socket therefore extracts a stable,
/// right-handed bone frame and only inherits positive bone scale when explicitly
/// requested.
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
    /// Additional attachment scale.
    /// </summary>
    public Vector3 ScaleMultiplier { get; set; } = Vector3.One;

    /// <summary>
    /// Optional positive bone-scale inheritance.
    ///
    /// Disabled by default because imported FBX skeletons frequently carry
    /// authoring/unit-conversion scale that should not be copied onto weapons,
    /// props or effects.
    /// </summary>
    public bool InheritBoneScale { get; set; }

    public bool IsBound { get; private set; }

    public string SourceRendererName { get; private set; } = string.Empty;

    public override int UpdateOrder => 1000;

    protected override void OnUpdate()
    {
        ApplySocketTransform();
    }

    protected override void OnRender(RenderContext context)
    {
        /*
         * Editor scene rendering does not run OnUpdate. Resolving again during
         * render also makes a runtime attachment use the exact latest skeletal
         * pose available for this frame.
         */
        ApplySocketTransform();
    }

    /// <summary>
    /// Finds the skeletal renderer belonging to the nearest character/model
    /// hierarchy and prefers a renderer that actually contains BoneName.
    /// </summary>
    public SkeletalMeshRenderer? ResolveSourceRenderer()
    {
        SkeletalMeshRenderer? fallback =
            null;

        SkeletalMeshRenderer? direct =
            GameObject.GetComponent<SkeletalMeshRenderer>();

        if (RendererContainsRequestedBone(direct))
        {
            return direct;
        }

        fallback ??=
            direct;

        GameObject branchToSkip =
            GameObject;

        for (GameObject? ancestor = GameObject.Parent;
             ancestor != null;
             ancestor = ancestor.Parent)
        {
            direct =
                ancestor.GetComponent<SkeletalMeshRenderer>();

            if (RendererContainsRequestedBone(direct))
            {
                return direct;
            }

            fallback ??=
                direct;

            SkeletalMeshRenderer? nested =
                FindRendererInChildren(
                    ancestor,
                    branchToSkip,
                    requireRequestedBone: true);

            if (nested != null)
            {
                return nested;
            }

            fallback ??=
                FindRendererInChildren(
                    ancestor,
                    branchToSkip,
                    requireRequestedBone: false);

            branchToSkip =
                ancestor;
        }

        return fallback;
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

        /*
         * Do not copy/decompose/recompose the complete bone matrix onto the
         * socket object. FBX conversion matrices can contain reflection,
         * negative scale or tiny unit scale. That was the C6 disappearing-
         * attachment failure.
         *
         * Instead extract a stable right-handed frame and compose the authored
         * socket offset explicitly.
         */
        if (!TryExtractStableBoneFrame(
                boneWorld,
                out Vector3 bonePosition,
                out Quaternion boneRotation,
                out Vector3 positiveBoneScale))
        {
            return false;
        }

        Quaternion offsetRotation =
            NormalizeSafe(
                Quaternion.CreateFromYawPitchRoll(
                    Radians(RotationOffsetDegrees.Y),
                    Radians(RotationOffsetDegrees.X),
                    Radians(RotationOffsetDegrees.Z)));

        Vector3 inheritedScale =
            InheritBoneScale
                ? positiveBoneScale
                : Vector3.One;

        Vector3 safeMultiplier =
            PositiveScale(
                ScaleMultiplier);

        /*
         * PositionOffset is a bone-local offset. Bone scale is only allowed to
         * affect that local offset when the user explicitly enables inheritance.
         */
        Vector3 localOffset =
            PositionOffset *
            inheritedScale;

        Vector3 worldOffset =
            Vector3.Transform(
                localOffset,
                boneRotation);

        Vector3 worldPosition =
            bonePosition +
            worldOffset;

        Quaternion worldRotation =
            NormalizeSafe(
                offsetRotation *
                boneRotation);

        Vector3 worldScale =
            PositiveScale(
                safeMultiplier *
                inheritedScale);

        if (!IsFinite(worldPosition) ||
            !IsFinite(worldRotation) ||
            !IsFinite(worldScale))
        {
            return false;
        }

        Transform.WorldPosition =
            worldPosition;

        Transform.WorldRotation =
            worldRotation;

        Transform.WorldScale =
            worldScale;

        IsBound = true;
        return true;
    }

    private bool RendererContainsRequestedBone(
        SkeletalMeshRenderer? renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(BoneName))
        {
            return true;
        }

        return renderer.BoneNames.Any(
            candidate =>
                string.Equals(
                    candidate,
                    BoneName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private SkeletalMeshRenderer? FindRendererInChildren(
        GameObject root,
        GameObject branchToSkip,
        bool requireRequestedBone)
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

            if (renderer != null &&
                (!requireRequestedBone ||
                 RendererContainsRequestedBone(renderer)))
            {
                return renderer;
            }

            renderer =
                FindRendererInChildren(
                    child,
                    branchToSkip,
                    requireRequestedBone);

            if (renderer != null)
            {
                return renderer;
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts a usable socket frame from a potentially mirrored/sheared FBX
    /// matrix. Translation comes straight from the matrix. The three basis rows
    /// are orthonormalized, reflection is removed, and scale is returned as
    /// positive magnitudes so attachment winding cannot be flipped.
    /// </summary>
    private static bool TryExtractStableBoneFrame(
        Matrix4x4 matrix,
        out Vector3 position,
        out Quaternion rotation,
        out Vector3 scale)
    {
        position =
            matrix.Translation;

        rotation =
            Quaternion.Identity;

        scale =
            Vector3.One;

        if (!IsFinite(position))
        {
            return false;
        }

        Vector3 x =
            new(
                matrix.M11,
                matrix.M12,
                matrix.M13);

        Vector3 y =
            new(
                matrix.M21,
                matrix.M22,
                matrix.M23);

        Vector3 z =
            new(
                matrix.M31,
                matrix.M32,
                matrix.M33);

        float scaleX =
            x.Length();

        float scaleY =
            y.Length();

        float scaleZ =
            z.Length();

        if (!float.IsFinite(scaleX) ||
            !float.IsFinite(scaleY) ||
            !float.IsFinite(scaleZ) ||
            scaleX < 0.000001f ||
            scaleY < 0.000001f ||
            scaleZ < 0.000001f)
        {
            /*
             * Some valid imported matrices are awkward for manual extraction.
             * Fall back to System.Numerics decomposition before giving up.
             */
            if (!Matrix4x4.Decompose(
                    matrix,
                    out Vector3 decomposedScale,
                    out Quaternion decomposedRotation,
                    out Vector3 decomposedPosition))
            {
                return false;
            }

            position =
                decomposedPosition;

            rotation =
                NormalizeSafe(
                    decomposedRotation);

            scale =
                PositiveScale(
                    decomposedScale);

            return
                IsFinite(position) &&
                IsFinite(rotation) &&
                IsFinite(scale);
        }

        x /=
            scaleX;

        /*
         * Gram-Schmidt removes any shear introduced by parent non-uniform
         * scaling before the matrix reaches the socket.
         */
        y -=
            x *
            Vector3.Dot(
                y,
                x);

        float yLength =
            y.Length();

        if (!float.IsFinite(yLength) ||
            yLength < 0.000001f)
        {
            return false;
        }

        y /=
            yLength;

        Vector3 rightHandedZ =
            Vector3.Cross(
                x,
                y);

        float zLength =
            rightHandedZ.Length();

        if (!float.IsFinite(zLength) ||
            zLength < 0.000001f)
        {
            return false;
        }

        rightHandedZ /=
            zLength;

        /*
         * Recompute Y as well so the final basis is strictly orthonormal.
         * We intentionally keep a right-handed basis even if the source matrix
         * was mirrored; mirroring a weapon/primitive is almost never desirable
         * and can make it vanish under back-face culling.
         */
        y =
            Vector3.Normalize(
                Vector3.Cross(
                    rightHandedZ,
                    x));

        Matrix4x4 rotationMatrix =
            new(
                x.X, x.Y, x.Z, 0.0f,
                y.X, y.Y, y.Z, 0.0f,
                rightHandedZ.X, rightHandedZ.Y, rightHandedZ.Z, 0.0f,
                0.0f, 0.0f, 0.0f, 1.0f);

        rotation =
            NormalizeSafe(
                Quaternion.CreateFromRotationMatrix(
                    rotationMatrix));

        scale =
            PositiveScale(
                new Vector3(
                    scaleX,
                    scaleY,
                    scaleZ));

        return
            IsFinite(rotation) &&
            IsFinite(scale);
    }

    private static float Radians(float degrees) =>
        degrees *
        MathF.PI /
        180.0f;

    private static Quaternion NormalizeSafe(
        Quaternion value) =>
        value.LengthSquared() > 0.000001f
            ? Quaternion.Normalize(value)
            : Quaternion.Identity;

    private static Vector3 PositiveScale(
        Vector3 value) =>
        new(
            PositiveNonZero(value.X),
            PositiveNonZero(value.Y),
            PositiveNonZero(value.Z));

    private static float PositiveNonZero(
        float value)
    {
        float magnitude =
            MathF.Abs(value);

        return magnitude < 0.0001f
            ? 0.0001f
            : magnitude;
    }

    private static bool IsFinite(
        Vector3 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z);

    private static bool IsFinite(
        Quaternion value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z) &&
        float.IsFinite(value.W);
}
