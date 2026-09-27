namespace ByteEngine.Core.Assets;

/// <summary>Conservative, filename-only PBR slot inference for selected textures.</summary>
public static class MaterialTextureAutoDetection
{
    public enum Slot { Unknown, BaseColor, NormalOpenGL, NormalDirectX, Normal, Roughness, Metallic, AmbientOcclusion, Emission, PackedOrm }

    public static Slot Classify(string path)
    {
        string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        string[] words = System.Text.RegularExpressions.Regex.Split(name, @"[^a-z0-9]+")
            .Where(word => word.Length > 0).ToArray();
        if (words.Length == 0) return Slot.Unknown;
        string last = words[^1];
        return last switch
        {
            "albedo" or "basecolor" or "diffuse" or "color" => Slot.BaseColor,
            "normalgl" => Slot.NormalOpenGL,
            "normaldx" => Slot.NormalDirectX,
            "normal" or "nrm" => Slot.Normal,
            "roughness" or "rough" => Slot.Roughness,
            "metallic" or "metalness" or "metal" => Slot.Metallic,
            "ao" or "ambientocclusion" or "occlusion" => Slot.AmbientOcclusion,
            "emissive" or "emission" => Slot.Emission,
            "orm" or "arm" => Slot.PackedOrm,
            _ => words.Length >= 2 && words[^2] == "base" && last == "color"
                ? Slot.BaseColor : Slot.Unknown
        };
    }

    public static MaterialAsset Create(string name, IEnumerable<AssetRecord> textures)
    {
        var material = new MaterialAsset { Name = name };
        var assigned = new HashSet<Slot>();
        foreach (AssetRecord record in textures.OrderBy(record => record.ProjectPath, StringComparer.OrdinalIgnoreCase))
        {
            if (record.Type != AssetType.Texture2D) continue;
            Slot slot = Classify(record.ProjectPath);
            if (slot == Slot.Unknown || !assigned.Add(slot)) continue;
            var reference = new AssetReference(record.Guid, record.ProjectPath);
            switch (slot)
            {
                case Slot.BaseColor: material.Standard.BaseColorTexture = reference; break;
                case Slot.Normal:
                case Slot.NormalOpenGL:
                case Slot.NormalDirectX:
                    if (!material.Standard.NormalTexture.IsEmpty) break;
                    material.Standard.NormalTexture = reference;
                    material.Standard.NormalConvention = slot == Slot.NormalDirectX
                        ? MaterialNormalConvention.DirectX
                        : slot == Slot.NormalOpenGL
                            ? MaterialNormalConvention.OpenGL : MaterialNormalConvention.Auto;
                    break;
                case Slot.Roughness: material.Standard.RoughnessTexture = reference; break;
                case Slot.Metallic: material.Standard.MetallicTexture = reference; break;
                case Slot.AmbientOcclusion: material.Standard.AmbientOcclusionTexture = reference; break;
                case Slot.Emission:
                    material.Standard.EmissionTexture = reference;
                    material.Standard.EmissionEnabled = true;
                    break;
                case Slot.PackedOrm:
                    material.Standard.PackedPbrTexture = reference;
                    material.Standard.PbrMapMode = MaterialPbrMapMode.Packed;
                    break;
            }
        }
        return material;
    }
}
