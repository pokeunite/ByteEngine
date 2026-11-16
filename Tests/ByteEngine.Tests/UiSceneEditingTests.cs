using System.Numerics;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Editor;
using ByteEngine.Editor.Commands;
using ByteEngine.Editor.Panels;

using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace ByteEngine.Tests;

internal static class UiSceneEditingTests
{
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS " + message);
    }

    internal static void Run(string root)
    {
        string output = Path.GetFullPath(".artifacts/ui-scene-editing");
        Directory.CreateDirectory(output);
        using var database = new AssetDatabase(output, []);
        using var assets = new AssetManager(database);
        var serializer = new SceneSerializer(new ComponentSerializer(output, database, assets, loadGraphicsAssets: false));
        var scene = new Scene("UI selection regression");
        var canvas = scene.CreateGameObject("HUD Canvas");
        canvas.AddComponent(new UiCanvas());
        var panel = scene.CreateGameObject("Health panel");
        panel.SetParent(canvas, false);
        var widget = panel.AddComponent(new UiWidget { Offset = new(100, 100), Size = new(300, 120), OrderInLayer = 1 });
        var label = scene.CreateGameObject("Health text");
        label.SetParent(panel, false);
        var text = label.AddComponent(new UiText { Text = "HEALTH", FontSize = 32, Offset = new(20, 20), OrderInLayer = 2 });
        var state = new EditorState { EditorScene = scene, Project = new ProjectData(), ProjectFilePath = Path.Combine(output, "test.byteproject") };
        state.Undo = new UndoManager(serializer, _ => { });
        state.Undo.Reset(state, true);
        Vector2 viewport = new(640, 360);
        Vector2 Measure(string value, string? font, int size) => new(value.Length * size * .5f, size);
        var elements = UiSelectionTool.Elements(scene, viewport, Measure);
        var panelElement = elements.Single(e => e.Object == panel);
        var textElement = elements.Single(e => e.Object == label);
        Check(panelElement.Position == new Vector2(50) && panelElement.Size == new Vector2(150, 60), "Scaled widget bounds match canvas layout");
        Check(textElement.Position == new Vector2(60) && textElement.Size == new Vector2(48, 16), "Text uses its rendered font size and parent bounds");
        Check(elements.Last() == textElement && UiSelectionTool.Contains(textElement, new(70, 65), viewport), "Top layer text is selectable over its panel");
        text.Anchor = UiAnchor.Center;
        text.Offset = Vector2.Zero;
        var centered = text.ResolveEditorBounds(viewport, Measure);
        Check(centered.Position == new Vector2(101, 72), "Centered text bounds follow renderer alignment");
        text.Anchor = UiAnchor.TopLeft;
        text.Offset = new(20);
        panel.AddComponent(new UiScrollContainer { ContentHeight = 400 });
        text.Offset = new(20, 110);
        textElement = UiSelectionTool.Elements(scene, viewport, Measure).Single(e => e.Object == label);
        Check(UiSelectionTool.Contains(textElement, new(65, 108), viewport) && !UiSelectionTool.Contains(textElement, new(65, 115), viewport), "Scroll clipping excludes invisible text pixels");
        text.Offset = new(20);
        widget.Visible = false;
        Check(UiSelectionTool.Elements(scene, viewport, Measure).Count == 0, "Hidden ancestors exclude all child UI");
        widget.Visible = true;
        var tool = new UiSelectionTool();
        state.Selection.Set(panel);
        state.Selection.Toggle(label);
        elements = UiSelectionTool.Elements(scene, viewport, Measure);
        tool.BeginDrag(state, elements, Vector2.Zero);
        tool.Move(state, new(20, 10));
        tool.Finish(state);
        Check(widget.Offset == new Vector2(140, 120) && text.Offset == new Vector2(20), "Multi-selection moves a parent once and preserves child offsets");
        Check(panel.Transform.LocalPosition == Vector3.Zero && state.Undo.UndoName == "Move UI Objects", "Dragging edits UI offsets and records one undo transaction");
        state.Undo.Undo(state);
        panel = state.EditorScene.FindGameObject(panel.Id)!;
        widget = panel.GetComponent<UiWidget>()!;
        Check(widget.Offset == new Vector2(100), "Undo restores the full drag");
        state.Undo.Redo(state);
        panel = state.EditorScene.FindGameObject(panel.Id)!;
        widget = panel.GetComponent<UiWidget>()!;
        Check(widget.Offset == new Vector2(140, 120), "Redo restores the full drag");
        label = state.EditorScene.FindGameObject(label.Id)!;
        text = label.GetComponent<UiText>()!;
        state.Selection.Set(label);
        tool.BeginDrag(state, UiSelectionTool.Elements(state.EditorScene, viewport, Measure), Vector2.Zero);
        tool.Move(state, new(30));
        tool.Finish(state, cancel: true);
        Check(text.Offset == new Vector2(20), "Escape restores the original text offset");
        panel.AddComponent(new UiContainer());
        var child = state.EditorScene.CreateGameObject("Layout child");
        child.SetParent(panel, false);
        child.AddComponent(new UiWidget());
        var managed = UiSelectionTool.Elements(state.EditorScene, viewport, Measure).Single(e => e.Object == child);
        Check(UiSelectionTool.IsLayoutManaged(managed), "Auto-layout children preserve their container's positioning");
        Check(HierarchyObjectIcons.Kind(label) == HierarchyObjectKind.Text && HierarchyObjectIcons.Kind(panel) == HierarchyObjectKind.Panel,
            "Hierarchy distinguishes text and panel icons");
        var mesh = state.EditorScene.CreateGameObject("3D object");
        mesh.AddComponent(new MeshRenderer());
        Check(HierarchyObjectIcons.Kind(mesh) == HierarchyObjectKind.Mesh, "Hierarchy identifies 3D objects");
        NativeMouseAndVisual(output, serializer);
    }

    private static void NativeMouseAndVisual(string output, SceneSerializer serializer)
    {
        using var window = new GameWindow(GameWindowSettings.Default, new NativeWindowSettings
        {
            ClientSize = new(1280, 800), StartVisible = false, API = ContextAPI.OpenGL, APIVersion = new(3, 3), Profile = ContextProfile.Core
        });
        window.Context.MakeCurrent();
        GL.LoadBindings(new OpenTK.Windowing.GraphicsLibraryFramework.GLFWBindingsContext());
        using var gui = new ImGuiController(window, Path.Combine(output, "test-layout.ini"));
        using var renderer = new Renderer2D();
        renderer.Initialize(1280, 800);
        using var renderer3D = new Renderer3D();
        using var framebuffer = new SceneFramebuffer();
        var scene = new Scene("UI editing preview");
        var canvas = scene.CreateGameObject("HUD Canvas");
        canvas.AddComponent(new UiCanvas { ScaleMode = UiScaleMode.ConstantPixels });
        var panel = scene.CreateGameObject("Health panel");
        panel.SetParent(canvas, false);
        panel.AddComponent(new UiWidget { Offset = new(60, 60), Size = new(300, 110), OrderInLayer = 1, Color = new(.12f, .17f, .22f, 1) });
        var label = scene.CreateGameObject("Health text");
        label.SetParent(panel, false);
        var text = label.AddComponent(new UiText { Text = "HEALTH 100 / 100", Offset = new(20, 20), FontSize = 24, OrderInLayer = 2 });
        foreach (var kind in new[] { UiWidgetKind.Button, UiWidgetKind.Image, UiWidgetKind.Slider, UiWidgetKind.ProgressBar })
        {
            var obj = scene.CreateGameObject(kind.ToString());
            obj.SetParent(canvas, false);
            obj.AddComponent(new UiWidget { Kind = kind, Offset = new(60, 220 + (int)kind * 45), Size = new(260, 35), Label = kind.ToString() });
        }
        scene.CreateGameObject("3D Mesh").AddComponent(new MeshRenderer());
        scene.CreateGameObject("Camera").AddComponent(new Camera3D());
        scene.CreateGameObject("Directional light").AddComponent(new DirectionalLight());
        var state = new EditorState { EditorScene = scene, Project = new ProjectData(), ProjectFilePath = Path.Combine(output, "test.byteproject") };
        state.Undo = new UndoManager(serializer, _ => { });
        state.Undo.Reset(state, true);
        var hierarchy = new HierarchyPanel();
        var tool = new UiSelectionTool();
        var io = ImGui.GetIO();
        io.DisplaySize = new(1280, 800);
        io.DeltaTime = 1f / 60;
        Vector2 start = default;
        Vector2 viewport = new(920, 650);
        void Frame(Vector2? mouse = null, bool? down = null, bool alt = false)
        {
            if (mouse is { } p) io.AddMousePosEvent(p.X, p.Y);
            if (down is { } held) io.AddMouseButtonEvent(0, held);
            io.AddKeyEvent(ImGuiKey.ModAlt, alt);
            framebuffer.RenderGame(renderer, renderer3D, scene, EditorMode.Edit, (int)viewport.X, (int)viewport.Y, 1280, 800, uiOnly: true);
            GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
            GL.Viewport(0, 0, 1280, 800);
            GL.ClearColor(.06f, .07f, .09f, 1);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            ImGui.NewFrame();
            ImGui.SetNextWindowPos(Vector2.Zero);
            ImGui.SetNextWindowSize(new(300, 800));
            hierarchy.Draw(state, () => { }, () => { }, () => { }, () => { }, () => { }, _ => { }, (_, _) => { });
            ImGui.SetNextWindowPos(new(305, 0));
            ImGui.SetNextWindowSize(new(975, 800));
            ImGui.Begin("Scene View UI selection");
            ImGui.Text("Click text/widgets. Drag to move. Alt-click cycles overlaps.");
            start = ImGui.GetCursorScreenPos();
            ImGui.Image(framebuffer.TextureId, viewport, new(0, 1), new(1, 0));
            tool.Draw(state, start, viewport, viewport, ImGui.IsItemHovered(), renderer.MeasureText, outlines: true);
            ImGui.End();
            gui.Render();
            GL.Finish();
        }
        Frame();
        Frame();
        Vector2 pointer = start + new Vector2(95, 90);
        Frame(pointer, true);
        Check(state.SelectedObject == label, "Native mouse selects rendered text over a widget");
        Frame(pointer + new Vector2(45, 30));
        Frame(pointer + new Vector2(45, 30), false);
        Check(text.Offset == new Vector2(65, 50), "Native mouse drag moves text in the preview");
        Frame(pointer + new Vector2(45, 30), alt: true);
        Frame(pointer + new Vector2(45, 30), true, alt: true);
        Frame(pointer + new Vector2(45, 30), false, alt: true);
        Check(state.SelectedObject == panel, "Alt-click selects an overlapping parent panel");
        state.Selection.Set(label);
        Frame(start + new Vector2(800, 550));
        Frame(start + new Vector2(800, 550));
        byte[] pixels = new byte[1280 * 800 * 4];
        GL.ReadPixels(0, 0, 1280, 800, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);
        RoadmapUiVisualTests.Png(Path.Combine(output, "selection-and-icons.png"), pixels, 1280, 800);
        Check(GL.GetError() == ErrorCode.NoError, "Actual UI, selection outlines and hierarchy icons render without GL errors");
    }
}

