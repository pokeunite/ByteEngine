using ByteEngine.Core.Animation;

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

/// <summary>
/// Persistent model-import settings stored in the model asset's .meta file.
///
/// C9 adds a Unity-style Generic/Humanoid rig classification plus the canonical
/// semantic Humanoid mapping. This is import metadata because the rig belongs to
/// the model/skeleton itself, not to one Animation Profile or scene object.
/// </summary>
public sealed class ModelImporterSettings
{
    public float ImportScale { get; set; } =
        1.0f;

    public bool GenerateNormals { get; set; } =
        true;

    public bool PreferEmbeddedMaterials { get; set; } =
        true;

    /// <summary>
    /// Generic keeps the source skeleton as authored.
    /// Humanoid opts this model into ByteEngine's canonical human-bone contract.
    /// </summary>
    public AnimationRigType RigType { get; set; } =
        AnimationRigType.Generic;

    /// <summary>
    /// Semantic Humanoid-role to source-bone-name mapping.
    ///
    /// The map is preserved even if RigType is temporarily switched back to
    /// Generic so authoring work is not destroyed by toggling rig type.
    /// </summary>
    public HumanoidBoneMap HumanoidMapping { get; set; } =
        new();

    public void Normalize()
    {
        ImportScale =
            float.IsFinite(
                ImportScale) &&
            ImportScale >
                0.0001f
                ? ImportScale
                : 1.0f;

        HumanoidMapping ??=
            new HumanoidBoneMap();

        HumanoidMapping.Normalize();
    }
}
