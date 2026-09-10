using System.Numerics;

using ByteEngine.Core.Graphics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class SceneViewPanel
    : IDisposable
{
    private readonly SceneFramebuffer _framebuffer =
        new();

    private Vector2 _lastViewportSize =
        new(
            800.0f,
            500.0f
        );

    public bool IsOpen { get; set; } =
        true;

    public void FrameSelected(
        EditorState state)
    {
        if (state.SelectedObject == null)
        {
            return;
        }

        state.Camera.Frame(
            state.SelectedObject,
            _lastViewportSize
        );
    }

    public void Draw(
        EditorState state,
        Renderer2D renderer,
        int windowWidth,
        int windowHeight,
        EditorProjectContext project,
        Action<AssetRecord, Vector2> createSprite)
    {
        bool isOpen =
            IsOpen;

        ImGui.Begin(
            "Scene View",
            ref isOpen,
            ImGuiWindowFlags.NoScrollbar |
            ImGuiWindowFlags.NoScrollWithMouse
        );

        IsOpen =
            isOpen;

        DrawToolbar(state);

        Vector2 viewportSize =
            ImGui.GetContentRegionAvail();

        viewportSize.X =
            Math.Max(
                viewportSize.X,
                1.0f
            );

        viewportSize.Y =
            Math.Max(
                viewportSize.Y,
                1.0f
            );

        _lastViewportSize =
            viewportSize;

        _framebuffer.Render(
            renderer,
            state.DisplayedScene,
            state.Mode,
            state.Camera,
            (int)viewportSize.X,
            (int)viewportSize.Y,
            windowWidth,
            windowHeight
        );

        ImGui.Image(
            _framebuffer.TextureId,
            viewportSize,
            new Vector2(
                0.0f,
                1.0f
            ),
            new Vector2(
                1.0f,
                0.0f
            )
        );

        Vector2 imageMinimum =
            ImGui.GetItemRectMin();

        if (ImGui.BeginDragDropTarget())
        {
            Guid? guid = AssetDragDrop.Accept();
            if (guid.HasValue && project.AssetDatabase.TryGetAsset(guid.Value, out AssetRecord? asset) && asset?.Type == AssetType.Texture2D)
            {
                Vector2 world = ScreenToWorld(state.Camera, ImGui.GetMousePos(), imageMinimum, viewportSize);
                createSprite(asset, world);
            }
            ImGui.EndDragDropTarget();
        }

        bool hovered =
            ImGui.IsItemHovered();

        HandleCameraInput(
            state,
            hovered
        );

        HandleSelection(
            state,
            hovered,
            imageMinimum,
            viewportSize
        );

        DrawSelectionOutline(
            state,
            imageMinimum,
            viewportSize
        );

        DrawCameraViewport(state, imageMinimum, viewportSize);

        ImGui.End();
    }

    private void DrawToolbar(
        EditorState state)
    {
        ImGui.TextDisabled(
            "MMB pan | Wheel zoom | F frame selected"
        );

        ImGui.SameLine();

        if (ImGui.SmallButton(
                "Frame Selected"))
        {
            FrameSelected(state);
        }

        ImGui.SameLine();

        if (ImGui.SmallButton(
                "Reset Camera"))
        {
            state.Camera.Reset();
        }

        ImGui.SameLine();

        ImGui.TextColored(
            state.Mode switch
            {
                EditorMode.Play =>
                    new Vector4(
                        0.35f,
                        1.0f,
                        0.45f,
                        1.0f
                    ),
                EditorMode.Paused =>
                    new Vector4(
                        1.0f,
                        0.75f,
                        0.2f,
                        1.0f
                    ),
                _ =>
                    new Vector4(
                        0.4f,
                        0.8f,
                        1.0f,
                        1.0f
                    )
            },
            state.Mode.ToString().ToUpperInvariant()
        );
    }

    private static void HandleCameraInput(
        EditorState state,
        bool hovered)
    {
        if (!hovered)
        {
            return;
        }

        ImGuiIOPtr io =
            ImGui.GetIO();

        if (ImGui.IsMouseDragging(
                ImGuiMouseButton.Middle))
        {
            state.Camera.Position -=
                io.MouseDelta /
                state.Camera.Zoom;
        }

        if (io.MouseWheel != 0.0f)
        {
            state.Camera.Zoom =
                Math.Clamp(
                    state.Camera.Zoom +
                    io.MouseWheel *
                    0.1f,
                    0.2f,
                    4.0f
                );
        }
    }

    private static void HandleSelection(
        EditorState state,
        bool hovered,
        Vector2 imageMinimum,
        Vector2 viewportSize)
    {
        if (!hovered ||
            !ImGui.IsMouseClicked(
                ImGuiMouseButton.Left))
        {
            return;
        }

        Vector2 mouse =
            ImGui.GetMousePos();

        Vector2 worldPosition =
            ScreenToWorld(
                state.Camera,
                mouse,
                imageMinimum,
                viewportSize
            );

        GameObject? selected =
            null;

        IReadOnlyList<GameObject> gameObjects =
            state.DisplayedScene.GameObjects;

        for (int i = gameObjects.Count - 1;
             i >= 0;
             i--)
        {
            if (!gameObjects[i].Active)
            {
                continue;
            }

            if (ContainsPoint(
                    gameObjects[i],
                    worldPosition))
            {
                selected =
                    gameObjects[i];

                break;
            }
        }

        state.SelectedObject =
            selected;

        if (selected != null)
        {
            state.SelectedAssetId = null;
            state.SelectedAssetPath = null;
        }
    }

    private static void DrawCameraViewport(EditorState state, Vector2 imageMinimum, Vector2 viewportSize)
    {
        GameObject? selected = state.SelectedObject;
        Camera2D? camera = selected?.GetComponent<Camera2D>();
        if (selected == null || camera == null) return;

        Vector2 half = new(
            state.Project.Window.Width / Math.Max(camera.Zoom, .01f) / 2f,
            state.Project.Window.Height / Math.Max(camera.Zoom, .01f) / 2f);
        Vector2 min = WorldToScreen(state.Camera, selected.Transform.Position - half, imageMinimum, viewportSize);
        Vector2 max = WorldToScreen(state.Camera, selected.Transform.Position + half, imageMinimum, viewportSize);
        ImGui.GetWindowDrawList().AddRect(min, max, ImGui.GetColorU32(new Vector4(.25f, .75f, 1f, 1f)), 0f, ImDrawFlags.None, 2f);
    }

    private static void DrawSelectionOutline(
        EditorState state,
        Vector2 imageMinimum,
        Vector2 viewportSize)
    {
        GameObject? selected =
            state.SelectedObject;

        if (selected == null ||
            !selected.Active)
        {
            return;
        }

        Transform2D transform =
            selected.Transform;

        Vector2 halfSize =
            transform.Size *
            0.5f;

        float radians =
            transform.Rotation *
            MathF.PI /
            180.0f;

        Vector2[] localCorners =
        {
            new(
                -halfSize.X,
                -halfSize.Y
            ),
            new(
                halfSize.X,
                -halfSize.Y
            ),
            new(
                halfSize.X,
                halfSize.Y
            ),
            new(
                -halfSize.X,
                halfSize.Y
            )
        };

        Vector2[] screenCorners =
            new Vector2[4];

        for (int i = 0;
             i < localCorners.Length;
             i++)
        {
            Vector2 world =
                Rotate(
                    localCorners[i],
                    radians
                ) +
                transform.Position;

            screenCorners[i] =
                WorldToScreen(
                    state.Camera,
                    world,
                    imageMinimum,
                    viewportSize
                );
        }

        ImGui.GetWindowDrawList()
            .AddQuad(
                screenCorners[0],
                screenCorners[1],
                screenCorners[2],
                screenCorners[3],
                ImGui.GetColorU32(
                    new Vector4(
                        1.0f,
                        0.82f,
                        0.22f,
                        1.0f
                    )
                ),
                3.0f
            );
    }

    private static bool ContainsPoint(
        GameObject gameObject,
        Vector2 worldPoint)
    {
        Transform2D transform =
            gameObject.Transform;

        Vector2 offset =
            worldPoint -
            transform.Position;

        float inverseRadians =
            -transform.Rotation *
            MathF.PI /
            180.0f;

        Vector2 local =
            Rotate(
                offset,
                inverseRadians
            );

        Vector2 halfSize =
            transform.Size *
            0.5f;

        return MathF.Abs(local.X) <=
                   halfSize.X &&
               MathF.Abs(local.Y) <=
                   halfSize.Y;
    }

    private static Vector2 ScreenToWorld(
        EditorCamera camera,
        Vector2 screen,
        Vector2 imageMinimum,
        Vector2 viewportSize)
    {
        Vector2 local =
            screen -
            imageMinimum -
            viewportSize *
            0.5f;

        return camera.Position +
               local /
               camera.Zoom;
    }

    private static Vector2 WorldToScreen(
        EditorCamera camera,
        Vector2 world,
        Vector2 imageMinimum,
        Vector2 viewportSize)
    {
        return imageMinimum +
               viewportSize *
               0.5f +
               (
                   world -
                   camera.Position
               ) *
               camera.Zoom;
    }

    private static Vector2 Rotate(
        Vector2 value,
        float radians)
    {
        float cosine =
            MathF.Cos(radians);

        float sine =
            MathF.Sin(radians);

        return new Vector2(
            value.X * cosine -
            value.Y * sine,
            value.X * sine +
            value.Y * cosine
        );
    }

    public void Dispose()
    {
        _framebuffer.Dispose();
    }
}
