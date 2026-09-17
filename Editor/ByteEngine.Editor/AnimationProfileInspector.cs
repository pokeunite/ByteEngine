using System.Numerics;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Editor.Panels;

using ImGuiNET;

namespace ByteEngine.Editor;

/// <summary>
/// Small AnimationController-facing profile surface.
///
/// Profile authoring lives in the dedicated .byteanim workspace. The character
/// component only owns the reference and a convenient Open button.
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
        ImGui.SeparatorText("ANIMATION PROFILE");

        AssetReference reference =
            controller.AnimationProfile ??
            AssetReference.Empty;

        if (reference.IsEmpty)
        {
            ImGui.TextWrapped(
                "No Animation Profile assigned. Create one in Assets > + Create > Animation Profile, then drag or pick it in the Animation Profile field above.");

            return;
        }

        AssetRecord? asset =
            project.AssetDatabase.Resolve(
                reference);

        if (asset == null ||
            asset.Type != AssetType.AnimationProfile)
        {
            ImGui.TextColored(
                new Vector4(
                    1.0f,
                    0.55f,
                    0.2f,
                    1.0f),
                "Assigned Animation Profile is missing or invalid.");

            ImGui.TextDisabled(
                reference.CachedProjectPath ??
                reference.Guid.ToString());

            return;
        }

        ImGui.TextDisabled(
            asset.ProjectPath);

        ImGui.TextWrapped(
            "Rig, locomotion, actions, layers and procedural animation are edited in the Animation Profile asset.");

        ImGui.BeginDisabled(
            context == PropertyEditorContext.Runtime);

        if (ImGui.Button(
                "Open Animation Profile"))
        {
            AnimationProfileWorkspaceRequest.Request(
                reference);
        }

        ImGui.EndDisabled();

        if (ImGui.IsItemHovered())
        {
            ImGui.SetTooltip(
                "Opens the dedicated Animation Profile editor. You can also double-click the .byteanim asset in the Asset Browser.");
        }
    }
}
