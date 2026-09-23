using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;

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
                new AnimationSourceCompatibleImporter(
                    new GltfModelImporter()),

            ".obj" =>
                new ObjModelImporter(),

            ".fbx" =>
                new AnimationSourceCompatibleImporter(
                    new FbxModelImporter()),

            _ =>
                throw new NotSupportedException(
                    $"Unsupported model format '{Path.GetExtension(path)}'.")
        };
    }
}

/// <summary>
/// Selects which source trajectory ByteEngine uses when a Humanoid clip is
/// retargeted onto another character.
/// </summary>
public enum AnimationRootMotionSource
{
    /// <summary>
    /// Prefer meaningful Skeleton Root motion. If the source does not provide
    /// it, promote meaningful Hips/Pelvis travel to the target root.
    /// </summary>
    Automatic,

    /// <summary>
    /// Only use explicit Skeleton Root travel. Hips translation remains pose
    /// motion and is never promoted to the target root.
    /// </summary>
    SkeletonRoot,

    /// <summary>
    /// Treat Hips/Pelvis model-space travel as the authoritative trajectory and
    /// generate the target root track from it.
    /// </summary>
    Hips
}

/// <summary>
/// Persistent model/animation-import settings stored in the asset's .meta file.
///
/// ByteEngine keeps mesh, rig and embedded animation settings together because
/// FBX/glTF commonly carry all three in one source file. Animation-only sources
/// use this same contract, including files with no render mesh.
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
    /// Imports embedded animation tracks from FBX/glTF sources.
    /// </summary>
    public bool ImportAnimations { get; set; } =
        true;

    /// <summary>
    /// New animation sources use Auto rig detection by default. A source is
    /// promoted to Humanoid only when ByteEngine can map every required
    /// Humanoid bone and the resulting hierarchy validates successfully.
    ///
    /// Turning this off makes RigType an explicit user choice.
    /// </summary>
    public bool AutoDetectHumanoidRig { get; set; } =
        true;

    /// <summary>
    /// Generic keeps the source skeleton as authored. Humanoid opts the source
    /// into ByteEngine's canonical semantic Humanoid contract.
    /// </summary>
    public AnimationRigType RigType { get; set; } =
        AnimationRigType.Generic;

    /// <summary>
    /// Semantic Humanoid-role to source-bone-name mapping.
    /// </summary>
    public HumanoidBoneMap HumanoidMapping { get; set; } =
        new();

    /// <summary>
    /// Root-motion trajectory used when this asset acts as a retarget source.
    /// </summary>
    public AnimationRootMotionSource RootMotionSource { get; set; } =
        AnimationRootMotionSource.Automatic;

    /// <summary>
    /// Sampling frequency used when this Humanoid's clips are baked onto a
    /// different target skeleton.
    /// </summary>
    public float RetargetSamplesPerSecond { get; set; } =
        60.0f;

    /// <summary>
    /// Optional Humanoid rig provider for animation sources that do not carry a
    /// usable skeleton/reference pose of their own.
    ///
    /// This is intentionally a model reference instead of a copied bone map:
    /// the assigned model supplies the complete Humanoid skeleton, semantic
    /// mapping, bind/reference pose and helper-node hierarchy needed to sample
    /// otherwise animation-only tracks.
    /// </summary>
    public AssetReference AnimationSourceRigModel { get; set; } =
        AssetReference.Empty;

    /// <summary>
    /// Convenience target remembered by the importer UI. Retargeting remains
    /// explicit and non-destructive until the user bakes a generated clip onto
    /// the selected target model.
    /// </summary>
    public AssetReference DefaultRetargetTargetModel { get; set; } =
        AssetReference.Empty;

    public void Normalize()
    {
        ImportScale =
            float.IsFinite(
                ImportScale) &&
            ImportScale >
                0.0001f
                ? ImportScale
                : 1.0f;

        RetargetSamplesPerSecond =
            float.IsFinite(
                RetargetSamplesPerSecond)
                ? Math.Clamp(
                    RetargetSamplesPerSecond,
                    15.0f,
                    240.0f)
                : 60.0f;

        if (!Enum.IsDefined(
                typeof(AnimationRootMotionSource),
                RootMotionSource))
        {
            RootMotionSource =
                AnimationRootMotionSource.Automatic;
        }

        HumanoidMapping ??=
            new HumanoidBoneMap();

        HumanoidMapping.Normalize();

        AnimationSourceRigModel ??=
            AssetReference.Empty;

        DefaultRetargetTargetModel ??=
            AssetReference.Empty;
    }

    public ModelImporterSettings Clone()
    {
        Normalize();

        return
            new ModelImporterSettings
            {
                ImportScale =
                    ImportScale,

                GenerateNormals =
                    GenerateNormals,

                PreferEmbeddedMaterials =
                    PreferEmbeddedMaterials,

                ImportAnimations =
                    ImportAnimations,

                AutoDetectHumanoidRig =
                    AutoDetectHumanoidRig,

                RigType =
                    RigType,

                HumanoidMapping =
                    HumanoidMapping.Clone(),

                RootMotionSource =
                    RootMotionSource,

                RetargetSamplesPerSecond =
                    RetargetSamplesPerSecond,

                AnimationSourceRigModel =
                    AnimationSourceRigModel,

                DefaultRetargetTargetModel =
                    DefaultRetargetTargetModel
            };
    }
}
