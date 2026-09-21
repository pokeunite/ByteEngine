using System.Numerics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Animation;

public readonly record struct SkeletalSocketTransform(Vector3 Position, Quaternion Rotation, Vector3 Scale)
{
    public Matrix4x4 Matrix => Matrix4x4.CreateScale(Scale) * Matrix4x4.CreateFromQuaternion(Rotation) * Matrix4x4.CreateTranslation(Position);
}

public static class SkeletalSocketResolver
{
    public static bool TryGetSocketWorldTransform(GameObject target, string socketName, out SkeletalSocketTransform transform)
    {
        transform = default;
        return TryResolveRenderer(target, socketName, out SkeletalMeshRenderer? renderer) &&
               TryGetSocketWorldTransform(renderer!, socketName, out transform);
    }

    public static bool TryGetSocketWorldPosition(GameObject target, string socketName, out Vector3 position)
    {
        bool found = TryGetSocketWorldTransform(target, socketName, out SkeletalSocketTransform transform);
        position = transform.Position;
        return found;
    }

    public static bool TryGetSocketWorldTransform(SkeletalMeshRenderer renderer, string socketName, out SkeletalSocketTransform transform)
    {
        transform = default;
        if (renderer == null || string.IsNullOrWhiteSpace(socketName)) return false;
        SkeletalSocketDefinition? socket = renderer.ResolvedModel?.FindSocket(socketName);
        if (socket == null || !renderer.TryGetBoneWorldMatrix(socket.BoneName, out Matrix4x4 boneWorld)) return false;
        if (!TryExtractStableBoneFrame(boneWorld, out Vector3 position, out Quaternion rotation, out Vector3 boneScale)) return false;
        Vector3 inherited = socket.InheritBoneScale ? boneScale : Vector3.One;
        Quaternion offset = NormalizeSafe(Quaternion.CreateFromYawPitchRoll(Radians(socket.RotationOffsetDegrees.Y), Radians(socket.RotationOffsetDegrees.X), Radians(socket.RotationOffsetDegrees.Z)));
        Vector3 worldPosition = position + Vector3.Transform(socket.PositionOffset * inherited, rotation);
        Quaternion worldRotation = NormalizeSafe(offset * rotation);
        Vector3 worldScale = PositiveScale(socket.Scale * inherited);
        if (!Finite(worldPosition) || !Finite(worldRotation) || !Finite(worldScale)) return false;
        transform = new(worldPosition, worldRotation, worldScale);
        return true;
    }

    public static IReadOnlyList<SkeletalSocketDefinition> GetSockets(GameObject target)
    {
        if (!TryResolveRenderer(target, null, out SkeletalMeshRenderer? renderer)) return Array.Empty<SkeletalSocketDefinition>();
        return renderer!.ResolvedModel?.Sockets ?? Array.Empty<SkeletalSocketDefinition>();
    }

    public static bool TryResolveRenderer(GameObject target, string? socketName, out SkeletalMeshRenderer? renderer)
    {
        renderer = Find(target, socketName);
        if (renderer != null) return true;
        foreach (GameObject child in target.Children)
        {
            renderer = FindRecursive(child, socketName);
            if (renderer != null) return true;
        }
        return false;
    }

    private static SkeletalMeshRenderer? FindRecursive(GameObject root, string? socketName)
    {
        SkeletalMeshRenderer? found = Find(root, socketName);
        if (found != null) return found;
        foreach (GameObject child in root.Children) { found = FindRecursive(child, socketName); if (found != null) return found; }
        return null;
    }

    private static SkeletalMeshRenderer? Find(GameObject item, string? socketName)
    {
        SkeletalMeshRenderer? renderer = item.GetComponent<SkeletalMeshRenderer>();
        if (renderer == null) return null;
        if (string.IsNullOrWhiteSpace(socketName)) return renderer;
        return renderer.ResolvedModel?.FindSocket(socketName) != null ? renderer : null;
    }

    public static bool TryExtractStableBoneFrame(Matrix4x4 matrix, out Vector3 position, out Quaternion rotation, out Vector3 scale)
    {
        position = matrix.Translation; rotation = Quaternion.Identity; scale = Vector3.One;
        if (!Finite(position)) return false;
        Vector3 x = new(matrix.M11, matrix.M12, matrix.M13);
        Vector3 y = new(matrix.M21, matrix.M22, matrix.M23);
        Vector3 z = new(matrix.M31, matrix.M32, matrix.M33);
        float sx=x.Length(), sy=y.Length(), sz=z.Length();
        if (!float.IsFinite(sx)||!float.IsFinite(sy)||!float.IsFinite(sz)||sx<1e-6f||sy<1e-6f||sz<1e-6f)
        {
            if (!Matrix4x4.Decompose(matrix,out Vector3 ds,out Quaternion dr,out Vector3 dp)) return false;
            position=dp; rotation=NormalizeSafe(dr); scale=PositiveScale(ds); return Finite(rotation)&&Finite(scale);
        }
        x/=sx; y-=Vector3.Dot(y,x)*x;
        if (y.LengthSquared()<1e-12f) y=Vector3.Cross(z,x);
        if (y.LengthSquared()<1e-12f) return false;
        y=Vector3.Normalize(y); z=Vector3.Normalize(Vector3.Cross(x,y)); y=Vector3.Normalize(Vector3.Cross(z,x));
        Matrix4x4 frame=new(x.X,x.Y,x.Z,0,y.X,y.Y,y.Z,0,z.X,z.Y,z.Z,0,0,0,0,1);
        rotation=NormalizeSafe(Quaternion.CreateFromRotationMatrix(frame)); scale=PositiveScale(new(sx,sy,sz));
        return Finite(rotation)&&Finite(scale);
    }

    private static Vector3 PositiveScale(Vector3 value)=>new(MathF.Max(MathF.Abs(value.X),.0001f),MathF.Max(MathF.Abs(value.Y),.0001f),MathF.Max(MathF.Abs(value.Z),.0001f));
    private static Quaternion NormalizeSafe(Quaternion value)=>value.LengthSquared()<1e-12f?Quaternion.Identity:Quaternion.Normalize(value);
    private static float Radians(float v)=>v*MathF.PI/180f;
    private static bool Finite(Vector3 v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z);
    private static bool Finite(Quaternion v)=>float.IsFinite(v.X)&&float.IsFinite(v.Y)&&float.IsFinite(v.Z)&&float.IsFinite(v.W);
}