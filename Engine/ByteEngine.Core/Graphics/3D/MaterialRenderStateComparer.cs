namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>Exact shader/resource/state equality for per-object material copies. Evaluated anew for each batch.</summary>
internal sealed class MaterialRenderStateComparer : IEqualityComparer<Material>
{
    internal static readonly MaterialRenderStateComparer Instance=new();
    public bool Equals(Material? x, Material? y)
    {
        if(ReferenceEquals(x,y))return true;
        if(x is null || y is null)return false;
        return x.BaseColor == y.BaseColor &&
            x.MainTexture == y.MainTexture &&
            x.NormalTexture == y.NormalTexture &&
            x.HeightFieldTexture == y.HeightFieldTexture &&
            x.HeightFieldUvTransform == y.HeightFieldUvTransform &&
            x.HeightFieldRange == y.HeightFieldRange &&
            x.HeightFieldBias == y.HeightFieldBias &&
            x.HeightFieldWorldSize == y.HeightFieldWorldSize &&
            x.SandSurface == y.SandSurface &&
            x.SandImprintTexture == y.SandImprintTexture &&
            x.SandImprintOrigin == y.SandImprintOrigin &&
            x.NormalStrength == y.NormalStrength &&
            x.DirectXNormalMap == y.DirectXNormalMap &&
            x.MetallicTexture == y.MetallicTexture &&
            x.RoughnessTexture == y.RoughnessTexture &&
            x.AmbientOcclusionTexture == y.AmbientOcclusionTexture &&
            x.AmbientOcclusionStrength == y.AmbientOcclusionStrength &&
            x.PackedPbrTexture == y.PackedPbrTexture &&
            x.PbrMapMode == y.PbrMapMode &&
            x.PackedAoChannel == y.PackedAoChannel &&
            x.PackedRoughnessChannel == y.PackedRoughnessChannel &&
            x.PackedMetallicChannel == y.PackedMetallicChannel &&
            x.EmissionEnabled == y.EmissionEnabled &&
            x.EmissionColor == y.EmissionColor &&
            x.EmissionIntensity == y.EmissionIntensity &&
            x.EmissionTexture == y.EmissionTexture &&
            x.UvTiling == y.UvTiling &&
            x.UvOffset == y.UvOffset &&
            x.Shading == y.Shading &&
            x.DecodeColorTexturesSrgb == y.DecodeColorTexturesSrgb &&
            x.Metallic == y.Metallic &&
            x.Roughness == y.Roughness &&
            x.BlendMode == y.BlendMode &&
            x.AlphaCutoff == y.AlphaCutoff &&
            x.DepthTest == y.DepthTest &&
            x.DepthWriteMode == y.DepthWriteMode &&
            x.CullMode == y.CullMode &&
            x.FrontFace == y.FrontFace &&
            x.PolygonMode == y.PolygonMode;
    }
    public int GetHashCode(Material value)
    {
        var hash=new HashCode();
        hash.Add(value.BaseColor);
        hash.Add(value.MainTexture);
        hash.Add(value.NormalTexture);
        hash.Add(value.HeightFieldTexture);
        hash.Add(value.HeightFieldUvTransform);
        hash.Add(value.HeightFieldRange);
        hash.Add(value.HeightFieldBias);
        hash.Add(value.HeightFieldWorldSize);
        hash.Add(value.SandSurface);
        hash.Add(value.SandImprintTexture);
        hash.Add(value.SandImprintOrigin);
        hash.Add(value.NormalStrength);
        hash.Add(value.DirectXNormalMap);
        hash.Add(value.MetallicTexture);
        hash.Add(value.RoughnessTexture);
        hash.Add(value.AmbientOcclusionTexture);
        hash.Add(value.AmbientOcclusionStrength);
        hash.Add(value.PackedPbrTexture);
        hash.Add(value.PbrMapMode);
        hash.Add(value.PackedAoChannel);
        hash.Add(value.PackedRoughnessChannel);
        hash.Add(value.PackedMetallicChannel);
        hash.Add(value.EmissionEnabled);
        hash.Add(value.EmissionColor);
        hash.Add(value.EmissionIntensity);
        hash.Add(value.EmissionTexture);
        hash.Add(value.UvTiling);
        hash.Add(value.UvOffset);
        hash.Add(value.Shading);
        hash.Add(value.DecodeColorTexturesSrgb);
        hash.Add(value.Metallic);
        hash.Add(value.Roughness);
        hash.Add(value.BlendMode);
        hash.Add(value.AlphaCutoff);
        hash.Add(value.DepthTest);
        hash.Add(value.DepthWriteMode);
        hash.Add(value.CullMode);
        hash.Add(value.FrontFace);
        hash.Add(value.PolygonMode);
        return hash.ToHashCode();
    }
}
