using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

using ImGuiNET;

namespace ByteEngine.Editor;

/// <summary>
/// Central Humanoid-rig authoring UI used by the Animation Profile workspace.
///
/// Rig metadata is persisted on the model's .meta file because Generic/Humanoid
/// classification is intrinsic to the imported skeleton. The Animation Profile
/// merely links to that model.
/// </summary>
internal static class HumanoidRigAuthoring
{
    private static readonly JsonSerializerOptions MetaJson =
        CreateMetaJson();

    public static bool Draw(
        AssetRecord asset,
        ModelAsset model,
        out AnimationRigType rigType)
    {
        ModelImporterSettings settings =
            asset.Metadata.ModelImporter;

        settings.Normalize();

        bool changed =
            false;

        string? saveError =
            null;

        ImGui.TextDisabled(
            "Rig classification is stored on the Reference Model asset.");

        int selectedRigType =
            (int)settings.RigType;

        string[] rigTypeNames =
            Enum.GetNames<AnimationRigType>();

        if (ImGui.Combo(
                "Rig Type",
                ref selectedRigType,
                rigTypeNames,
                rigTypeNames.Length))
        {
            settings.RigType =
                (AnimationRigType)selectedRigType;

            if (settings.RigType ==
                    AnimationRigType.Humanoid &&
                settings.HumanoidMapping.MappedCount ==
                    0 &&
                model.Skeleton !=
                    null)
            {
                settings.HumanoidMapping =
                    HumanoidRigMapper.AutoMap(
                        model.Skeleton);
            }

            changed =
                true;
        }

        rigType =
            settings.RigType;

        if (settings.RigType ==
            AnimationRigType.Generic)
        {
            ImGui.TextWrapped(
                "Generic keeps the source skeleton exactly as authored. Humanoid enables semantic human-bone mapping and later reusable Humanoid animation retargeting.");

            if (changed)
            {
                Persist(
                    asset,
                    out saveError);
            }

            DrawSaveError(
                saveError);

            return changed;
        }

        ImGui.Spacing();

        if (model.Skeleton ==
            null)
        {
            ImGui.TextColored(
                new Vector4(
                    1.0f,
                    0.38f,
                    0.30f,
                    1.0f),
                "Humanoid requires a model with a skeleton.");

            if (changed)
            {
                Persist(
                    asset,
                    out saveError);
            }

            DrawSaveError(
                saveError);

            return changed;
        }

        ImGui.Text(
            $"Skeleton: {model.Skeleton.Name}");

        ImGui.TextDisabled(
            $"{model.Skeleton.Bones.Count} source bone(s)");

        if (ImGui.Button(
                "Auto Map Humanoid Bones"))
        {
            settings.HumanoidMapping =
                HumanoidRigMapper.AutoMap(
                    model.Skeleton);

            changed =
                true;
        }

        ImGui.SameLine();

        if (ImGui.Button(
                "Clear Mapping"))
        {
            settings.HumanoidMapping =
                new HumanoidBoneMap();

            changed =
                true;
        }

        HumanoidRigValidationResult validation =
            HumanoidRigMapper.Validate(
                model.Skeleton,
                settings.HumanoidMapping);

        DrawValidation(
            validation);

        if (ImGui.TreeNodeEx(
                "Required Bone Mapping",
                ImGuiTreeNodeFlags.DefaultOpen))
        {
            foreach (HumanoidBone semanticBone
                     in HumanoidBoneCatalog.Required)
            {
                if (DrawBonePicker(
                        semanticBone,
                        model.Skeleton,
                        settings.HumanoidMapping))
                {
                    changed =
                        true;
                }
            }

            ImGui.TreePop();
        }

        if (ImGui.TreeNode(
                "Optional Bone Mapping"))
        {
            foreach (HumanoidBone semanticBone
                     in Enum.GetValues<HumanoidBone>())
            {
                if (HumanoidBoneCatalog.IsRequired(
                        semanticBone))
                {
                    continue;
                }

                if (DrawBonePicker(
                        semanticBone,
                        model.Skeleton,
                        settings.HumanoidMapping))
                {
                    changed =
                        true;
                }
            }

            ImGui.TreePop();
        }

        if (changed)
        {
            settings.Normalize();

            Persist(
                asset,
                out saveError);
        }

        DrawSaveError(
            saveError);

        rigType =
            settings.RigType;

        return changed;
    }

