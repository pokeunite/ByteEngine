using System.Numerics;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Immutable description of one 3D draw submitted during scene traversal.
/// </summary>
public readonly record struct RenderSubmission(
    Mesh Mesh,
    Material Material,
    Matrix4x4 ModelMatrix,
    BoundingBox3D WorldBounds,
    RenderQueue3D Queue,
    bool FrustumCullingEnabled,
    bool CastShadows,
    bool ReceiveShadows,
    float DistanceSquaredToCamera,
    int SubmissionIndex)
{
    public Mesh? ShadowMesh {get;init;}
    public Matrix4x4? ShadowModelMatrix {get;init;}
    internal RenderSubmission AsShadowCaster()=>ShadowMesh is {} mesh?this with{Mesh=mesh,ModelMatrix=ShadowModelMatrix??ModelMatrix,WorldBounds=mesh.LocalBounds.Transform(ShadowModelMatrix??ModelMatrix),ShadowMesh=null,ShadowModelMatrix=null}:this;
}
