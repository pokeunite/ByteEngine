using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Editor.Panels;

internal static class BlueprintVisibilityTrace
{
    private static DateTime _lastSampleUtc;
    private static Guid _lastSelectionId;

    public static void Sample(EditorState state, Vector2 viewport, bool is3D, SceneFramebuffer framebuffer)
    {
        if (!RuntimeDiagnostics.DebugBlueprintVisibility) return;
        GameObject? selected = state.SelectedObject;
        if (selected != null && !ReferenceEquals(selected.Scene, state.DisplayedScene))
            selected = state.DisplayedScene.FindGameObject(selected.Id);
        Guid selectionId = selected?.Id ?? Guid.Empty;
        DateTime now = DateTime.UtcNow;
        if (selectionId == _lastSelectionId && now - _lastSampleUtc < TimeSpan.FromSeconds(1)) return;
        _lastSelectionId = selectionId;
        _lastSampleUtc = now;

        GameObject? root = selected;
        while (root != null && root.GetComponent<BlueprintInstance>() == null) root = root.Parent;
        root ??= state.DisplayedScene.GameObjects.FirstOrDefault(item => item.GetComponent<BlueprintInstance>() != null);
        EditorCamera3D camera = state.Camera3D;
        RuntimeDiagnostics.RecordBlueprintVisibility(
            $"scene={state.DisplayedScene.Name} mode={state.Mode} view={(is3D ? "3D" : "2D")} viewport={viewport.X:0}x{viewport.Y:0} selected={selected?.Name ?? "<none>"} camera={Format(camera.Position)} forward={Format(camera.Forward)} fov={camera.FieldOfView:0.#}");
        if (root == null)
        {
            RuntimeDiagnostics.RecordBlueprintVisibility("no Blueprint instance found in displayed scene; select the character Blueprint in Hierarchy");
            return;
        }

        BlueprintInstance instance = root.GetComponent<BlueprintInstance>()!;
        RuntimeDiagnostics.RecordBlueprintVisibility(
            $"blueprint={root.Name} id={root.Id} asset={instance.Blueprint.CachedProjectPath ?? "<none>"} active={root.ActiveInHierarchy} worldPos={Format(root.Transform.WorldPosition)} worldScale={Format(root.Transform.WorldScale)}");

        int rendererCount = 0;
        int logged = 0;
        void Visit(GameObject item)
        {
            ModelHierarchyInstance? hierarchy = item.GetComponent<ModelHierarchyInstance>();
            VisualModelOverride? visual = item.GetComponent<VisualModelOverride>();
            if ((hierarchy != null || visual != null) && logged < 8)
            {
                logged++;
                RuntimeDiagnostics.RecordBlueprintVisibility(
                    $"modelNode={item.Name} active={item.ActiveInHierarchy} localScale={Format(item.Transform.LocalScale)} worldScale={Format(item.Transform.WorldScale)} rotation={Format(item.Transform.EulerAngles)} source={hierarchy?.Model.CachedProjectPath ?? "<none>"} importScale={hierarchy?.AppliedImportScale:0.####} autoGrounded={hierarchy?.AutoGrounded}");
                if (hierarchy != null && state.Mode == EditorMode.Edit &&
                    framebuffer.TryGetEditorModelWorldBounds(state.DisplayedScene, item, out BoundingBox3D editorBounds))
                    RuntimeDiagnostics.RecordBlueprintVisibility(
                        $"editorSkinned={item.Name} {DescribeBounds(editorBounds, camera, viewport)}");
            }
            foreach (SkeletalMeshRenderer renderer in item.Components.OfType<SkeletalMeshRenderer>())
            {
                rendererCount++;
                if (logged >= 8) continue;
                logged++;
                bool hasBounds = renderer.TryGetCurrentModelBounds(out BoundingBox3D localBounds);
                string boundsText = hasBounds
                    ? DescribeBounds(localBounds.Transform(item.Transform.WorldMatrix), camera, viewport)
                    : "bounds=<unavailable>";
                RuntimeDiagnostics.RecordBlueprintVisibility(
                    $"skeletal={item.Name} active={item.ActiveInHierarchy} visible={renderer.Visible} loaded={renderer.ModelLoaded} meshes={renderer.SkinnedMeshCount} model={renderer.Model.CachedProjectPath ?? "<none>"} worldScale={Format(item.Transform.WorldScale)} {boundsText}");
            }
            foreach (MeshRenderer renderer in item.Components.OfType<MeshRenderer>())
            {
                rendererCount++;
                if (logged >= 8) continue;
                logged++;
                string boundsText = renderer.Mesh != null
                    ? DescribeBounds(renderer.Mesh.LocalBounds.Transform(item.Transform.WorldMatrix), camera, viewport)
                    : "bounds=<unavailable>";
                RuntimeDiagnostics.RecordBlueprintVisibility(
                    $"mesh={item.Name} active={item.ActiveInHierarchy} visible={renderer.Visible} assigned={renderer.Mesh != null} primitive={renderer.UsePrimitive} worldScale={Format(item.Transform.WorldScale)} {boundsText}");
            }
            foreach (GameObject child in item.Children) Visit(child);
        }
        Visit(root);
        RuntimeDiagnostics.RecordBlueprintVisibility(
            $"rendererCount={rendererCount} shown={logged}; in Edit, skinned models use editorSkinned bounds and their static bind-pose meshes are suppressed");
    }

    private static string DescribeBounds(BoundingBox3D bounds, EditorCamera3D camera, Vector2 viewport)
    {
        if (!bounds.IsValid) return "bounds=<invalid>";
        float depth = Vector3.Dot(bounds.Center - camera.Position, camera.Forward);
        float pixels = depth > 0.05f
            ? bounds.Size.Y * viewport.Y / (2f * depth * MathF.Tan(camera.FieldOfView * MathF.PI / 360f))
            : 0f;
        return $"worldCenter={Format(bounds.Center)} worldSize={Format(bounds.Size)} cameraDepth={depth:0.###} approxHeightPx={pixels:0.#} {(depth <= 0.05f ? "behind/near-camera" : pixels < 12f ? "TINY" : "")}";
    }

    private static string Format(Vector3 value) => $"({value.X:0.###},{value.Y:0.###},{value.Z:0.###})";
}
