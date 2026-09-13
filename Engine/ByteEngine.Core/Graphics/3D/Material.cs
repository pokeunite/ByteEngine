using System.Numerics;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// ByteEngine's standard 3D surface material.
///
/// v0.9-b extends the original PBR surface inputs with explicit render-state
/// controls while preserving the old renderer's behavior by default.
/// </summary>
public sealed class Material
{
    public Vector4 BaseColor { get; set; } =
        Vector4.One;

    public Texture2D? MainTexture { get; set; }

    public Texture2D? NormalTexture { get; set; }

    public float Metallic { get; set; }

    public float Roughness { get; set; } =
        1.0f;

    public BlendMode3D BlendMode { get; set; } =
        BlendMode3D.Opaque;

    /// <summary>
    /// Alpha threshold used only by Cutout materials.
    /// </summary>
    public float AlphaCutoff { get; set; } =
        0.5f;

    public bool DepthTest { get; set; } =
        true;

    public DepthWriteMode3D DepthWriteMode { get; set; } =
        DepthWriteMode3D.Automatic;

    /// <summary>
    /// None means double-sided rendering.
    /// </summary>
    public CullMode3D CullMode { get; set; } =
        CullMode3D.None;

    public FrontFaceWinding3D FrontFace { get; set; } =
        FrontFaceWinding3D.CounterClockwise;

    public PolygonMode3D PolygonMode { get; set; } =
        PolygonMode3D.Fill;

    public bool ResolveDepthWrite()
    {
        return DepthWriteMode switch
        {
            DepthWriteMode3D.Enabled =>
                true,

            DepthWriteMode3D.Disabled =>
                false,

            _ =>
                BlendMode is
                    BlendMode3D.Opaque or
                    BlendMode3D.Cutout
        };
    }

    /// <summary>
    /// Copies the material settings while retaining references to the same
    /// immutable/shared texture resources.
    /// </summary>
    public Material Clone()
    {
        return
            new Material
            {
                BaseColor =
                    BaseColor,

                MainTexture =
                    MainTexture,

                NormalTexture =
                    NormalTexture,

                Metallic =
                    Metallic,

                Roughness =
                    Roughness,

                BlendMode =
                    BlendMode,

                AlphaCutoff =
                    AlphaCutoff,

                DepthTest =
                    DepthTest,

                DepthWriteMode =
                    DepthWriteMode,

                CullMode =
                    CullMode,

                FrontFace =
                    FrontFace,

                PolygonMode =
                    PolygonMode
            };
    }
}
