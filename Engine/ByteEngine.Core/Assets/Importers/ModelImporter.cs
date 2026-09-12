namespace ByteEngine.Core.Assets.Importers;

public abstract class ModelImporter
{
    public abstract IReadOnlyCollection<string> Extensions { get; }

    public abstract ImportedModel Import(
        AssetRecord source,
        ModelImporterSettings settings);

    public static ModelImporter ForPath(
        string path)
    {
        return Path.GetExtension(
                path)
            .ToLowerInvariant() switch
        {
            ".glb" or
            ".gltf" =>
                new GltfModelImporter(),

            ".obj" =>
                new ObjModelImporter(),

            ".fbx" =>
                new FbxModelImporter(),

            _ =>
                throw new NotSupportedException(
                    $"Unsupported model format '{Path.GetExtension(path)}'.")
        };
    }
}

public sealed class ModelImporterSettings
{
    public float ImportScale { get; set; } =
        1.0f;

    public bool GenerateNormals { get; set; } =
        true;

    public bool PreferEmbeddedMaterials { get; set; } =
        true;
}
