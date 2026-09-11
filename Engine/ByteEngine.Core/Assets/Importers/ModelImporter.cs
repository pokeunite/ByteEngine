namespace ByteEngine.Core.Assets.Importers;

public abstract class ModelImporter
{
    public abstract IReadOnlyCollection<string> Extensions { get; }
    public abstract ImportedModel Import(AssetRecord source, ModelImporterSettings settings);

    public static ModelImporter ForPath(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".glb" or ".gltf" => new GltfModelImporter(),
        ".obj" => new ObjModelImporter(),
        ".fbx" => throw new NotSupportedException(
            "FBX static-mesh import is not available in this build. Export the model as GLB or GLTF."),
        _ => throw new NotSupportedException($"Unsupported model format '{Path.GetExtension(path)}'.")
    };
}

public sealed class ModelImporterSettings
{
    public float ImportScale { get; set; } = 1f;
    public bool GenerateNormals { get; set; } = true;
    public bool PreferEmbeddedMaterials { get; set; } = true;
}
