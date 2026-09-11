using System.Numerics;

using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class GameViewPanel : IDisposable
{
    private readonly SceneFramebuffer _framebuffer =
        new();

    public bool IsOpen { get; set; } =
        true;

    public void Draw(
        EditorState state,
        Renderer2D renderer,
        Renderer3D renderer3D,
        int windowWidth,
        int windowHeight)
    {
        bool isOpen =
            IsOpen;

        bool visible =
            ImGui.Begin(
                "Game View",
                ref isOpen,
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse);

        IsOpen =
            isOpen;

        /*
         * IMPORTANT:
         *
         * ImGui.Begin returns false when this docked window is
         * hidden behind another tab.
         *
         * Previously ByteEngine continued rendering the entire
         * Game View framebuffer even while the tab was invisible.
         * That caused unnecessary continuous GPU work.
         */
        if (!visible)
        {
            ImGui.End();

            return;
        }

        int renderWidth =
            Math.Clamp(
                state.Project.Window.Width,
                1,
                4096);

        int renderHeight =
            Math.Clamp(
                state.Project.Window.Height,
                1,
                4096);

        ImGui.TextDisabled(
            $"{renderWidth} x {renderHeight} | " +
            (
                state.Mode ==
                EditorMode.Edit
                    ? "Game camera preview"
                    : "Runtime game camera"
            ));

        Vector2 available =
            ImGui.GetContentRegionAvail();

        available.X =
            Math.Max(
                available.X,
                1.0f);

        available.Y =
            Math.Max(
                available.Y,
                1.0f);

        float scale =
            Math.Min(
                available.X /
                renderWidth,

                available.Y /
                renderHeight);

        scale =
            Math.Max(
                scale,
                0.0001f);

        Vector2 displaySize =
            new(
                renderWidth *
                scale,

                renderHeight *
                scale);

        Vector2 offset =
            (available -
             displaySize) *
            0.5f;

        _framebuffer.RenderGame(
            renderer,
            renderer3D,
            state.DisplayedScene,
            state.Mode,
            renderWidth,
            renderHeight,
            windowWidth,
            windowHeight);

        Vector2 cursor =
            ImGui.GetCursorPos();

        Vector2 screen =
            ImGui.GetCursorScreenPos();

        ImGui.GetWindowDrawList()
            .AddRectFilled(
                screen,
                screen +
                available,
                ImGui.GetColorU32(
                    new Vector4(
                        0.0f,
                        0.0f,
                        0.0f,
                        1.0f)));

        ImGui.SetCursorPos(
            cursor +
            offset);

        ImGui.Image(
            _framebuffer.TextureId,
            displaySize,
            new Vector2(
                0.0f,
                1.0f),
            new Vector2(
                1.0f,
                0.0f));

        ImGui.End();
    }

    public void Dispose()
    {
        _framebuffer.Dispose();
    }
}