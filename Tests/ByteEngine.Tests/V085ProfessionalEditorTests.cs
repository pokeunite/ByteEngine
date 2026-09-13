using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Editor;
using ByteEngine.Editor.Commands;
using ByteEngine.Editor.Gizmos;
using ByteEngine.Editor.Panels;
using ImGuiNET;

namespace ByteEngine.Tests;

internal static class V085ProfessionalEditorTests
{
    public static void Run(string root)
    {
        TestGizmos();
        TestIdleDebounce();
        TestStableDocumentIdentity();
        TestPropertyParity();
        using var project = EditorProjectContext.Create(Path.Combine(root, "B5Project.byteproject"), _ => { });
        TestRuntimeIsolation(project);
        TestSingleDragUndo(project);
        TestBlueprintEviction(project);
    }

    private static void TestGizmos()
    {
        var gizmo = new Gizmo3DController();
        gizmo.ApplyShortcuts(true, false, false);
        Assert(gizmo.Mode == Gizmo3DMode.Move, "W selects Move");
        gizmo.ApplyShortcuts(false, true, false);
        Assert(gizmo.Mode == Gizmo3DMode.Rotate, "E selects Rotate");
        gizmo.ApplyShortcuts(false, false, true);
        Assert(gizmo.Mode == Gizmo3DMode.Scale, "R selects Scale");
        Assert(TransformGizmoInteraction.CanUseShortcuts(true, false, false, false), "Focused viewport accepts keys");
        Assert(!TransformGizmoInteraction.CanUseShortcuts(true, true, false, false), "Text input owns keyboard");
        Assert(!TransformGizmoInteraction.CanUseShortcuts(true, false, true, false), "Numeric active item owns keyboard");
        Assert(!TransformGizmoInteraction.CanUseShortcuts(true, false, false, true), "Captured gameplay owns keyboard");
        Assert(!TransformGizmoInteraction.CanUseShortcuts(false, false, false, false), "Unfocused viewport ignores keys");
        var scene = new Scene("Gizmo math");
        var item = scene.CreateGameObject("Object");
        Gizmo3DController.ApplyDragDelta(item, Gizmo3DMode.Move, Vector3.UnitX, 20, 2, Vector3.One, Vector3.Zero, Vector3.One);
        Assert(Near(item.Transform.WorldPosition, new Vector3(3, 1, 1)), "Translate X changes only X");
        Gizmo3DController.ApplyDragDelta(item, Gizmo3DMode.Rotate, Vector3.UnitY, 60, 0, Vector3.Zero, Vector3.Zero, Vector3.One);
        Assert(Near(item.Transform.EulerAngles, new Vector3(0, 30, 0)), "Rotate Y changes only yaw");
        Assert(Near(TransformGizmoInteraction.Scale(Vector3.One, 2, .5f), new Vector3(1, 1, 1.5f)), "Scale Z changes only Z");
        Assert(Near(TransformGizmoInteraction.Scale(new Vector3(1, 2, 3), 3, 1), new Vector3(2, 4, 6)), "Uniform scale preserves proportions");
        Quaternion rotation = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI / 2);
        Assert(Near(TransformGizmoInteraction.Axis(0, GizmoOrientation.World, rotation), Vector3.UnitX), "World orientation ignores rotation");
        Assert(Near(TransformGizmoInteraction.Axis(0, GizmoOrientation.Local, rotation), -Vector3.UnitZ), "Local orientation follows object rotation");
        Assert(MathF.Abs(TransformGizmoInteraction.SignedAngle(Vector3.UnitZ, Vector3.UnitX, Vector3.UnitY) - MathF.PI / 2) < .0001f,
            "Ring drag computes signed axis angle");
    }

    private static void TestIdleDebounce()
    {
        var idle = new EditorIdleDebounce();
        Assert(!idle.Ready(0, true, true) && !idle.Ready(1, true, true), "Editing blocks autosave across frames");
        Assert(!idle.Ready(2, true, false) && !idle.Ready(2.49, true, false), "Release starts idle debounce");
        Assert(idle.Ready(2.51, true, false), "Autosave allowed after idle");
        Assert(!idle.Ready(3, true, true) && !idle.Ready(4, true, false), "New edit resets idle period");
        var popup = new EventNamePopupState();
        popup.BeginRename(Guid.NewGuid(), "Event");
        popup.ConsumeOpenRequest();
        popup.MarkVisible();
        foreach (char c in "Player Death")
        {
            popup.Buffer += c;
            Assert(!idle.Ready(10, true, popup.IsOpen) && popup.IsVisible, "Rename stays open while dirty and blocks autosave");
        }
    }

    private static void TestStableDocumentIdentity()
    {
        nint previous = ImGui.GetCurrentContext();
        nint context = ImGui.CreateContext();
        try
        {
            ImGui.SetCurrentContext(context);
            // ImGui hashes ### suffixes independently of display names.
            ImGui.GetIO().DisplaySize = new Vector2(800, 600);
            ImGui.GetIO().DeltaTime = 1f / 60;
            ImGui.GetIO().Fonts.GetTexDataAsRGBA32(out nint pixels, out int width, out int height);
            ImGui.NewFrame();
            ImGui.Begin("Identity Test");
            Assert(ImGui.GetID("Event###EventWorkspace:1") == ImGui.GetID("Event *###EventWorkspace:1"),
                "Dirty transition preserves document identity");
            Assert(ImGui.GetID("Old###EventWorkspace:1") == ImGui.GetID("New###EventWorkspace:1"),
                "Rename preserves document identity");
            ImGui.End();
            ImGui.EndFrame();
        }
        finally { ImGui.DestroyContext(context); ImGui.SetCurrentContext(previous); }
    }

    private static void TestPropertyParity()
    {
        foreach (Type type in ComponentMetadataRegistry.RegisteredTypes)
        {
            var scene = ComponentPropertyRenderer.Descriptors(type, PropertyEditorContext.Scene);
            Assert(ReferenceEquals(scene, ComponentPropertyRenderer.Descriptors(type, PropertyEditorContext.Blueprint)) &&
                ReferenceEquals(scene, ComponentPropertyRenderer.Descriptors(type, PropertyEditorContext.Runtime)),
                $"{type.Name} uses one descriptor source");
        }
        var boom = ComponentPropertyRenderer.Descriptors(typeof(CameraBoom3D), PropertyEditorContext.Scene);
        Assert(boom.Any(p => p.Property.Name == nameof(CameraBoom3D.CameraObjectId)) &&
            boom.Any(p => p.Property.Name == nameof(CameraBoom3D.ActualArmLength) && p.ReadOnly),
            "Camera references and runtime debug values have descriptors");
        var controller = new CharacterController3D();
        var velocity = ComponentPropertyRenderer.Descriptors(controller.GetType(), PropertyEditorContext.Runtime)
            .Single(p => p.Property.Name == nameof(CharacterController3D.Velocity));
        Assert(!ComponentPropertyRenderer.SetValue(controller, velocity, Vector3.One, PropertyEditorContext.Runtime),
            "Private runtime setters remain read only");
    }

    private static void TestRuntimeIsolation(EditorProjectContext project)
    {
        var edit = new Scene("Edit");
        var player = edit.CreateGameObject("Player");
        player.AddComponent(new CharacterController3D { MoveSpeed = 5 });
        player.AddComponent(new CameraBoom3D { ArmLength = 5 });
        var link = player.AddComponent(new BlueprintInstance { SourceSnapshot = "original" });
        var runtime = project.Scenes.CloneForRuntime(edit);
        var runtimePlayer = runtime.FindGameObject(player.Id)!;
        var movement = runtimePlayer.GetComponent<CharacterController3D>()!;
        var descriptor = ComponentPropertyRenderer.Descriptors(movement.GetType(), PropertyEditorContext.Runtime)
            .Single(p => p.Property.Name == nameof(CharacterController3D.MoveSpeed));
        Assert(ComponentPropertyRenderer.SetValue(movement, descriptor, 9f, PropertyEditorContext.Runtime), "Runtime movement is editable");
        var camera = runtimePlayer.GetComponent<CameraBoom3D>()!;
        var distance = ComponentPropertyRenderer.Descriptors(camera.GetType(), PropertyEditorContext.Runtime)
            .Single(p => p.Property.Name == nameof(CameraBoom3D.ArmLength));
        ComponentPropertyRenderer.SetValue(camera, distance, 3f, PropertyEditorContext.Runtime);
        Assert(movement.MoveSpeed == 9 && camera.ArmLength == 3 &&
            player.GetComponent<CharacterController3D>()!.MoveSpeed == 5 &&
            player.GetComponent<CameraBoom3D>()!.ArmLength == 5 && link.SourceSnapshot == "original",
            "Runtime edits preserve editor values and Blueprint baseline");
    }

    private static void TestSingleDragUndo(EditorProjectContext project)
    {
        var scene = new Scene("Drag");
        var item = scene.CreateGameObject("Object");
        var state = new EditorState { EditorScene = scene, Project = project.Project, ProjectFilePath = project.ProjectFilePath };
        var undo = new UndoManager(project.Scenes, _ => { });
        state.Undo = undo;
        undo.BeginGesture(state, "Move Object");
        for (int frame = 0; frame < 10; frame++) item.Transform.WorldPosition = new Vector3(frame, 0, 0);
        undo.CommitGesture(state);
        Assert(undo.CanUndo, "Drag creates an undo entry");
        undo.Undo(state);
        Assert(!undo.CanUndo && Near(state.EditorScene.FindGameObject(item.Id)!.Transform.WorldPosition, Vector3.Zero),
            "One undo restores the entire drag");
    }

    private static void TestBlueprintEviction(EditorProjectContext project)
    {
        var scene = new Scene("Eviction");
        var item = scene.CreateGameObject("Gorilla");
        item.AddComponent(new CharacterController3D());
        string path = Path.Combine(project.ProjectRoot, "Assets", "BP_Gorilla.byteblueprint");
        var old = BlueprintPromotionService.Promote(project, item, path);
        var reference = new AssetReference(old.Asset.Guid, old.Asset.ProjectPath);
        using var workspace = new BlueprintWorkspacePanel();
        workspace.Open(old.Asset, project);
        Assert(workspace.OpenAssetId == old.Asset.Guid, "Workspace loads old Blueprint");
        var runtime = new Scene("Runtime");
        Assert(RuntimeBlueprintSpawner.Spawn(runtime, reference, Vector3.Zero, project, _ => { }) != null, "Old Blueprint spawns before deletion");
        BlueprintPromotionService.UnpackInstances(scene, reference);
        BlueprintPromotionService.UnpackInstances(runtime, reference);
        File.Delete(old.Asset.FullPath);
        File.Delete(old.Asset.MetaPath);
        project.AssetDatabase.Scan();
        Assert(project.AssetDatabase.Resolve(reference) == null && workspace.OpenAssetId == null, "Scan evicts asset and open workspace");
        Assert(RuntimeBlueprintSpawner.Spawn(runtime, reference, Vector3.Zero, project, _ => { }) == null, "Deleted Blueprint cannot spawn");
        Assert(old.Instance.SourceSnapshot.Length == 0 && old.Instance.ObjectMap.Count == 0 && old.Instance.Blueprint.IsEmpty,
            "Detached instance retains no baseline or source reference");
        item.RemoveComponent(item.GetComponent<CharacterController3D>()!);
        var fresh = BlueprintPromotionService.Promote(project, item, path);
        Assert(fresh.Asset.Guid != old.Asset.Guid && project.AssetDatabase.Resolve(reference) == null &&
            fresh.Blueprint.Root.Components.All(c => c.Type != nameof(CharacterController3D)),
            "Same filename gets fresh GUID and clean components");
        Assert(BlueprintPromotionService.UnpackInstances(scene, reference) == 0 && item.GetComponent<BlueprintInstance>() != null,
            "Old GUID cannot match recreated filename");
    }
    private static bool Near(Vector3 a, Vector3 b) => Vector3.Distance(a, b) < .001f;
    private static void Assert(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("FAILED v0.8-b.5: " + message);
    }
}
