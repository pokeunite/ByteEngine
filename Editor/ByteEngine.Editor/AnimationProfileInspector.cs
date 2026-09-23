using System.Numerics;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Editor.Panels;

using ImGuiNET;

namespace ByteEngine.Editor;

/// <summary>
/// AnimationController-facing profile surface.
///
/// When a valid Animation Profile is assigned it becomes the authoring source
/// of truth for locomotion settings. Legacy component slots are hidden by
/// ComponentPropertyRenderer so users never edit values that the profile will
/// overwrite at runtime.
/// </summary>
internal static class AnimationProfileInspector
{
    public static void Draw(
        AnimationController controller,
        EditorProjectContext project,
        PropertyEditorContext context,
        Action begin,
        Action changed,
        Action end)
    {
        /*
         * Safe to call from multiple editor surfaces. The retarget window
         * internally draws at most once per ImGui frame.
         */
        HumanoidRetargetBakeWindow.Draw();

        ImGui.SeparatorText(
            "ANIMATION PROFILE");

        AssetReference reference =
            controller.AnimationProfile ??
            AssetReference.Empty;

        AssetRecord? asset =
            reference.IsEmpty
                ? null
                : project.AssetDatabase.Resolve(
                    reference);

        /*
         * Asset references are GUID-backed, so a rename keeps working. If the
         * profile was actually deleted from the Asset Browser, remove the stale
         * component reference automatically instead of leaving a dead path in
         * the inspector.
         */
        if (!reference.IsEmpty &&
            (asset ==
                 null ||
             asset.Type !=
                 AssetType.AnimationProfile))
        {
            if (context !=
                PropertyEditorContext.Runtime)
            {
                begin();

                controller.AnimationProfile =
                    AssetReference.Empty;

                controller.ApplyAnimationProfile();

                changed();
                end();

                reference =
                    AssetReference.Empty;

                asset =
                    null;

                ImGui.TextColored(
                    new Vector4(
                        1.0f,
                        0.68f,
                        0.25f,
                        1.0f),
                    "Deleted Animation Profile reference cleared automatically.");
            }
            else
            {
                ImGui.TextColored(
                    new Vector4(
                        1.0f,
                        0.45f,
                        0.30f,
                        1.0f),
                    "Assigned Animation Profile is missing.");

                return;
            }
        }

        if (reference.IsEmpty ||
            asset ==
                null)
        {
            DrawProfileRequiredHelp();

            return;
        }

        ImGui.TextColored(
            new Vector4(
                0.35f,
                0.86f,
                0.48f,
                1.0f),
            "Animation Profile Active");

        ImGui.TextDisabled(
            asset.ProjectPath);

        ImGui.TextWrapped(
            "This profile now owns locomotion clip assignments and playback settings. The duplicate legacy slots on Animation Controller are hidden while the profile is assigned.");

        ImGui.TextColored(
            new Vector4(
                1.0f,
                0.68f,
                0.25f,
                1.0f),
            "After editing the profile, press Apply & Save before testing it.");

        ImGui.BeginDisabled(
            context ==
            PropertyEditorContext.Runtime);

        if (ImGui.Button(
                "Open Animation Profile"))
        {
            AnimationProfileWorkspaceRequest.Request(
                reference);
        }

        ImGui.SameLine();

        if (ImGui.Button(
                "Retarget Animation (Experimental)..."))
        {
            HumanoidRetargetBakeWindow.Open(
                reference,
                project);
        }

        ImGui.EndDisabled();

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Experimental: preview a temporary Humanoid retarget result. Nothing is baked into the target character until you explicitly choose Bake To Character.");
        }
    }

    private static void DrawProfileRequiredHelp()
    {
        ImGui.TextColored(
            new Vector4(
                1.0f,
                0.68f,
                0.25f,
                1.0f),
            "RETARGETING (EXPERIMENTAL) SETUP REQUIRED");

        ImGui.TextWrapped(
            "Experimental retargeting needs an Animation Profile because the profile identifies the target character model and Humanoid skeleton.");

        ImGui.BulletText(
            "1. Assets > + Create > Animation Profile");

        ImGui.BulletText(
            "2. Assign that profile to Animation Controller");

        ImGui.BulletText(
            "3. Open the profile and set its Reference Model");

        ImGui.BulletText(
            "4. Press Apply & Save, then use Retarget Animation (Experimental)");

        ImGui.BeginDisabled();

        ImGui.Button(
            "Retarget Animation (Experimental)... (Animation Profile Required)");

        ImGui.EndDisabled();
    }
}
