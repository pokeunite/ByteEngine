using System.Numerics;
using System.Text.RegularExpressions;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Editor;

/// <summary>Explicitly materializes model-owned PBR sub-assets only on user request.</summary>
internal static class ImportedMaterialExtraction
{
    public static IReadOnlyList<AssetRecord> Extract(AssetRecord modelRecord,
        EditorProjectContext project, IEnumerable<string>? materialKeys = null)
    {
        if (modelRecord.Type != AssetType.Model3D)
            throw new ArgumentException("Choose a 3D model.", nameof(modelRecord));
        ModelAsset model = project.Assets.LoadModel(
            new AssetReference(modelRecord.Guid, modelRecord.ProjectPath));
        HashSet<string>? selected = materialKeys?.ToHashSet(StringComparer.Ordinal);
        ImportedMaterial[] materials = model.Materials
            .Where(item => selected == null || selected.Contains(item.Key)).ToArray();
        string directory = Path.GetDirectoryName(modelRecord.FullPath)!;
        var texturePaths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (ImportedMaterial source in materials)
        {
            ExtractTexture(source.BaseColorTexture, "Albedo");
            ExtractTexture(source.NormalTexture, "Normal");
            ExtractTexture(source.MetallicRoughnessTexture, "MetallicRoughness");
            ExtractTexture(source.MetallicTexture, "Metallic");
            ExtractTexture(source.RoughnessTexture, "Roughness");
            ExtractTexture(source.AmbientOcclusionTexture, "AO");
            ExtractTexture(source.EmissionTexture, "Emission");
        }
        project.AssetDatabase.Scan();
        var results = new List<AssetRecord>();
        foreach (ImportedMaterial source in materials)
        {
            var p = new MaterialParameters
            {
                SurfaceType = source.SurfaceType,
                AlphaCutoff = source.AlphaCutoff,
                DoubleSided = source.DoubleSided,
                Shading = source.Unlit ? MaterialShadingMode.Unlit : MaterialShadingMode.Lit,
                BaseColor = source.BaseColor,
                Metallic = source.Metallic,
                Roughness = source.Roughness,
                AmbientOcclusionStrength = source.AmbientOcclusionStrength,
                EmissionColor = source.EmissionColor,
                EmissionEnabled = source.EmissionColor.LengthSquared() > 0f ||
                    source.EmissionTexture != null,
                BaseColorTexture = Reference(source.BaseColorTexture),
                NormalTexture = Reference(source.NormalTexture),
                AmbientOcclusionTexture = Reference(source.AmbientOcclusionTexture),
                EmissionTexture = Reference(source.EmissionTexture),
                MetallicTexture = Reference(source.MetallicTexture),
                RoughnessTexture = Reference(source.RoughnessTexture),
            };
            if (source.MetallicRoughnessTexture != null)
            {
                p.PbrMapMode = MaterialPbrMapMode.Packed;
                p.PackedAoChannel = MaterialMapChannel.None;
                p.PackedRoughnessChannel = MaterialMapChannel.Green;
                p.PackedMetallicChannel = MaterialMapChannel.Blue;
                p.PackedPbrTexture = Reference(source.MetallicRoughnessTexture);
            }
            if (source.NormalTexture?.Name.Contains("NormalDX",
                StringComparison.OrdinalIgnoreCase) == true)
                p.NormalConvention = MaterialNormalConvention.DirectX;
            var document = new MaterialAsset
            {
                Name = source.Name,
                Standard = p
            };
            string output = UniquePath(directory,
                $"M_{Safe(model.Name)}_{Safe(source.Name)}", ".bmat");
            document.Name = Path.GetFileNameWithoutExtension(output);
            MaterialAssetSerializer.Save(output, document);
            project.AssetDatabase.Scan();
            string projectPath = ProjectPath(output);
            if (project.AssetDatabase.TryGetAsset(projectPath, out AssetRecord? record) &&
                record != null) results.Add(record);
        }
        return results;

        void ExtractTexture(ImportedTexture? texture, string suffix)
        {
            if (texture == null || texture.EncodedData.Length == 0 ||
                texturePaths.ContainsKey(texture.Key)) return;
            string? extension = TextureExtension(texture);
            if (extension == null) return;
            string path = UniquePath(directory,
                $"T_{Safe(model.Name)}_{Safe(texture.Name)}_{suffix}", extension);
            File.WriteAllBytes(path, texture.EncodedData);
            texturePaths[texture.Key] = path;
        }
        AssetReference Reference(ImportedTexture? texture)
        {
            if (texture == null || !texturePaths.TryGetValue(texture.Key, out string? path))
                return AssetReference.Empty;
            string projectPath = ProjectPath(path);
            return project.AssetDatabase.TryGetAsset(projectPath, out AssetRecord? record) &&
                record != null
                ? new AssetReference(record.Guid, record.ProjectPath)
                : AssetReference.Empty;
        }
        string ProjectPath(string path) =>
            Path.GetRelativePath(project.ProjectRoot, path).Replace('\\', '/');
    }

    private static string? TextureExtension(ImportedTexture texture)
    {
        ReadOnlySpan<byte> bytes = texture.EncodedData;
        if (bytes.Length >= 8 && bytes[0] == 137 && bytes[1] == 80 &&
            bytes[2] == 78 && bytes[3] == 71) return ".png";
        if (bytes.Length >= 3 && bytes[0] == 255 && bytes[1] == 216)
            return ".jpg";
        string extension = Path.GetExtension(texture.SourcePath ?? string.Empty).ToLowerInvariant();
        if (extension is ".tga" or ".bmp" or ".jpeg" or ".jpg")
            return extension;
        return null;
    }

    private static string Safe(string name)
    {
        string safe = Regex.Replace(name, @"[^a-zA-Z0-9_-]+", "_").Trim('_');
        return safe.Length > 0 ? safe : "Material";
    }

    private static string UniquePath(string directory, string name, string extension)
    {
        string path = Path.Combine(directory, name + extension);
        for (int index = 1; File.Exists(path); index++)
            path = Path.Combine(directory, $"{name}_{index}{extension}");
        return path;
    }
}
