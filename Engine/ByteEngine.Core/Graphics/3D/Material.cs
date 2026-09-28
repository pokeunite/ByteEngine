using System.Numerics;
using ByteEngine.Core.Assets;

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

    /// <summary>
    /// Material textures deliberately use a 3D/PBR sampling profile instead of
    /// the texture asset's 2D sprite sampling profile. This keeps pixel-art/UI
    /// defaults independent while giving 3D materials repeat wrapping,
    /// trilinear filtering and mipmaps.
    /// </summary>
    public Texture2D? MainTexture { get; set; }

    public Texture2D? NormalTexture { get; set; }

    public float NormalStrength { get; set; } = 1f;
    public bool DirectXNormalMap { get; set; }

    public Texture2D? MetallicTexture { get; set; }

    public Texture2D? RoughnessTexture { get; set; }

    public Texture2D? AmbientOcclusionTexture { get; set; }

    public float AmbientOcclusionStrength { get; set; } = 1f;

    public Texture2D? PackedPbrTexture { get; set; }

    public MaterialPbrMapMode PbrMapMode { get; set; }
    public MaterialMapChannel PackedAoChannel { get; set; } = MaterialMapChannel.Red;
    public MaterialMapChannel PackedRoughnessChannel { get; set; } = MaterialMapChannel.Green;
    public MaterialMapChannel PackedMetallicChannel { get; set; } = MaterialMapChannel.Blue;
    public bool EmissionEnabled { get; set; }
    public Vector3 EmissionColor { get; set; } = Vector3.One;
    public float EmissionIntensity { get; set; } = 1f;

    public Texture2D? EmissionTexture { get; set; }

    public Vector2 UvTiling { get; set; } = Vector2.One;
    public Vector2 UvOffset { get; set; }
    public MaterialShadingMode Shading { get; set; } = MaterialShadingMode.Lit;
    public bool DecodeColorTexturesSrgb { get; set; }

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
    /// Refreshes an existing renderer-local material while retaining references
    /// to the same shared texture resources.
    /// </summary>
    public void CopyFrom(Material source)
    {
        BaseColor = source.BaseColor;
        MainTexture = source.MainTexture;
        NormalTexture = source.NormalTexture;
        NormalStrength = source.NormalStrength;
        DirectXNormalMap = source.DirectXNormalMap;
        MetallicTexture = source.MetallicTexture;
        RoughnessTexture = source.RoughnessTexture;
        AmbientOcclusionTexture = source.AmbientOcclusionTexture;
        AmbientOcclusionStrength = source.AmbientOcclusionStrength;
        PackedPbrTexture = source.PackedPbrTexture;
        PbrMapMode = source.PbrMapMode;
        PackedAoChannel = source.PackedAoChannel;
        PackedRoughnessChannel = source.PackedRoughnessChannel;
        PackedMetallicChannel = source.PackedMetallicChannel;
        EmissionEnabled = source.EmissionEnabled;
        EmissionColor = source.EmissionColor;
        EmissionIntensity = source.EmissionIntensity;
        EmissionTexture = source.EmissionTexture;
        UvTiling = source.UvTiling;
        UvOffset = source.UvOffset;
        Shading = source.Shading;
        DecodeColorTexturesSrgb = source.DecodeColorTexturesSrgb;
        Metallic = source.Metallic;
        Roughness = source.Roughness;
        BlendMode = source.BlendMode;
        AlphaCutoff = source.AlphaCutoff;
        DepthTest = source.DepthTest;
        DepthWriteMode = source.DepthWriteMode;
        CullMode = source.CullMode;
        FrontFace = source.FrontFace;
        PolygonMode = source.PolygonMode;
    }

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
                NormalStrength = NormalStrength,
                DirectXNormalMap = DirectXNormalMap,
                MetallicTexture = MetallicTexture,
                RoughnessTexture = RoughnessTexture,
                AmbientOcclusionTexture = AmbientOcclusionTexture,
                AmbientOcclusionStrength = AmbientOcclusionStrength,
                PackedPbrTexture = PackedPbrTexture,
                PbrMapMode = PbrMapMode,
                PackedAoChannel = PackedAoChannel,
                PackedRoughnessChannel = PackedRoughnessChannel,
                PackedMetallicChannel = PackedMetallicChannel,
                EmissionEnabled = EmissionEnabled,
                EmissionColor = EmissionColor,
                EmissionIntensity = EmissionIntensity,
                EmissionTexture = EmissionTexture,
                UvTiling = UvTiling,
                UvOffset = UvOffset,
                Shading = Shading,
                DecodeColorTexturesSrgb = DecodeColorTexturesSrgb,

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
