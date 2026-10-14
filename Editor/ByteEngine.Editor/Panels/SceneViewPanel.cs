using System.Numerics;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

using ByteEngine.Editor.Gizmos;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class SceneViewPanel : IDisposable
{
    private readonly SceneFramebuffer _framebuffer =
        new();

    private readonly GizmoController _gizmo =
        new();

    private readonly Gizmo3DController _gizmo3D =
        new();

    private readonly FoliagePaintTool _foliagePaint = new();
    private readonly TerrainSculptTool _terrainSculpt = new();
    private bool _is3D =
        true;

    private int _colliderDisplay;
    private bool _liveDuringPlay;
    private Scene? _renderedScene;
    private Matrix4x4 _renderedCamera;
    private Vector2 _renderedSize;
    private float _renderedFieldOfView;

    private Vector2 _lastViewportSize =
        new(
            800.0f,
            500.0f
        );

    private Vector3 _contextWorld;

    private bool _focusRequested;

    private bool _rightMouseStartedInViewport;

    private float _rightMouseTravel;

    public bool IsOpen { get; set; } =
        true;

    public bool IsFocused { get; private set; }

    public void RequestFocus()
    {
        IsOpen = true;
        _focusRequested = true;
    }

    public void FrameSelected(
        EditorState state)
    {
        if (state.SelectedObject ==
            null)
        {
            return;
        }

        if (_is3D)
        {
            GameObject selected = state.DisplayedScene.FindGameObject(state.SelectedObject.Id)
                ?? state.SelectedObject;
            Vector3 minimum = new(float.PositiveInfinity);
            Vector3 maximum = new(float.NegativeInfinity);
            bool foundPose = false;
            if (selected.GetComponent<FoliagePatch>() is { } foliage)
            {
                state.Camera3D.Frame(foliage.GetWorldBounds());
                return;
            }
            if (selected.Components.OfType<HeightfieldCollider3D>().FirstOrDefault() is {} terrain)
            {
                state.Camera3D.Frame(terrain.WorldTerrainBounds);
                return;
            }
            foreach (GameObject item in state.DisplayedScene.GameObjects)
            {
                if (item.GetComponent<ModelHierarchyInstance>() == null ||
                    !ReferenceEquals(item, selected) &&
                    !item.IsDescendantOf(selected) &&
                    !selected.IsDescendantOf(item) ||
                    !_framebuffer.TryGetEditorModelWorldBounds(state.DisplayedScene, item,
                        out BoundingBox3D bounds))
                    continue;
                minimum = Vector3.Min(minimum, bounds.Minimum);
                maximum = Vector3.Max(maximum, bounds.Maximum);
                foundPose = true;
            }
            if (foundPose)
                state.Camera3D.Frame(new BoundingBox3D(minimum, maximum));
            else
                state.Camera3D.Frame(selected);
        }
        else
        {
            state.Camera.Frame(
                state.SelectedObject,
                _lastViewportSize
            );
        }
    }

    public void Draw(
        EditorState state,
        Renderer2D renderer,
        Renderer3D renderer3D,
        int windowWidth,
        int windowHeight,
        EditorProjectContext project,
        Action<AssetRecord, Vector2> createSpriteFromAsset,
        Action<AssetRecord, Vector3> createModelFromAsset,
        Action<AssetRecord, Vector3> createBlueprintFromAsset,
        Action<Vector3> createEmpty,
        Action<Vector3> createSprite,
        Action<Vector3> createCamera,
        Action paste,
        bool gameViewVisible)
    {
        if (!EditorPreferences.Enable2DEditor)
        {
            _is3D =
                true;
        }

        bool isOpen =
            IsOpen;

        if (_focusRequested)
        {
            ImGui.SetNextWindowFocus();
            _focusRequested = false;
        }

        bool visible =
            ImGui.Begin(
                "Scene View",
                ref isOpen,
                ImGuiWindowFlags.NoScrollbar |
                ImGuiWindowFlags.NoScrollWithMouse |
                (_gizmo3D.OwnsMouse ? ImGuiWindowFlags.NoMove : ImGuiWindowFlags.None)
            );

        IsOpen =
            isOpen;

        IsFocused = ImGui.IsWindowFocused(ImGuiFocusedFlags.RootAndChildWindows);

        /*
         * Remember the dock node used by Scene View.
         *
         * Event Modules, Blueprint editors and future document
         * editors can open into this same dock node and become
         * tabs beside Scene View.
         */
        uint dockId =
            ImGui.GetWindowDockID();

        if (dockId !=
            0)
        {
            EditorWorkspaceDocking.SceneDocumentDockId =
                dockId;
        }

        if (!visible)
        {
            _terrainSculpt.EndStroke(state);
            ImGui.End();

            return;
        }

        if (_is3D)
            _gizmo3D.HandleShortcuts(IsFocused && state.Mode == EditorMode.Edit);
        else
            _gizmo.HandleShortcuts(IsFocused, state.Mode == EditorMode.Edit);

        DrawToolbar(
            state
        );

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

        // Play keeps the secondary viewport on demand. Navigation/resize still refreshes it;
        // continuous simulation inspection is an explicit toolbar option.
        bool refreshScene = state.Mode != EditorMode.Play || !gameViewVisible || _liveDuringPlay || !_is3D ||
            _framebuffer.TextureId == IntPtr.Zero || !ReferenceEquals(_renderedScene, state.DisplayedScene) ||
            _renderedCamera != state.Camera3D.View || _renderedSize != viewportSize ||
            _renderedFieldOfView != state.Camera3D.FieldOfView;
        if (refreshScene)
        {
        _framebuffer.Render(
            renderer,
            renderer3D,
            state.DisplayedScene,
            state.Mode,
            state.Camera,
            state.Camera3D,
            _is3D,
            (int)viewportSize.X,
            (int)viewportSize.Y,
            windowWidth,
            windowHeight
        );
            _renderedScene = state.DisplayedScene;
            _renderedCamera = state.Camera3D.View;
            _renderedSize = viewportSize;
            _renderedFieldOfView = state.Camera3D.FieldOfView;
        }

        if (RuntimeDiagnostics.DebugBlueprintVisibility)
            BlueprintVisibilityTrace.Sample(state, viewportSize, _is3D, _framebuffer);

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

        Vector2 minimum =
            ImGui.GetItemRectMin();

        if (_is3D)
        {
            CameraRigGizmoRenderer.Draw(state.DisplayedScene, state.Camera3D, minimum, viewportSize);
        }

        bool hovered =
            ImGui.IsItemHovered();

        ImGuiIOPtr sceneIo =
            ImGui.GetIO();

        if (hovered &&
            ImGui.IsMouseClicked(
                ImGuiMouseButton.Right))
        {
            _rightMouseStartedInViewport =
                true;

            _rightMouseTravel =
                0.0f;
        }

        if (_rightMouseStartedInViewport &&
            ImGui.IsMouseDown(
                ImGuiMouseButton.Right))
        {
            _rightMouseTravel +=
                sceneIo.MouseDelta.Length();

            if (ImGui.IsMouseDragging(
                    ImGuiMouseButton.Right) ||
                ImGui.IsKeyDown(ImGuiKey.W) ||
                ImGui.IsKeyDown(ImGuiKey.A) ||
                ImGui.IsKeyDown(ImGuiKey.S) ||
                ImGui.IsKeyDown(ImGuiKey.D) ||
                ImGui.IsKeyDown(ImGuiKey.Q) ||
                ImGui.IsKeyDown(ImGuiKey.E))
            {
                _rightMouseTravel =
                    Math.Max(
                        _rightMouseTravel,
                        3.0f);
            }
        }

        if (ImGui.BeginDragDropTarget())
        {
            Guid? guid =
                AssetDragDrop.Accept();

            if (guid.HasValue &&
                project.AssetDatabase.TryGetAsset(
                    guid.Value,
                    out AssetRecord? asset) &&
                asset != null)
            {
                if (!_is3D &&
                    asset.Type ==
                    AssetType.Texture2D)
                {
                    createSpriteFromAsset(
                        asset,
                        GizmoController.ScreenToWorld(
                            state.Camera,
                            ImGui.GetMousePos(),
                            minimum,
                            viewportSize
                        )
                    );
                }
                else if (asset.Type == AssetType.VfxEffect || asset.Type ==
                         AssetType.Model3D)
                {
                    // A model drop is an explicit request for 3D authoring.
                    // Do not silently discard it just because this tab was
                    // last left in 2D mode.
                    _is3D = true;

                    createModelFromAsset(
                        asset,
                        Gizmo3DController.ScreenToGroundPlane(
                            ImGui.GetMousePos(),
                            state.Camera3D,
                            minimum,
                            viewportSize
                        )
                    );
                }
                else if (_is3D &&
                         asset.Type ==
                         AssetType.Blueprint)
                {
                    createBlueprintFromAsset(
                        asset,
                        Gizmo3DController.ScreenToGroundPlane(
                            ImGui.GetMousePos(),
                            state.Camera3D,
                            minimum,
                            viewportSize
                        )
                    );
                }
            }

            ImGui.EndDragDropTarget();
        }

        if (_is3D)
        {
            DrawColliderOutlines(
                state,
                minimum,
                viewportSize
            );

            if (!_terrainSculpt.Update(state, hovered, minimum, viewportSize) && !_foliagePaint.Update(state, hovered, minimum, viewportSize)) _gizmo3D.UpdateAndDraw(
                state,
                state.Camera3D,
                hovered,
                minimum,
                viewportSize
            );
            HandleCamera3DInput(state, hovered && !_gizmo3D.OwnsMouse);
        }
        else
        {
            HandleCameraInput(
                state,
                hovered
            );

            _gizmo.Update(
                state,
                hovered,
                minimum,
                viewportSize
            );

            _gizmo.Draw(
                state,
                minimum,
                viewportSize
            );

            DrawCameraViewport(
                state,
                minimum,
                viewportSize
            );
        }

        bool rightMouseReleased =
            ImGui.IsMouseReleased(
                ImGuiMouseButton.Right);

        bool openContextMenu =
            hovered &&
            _rightMouseStartedInViewport &&
            rightMouseReleased &&
            _rightMouseTravel <
                3.0f;

        if (openContextMenu)
        {
            _contextWorld =
                _is3D
                    ? Gizmo3DController.ScreenToGroundPlane(
                        ImGui.GetMousePos(),
                        state.Camera3D,
                        minimum,
                        viewportSize
                    )
                    : new Vector3(
                        GizmoController.ScreenToWorld(
                            state.Camera,
                            ImGui.GetMousePos(),
                            minimum,
                            viewportSize
                        ),
                        0.0f
                    );

            ImGui.OpenPopup(
                "Scene View Context"
            );
        }

        if (rightMouseReleased)
        {
            _rightMouseStartedInViewport =
                false;

            _rightMouseTravel =
                0.0f;
        }

        if (ImGui.BeginPopup(
                "Scene View Context"))
        {
            bool editable =
                state.Mode ==
                EditorMode.Edit;

            if (ImGui.MenuItem(
                    "Create Empty",
                    string.Empty,
                    false,
                    editable))
            {
                createEmpty(
                    _contextWorld
                );
            }

            if (EditorPreferences.Enable2DEditor &&
                !_is3D)
            {
                if (ImGui.MenuItem(
                        "Create Sprite",
                        string.Empty,
                        false,
                        editable))
                {
                    createSprite(
                        _contextWorld
                    );
                }

                if (ImGui.MenuItem(
                        "Create Camera 2D",
                        string.Empty,
                        false,
                        editable))
                {
                    createCamera(
                        _contextWorld
                    );
                }
            }

            ImGui.Separator();

            if (ImGui.MenuItem(
                    "Paste",
                    "Ctrl+V",
                    false,
                    editable))
            {
                paste();
            }

            ImGui.EndPopup();
        }

        ImGui.End();
    }

    private void DrawToolbar(
        EditorState state)
    {
        EditorUi.BeginToolbar("##SceneToolbar");

        if (EditorPreferences.Enable2DEditor)
        {
            if (EditorUi.ToolbarToggle("2D", !_is3D, "Switch to 2D scene editing")) _is3D = false;
            ImGui.SameLine();
            if (EditorUi.ToolbarToggle("3D", _is3D, "Switch to 3D scene editing")) _is3D = true;
            EditorUi.ToolbarSeparator();
        }
        else
        {
            EditorUi.StatusBadge("3D");
            EditorUi.ToolbarSeparator();
        }

        if (!_is3D) _gizmo.DrawToolbar(state);
        else _gizmo3D.DrawToolbar();
        _foliagePaint.DrawToolbar(state);
        if (_is3D) _terrainSculpt.DrawToolbar(state);

        EditorUi.ToolbarSeparator();
        if (EditorUi.ToolbarButton("Frame", "Frame Selected (F)")) FrameSelected(state);
        ImGui.SameLine();
        if (EditorUi.ToolbarButton("Reset Camera", "Reset the Scene camera"))
        {
            if (_is3D) state.Camera3D.Reset();
            else state.Camera.Reset();
        }

        ImGui.SameLine(Math.Max(ImGui.GetWindowWidth() - 92.0f, ImGui.GetCursorPosX()));
        EditorUi.StatusBadge(state.Mode.ToString().ToUpperInvariant(), state.Mode switch
        {
            EditorMode.Play => EditorStatusKind.Success,
            EditorMode.Paused => EditorStatusKind.Warning,
            _ => EditorStatusKind.Neutral
        });

        EditorUi.EndToolbar();
        if (_is3D)
        {
            ImGui.SetNextItemWidth(125);
            ImGui.Combo("Colliders", ref _colliderDisplay, "Hidden\0Selected\0All\0");
            if (ImGui.IsItemHovered()) ImGui.SetTooltip("Collision outlines for debugging. Hidden avoids the scene-wide overlay.");
            if (state.Mode == EditorMode.Play)
            {
                ImGui.SameLine();
                ImGui.Checkbox("Live Scene", ref _liveDuringPlay);
                if (ImGui.IsItemHovered()) ImGui.SetTooltip("When Game View is visible, continuously render this second viewport too. Otherwise refresh it on navigation or resize. Scene View always stays live when it is the only visible viewport.");
            }
        }
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

        if (io.MouseWheel !=
            0.0f)
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

    private static void HandleCamera3DInput(
        EditorState state,
        bool hovered)
    {
        if (!hovered)
        {
            return;
        }

        ImGuiIOPtr io =
            ImGui.GetIO();

        EditorCamera3D camera =
            state.Camera3D;

        if (ImGui.IsMouseDragging(
                ImGuiMouseButton.Right))
        {
            camera.Yaw +=
                io.MouseDelta.X *
                0.18f;

            camera.Pitch =
                Math.Clamp(
                    camera.Pitch -
                    io.MouseDelta.Y *
                    0.18f,
                    -89.0f,
                    89.0f
                );
        }

        float speed =
            (io.KeyShift
                ? 12.0f
                : 5.0f) *
            io.DeltaTime;

        if (ImGui.IsMouseDown(
                ImGuiMouseButton.Right))
        {
            if (ImGui.IsKeyDown(
                    ImGuiKey.W))
            {
                camera.Position +=
                    camera.Forward *
                    speed;
            }

            if (ImGui.IsKeyDown(
                    ImGuiKey.S))
            {
                camera.Position -=
                    camera.Forward *
                    speed;
            }

            if (ImGui.IsKeyDown(
                    ImGuiKey.D))
            {
                camera.Position +=
                    camera.Right *
                    speed;
            }

            if (ImGui.IsKeyDown(
                    ImGuiKey.A))
            {
                camera.Position -=
                    camera.Right *
                    speed;
            }

            if (ImGui.IsKeyDown(
                    ImGuiKey.E))
            {
                camera.Position +=
                    Vector3.UnitY *
                    speed;
            }

            if (ImGui.IsKeyDown(
                    ImGuiKey.Q))
            {
                camera.Position -=
                    Vector3.UnitY *
                    speed;
            }
        }

        if (ImGui.IsMouseDragging(
                ImGuiMouseButton.Middle))
        {
            camera.Position +=
                (
                    -camera.Right *
                    io.MouseDelta.X +
                    Vector3.UnitY *
                    io.MouseDelta.Y
                ) *
                speed *
                0.12f;
        }

        if (io.MouseWheel !=
            0.0f)
        {
            Vector3 focus =
                state.SelectedObject?
                    .Transform
                    .WorldPosition ??
                Vector3.Zero;

            float zoomDistance =
                Math.Max(
                    Vector3.Distance(
                        camera.Position,
                        focus
                    ) *
                    0.12f,
                    0.35f
                );

            camera.Position +=
                camera.Forward *
                io.MouseWheel *
                zoomDistance;
        }

        if (ImGui.IsKeyPressed(
                ImGuiKey.F) &&
            state.SelectedObject !=
            null)
        {
            camera.Frame(
                state.SelectedObject
            );
        }
    }

    private void DrawColliderOutlines(EditorState state, Vector2 minimum, Vector2 size)
    {
        if (_colliderDisplay == 0) return;
        var drawList = ImGui.GetWindowDrawList();
        var selected = state.SelectedObject == null ? null :
            state.DisplayedScene.FindGameObject(state.SelectedObject.Id);
        var frustum = new Frustum3D(state.Camera3D.View * state.Camera3D.Projection(size.X / size.Y));
        void DrawObject(GameObject gameObject)
        {
            if (!gameObject.ActiveInHierarchy) return;
            foreach (var component in gameObject.Components)
            {
                if (component is not Collider3D collider || !collider.Enabled) continue;
                var bounds = new BoundingBox3D(collider.Center - collider.Size * .5f,
                    collider.Center + collider.Size * .5f).Transform(gameObject.Transform.WorldMatrix);
                if (!frustum.Intersects(bounds)) continue;
                uint color = ImGui.GetColorU32(ReferenceEquals(selected, gameObject)
                    ? new Vector4(1, .75f, .15f, 1) : new Vector4(.25f, .9f, .45f, .75f));
                if (collider is CapsuleCollider3D capsule)
                    DrawCapsule(drawList, gameObject, capsule, state.Camera3D, minimum, size, color);
                else
                    DrawBox(drawList, gameObject, collider, state.Camera3D, minimum, size, color);
            }
        }
        drawList.PushClipRect(minimum, minimum + size, true);
        try
        {
            if (_colliderDisplay == 1)
            {
                if (selected != null) DrawObject(selected);
            }
            else
                foreach (var gameObject in state.DisplayedScene.GameObjects) DrawObject(gameObject);
        }
        finally { drawList.PopClipRect(); }
    }

    private static void DrawBox(ImDrawListPtr drawList, GameObject gameObject, Collider3D collider,
        EditorCamera3D camera, Vector2 minimum, Vector2 size, uint color)
    {
        var half = collider.Size * .5f;
        var matrix = gameObject.Transform.WorldMatrix * camera.View * camera.Projection(size.X / size.Y);
        Span<Vector4> corners = stackalloc Vector4[8];
        for (int i = 0; i < 8; i++)
        {
            var local = collider.Center + new Vector3((i & 1) == 0 ? -half.X : half.X,
                (i & 2) == 0 ? -half.Y : half.Y, (i & 4) == 0 ? -half.Z : half.Z);
            corners[i] = Vector4.Transform(new Vector4(local, 1), matrix);
        }
        for (int i = 0; i < 8; i++)
            for (int bit = 1; bit <= 4; bit *= 2)
            {
                if ((i & bit) != 0) continue;
                var a = corners[i]; var b = corners[i | bit];
                // Clip against the near plane before division. Boxes behind the camera
                // must not produce mirrored, screen-spanning diagonal lines.
                if (a.Z < 0 && b.Z < 0) continue;
                if (a.Z < 0) a = Vector4.Lerp(a, b, -a.Z / (b.Z - a.Z));
                if (b.Z < 0) b = Vector4.Lerp(b, a, -b.Z / (a.Z - b.Z));
                if (a.W <= .0001f || b.W <= .0001f) continue;
                var pa = minimum + new Vector2((a.X / a.W + 1) * .5f * size.X, (1 - a.Y / a.W) * .5f * size.Y);
                var pb = minimum + new Vector2((b.X / b.W + 1) * .5f * size.X, (1 - b.Y / b.W) * .5f * size.Y);
                drawList.AddLine(pa, pb, color, 2);
            }
    }

    private static void DrawCapsule(
        ImDrawListPtr drawList,
        GameObject gameObject,
        CapsuleCollider3D capsule,
        EditorCamera3D camera,
        Vector2 minimum,
        Vector2 size,
        uint color)
    {
        float halfLine =
            Math.Max(
                capsule.Height *
                0.5f -
                capsule.Radius,
                0.0f
            );

        const int segments =
            24;

        for (int plane = 0;
             plane < 2;
             plane++)
        {
            Vector2? previous =
                null;

            Vector2 first =
                default;

            for (int index = 0;
                 index <= segments;
                 index++)
            {
                float angle =
                    index *
                    MathF.Tau /
                    segments;

                Vector3 local =
                    plane ==
                    0
                        ? capsule.Center +
                          new Vector3(
                              MathF.Cos(angle) *
                              capsule.Radius,
                              halfLine,
                              MathF.Sin(angle) *
                              capsule.Radius
                          )
                        : capsule.Center +
                          new Vector3(
                              MathF.Cos(angle) *
                              capsule.Radius,
                              -halfLine,
                              MathF.Sin(angle) *
                              capsule.Radius
                          );

                Vector2 point =
                    Gizmo3DController.Project(
                        Vector3.Transform(
                            local,
                            gameObject.Transform.WorldMatrix
                        ),
                        camera,
                        minimum,
                        size
                    );

                if (index ==
                    0)
                {
                    first =
                        point;
                }

                if (previous.HasValue)
                {
                    drawList.AddLine(
                        previous.Value,
                        point,
                        color,
                        2.0f
                    );
                }

                previous =
                    point;
            }

            if (previous.HasValue)
            {
                drawList.AddLine(
                    previous.Value,
                    first,
                    color,
                    2.0f
                );
            }
        }

        foreach (Vector3 axis
                 in new[]
                 {
                     Vector3.UnitX,
                     -Vector3.UnitX,
                     Vector3.UnitZ,
                     -Vector3.UnitZ
                 })
        {
            Vector3 bottom =
                capsule.Center +
                axis *
                capsule.Radius -
                Vector3.UnitY *
                halfLine;

            Vector3 top =
                capsule.Center +
                axis *
                capsule.Radius +
                Vector3.UnitY *
                halfLine;

            drawList.AddLine(
                Gizmo3DController.Project(
                    Vector3.Transform(
                        bottom,
                        gameObject.Transform.WorldMatrix
                    ),
                    camera,
                    minimum,
                    size
                ),

                Gizmo3DController.Project(
                    Vector3.Transform(
                        top,
                        gameObject.Transform.WorldMatrix
                    ),
                    camera,
                    minimum,
                    size
                ),

                color,
                2.0f
            );
        }
    }

    private static void DrawCameraViewport(
        EditorState state,
        Vector2 minimum,
        Vector2 viewportSize)
    {
        GameObject? selected =
            state.SelectedObject;

        Camera2D? camera =
            selected?
                .GetComponent<Camera2D>();

        if (selected ==
                null ||
            camera ==
                null)
        {
            return;
        }

        Vector2 half =
            new(
                state.Project.Window.Width /
                Math.Max(
                    camera.Zoom,
                    0.01f
                ) /
                2.0f,

                state.Project.Window.Height /
                Math.Max(
                    camera.Zoom,
                    0.01f
                ) /
                2.0f
            );

        Vector2 min =
            GizmoController.WorldToScreen(
                state.Camera,
                selected.Transform.Position -
                half,
                minimum,
                viewportSize
            );

        Vector2 max =
            GizmoController.WorldToScreen(
                state.Camera,
                selected.Transform.Position +
                half,
                minimum,
                viewportSize
            );

        ImGui.GetWindowDrawList()
            .AddRect(
                min,
                max,
                ImGui.GetColorU32(
                    new Vector4(
                        0.25f,
                        0.75f,
                        1.0f,
                        1.0f
                    )
                ),
                0.0f,
                ImDrawFlags.None,
                2.0f
            );
    }

    public void Dispose()
    {
        _framebuffer.Dispose();
    }
}
