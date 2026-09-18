using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Tests;

internal static class C9HumanoidRigFoundationTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyCanonicalHumanoidMap();
        VerifyModelImporterMetadataRoundTrip();
        VerifyGenericIsBackwardCompatibleDefault();
    }

    private static void VerifyCanonicalHumanoidMap()
    {
        var map =
            new HumanoidBoneMap();

        map.SetBone(
            HumanoidBone.Hips,
            " mixamorig:Hips ");

        map.SetBone(
            HumanoidBone.LeftUpperArm,
            "mixamorig:LeftArm");

        map.SetBone(
            HumanoidBone.LeftLowerArm,
            "mixamorig:LeftForeArm");

        map.SetBone(
            HumanoidBone.LeftHand,
            "mixamorig:LeftHand");

        map.SetBone(
            HumanoidBone.RightUpperArm,
            "mixamorig:RightArm");

        map.SetBone(
            HumanoidBone.RightLowerArm,
            "mixamorig:RightForeArm");

        map.SetBone(
            HumanoidBone.RightHand,
            "mixamorig:RightHand");

        map.SetBone(
            HumanoidBone.Spine,
            "mixamorig:Spine");

        map.SetBone(
            HumanoidBone.Head,
            "mixamorig:Head");

        map.SetBone(
            HumanoidBone.LeftUpperLeg,
            "mixamorig:LeftUpLeg");

        map.SetBone(
            HumanoidBone.LeftLowerLeg,
            "mixamorig:LeftLeg");

        map.SetBone(
            HumanoidBone.LeftFoot,
            "mixamorig:LeftFoot");

        map.SetBone(
            HumanoidBone.RightUpperLeg,
            "mixamorig:RightUpLeg");

        map.SetBone(
            HumanoidBone.RightLowerLeg,
            "mixamorig:RightLeg");

        map.SetBone(
            HumanoidBone.RightFoot,
            "mixamorig:RightFoot");

        map.Normalize();

        Assert(
            map.GetBoneName(
                HumanoidBone.Hips) ==
            "mixamorig:Hips",
            "C9A: Humanoid mapping did not normalize source bone names.");

        Assert(
            map.MissingRequiredBones().Count ==
            0,
            "C9A: fully mapped core Humanoid was reported incomplete.");

        map.ClearBone(
            HumanoidBone.Head);

        Assert(
            map.MissingRequiredBones()
                .Contains(
                    HumanoidBone.Head),
            "C9A: required Humanoid bone tracking did not report Head.");
    }

    private static void VerifyModelImporterMetadataRoundTrip()
    {
        var metadata =
            new AssetMetadata
            {
                Guid =
                    Guid.NewGuid(),

                Type =
                    AssetType.Model3D
            };

        metadata.ModelImporter.RigType =
            AnimationRigType.Humanoid;

        metadata.ModelImporter.HumanoidMapping.SetBone(
            HumanoidBone.Hips,
            "Rig|Hips");

        metadata.ModelImporter.HumanoidMapping.SetBone(
            HumanoidBone.LeftHand,
            "Rig|LeftHand");

        metadata.ModelImporter.Normalize();

        JsonSerializerOptions json =
            CreateMetaJson();

        string encoded =
            JsonSerializer.Serialize(
                metadata,
                json);

        AssetMetadata? clone =
            JsonSerializer.Deserialize<AssetMetadata>(
                encoded,
                json);

        Assert(
            clone !=
            null,
            "C9A: model metadata JSON round-trip returned null.");

        clone!.ModelImporter.Normalize();

        Assert(
            clone.ModelImporter.RigType ==
            AnimationRigType.Humanoid,
            "C9A: Humanoid rig type did not persist in model import metadata.");

        Assert(
            clone.ModelImporter.HumanoidMapping.GetBoneName(
                HumanoidBone.Hips) ==
            "Rig|Hips",
            "C9A: Hips semantic mapping did not persist in model import metadata.");

        Assert(
            clone.ModelImporter.HumanoidMapping.GetBoneName(
                HumanoidBone.LeftHand) ==
            "Rig|LeftHand",
            "C9A: LeftHand semantic mapping did not persist in model import metadata.");

        Assert(
            encoded.Contains(
                "\"rigType\": \"Humanoid\"",
                StringComparison.Ordinal),
            "C9A: Rig type should serialize as a readable enum name.");

        Assert(
            encoded.Contains(
                "\"Hips\": \"Rig|Hips\"",
                StringComparison.Ordinal),
            "C9A: Humanoid semantic bone keys should serialize as readable names.");
    }

    private static void VerifyGenericIsBackwardCompatibleDefault()
    {
        var settings =
            new ModelImporterSettings();

        Assert(
            settings.RigType ==
            AnimationRigType.Generic,
            "C9A: existing models must remain Generic by default.");

        Assert(
            settings.HumanoidMapping.MappedCount ==
            0,
            "C9A: a new Generic model should not invent Humanoid mappings.");
    }

    private static JsonSerializerOptions CreateMetaJson()
    {
        var options =
            new JsonSerializerOptions
            {
                PropertyNamingPolicy =
                    JsonNamingPolicy.CamelCase,

                PropertyNameCaseInsensitive =
                    true,

                WriteIndented =
                    true,

                AllowTrailingCommas =
                    true,

                ReadCommentHandling =
                    JsonCommentHandling.Skip
            };

        options.Converters.Add(
            new JsonStringEnumConverter());

        return options;
    }

    private static void Assert(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                message);
        }
    }
}
