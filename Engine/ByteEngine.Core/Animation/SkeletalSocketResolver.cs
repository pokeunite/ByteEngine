using System.Numerics;

using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Animation;

public readonly record struct SkeletalSocketTransform(
    Vector3 Position,
    Quaternion Rotation,
    Vector3 Scale)
{
    public Matrix4x4 Matrix =>
        Matrix4x4.CreateScale(Scale) *
        Matrix4x4.CreateFromQuaternion(Rotation) *
        Matrix4x4.CreateTranslation(Position);
}

/// <summary>
/// Resolves authored skeletal sockets from the exact current pose maintained by
/// SkeletalMeshRenderer.
///
/// The primary path is MODEL SPACE. World space is derived only for callers
/// that explicitly need it (for example KeepWorld at attach time). Persistent
/// followers should use model/local space so ordinary scene parenting handles
/// character movement and facing.
/// </summary>
public static class SkeletalSocketResolver
{
    public static bool TryGetSocketModelTransform(
        GameObject target,
        string socketName,
        out SkeletalSocketTransform transform)
    {
        transform = default;

        return
            TryResolveRenderer(
                target,
                socketName,
                out SkeletalMeshRenderer? renderer) &&
            renderer != null &&
            TryGetSocketModelTransform(
                renderer,
                socketName,
                out transform);
    }

    public static bool TryGetSocketModelTransform(
        SkeletalMeshRenderer renderer,
        string socketName,
        out SkeletalSocketTransform transform)
    {
        transform = default;

        if (renderer == null ||
            string.IsNullOrWhiteSpace(socketName) ||
            renderer.ResolvedModel?.FindSocket(socketName) is not { } socket)
        {
            return false;
        }

        return TryGetSocketModelTransform(
            renderer,
            socket,
            out transform);
    }

    public static bool TryGetSocketModelTransform(
        SkeletalMeshRenderer renderer,
        SkeletalSocketDefinition socket,
        out SkeletalSocketTransform transform)
    {
        transform = default;

        ArgumentNullException.ThrowIfNull(renderer);
        ArgumentNullException.ThrowIfNull(socket);

        if (string.IsNullOrWhiteSpace(
                socket.BoneName) ||
            !renderer.TryGetBoneModelMatrix(
                socket.BoneName,
                out Matrix4x4 boneModel) ||
            !TryExtractStableBoneFrame(
                boneModel,
                out Vector3 bonePosition,
                out Quaternion boneRotation,
                out Vector3 boneScale))
        {
            return false;
        }

        Vector3 inheritedScale =
            socket.InheritBoneScale
                ? SanitizeScale(
                    boneScale)
                : Vector3.One;

        Quaternion offsetRotation =
            NormalizeSafe(
                Quaternion.CreateFromYawPitchRoll(
                    Radians(
                        socket.RotationOffsetDegrees.Y),
                    Radians(
                        socket.RotationOffsetDegrees.X),
                    Radians(
                        socket.RotationOffsetDegrees.Z)));

        Vector3 modelPosition =
            bonePosition +
            Vector3.Transform(
                socket.PositionOffset *
                inheritedScale,
                boneRotation);

        Quaternion modelRotation =
            NormalizeSafe(
                boneRotation *
                offsetRotation);

        Vector3 modelScale =
            SanitizeScale(
                socket.Scale *
                inheritedScale);

        if (!Finite(modelPosition) ||
            !Finite(modelRotation) ||
            !Finite(modelScale))
        {
            return false;
        }

        transform =
            new SkeletalSocketTransform(
                modelPosition,
                modelRotation,
                modelScale);

        return true;
    }

    public static bool TryGetSocketModelMatrix(
        SkeletalMeshRenderer renderer,
        string socketName,
        out Matrix4x4 matrix)
    {
        if (TryGetSocketModelTransform(
                renderer,
                socketName,
                out SkeletalSocketTransform transform))
        {
            matrix = transform.Matrix;
            return true;
        }

        matrix = Matrix4x4.Identity;
        return false;
    }

    public static bool TryGetSocketWorldTransform(
        GameObject target,
        string socketName,
        out SkeletalSocketTransform transform)
    {
        transform = default;

        return
            TryResolveRenderer(
                target,
                socketName,
                out SkeletalMeshRenderer? renderer) &&
            renderer != null &&
            TryGetSocketWorldTransform(
                renderer,
                socketName,
                out transform);
    }

    public static bool TryGetSocketWorldPosition(
        GameObject target,
        string socketName,
        out Vector3 position)
    {
        bool found =
            TryGetSocketWorldTransform(
                target,
                socketName,
                out SkeletalSocketTransform transform);

        position =
            found
                ? transform.Position
                : Vector3.Zero;

        return found;
    }

