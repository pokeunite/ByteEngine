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
    float DistanceSquaredToCamera,
    int SubmissionIndex);
