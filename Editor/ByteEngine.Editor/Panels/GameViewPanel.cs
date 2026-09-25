using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Gameplay;
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
        Action<bool> captureInput,
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
            if (Input.IsGameInputCaptured)
            {
                releaseInput();
            }

            Input.SetGameViewPointer(
                Input.GameViewPointerNormalized,
                Input.GameViewSize,
                false,
                false,
                Vector2.Zero);

            ImGui.End();
            return;
        }

        int projectWidth =
            Math.Clamp(
                state.Project.Window.Width,
                1,
                4096);

        int projectHeight =
            Math.Clamp(
                state.Project.Window.Height,
                1,
                4096);

        ImGui.TextDisabled(
            $"Project {projectWidth} x {projectHeight} | " +
            (state.Mode == EditorMode.Edit
                ? "Game camera preview"
                : "Runtime game camera"));

        if (state.Mode == EditorMode.Play)
        {
            ImGui.TextDisabled(
                Input.IsGameInputCaptured
                    ? "Mouse captured — Esc to release"
                    : "Click Game View to capture mouse");
        }

        /*
         * The editor preview now follows the actual Game View panel size.
         * This removes the old 16:9 fit/letterbox behavior that made the
         * preview look smaller and left black borders around it.
         *
         * Project output resolution is still stored separately in
         * state.Project.Window and remains the packaged game's setting.
         */
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

        int renderWidth =
            Math.Clamp(
                (int)MathF.Round(
                    available.X),
                1,
                4096);

        int renderHeight =
            Math.Clamp(
                (int)MathF.Round(
                    available.Y),
                1,
                4096);

        _framebuffer.RenderGame(
            renderer,
            renderer3D,
            state.DisplayedScene,
            state.Mode,
            renderWidth,
            renderHeight,
            windowWidth,
            windowHeight);

        Vector2 imageMin =
            ImGui.GetCursorScreenPos();

        Vector2 displaySize =
            available;

        Vector2 imageMax =
            imageMin +
            displaySize;

        Vector2 mouse =
            ImGui.GetMousePos();

        bool pointerInside =
            mouse.X >= imageMin.X &&
            mouse.X <= imageMax.X &&
            mouse.Y >= imageMin.Y &&
            mouse.Y <= imageMax.Y;

        bool pointerAim = state.DisplayedScene.ActiveCamera?.GameObject.Parent?
            .GetComponent<PlayerShooter3D>()?.AimAtPointer == true;
        if (pointerAim && Input.IsGameInputCaptured && !pointerInside)
            releaseInput();

        if (state.Mode == EditorMode.Play &&
            !Input.IsGameInputCaptured &&
            pointerInside &&
            ImGui.IsMouseClicked(
                ImGuiMouseButton.Left))
        {
            captureInput(!pointerAim);
        }
        else if (state.Mode != EditorMode.Play &&
                 Input.IsGameInputCaptured)
        {
            releaseInput();
        }

        Vector2 normalizedPointer =
            new(
                displaySize.X > 0.0f
                    ? (mouse.X - imageMin.X) /
                      displaySize.X
                    : 0.5f,
                displaySize.Y > 0.0f
                    ? (mouse.Y - imageMin.Y) /
                      displaySize.Y
                    : 0.5f);

        Input.SetGameViewPointer(
            normalizedPointer,
            new Vector2(
                renderWidth,
                renderHeight),
            pointerInside,
            ImGui.IsWindowFocused(
                ImGuiFocusedFlags.RootAndChildWindows),
            mouse -
            imageMin);

        ImGui.Image(
            _framebuffer.TextureId,
            displaySize,
            new Vector2(
                0.0f,
                1.0f),
            new Vector2(
                1.0f,
                0.0f));

        ImDrawListPtr drawList =
            ImGui.GetWindowDrawList();

        if (EditorPreferences.ShowFpsCounter)
        {
            DrawFpsCounter(
                drawList,
                imageMin,
                imageMax);
        }

        if (state.Mode == EditorMode.Play &&
            Input.IsGameInputCaptured)
        {
            const float radius =
                8.0f;

            uint color =
                ImGui.GetColorU32(
                    new Vector4(
                        1.0f,
                        1.0f,
                        1.0f,
                        0.95f));

            Vector2 crosshair =
                imageMin +
                displaySize *
                0.5f;

            drawList.AddLine(
                crosshair -
                new Vector2(
                    radius,
                    0.0f),
                crosshair +
                new Vector2(
                    radius,
                    0.0f),
                color,
                1.5f);

            drawList.AddLine(
                crosshair -
                new Vector2(
                    0.0f,
                    radius),
                crosshair +
                new Vector2(
                    0.0f,
                    radius),
                color,
                1.5f);
        }

        ImGui.End();
    }

    private static void DrawFpsCounter(
        ImDrawListPtr drawList,
        Vector2 imageMin,
        Vector2 imageMax)
    {
        if (imageMax.X <= imageMin.X ||
            imageMax.Y <= imageMin.Y)
        {
            return;
        }

        float fps =
            ImGui.GetIO()
                .Framerate;

        string text =
            fps > 0.0f
                ? $"FPS {fps:0.0}"
                : "FPS --";

        Vector2 textSize =
            ImGui.CalcTextSize(
                text);

        const float horizontalPadding =
            7.0f;

        const float verticalPadding =
            4.0f;

        const float edgeMargin =
            8.0f;

        Vector2 boxMax =
            new(
                imageMax.X -
                edgeMargin,
                imageMin.Y +
                edgeMargin +
                textSize.Y +
                verticalPadding *
                2.0f);

        Vector2 boxMin =
            new(
                boxMax.X -
                textSize.X -
                horizontalPadding *
                2.0f,
                imageMin.Y +
                edgeMargin);

        uint background =
            ImGui.GetColorU32(
                new Vector4(
                    0.03f,
                    0.03f,
                    0.03f,
                    0.78f));

        uint foreground =
            ImGui.GetColorU32(
                new Vector4(
                    0.95f,
                    0.95f,
                    0.95f,
                    1.0f));

        drawList.AddRectFilled(
            boxMin,
            boxMax,
            background,
            3.0f);

        drawList.AddText(
            boxMin +
            new Vector2(
                horizontalPadding,
                verticalPadding),
            foreground,
            text);
    }

    public void Dispose()
    {
        _framebuffer.Dispose();
    }
}
