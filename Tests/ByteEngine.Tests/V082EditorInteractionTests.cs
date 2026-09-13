using System.Numerics;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Editor;
using ByteEngine.Editor.Gizmos;
using ByteEngine.Editor.Panels;
using ByteEngine.Editor.Selection;

namespace ByteEngine.Tests;

internal static class V082EditorInteractionTests
{
    public static void Run(string root)
    {
        TestBlueprintPromotionPropagationAndRecreation(root);
        TestAssetSelection();
        TestCameraPrompt();
        TestGizmoModes();
        TestEventPopupState();
    }

    private static void TestBlueprintPromotionPropagationAndRecreation(string root)
    {
        string projectRequest = Path.Combine(root, "B2Project.byteproject");
        using EditorProjectContext project = EditorProjectContext.Create(projectRequest, _ => { });
        var scene = new Scene("Promotion");
        GameObject parent = scene.CreateGameObject("Parent");
        GameObject gorilla = scene.CreateGameObject("Gorilla");
        gorilla.SetParent(parent, false);
        gorilla.Transform.LocalPosition = new Vector3(2f, 3f, 4f);
        gorilla.Transform.EulerAngles = new Vector3(10f, 25f, 5f);
        gorilla.Transform.LocalScale = new Vector3(1.5f, 2f, .75f);
        gorilla.AddComponent(new MeshRenderer());
        GameObject visual = scene.CreateGameObject("Visual");
        visual.SetParent(gorilla, false);
        visual.Transform.LocalPosition = new Vector3(0f, 1f, 0f);

        Guid originalId = gorilla.Id;
        Guid parentId = parent.Id;
        Vector3 worldPosition = gorilla.Transform.WorldPosition;
        Quaternion worldRotation = gorilla.Transform.WorldRotation;
        Vector3 worldScale = gorilla.Transform.WorldScale;
        string firstPath = Path.Combine(project.ProjectRoot, "Assets", "BP_Gorilla.byteblueprint");
        BlueprintPromotionResult promoted = BlueprintPromotionService.Promote(project, gorilla, firstPath);

        Assert(gorilla.Id == originalId && gorilla.Parent?.Id == parentId &&
            Near(gorilla.Transform.WorldPosition, worldPosition) &&
            Near(gorilla.Transform.WorldRotation, worldRotation) &&
            Near(gorilla.Transform.WorldScale, worldScale),
            "Create Blueprint From Selected preserves object identity, hierarchy and world transform");
        BlueprintInstance instance = gorilla.GetComponent<BlueprintInstance>()!;
        Assert(instance != null && instance.Blueprint.Guid == promoted.Asset.Guid &&
            instance.InstanceId != Guid.Empty && instance.ObjectMap[promoted.Blueprint.Root.Id] == originalId &&
            !string.IsNullOrWhiteSpace(instance.SourceSnapshot),
            "Promoted object receives complete BlueprintInstance source state");

        BlueprintDefinition source = new BlueprintSerializer().Load(firstPath);
        source.Root.Components.Add(new ComponentData { Type = nameof(HealthComponent) });
        source.Children.Add(new GameObjectData
        {
            Id = Guid.NewGuid(),
            Name = "Inherited Child",
            ParentId = source.Root.Id,
            Transform = new TransformData
            {
                LocalPosition = new Vector3Data { X = 0f, Y = 2f, Z = 0f },
                LocalRotation = new QuaternionData { W = 1f },
                LocalScale = new Vector3Data { X = 1f, Y = 1f, Z = 1f }
            }
        });
        new BlueprintSerializer().Save(source, firstPath);
        project.AssetDatabase.Scan();
        BlueprintInstanceSynchronizer.Propagate(
            project, scene, new ByteEngine.Core.Assets.AssetReference(promoted.Asset.Guid, promoted.Asset.ProjectPath));
        gorilla = scene.FindGameObject(originalId)!;
        Assert(gorilla.GetComponent<HealthComponent>() != null &&
            gorilla.Children.Any(item => item.Name == "Inherited Child"),
            "Source component and child changes immediately propagate to converted object");

        int unpacked = BlueprintPromotionService.UnpackInstances(
            scene, new ByteEngine.Core.Assets.AssetReference(promoted.Asset.Guid, promoted.Asset.ProjectPath));
        Assert(unpacked == 1 && gorilla.GetComponent<BlueprintInstance>() == null &&
            gorilla.GetComponent<HealthComponent>() != null,
            "Deleting a Blueprint can unpack its instance without losing materialized content");
        File.Delete(promoted.Asset.FullPath);
        if (File.Exists(promoted.Asset.MetaPath)) File.Delete(promoted.Asset.MetaPath);
        project.AssetDatabase.Scan();

        gorilla.RemoveComponent(gorilla.GetComponent<HealthComponent>()!);
        string secondPath = Path.Combine(project.ProjectRoot, "Assets", "BP_Gorilla_Recreated.byteblueprint");
        BlueprintPromotionResult recreated = BlueprintPromotionService.Promote(project, gorilla, secondPath);
        BlueprintDefinition recreatedSource = new BlueprintSerializer().Load(secondPath);
        Assert(recreated.Instance.Blueprint.Guid != promoted.Asset.Guid &&
            recreatedSource.Root.Components.All(item => item.Type != nameof(HealthComponent)) &&
            recreated.Instance.SourceSnapshot.Contains(nameof(HealthComponent), StringComparison.Ordinal) == false,
            "Recreating a Blueprint after unpack does not restore stale components or baseline state");
    }

