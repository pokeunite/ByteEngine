using System.Numerics;
using ByteEngine.Core.Graphics;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class GameViewPanel : IDisposable
{
    private readonly SceneFramebuffer _framebuffer = new();
    public bool IsOpen { get; set; } = true;

    public void Draw(EditorState state, Renderer2D renderer, int windowWidth, int windowHeight)
    {
        bool isOpen = IsOpen;
        ImGui.Begin("Game View", ref isOpen, ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
        IsOpen = isOpen;
        int renderWidth = Math.Max(state.Project.Window.Width, 1);
        int renderHeight = Math.Max(state.Project.Window.Height, 1);
        ImGui.TextDisabled($"{renderWidth} x {renderHeight} | " +
            (state.Mode == EditorMode.Edit ? "Game camera preview" : "Runtime game camera"));

        Vector2 available = ImGui.GetContentRegionAvail();
        available.X = Math.Max(available.X, 1f);
        available.Y = Math.Max(available.Y, 1f);
        float scale = Math.Min(available.X / renderWidth, available.Y / renderHeight);
        Vector2 displaySize = new(renderWidth * scale, renderHeight * scale);
        Vector2 offset = (available - displaySize) * .5f;

        _framebuffer.RenderGame(renderer, state.DisplayedScene, state.Mode,
            renderWidth, renderHeight, windowWidth, windowHeight);

        Vector2 cursor = ImGui.GetCursorPos();
        Vector2 screen = ImGui.GetCursorScreenPos();
        ImGui.GetWindowDrawList().AddRectFilled(screen, screen + available,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 1f)));
        ImGui.SetCursorPos(cursor + offset);
        ImGui.Image(_framebuffer.TextureId, displaySize, new Vector2(0f, 1f), new Vector2(1f, 0f));
        ImGui.End();
    }

    public void Dispose() => _framebuffer.Dispose();
}
