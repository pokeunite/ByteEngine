using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class GameViewPanel : IDisposable
{
    private readonly SceneFramebuffer _framebuffer = new();
    private bool _focusRequested;

    public bool IsOpen { get; set; } = true;

    public void RequestFocus()
    {
        IsOpen = true;
        _focusRequested = true;
    }

    public void Draw(
        EditorState state,
        Renderer2D renderer,
        Renderer3D renderer3D,
        int windowWidth,
        int windowHeight,
        Action captureInput,
        Action releaseInput)
    {
        bool isOpen = IsOpen;

        if (_focusRequested)
        {
            ImGui.SetNextWindowFocus();
            _focusRequested = false;
        }

        bool visible = ImGui.Begin(
            "Game View",
            ref isOpen,
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse);

        IsOpen = isOpen;

        if (!visible)
        {
            if (Input.IsGameInputCaptured) releaseInput();
            Input.SetGameViewPointer(
                Input.GameViewPointerNormalized,
                Input.GameViewSize,
                false,
                false,
                Vector2.Zero);

            ImGui.End();
            return;
        }

        int renderWidth = Math.Clamp(state.Project.Window.Width, 1, 4096);
        int renderHeight = Math.Clamp(state.Project.Window.Height, 1, 4096);

        ImGui.TextDisabled(
            $"{renderWidth} x {renderHeight} | " +
            (state.Mode == EditorMode.Edit ? "Game camera preview" : "Runtime game camera"));
        if (state.Mode == EditorMode.Play)
            ImGui.TextDisabled(Input.IsGameInputCaptured
                ? "Mouse captured — Esc to release"
                : "Click Game View to capture mouse");

        Vector2 available = ImGui.GetContentRegionAvail();
        available.X = Math.Max(available.X, 1f);
        available.Y = Math.Max(available.Y, 1f);

        float scale = Math.Min(
            available.X / renderWidth,
            available.Y / renderHeight);

        scale = Math.Max(scale, .0001f);

        Vector2 displaySize = new(
            renderWidth * scale,
            renderHeight * scale);

        Vector2 offset = (available - displaySize) * .5f;

        _framebuffer.RenderGame(
            renderer,
            renderer3D,
            state.DisplayedScene,
            state.Mode,
            renderWidth,
            renderHeight,
            windowWidth,
            windowHeight);

        Vector2 cursor = ImGui.GetCursorPos();
        Vector2 screen = ImGui.GetCursorScreenPos();

        ImGui.GetWindowDrawList().AddRectFilled(
            screen,
            screen + available,
            ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 1f)));

        ImGui.SetCursorPos(cursor + offset);

        Vector2 imageMin = ImGui.GetCursorScreenPos();
        Vector2 imageMax = imageMin + displaySize;
        Vector2 mouse = ImGui.GetMousePos();

        bool pointerInside =
            mouse.X >= imageMin.X &&
            mouse.X <= imageMax.X &&
            mouse.Y >= imageMin.Y &&
            mouse.Y <= imageMax.Y;

        if (state.Mode == EditorMode.Play && !Input.IsGameInputCaptured && pointerInside &&
            ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            captureInput();
        }
        else if (state.Mode != EditorMode.Play && Input.IsGameInputCaptured)
        {
            releaseInput();
        }

        Vector2 normalizedPointer = new(
            displaySize.X > 0f ? (mouse.X - imageMin.X) / displaySize.X : .5f,
            displaySize.Y > 0f ? (mouse.Y - imageMin.Y) / displaySize.Y : .5f);

        Input.SetGameViewPointer(
            normalizedPointer,
            new Vector2(renderWidth, renderHeight),
            pointerInside,
            ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows),
            mouse - imageMin);

        ImGui.Image(
            _framebuffer.TextureId,
            displaySize,
            new Vector2(0f, 1f),
            new Vector2(1f, 0f));

        if (state.Mode == EditorMode.Play && Input.IsGameInputCaptured)
        {
            const float radius = 8f;
            uint color = ImGui.GetColorU32(new Vector4(1f, 1f, 1f, .95f));
            Vector2 crosshair = imageMin + displaySize * .5f;
            ImDrawListPtr drawList = ImGui.GetWindowDrawList();
            drawList.AddLine(crosshair - new Vector2(radius, 0f), crosshair + new Vector2(radius, 0f), color, 1.5f);
            drawList.AddLine(crosshair - new Vector2(0f, radius), crosshair + new Vector2(0f, radius), color, 1.5f);
        }

        ImGui.End();
    }

    public void Dispose()
    {
        _framebuffer.Dispose();
    }
}