    private static void TestAssetSelection()
    {
        string[] files = { "A", "B", "C", "D" };
        var selection = new AssetSelectionModel();
        selection.Click(files, 0, false, false);
        selection.Click(files, 2, true, false);
        Assert(selection.Count == 2 && selection.Contains("A") && selection.Contains("C"),
            "Asset Ctrl-click toggles individual selection");
        selection.Click(files, 3, false, true);
        Assert(selection.Count == 2 && selection.Contains("C") && selection.Contains("D"),
            "Asset Shift-click selects the anchored range");

        selection.Marquee(new[]
        {
            new AssetSelectionBounds("A", new Vector2(0f, 0f), new Vector2(50f, 20f)),
            new AssetSelectionBounds("B", new Vector2(0f, 25f), new Vector2(50f, 45f)),
            new AssetSelectionBounds("C", new Vector2(0f, 50f), new Vector2(50f, 70f))
        }, new Vector2(-5f, 22f), new Vector2(55f, 48f), false);
        Assert(selection.Count == 1 && selection.Contains("B"), "Asset marquee selects intersecting asset rows");
        Assert(AssetDeleteCommand.ShouldBegin(true, true, selection.Count) &&
            !AssetDeleteCommand.ShouldBegin(false, true, selection.Count),
            "Delete key command only begins while Asset Browser owns focus");
    }

    private static void TestCameraPrompt()
    {
        var scene = new Scene("Camera Prompt");
        Camera3D first = scene.CreateGameObject("Main Camera").AddComponent(new Camera3D());
        CameraActivationRequest firstRequest = CameraActivationPrompt.Create(first, null);
        Assert(firstRequest.Kind == CameraActivationPromptKind.UseAsFirstCamera,
            "First manually added Camera requests active-camera confirmation");

        Camera3D second = scene.CreateGameObject("Second Camera").AddComponent(new Camera3D());
        CameraActivationRequest replace = CameraActivationPrompt.Create(second, first);
        Assert(replace.Kind == CameraActivationPromptKind.ReplaceActiveCamera &&
            ReferenceEquals(replace.PreviousActiveCamera, first),
            "Additional Camera prompt identifies the current active Camera");
        CameraActivationPrompt.Apply(scene, replace, true);
        Assert(ReferenceEquals(scene.ActiveCamera, second), "Camera prompt Make Active decision updates scene ownership");
    }

    private static void TestGizmoModes()
    {
        var scene = new Scene("Gizmo");
        GameObject item = scene.CreateGameObject("Item");
        var gizmo = new Gizmo3DController();
        gizmo.SetMode(Gizmo3DMode.Rotate);
        Gizmo3DController.ApplyDragDelta(item, gizmo.Mode, Vector3.UnitY, 40f, 0f,
            Vector3.Zero, Vector3.Zero, Vector3.One);
        Assert(gizmo.Mode == Gizmo3DMode.Rotate && MathF.Abs(item.Transform.EulerAngles.Y - 20f) < .01f,
            "Scene rotation gizmo mode applies synchronized Y-axis Euler rotation");

        var blueprintGizmo = new BlueprintTransformGizmo3D();
        blueprintGizmo.SetMode(BlueprintGizmoMode.Rotate);
        BlueprintTransformGizmo3D.ApplyDragDelta(item, blueprintGizmo.Mode, Vector3.UnitX, 30f, 0f,
            Vector3.Zero, Vector3.Zero, Vector3.One);
        Assert(blueprintGizmo.Mode == BlueprintGizmoMode.Rotate &&
            MathF.Abs(item.Transform.EulerAngles.X - 15f) < .01f,
            "Blueprint rotation gizmo exposes and applies X/Y/Z rotation mode");
    }

    private static void TestEventPopupState()
    {
        var state = new EventNamePopupState();
        Guid id = Guid.NewGuid();
        state.BeginRename(id, "Player Death");
        Assert(state.IsOpen && state.IsRename && state.TargetEventId == id &&
            state.ConsumeOpenRequest() && !state.ConsumeOpenRequest() &&
            state.ConsumeFocusRequest() && !state.ConsumeFocusRequest(),
            "Event rename popup opens and requests keyboard focus exactly once");
        state.Buffer = "Player Defeated";
        state.BeginRename(id, "Should Not Reset");
        Assert(state.Buffer == "Player Defeated", "Active event rename typing is not reset by repeated open requests");
        state.Close();
        Assert(!state.IsOpen && state.TargetEventId == null, "Event rename state closes cleanly");
    }

    private static bool Near(Vector3 left, Vector3 right) => Vector3.Distance(left, right) < .001f;
    private static bool Near(Quaternion left, Quaternion right) =>
        MathF.Abs(Quaternion.Dot(left, right)) > .9999f;

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
    }
}
