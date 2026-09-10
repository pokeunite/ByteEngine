using ByteEngine.Core.Scene;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class HierarchyPanel
{
    public bool IsOpen { get; set; } =
        true;

    public void Draw(
        EditorState state,
        Action createObject,
        Action deleteObject)
    {
        bool isOpen =
            IsOpen;

        ImGui.Begin(
            "Hierarchy",
            ref isOpen
        );

        IsOpen =
            isOpen;

        ImGui.TextDisabled(
            state.DisplayedScene.Name
        );

        ImGui.SameLine();

        ImGui.TextColored(
            state.Mode == EditorMode.Edit
                ? new System.Numerics.Vector4(
                    0.4f,
                    0.8f,
                    1.0f,
                    1.0f
                )
                : new System.Numerics.Vector4(
                    0.4f,
                    1.0f,
                    0.5f,
                    1.0f
                ),
            state.Mode.ToString()
        );

        ImGui.Separator();

        foreach (GameObject gameObject
                 in state.DisplayedScene.GameObjects)
        {
            string label =
                $"{gameObject.Name}##{gameObject.Id}";

            bool selected =
                ReferenceEquals(
                    state.SelectedObject,
                    gameObject
                );

            if (ImGui.Selectable(
                    label,
                    selected))
            {
                state.SelectedObject =
                    gameObject;

                state.SelectedAssetId = null;
                state.SelectedAssetPath = null;
            }
        }

        if (state.Mode == EditorMode.Edit &&
            ImGui.BeginPopupContextWindow(
                "HierarchyContext",
                ImGuiPopupFlags.MouseButtonRight |
                ImGuiPopupFlags.NoOpenOverItems))
        {
            if (ImGui.MenuItem(
                    "Create Empty"))
            {
                createObject();
            }

            ImGui.EndPopup();
        }

        if (state.Mode == EditorMode.Edit &&
            state.SelectedObject != null &&
            ImGui.IsWindowFocused() &&
            ImGui.IsKeyPressed(
                ImGuiKey.Delete))
        {
            deleteObject();
        }

        ImGui.End();
    }
}