    private static void DrawValidation(
        HumanoidRigValidationResult validation)
    {
        ImGui.SeparatorText(
            "HUMANOID VALIDATION");

        if (validation.IsReady)
        {
            ImGui.TextColored(
                new Vector4(
                    0.35f,
                    0.86f,
                    0.48f,
                    1.0f),
                "Humanoid Ready");

            ImGui.TextDisabled(
                $"{validation.RequiredMappedCount}/{HumanoidBoneCatalog.Required.Count} required bones mapped; {validation.MappedBoneCount} total semantic bones mapped.");

            return;
        }

        ImGui.TextColored(
            new Vector4(
                1.0f,
                0.58f,
                0.24f,
                1.0f),
            "Humanoid Needs Attention");

        ImGui.TextDisabled(
            $"{validation.RequiredMappedCount}/{HumanoidBoneCatalog.Required.Count} required bones mapped.");

        if (!validation.HasSkeleton)
        {
            ImGui.BulletText(
                "No skeleton was imported.");

            return;
        }

        if (validation.MissingRequiredBones.Count >
            0)
        {
            ImGui.TextWrapped(
                $"Missing required: {string.Join(", ", validation.MissingRequiredBones.Select(DisplayName))}");
        }

        if (validation.InvalidMappedBones.Count >
            0)
        {
            ImGui.TextWrapped(
                $"Mapped source bone not found: {string.Join(", ", validation.InvalidMappedBones.Select(DisplayName))}");
        }

        if (validation.DuplicateSourceBones.Count >
            0)
        {
            ImGui.TextWrapped(
                $"A source bone is assigned more than once: {string.Join(", ", validation.DuplicateSourceBones)}");
        }
    }

    private static bool DrawBonePicker(
        HumanoidBone semanticBone,
        SkeletonAsset skeleton,
        HumanoidBoneMap mapping)
    {
        string? current =
            mapping.GetBoneName(
                semanticBone);

        bool currentExists =
            current !=
                null &&
            skeleton.Bones.Any(
                bone =>
                    string.Equals(
                        bone.Name,
                        current,
                        StringComparison.OrdinalIgnoreCase));

        string preview =
            current ==
                null
                ? "None"
                : currentExists
                    ? current
                    : $"{current} (Missing)";

        bool changed =
            false;

        string label =
            $"{DisplayName(semanticBone)}##HumanoidBone:{semanticBone}";

        if (ImGui.BeginCombo(
                label,
                preview))
        {
            if (ImGui.Selectable(
                    "None",
                    current ==
                        null))
            {
                mapping.ClearBone(
                    semanticBone);

                changed =
                    true;
            }

            foreach (Bone bone
                     in skeleton.Bones
                         .OrderBy(
                             bone =>
                                 bone.Name,
                             StringComparer.OrdinalIgnoreCase))
            {
                bool selected =
                    string.Equals(
                        bone.Name,
                        current,
                        StringComparison.OrdinalIgnoreCase);

                if (ImGui.Selectable(
                        bone.Name,
                        selected))
                {
                    ClearDuplicateAssignment(
                        mapping,
                        semanticBone,
                        bone.Name);

                    mapping.SetBone(
                        semanticBone,
                        bone.Name);

                    changed =
                        true;
                }

                if (selected)
                {
                    ImGui.SetItemDefaultFocus();
                }
            }

            ImGui.EndCombo();
        }

        return changed;
    }

    private static void ClearDuplicateAssignment(
        HumanoidBoneMap mapping,
        HumanoidBone semanticBone,
        string sourceBoneName)
    {
        HumanoidBone[] duplicates =
            mapping.Bones
                .Where(
                    pair =>
                        pair.Key !=
                            semanticBone &&
                        string.Equals(
                            pair.Value,
                            sourceBoneName,
                            StringComparison.OrdinalIgnoreCase))
                .Select(
                    pair =>
                        pair.Key)
                .ToArray();

        foreach (HumanoidBone duplicate
                 in duplicates)
        {
            mapping.ClearBone(
                duplicate);
        }
    }

    private static void Persist(
        AssetRecord asset,
        out string? error)
    {
        error =
            null;

        try
        {
            string temporary =
                asset.MetaPath +
                ".tmp";

            File.WriteAllText(
                temporary,
                JsonSerializer.Serialize(
                    asset.Metadata,
                    MetaJson));

            File.Move(
                temporary,
                asset.MetaPath,
                true);
        }
        catch (Exception exception)
        {
            error =
                exception.Message;
        }
    }

    private static void DrawSaveError(
        string? error)
    {
        if (string.IsNullOrWhiteSpace(
                error))
        {
            return;
        }

        ImGui.TextColored(
            new Vector4(
                1.0f,
                0.35f,
                0.30f,
                1.0f),
            "Could not save model rig metadata.");

        ImGui.TextWrapped(
            error);
    }

    private static string DisplayName(
        HumanoidBone bone)
    {
        string text =
            bone.ToString();

        var output =
            new System.Text.StringBuilder(
                text.Length +
                8);

        for (int index = 0;
             index < text.Length;
             index++)
        {
            char current =
                text[index];

            if (index >
                    0 &&
                char.IsUpper(
                    current) &&
                !char.IsUpper(
                    text[index - 1]))
            {
                output.Append(
                    ' ');
            }

            output.Append(
                current);
        }

        return output.ToString();
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
}