    public static bool TryGetSocketWorldTransform(
        SkeletalMeshRenderer renderer,
        string socketName,
        out SkeletalSocketTransform transform)
    {
        transform = default;

        if (renderer == null ||
            !TryGetSocketModelTransform(
                renderer,
                socketName,
                out SkeletalSocketTransform model))
        {
            return false;
        }

        Transform owner =
            renderer.Transform;

        Vector3 worldPosition =
            Vector3.Transform(model.Position, owner.WorldMatrix);

        Quaternion worldRotation =
            NormalizeSafe(
                GetHierarchyWorldRotation(renderer.GameObject) *
                model.Rotation);

        Vector3 worldScale =
            SanitizeScale(
                model.Scale *
                owner.WorldScale);

        if (!Finite(worldPosition) ||
            !Finite(worldRotation) ||
            !Finite(worldScale))
        {
            return false;
        }

        transform =
            new SkeletalSocketTransform(
                worldPosition,
                worldRotation,
                worldScale);

        return true;
    }

    public static bool TryGetSocketWorldMatrix(
        SkeletalMeshRenderer renderer,
        string socketName,
        out Matrix4x4 matrix)
    {
        if (TryGetSocketModelMatrix(
                renderer,
                socketName,
                out Matrix4x4 model))
        {
            matrix =
                model *
                renderer.Transform.WorldMatrix;

            return true;
        }

        matrix = Matrix4x4.Identity;
        return false;
    }

    public static IReadOnlyList<SkeletalSocketDefinition> GetSockets(
        GameObject target)
    {
        if (!TryResolveRenderer(
                target,
                null,
                out SkeletalMeshRenderer? renderer))
        {
            return Array.Empty<SkeletalSocketDefinition>();
        }

        return
            renderer!.ResolvedModel?.Sockets ??
            Array.Empty<SkeletalSocketDefinition>();
    }

    public static bool TryResolveRenderer(
        GameObject target,
        string? socketName,
        out SkeletalMeshRenderer? renderer)
    {
        ArgumentNullException.ThrowIfNull(target);

        renderer =
            Find(
                target,
                socketName);

        if (renderer != null)
        {
            return true;
        }

        foreach (GameObject child
                 in target.Children)
        {
            renderer =
                FindRecursive(
                    child,
                    socketName);

            if (renderer != null)
            {
                return true;
            }
        }

        return false;
    }

    private static SkeletalMeshRenderer? FindRecursive(
        GameObject root,
        string? socketName)
    {
        SkeletalMeshRenderer? found =
            Find(
                root,
                socketName);

        if (found != null)
        {
            return found;
        }

        foreach (GameObject child
                 in root.Children)
        {
            found =
                FindRecursive(
                    child,
                    socketName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static SkeletalMeshRenderer? Find(
        GameObject item,
        string? socketName)
    {
        SkeletalMeshRenderer? renderer =
            item.GetComponent<SkeletalMeshRenderer>();

        if (renderer == null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(socketName))
        {
            return renderer;
        }

        return
            renderer.ResolvedModel?.FindSocket(
                socketName) != null
                ? renderer
                : null;
    }

    /// <summary>
    /// Compatibility API retained for existing editor/tests. Unlike the old
    /// implementation this does not Gram-Schmidt/rebuild the bone basis or force
    /// every scale component positive. It uses the animated pose's own TRS
    /// decomposition, which is the same basic strategy used by Flax BoneSocket.
    /// </summary>
    public static bool TryExtractStableBoneFrame(
        Matrix4x4 matrix,
        out Vector3 position,
        out Quaternion rotation,
        out Vector3 scale)
    {
        position =
            Vector3.Zero;

        rotation =
            Quaternion.Identity;

        scale =
            Vector3.One;

        if (!Matrix4x4.Decompose(
                matrix,
                out Vector3 decomposedScale,
                out Quaternion decomposedRotation,
                out Vector3 decomposedPosition))
        {
            return false;
        }

        decomposedRotation =
            NormalizeSafe(
                decomposedRotation);

        decomposedScale =
            SanitizeScale(
                decomposedScale);

        if (!Finite(decomposedPosition) ||
            !Finite(decomposedRotation) ||
            !Finite(decomposedScale))
        {
            return false;
        }

        position = decomposedPosition;
        rotation = decomposedRotation;
        scale = decomposedScale;

        return true;
    }

    private static Vector3 SanitizeScale(
        Vector3 value) =>
        new(
            NonZero(value.X),
            NonZero(value.Y),
            NonZero(value.Z));

    private static float NonZero(
        float value) =>
        MathF.Abs(value) <
            0.0001f
            ? MathF.CopySign(
                0.0001f,
                value == 0.0f
                    ? 1.0f
                    : value)
            : value;

    internal static Quaternion GetHierarchyWorldRotation(GameObject gameObject)
    {
        Quaternion rotation = NormalizeSafe(gameObject.Transform.LocalRotation);
        for (GameObject? parent = gameObject.Parent; parent != null; parent = parent.Parent)
            rotation = NormalizeSafe(parent.Transform.LocalRotation * rotation);
        return rotation;
    }

    private static Quaternion NormalizeSafe(
        Quaternion value) =>
        value.LengthSquared() <
            0.000000000001f
            ? Quaternion.Identity
            : Quaternion.Normalize(value);

    private static float Radians(
        float value) =>
        value *
        MathF.PI /
        180.0f;

    private static bool Finite(
        Vector3 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z);

    private static bool Finite(
        Quaternion value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z) &&
        float.IsFinite(value.W);
}
