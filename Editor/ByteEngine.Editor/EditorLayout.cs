using System.Numerics;

using ImGuiNET;

namespace ByteEngine.Editor;

internal sealed class EditorLayout
{
    private bool _layoutChecked;

    private bool _resetRequested;

    public void RequestReset()
    {
        _resetRequested =
            true;
    }

    public void DrawDockSpace()
    {
        ImGuiViewportPtr viewport =
            ImGui.GetMainViewport();

        ImGui.SetNextWindowPos(
            viewport.WorkPos
        );

        ImGui.SetNextWindowSize(
            viewport.WorkSize
        );

        ImGui.SetNextWindowViewport(
            viewport.ID
        );

        ImGui.PushStyleVar(
            ImGuiStyleVar.WindowRounding,
            0.0f
        );

        ImGui.PushStyleVar(
            ImGuiStyleVar.WindowBorderSize,
            0.0f
        );

        ImGui.PushStyleVar(
            ImGuiStyleVar.WindowPadding,
            Vector2.Zero
        );

        ImGuiWindowFlags windowFlags =
            ImGuiWindowFlags.NoDocking |
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoBringToFrontOnFocus |
            ImGuiWindowFlags.NoNavFocus |
            ImGuiWindowFlags.NoSavedSettings;

        ImGui.PushStyleColor(
            ImGuiCol.WindowBg,
            new Vector4(
                0.070f,
                0.082f,
                0.102f,
                1.0f));

        ImGui.Begin(
            "ByteEngine Dock Host",
            windowFlags
        );

        ImGui.PopStyleColor();
        ImGui.PopStyleVar(3);

        uint dockSpaceId =
            ImGui.GetID(
                "ByteEngine DockSpace"
            );

        if (!_layoutChecked)
        {
            if (!ImGuiDockBuilder.NodeExists(
                    dockSpaceId))
            {
                ImGuiDockBuilder.BuildDefaultLayout(
                    dockSpaceId,
                    viewport.WorkSize
                );
            }

            _layoutChecked =
                true;
        }

        if (_resetRequested)
        {
            ImGuiDockBuilder.BuildDefaultLayout(
                dockSpaceId,
                viewport.WorkSize
            );

            _resetRequested =
                false;
        }

        ImGui.DockSpace(
            dockSpaceId,
            Vector2.Zero,
            ImGuiDockNodeFlags.PassthruCentralNode
        );

        ImGui.End();
    }
}
