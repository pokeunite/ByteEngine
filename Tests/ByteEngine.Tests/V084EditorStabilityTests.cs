using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ByteEngine.Editor;
using ByteEngine.Editor.Gizmos;
using ByteEngine.Editor.Panels;
using ByteEngine.Editor.Selection;

namespace ByteEngine.Tests;

internal static class V084EditorStabilityTests
{
    public static void Run()
    {
        TestReusablePopupLifecycle();
        TestRepeatedEventOperations();
        TestSequentialNodeScale();
        TestRepeatedCameraPrompts();
        TestRepeatedDeleteOperations();
        TestAssetSelectionRecovery();
        TestRepeatedGizmoModes();
    }

    private static void TestReusablePopupLifecycle()
    {
        var popup = new PopupInteractionState();
        Assert(popup.Request(true) && popup.ConsumeOpenRequest() && popup.ConsumeFocusRequest(),
            "Popup request and focus are consumed once");
        popup.MarkVisible();
        Assert(popup.IsOpen && popup.IsVisible && !popup.Request(),
            "A genuinely visible popup rejects a competing request");
        popup.Reset();
        Assert(popup.Request(true) && popup.ConsumeOpenRequest(),
            "Popup lifecycle reopens after explicit completion");
        popup.RecoverWhenNotVisible();
        Assert(!popup.IsOpen && popup.Request(),
            "Popup lifecycle self-recovers when ImGui never shows or externally closes it");
    }

    private static void TestRepeatedEventOperations()
    {
        var state = new EventNamePopupState();
        BeginVisibleCreate(state, "A");
        state.Reset();
        BeginVisibleCreate(state, "B");
        state.Reset();
        BeginVisibleCreate(state, "C");
        state.Reset();

        Guid first = Guid.NewGuid();
        state.BeginRename(first, "A");
        Assert(state.IsRename && state.TargetEventId == first && state.ConsumeOpenRequest() && state.ConsumeFocusRequest(),
            "Event rename opens with GUID identity and one-shot focus");
        state.MarkVisible();
        state.Reset();

        state.BeginRename(Guid.NewGuid(), "Cancelled");
        state.ConsumeOpenRequest();
        state.MarkVisible();
        state.Reset();
        BeginVisibleCreate(state, "D");
        Assert(state.Buffer == "D", "Create remains reusable after rename cancellation");
        state.Reset();

        state.BeginCreate(Vector2.One);
        state.ConsumeOpenRequest();
        state.RecoverWhenNotVisible();
        state.BeginRename(Guid.NewGuid(), "Recovered Rename");
        Assert(state.IsRename && state.ConsumeOpenRequest(),
            "Stale Event popup bookkeeping cannot block a later rename");
    }

    private static void BeginVisibleCreate(EventNamePopupState state, string name)
    {
        state.BeginCreate(new Vector2(10f, 20f));
        Assert(state.ConsumeOpenRequest() && !state.ConsumeOpenRequest() &&
            state.ConsumeFocusRequest() && !state.ConsumeFocusRequest(),
            $"Event create {name} has one-shot open and focus requests");
        state.MarkVisible();
        state.Buffer = name;
    }

    private static void TestSequentialNodeScale()
    {
        var state = new ByteGraphViewSettingsState();
        state.Begin(.72f);
        Assert(state.ConsumeOpenRequest(), "ByteGraph View requests its popover once");
        state.MarkVisible();
        foreach (float value in new[] { .8f, .9f, .65f, 1f })
            Assert(state.SetNodeScale(value) && Near(state.NodeScale, value),
                $"Node Scale accepts sequential value {value:0.00}");
        Assert(state.IsOpen, "ByteGraph View remains open throughout sequential adjustments");
        state.Reset();
        state.Begin(.65f);
        state.ConsumeOpenRequest();
        state.MarkVisible();
        Assert(state.SetNodeScale(.85f) && state.SetNodeScale(1f),
            "Node Scale remains reusable after closing and reopening View");
        state.SetNodeScale(100f);
        Assert(Near(state.NodeScale, ByteGraphViewSettingsState.MaximumNodeScale),
            "Node Scale state clamps invalid oversized input");
    }

