using System.Numerics;

namespace ByteEngine.Core.Graphics.ThreeD;

public sealed partial class SkeletalMeshRenderer
{
    /// <summary>Copies the currently displayed CPU-skinned pose. Caller owns and disposes the returned meshes.
    /// No animation controller or scene object is duplicated; poses remain frozen after the source advances.</summary>
    public IReadOnlyList<(Mesh Mesh, Matrix4x4 World)> CapturePoseMeshes()
    {
        if (!ResolveRuntimeResources()) return Array.Empty<(Mesh, Matrix4x4)>();
        if (_poseDirty) UpdatePoseAndMeshes();
        return _runtimeMeshes.Select(runtime => (
            new Mesh((float[])runtime.DeformedVertices.Clone(), runtime.Mesh.IndexData.ToArray()),
            runtime.MeshToModelMatrix * Transform.WorldMatrix)).ToArray();
    }
}
