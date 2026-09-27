using System.Text.RegularExpressions;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Editor;

internal static class LocalMaterialAssetFactory
{
    public static AssetReference Save(EditorProjectContext project, string objectName, Material source)
    {
        string safe = Regex.Replace(objectName, @"[^a-zA-Z0-9_-]+", "_").Trim('_');
        if (safe.Length == 0) safe = "Object";
        string directory = project.AssetDatabase.ResolveProjectPath(project.Project.AssetDirectory);
        Directory.CreateDirectory(directory);
        string stem = "M_" + safe;
        string path = Path.Combine(directory, stem + ".bmat");
        for (int number = 1; File.Exists(path); number++)
            path = Path.Combine(directory, $"{stem}_{number}.bmat");
        var parameters = new MaterialParameters
        {
            BaseColor = source.BaseColor,
            BaseColorTexture = Reference(source.MainTexture),
            NormalTexture = Reference(source.NormalTexture),
            NormalStrength = source.NormalStrength,
            NormalConvention = source.DirectXNormalMap
                ? MaterialNormalConvention.DirectX : MaterialNormalConvention.Auto,
            Metallic = source.Metallic,
            MetallicTexture = Reference(source.MetallicTexture),
            Roughness = source.Roughness,
            RoughnessTexture = Reference(source.RoughnessTexture),
            AmbientOcclusionTexture = Reference(source.AmbientOcclusionTexture),
            AmbientOcclusionStrength = source.AmbientOcclusionStrength,
            PbrMapMode = source.PbrMapMode,
            PackedPbrTexture = Reference(source.PackedPbrTexture),
            PackedAoChannel = source.PackedAoChannel,
            PackedRoughnessChannel = source.PackedRoughnessChannel,
            PackedMetallicChannel = source.PackedMetallicChannel,
            EmissionEnabled = source.EmissionEnabled,
            EmissionColor = source.EmissionColor,
            EmissionTexture = Reference(source.EmissionTexture),
            EmissionIntensity = source.EmissionIntensity,
            UvTiling = source.UvTiling,
            UvOffset = source.UvOffset,
            Shading = source.Shading,
            SurfaceType = source.BlendMode switch
            {
                BlendMode3D.Cutout => MaterialSurfaceType.Cutout,
                BlendMode3D.AlphaBlend => MaterialSurfaceType.Transparent,
                BlendMode3D.Additive => MaterialSurfaceType.Additive,
                _ => MaterialSurfaceType.Opaque
            },
            AlphaCutoff = source.AlphaCutoff,
            DoubleSided = source.CullMode == CullMode3D.None,
            DepthTest = source.DepthTest,
            DepthWriteMode = source.DepthWriteMode,
            CullMode = source.CullMode,
            FrontFace = source.FrontFace,
            PolygonMode = source.PolygonMode
        };
        MaterialAssetSerializer.Save(path, new MaterialAsset
        {
            Name = Path.GetFileNameWithoutExtension(path),
            Standard = parameters
        });
        project.AssetDatabase.Scan();
        string projectPath = Path.GetRelativePath(project.ProjectRoot, path).Replace('\\', '/');
        if (!project.AssetDatabase.TryGetAsset(projectPath, out AssetRecord? record) ||
            record?.Type != AssetType.Material)
            throw new IOException("Saved material was not indexed by the asset database.");
        return new AssetReference(record.Guid, record.ProjectPath);

        AssetReference Reference(Texture2D? texture)
        {
            if (texture == null) return AssetReference.Empty;
            if (string.IsNullOrWhiteSpace(texture.FilePath))
                throw new InvalidOperationException(
                    "This local material uses an embedded model texture. Use Extract Material on the model first.");
            string assetPath = Path.GetRelativePath(project.ProjectRoot, texture.FilePath)
                .Replace('\\', '/');
            if (assetPath.StartsWith("..", StringComparison.Ordinal) ||
                !project.AssetDatabase.TryGetAsset(assetPath, out AssetRecord? record) ||
                record?.Type != AssetType.Texture2D)
                throw new InvalidOperationException(
                    $"Texture '{texture.FilePath}' is not a project texture asset.");
            return new AssetReference(record.Guid, record.ProjectPath);
        }
    }
}
