using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Core.Assets;

public enum MaterialAssetKind { Standard, Instance }
public enum MaterialSurfaceType { Opaque, Cutout, Transparent, Additive }
public enum MaterialShadingMode { Lit, Unlit }
public enum MaterialPbrMapMode { Separate, Packed }
public enum MaterialMapChannel { None, Red, Green, Blue, Alpha }
public enum MaterialNormalConvention { Auto, OpenGL, DirectX }

/// <summary>
/// Editor-facing, GPU-independent material document. The asset GUID lives in its
/// normal .meta sidecar; texture and parent links are GUID-backed references.
/// </summary>
public sealed class MaterialAsset
{
    public const int CurrentVersion = 1;
    public int Version { get; set; } = CurrentVersion;
    public MaterialAssetKind Kind { get; set; } = MaterialAssetKind.Standard;
    public string Name { get; set; } = "New Material";
    public AssetReference ParentMaterial { get; set; } = AssetReference.Empty;
    public MaterialParameters Standard { get; set; } = new();
    public Dictionary<string, JsonElement> Overrides { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public bool HasOverride(string property) => Overrides.ContainsKey(property);

    public void SetOverride<T>(string property, T value)
    {
        if (!MaterialParameters.KnownProperties.Contains(property))
            throw new ArgumentException($"Unknown material property '{property}'.", nameof(property));
        Overrides[property] = MaterialAssetSerializer.SerializeOverrideValue(value);
    }

    public void RemoveOverride(string property) => Overrides.Remove(property);
}

public sealed class MaterialParameters
{
    public static readonly IReadOnlySet<string> KnownProperties = new HashSet<string>(
        StringComparer.OrdinalIgnoreCase)
    {
        nameof(SurfaceType), nameof(Shading), nameof(BaseColor), nameof(BaseColorTexture),
        nameof(NormalTexture), nameof(NormalStrength), nameof(NormalConvention),
        nameof(Metallic), nameof(MetallicTexture), nameof(Roughness), nameof(RoughnessTexture),
        nameof(AmbientOcclusionStrength), nameof(AmbientOcclusionTexture),
        nameof(PbrMapMode), nameof(PackedPbrTexture), nameof(PackedAoChannel),
        nameof(PackedRoughnessChannel), nameof(PackedMetallicChannel),
        nameof(EmissionEnabled), nameof(EmissionColor), nameof(EmissionTexture),
        nameof(EmissionIntensity), nameof(UvTiling), nameof(UvOffset),
        nameof(DoubleSided), nameof(AlphaCutoff), nameof(DepthTest),
        nameof(DepthWriteMode), nameof(CullMode), nameof(FrontFace), nameof(PolygonMode)
    };

    public MaterialSurfaceType SurfaceType { get; set; } = MaterialSurfaceType.Opaque;
    public MaterialShadingMode Shading { get; set; } = MaterialShadingMode.Lit;
    public Vector4 BaseColor { get; set; } = Vector4.One;
    public AssetReference BaseColorTexture { get; set; } = AssetReference.Empty;
    public AssetReference NormalTexture { get; set; } = AssetReference.Empty;
    public float NormalStrength { get; set; } = 1f;
    public MaterialNormalConvention NormalConvention { get; set; } = MaterialNormalConvention.Auto;
    public float Metallic { get; set; }
    public AssetReference MetallicTexture { get; set; } = AssetReference.Empty;
    public float Roughness { get; set; } = 1f;
    public AssetReference RoughnessTexture { get; set; } = AssetReference.Empty;
    public float AmbientOcclusionStrength { get; set; } = 1f;
    public AssetReference AmbientOcclusionTexture { get; set; } = AssetReference.Empty;
    public MaterialPbrMapMode PbrMapMode { get; set; } = MaterialPbrMapMode.Separate;
    public AssetReference PackedPbrTexture { get; set; } = AssetReference.Empty;
    public MaterialMapChannel PackedAoChannel { get; set; } = MaterialMapChannel.Red;
    public MaterialMapChannel PackedRoughnessChannel { get; set; } = MaterialMapChannel.Green;
    public MaterialMapChannel PackedMetallicChannel { get; set; } = MaterialMapChannel.Blue;
    public bool EmissionEnabled { get; set; }
    public Vector3 EmissionColor { get; set; } = Vector3.One;
    public AssetReference EmissionTexture { get; set; } = AssetReference.Empty;
    public float EmissionIntensity { get; set; } = 1f;
    public Vector2 UvTiling { get; set; } = Vector2.One;
    public Vector2 UvOffset { get; set; }
    public bool DoubleSided { get; set; } = true;
    public float AlphaCutoff { get; set; } = .5f;
    public bool DepthTest { get; set; } = true;
    public DepthWriteMode3D DepthWriteMode { get; set; } = DepthWriteMode3D.Automatic;
    public CullMode3D CullMode { get; set; } = CullMode3D.None;
    public FrontFaceWinding3D FrontFace { get; set; } = FrontFaceWinding3D.CounterClockwise;
    public PolygonMode3D PolygonMode { get; set; } = PolygonMode3D.Fill;

    public MaterialParameters Clone() => (MaterialParameters)MemberwiseClone();

    public void Normalize()
    {
        Metallic = Math.Clamp(float.IsFinite(Metallic) ? Metallic : 0f, 0f, 1f);
        Roughness = Math.Clamp(float.IsFinite(Roughness) ? Roughness : 1f, .04f, 1f);
        NormalStrength = Math.Clamp(float.IsFinite(NormalStrength) ? NormalStrength : 1f, 0f, 4f);
        AmbientOcclusionStrength = Math.Clamp(
            float.IsFinite(AmbientOcclusionStrength) ? AmbientOcclusionStrength : 1f, 0f, 1f);
        EmissionIntensity = Math.Max(0f, float.IsFinite(EmissionIntensity) ? EmissionIntensity : 0f);
        AlphaCutoff = Math.Clamp(float.IsFinite(AlphaCutoff) ? AlphaCutoff : .5f, 0f, 1f);
        UvTiling = new Vector2(
            float.IsFinite(UvTiling.X) && UvTiling.X != 0f ? UvTiling.X : 1f,
            float.IsFinite(UvTiling.Y) && UvTiling.Y != 0f ? UvTiling.Y : 1f);
        UvOffset = new Vector2(
            float.IsFinite(UvOffset.X) ? UvOffset.X : 0f,
            float.IsFinite(UvOffset.Y) ? UvOffset.Y : 0f);
    }
}
