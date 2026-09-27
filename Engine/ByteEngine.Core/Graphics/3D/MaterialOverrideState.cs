using System.Numerics;
using ByteEngine.Core.Assets;

namespace ByteEngine.Core.Graphics.ThreeD;

public enum MaterialTextureSlot
{
    BaseColor, Normal, Metallic, Roughness, AmbientOcclusion, PackedPbr, Emission
}

/// <summary>
/// A renderer-owned, transient layer on top of a local/imported/shared material.
/// Never mutates a .bmat or another renderer's material.
/// </summary>
public sealed class MaterialOverrideState
{
    private readonly Dictionary<MaterialTextureSlot, AssetReference> _textures = new();

    public Vector4? BaseColor { get; set; }
    public float? Metallic { get; set; }
    public float? Roughness { get; set; }
    public Vector3? EmissionColor { get; set; }
    public float? EmissionIntensity { get; set; }

    public bool IsEmpty =>
        BaseColor == null && Metallic == null && Roughness == null &&
        EmissionColor == null && EmissionIntensity == null && _textures.Count == 0;

    /// <summary>Empty reference deliberately clears a texture; ResetTexture inherits it.</summary>
    public void SetTexture(MaterialTextureSlot slot, AssetReference reference) =>
        _textures[slot] = reference ?? AssetReference.Empty;

    public void ResetTexture(MaterialTextureSlot slot) => _textures.Remove(slot);

    public void Reset()
    {
        BaseColor = null;
        Metallic = null;
        Roughness = null;
        EmissionColor = null;
        EmissionIntensity = null;
        _textures.Clear();
    }

    public void Apply(Material source, Material destination, AssetManager? assets)
    {
        destination.CopyFrom(source);
        if (BaseColor is { } color) destination.BaseColor = color;
        if (Metallic is { } metallic) destination.Metallic = Math.Clamp(metallic, 0f, 1f);
        if (Roughness is { } roughness) destination.Roughness = Math.Clamp(roughness, .04f, 1f);
        if (EmissionColor is { } emissionColor)
        {
            destination.EmissionColor = emissionColor;
            destination.EmissionEnabled = true;
        }
        if (EmissionIntensity is { } emissionIntensity)
        {
            destination.EmissionIntensity = Math.Max(0f, emissionIntensity);
            destination.EmissionEnabled = true;
        }
        foreach ((MaterialTextureSlot slot, AssetReference reference) in _textures)
        {
            Texture2D? texture = reference.IsEmpty || assets == null
                ? null : assets.LoadTexture(reference);
            switch (slot)
            {
                case MaterialTextureSlot.BaseColor: destination.MainTexture = texture; break;
                case MaterialTextureSlot.Normal: destination.NormalTexture = texture; break;
                case MaterialTextureSlot.Metallic: destination.MetallicTexture = texture; break;
                case MaterialTextureSlot.Roughness: destination.RoughnessTexture = texture; break;
                case MaterialTextureSlot.AmbientOcclusion: destination.AmbientOcclusionTexture = texture; break;
                case MaterialTextureSlot.PackedPbr:
                    destination.PackedPbrTexture = texture;
                    destination.PbrMapMode = texture == null ? MaterialPbrMapMode.Separate : MaterialPbrMapMode.Packed;
                    break;
                case MaterialTextureSlot.Emission:
                    destination.EmissionTexture = texture;
                    destination.EmissionEnabled = texture != null;
                    break;
            }
        }
    }
}