    private static void TestRepeatedCameraPrompts()
    {
        var scene = new Scene("Repeated Cameras");
        Camera3D existing = scene.CreateGameObject("Existing").AddComponent(new Camera3D());
        var state = new CameraActivationPromptState();
        for (int index = 0; index < 4; index++)
        {
            Camera3D camera = scene.CreateGameObject($"Camera {index}").AddComponent(new Camera3D());
            state.Begin(CameraActivationPrompt.Create(camera, existing));
            Assert(state.Request?.Camera == camera && state.ConsumeOpenRequest(),
                $"Camera prompt {index + 1} opens with the current camera request");
            state.MarkVisible();
            CameraActivationRequest request = state.Request
                ?? throw new InvalidOperationException("Camera prompt request unexpectedly missing.");
            if (index == 1) CameraActivationPrompt.Apply(scene, request, true);
            state.Reset();
            Assert(!state.IsOpen && state.Request == null, "Camera prompt reset clears request payload");
        }
    }

    private static void TestRepeatedDeleteOperations()
    {
        var state = new AssetDeleteInteractionState();
        state.Begin(new[] { "A.asset", "B.asset" }, false);
        Assert(state.Targets.Count == 2 && state.ConsumeOpenRequest(), "Grouped asset delete snapshots selected targets");
        state.MarkVisible();
        state.Reset();
        Assert(state.Targets.Count == 0 && !state.IsDirectory && !state.IsOpen,
            "Delete cancellation clears targets, directory flag and popup state");

        state.Begin(new[] { "Folder" }, true);
        state.ConsumeOpenRequest();
        state.MarkVisible();
        state.Reset();
        state.Begin(new[] { "C.asset" }, false);
        Assert(state.Targets.SequenceEqual(new[] { "C.asset" }) && !state.IsDirectory && state.ConsumeOpenRequest(),
            "A later delete never inherits earlier targets or directory state");
    }

    private static void TestAssetSelectionRecovery()
    {
        string[] files = { "A", "B", "C", "D" };
        var selection = new AssetSelectionModel();
        selection.Click(files, 0, false, false);
        selection.Click(files, 2, true, false);
        selection.Click(files, 3, false, true);
        selection.Marquee(new[]
        {
            new AssetSelectionBounds("A", Vector2.Zero, new Vector2(10f)),
            new AssetSelectionBounds("B", new Vector2(20f), new Vector2(30f))
        }, new Vector2(-1f), new Vector2(11f), false);
        Assert(selection.Count == 1 && selection.PrimaryPath == "A", "Marquee deterministically replaces selection");
        selection.Clear();
        selection.Click(files, 1, true, false);
        Assert(selection.Count == 1 && selection.PrimaryPath == "B", "Ctrl-select works after empty-area clear");
        selection.Retain(new[] { "A", "C", "D" });
        Assert(selection.Count == 0 && selection.PrimaryPath == null, "Selection recovers when selected asset disappears");
    }

    private static void TestRepeatedGizmoModes()
    {
        var sceneGizmo = new Gizmo3DController();
        var blueprintGizmo = new BlueprintTransformGizmo3D();
        for (int repeat = 0; repeat < 2; repeat++)
        {
            sceneGizmo.SetMode(Gizmo3DMode.Move);
            sceneGizmo.SetMode(Gizmo3DMode.Rotate);
            sceneGizmo.SetMode(Gizmo3DMode.Scale);
            blueprintGizmo.SetMode(BlueprintGizmoMode.Translate);
            blueprintGizmo.SetMode(BlueprintGizmoMode.Rotate);
            blueprintGizmo.SetMode(BlueprintGizmoMode.Scale);
        }
        sceneGizmo.ApplyShortcuts(true, false, false);
        sceneGizmo.ApplyShortcuts(false, true, false);
        sceneGizmo.ApplyShortcuts(false, false, true);
        sceneGizmo.ApplyShortcuts(true, false, false);
        blueprintGizmo.ApplyShortcuts(true, false, false);
        blueprintGizmo.ApplyShortcuts(false, true, false);
        blueprintGizmo.ApplyShortcuts(false, false, true);
        blueprintGizmo.ApplyShortcuts(true, false, false);
        Assert(sceneGizmo.Mode == Gizmo3DMode.Move && blueprintGizmo.Mode == BlueprintGizmoMode.Translate,
            "Scene and Blueprint toolbar/keyboard mode state remains reusable across W/E/R cycles");
    }

    private static bool Near(float left, float right) => MathF.Abs(left - right) < .0001f;
    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
    }
}
