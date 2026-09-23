using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

using ByteEngine.Editor.Panels;

using ImGuiNET;

namespace ByteEngine.Editor;

/// <summary>
/// Compact Humanoid-rig surface used by Animation Profile authoring.
///
/// Detailed mapping now lives in the dedicated Humanoid Rig Configurator so the
/// Animation Profile does not become a several-screen-long list of bone fields.
/// </summary>
internal static class HumanoidRigAuthoring
{
    private static readonly JsonSerializerOptions MetaJson =
        CreateMetaJson();

    public static bool Draw(
        AssetRecord asset,
        ModelAsset model,
        EditorProjectContext project,
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
            "Rig classification and Humanoid mapping are stored on the model asset.");

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

            PersistAndRefresh(
                asset,
                project,
                out saveError);
        }

        rigType =
            settings.RigType;

        if (settings.RigType ==
            AnimationRigType.Generic)
        {
            ImGui.TextWrapped(
                "Generic keeps the source skeleton exactly as authored. Switch to Humanoid to enable experimental humanoid animation retargeting.");

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

            PersistAndRefresh(
                asset,
                project,
                out saveError);
        }

        ImGui.SameLine();

        if (ImGui.Button(
                "Configure Humanoid..."))
        {
            HumanoidRigConfiguratorRequest.Request(
                asset.Guid);
        }

        HumanoidRigValidationResult validation =
            HumanoidRigMapper.Validate(
                model.Skeleton,
                settings.HumanoidMapping);

        DrawValidation(
            validation);

        HumanoidRigDiagnosticReport diagnostics =
            HumanoidRigDiagnostics.Analyze(
                model.Skeleton,
                settings.HumanoidMapping);

        if (validation.IsReady)
        {
            if (diagnostics.Errors.Count >
                0)
            {
                ImGui.TextColored(
                    new Vector4(
                        1.0f,
                        0.38f,
                        0.30f,
                        1.0f),
                    "Hierarchy check failed - open Configure Humanoid.");
            }
            else if (!diagnostics.ApproximatelyTPose)
            {
                ImGui.TextColored(
                    new Vector4(
                        1.0f,
                        0.67f,
                        0.25f,
                        1.0f),
                    "Reference pose is not close to a clean T-pose.");

                ImGui.TextDisabled(
                    "Open Configure Humanoid to inspect the skeleton visually.");
            }
        }

        DrawSaveError(
            saveError);

        rigType =
            settings.RigType;

        return changed;
    }

    internal static bool Persist(
        AssetRecord asset,
        out string? error)
    {
        error =
            null;

        try
        {
            asset.Metadata.ModelImporter.Normalize();

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

            return true;
        }
        catch (Exception exception)
        {
            error =
                exception.Message;

            return false;
        }
    }

    private static void PersistAndRefresh(
        AssetRecord asset,
        EditorProjectContext project,
        out string? error)
    {
        if (!Persist(
                asset,
                out error))
        {
            return;
        }

        try
        {
            project.Assets.ReimportModel(
                asset.Guid);
        }
        catch (Exception exception)
        {
            error =
                exception.Message;
        }
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

    internal static void ClearDuplicateAssignment(
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

    internal static string DisplayName(
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
