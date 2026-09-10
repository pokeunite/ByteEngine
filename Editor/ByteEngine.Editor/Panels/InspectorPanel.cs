using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class InspectorPanel
{
    public bool IsOpen { get; set; } = true;

    public void Draw(EditorState state, EditorProjectContext project)
    {
        bool isOpen = IsOpen;
        ImGui.Begin("Inspector", ref isOpen);
        IsOpen = isOpen;

        if (state.Selection.Count > 1)
        {
            ImGui.Text($"{state.Selection.Count} Objects Selected");
            ImGui.TextDisabled("Use Scene View gizmos to move the selection.");
            ImGui.End();
            return;
        }

        if (state.SelectedObject == null)
        {
            DrawAssetOrEmpty(state, project);
            ImGui.End();
            return;
        }

        bool readOnly = state.Mode != EditorMode.Edit;
        if (readOnly) ImGui.TextColored(new Vector4(1f, .75f, .2f, 1f), "Editor values are read-only during Play Mode.");
        ImGui.BeginDisabled(readOnly);

        GameObject selected = state.SelectedObject;
        string oldName = selected.Name;
        string name = oldName;
        bool nameChanged = ImGui.InputText("Name", ref name, 256);
        string nextName = string.IsNullOrWhiteSpace(name) ? "GameObject" : name;
        if (nameChanged) selected.Name = nextName;
        TrackItem(state, "Rename GameObject", nameChanged, () => selected.Name = oldName, () => selected.Name = nextName);

        bool oldActive = selected.Active;
        bool active = oldActive;
        bool activeChanged = ImGui.Checkbox("Active", ref active);
        if (activeChanged) selected.Active = active;
        TrackItem(state, "Set Active", activeChanged, () => selected.Active = oldActive, () => selected.Active = active);

        ImGui.SeparatorText("Transform");
        Vector2 oldPosition = selected.Transform.Position;
        Vector2 position = oldPosition;
        bool positionChanged = ImGui.DragFloat2("Position", ref position, 1f);
        if (positionChanged) selected.Transform.Position = position;
        TrackItem(state, "Move GameObject", positionChanged, () => selected.Transform.Position = oldPosition, () => selected.Transform.Position = position);

        float oldRotation = selected.Transform.Rotation;
        float rotation = oldRotation;
        bool rotationChanged = ImGui.DragFloat("Rotation", ref rotation, .25f);
        if (rotationChanged) selected.Transform.Rotation = rotation;
        TrackItem(state, "Rotate GameObject", rotationChanged, () => selected.Transform.Rotation = oldRotation, () => selected.Transform.Rotation = rotation);

        Vector2 oldSize = selected.Transform.Size;
        Vector2 size = oldSize;
        bool sizeChanged = ImGui.DragFloat2("Size", ref size, 1f, 1f, 10000f);
        Vector2 nextSize = new(Math.Max(1f, size.X), Math.Max(1f, size.Y));
        if (sizeChanged) selected.Transform.Size = nextSize;
        TrackItem(state, "Scale GameObject", sizeChanged, () => selected.Transform.Size = oldSize, () => selected.Transform.Size = nextSize);

        foreach (Component component in selected.Components.ToArray())
        {
            ImGui.SeparatorText(component.GetType().Name);
            bool oldEnabled = component.Enabled;
            bool enabled = oldEnabled;
            bool enabledChanged = ImGui.Checkbox($"Enabled##{component.GetHashCode()}", ref enabled);
            if (enabledChanged) component.Enabled = enabled;
            TrackItem(state, "Set Component Enabled", enabledChanged, () => component.Enabled = oldEnabled, () => component.Enabled = enabled);
            ImGui.SameLine();
            if (ImGui.SmallButton($"Remove##{component.GetHashCode()}"))
                state.Undo?.Execute(state, $"Remove {component.GetType().Name}", () => selected.RemoveComponent(component));
            DrawComponentProperties(state, project, component);
        }

        ImGui.Separator();
        if (ImGui.Button("Add Component")) ImGui.OpenPopup("Add Component Popup");
        if (ImGui.BeginPopup("Add Component Popup"))
        {
            DrawAddComponentItem<Camera2D>("Camera2D", selected, state, () => new Camera2D());
            DrawAddComponentItem<SpriteRenderer>("SpriteRenderer", selected, state, () => new SpriteRenderer());
            ImGui.EndPopup();
        }

        ImGui.EndDisabled();
        ImGui.End();
    }

    private static void DrawAddComponentItem<T>(string name, GameObject target, EditorState state, Func<T> factory) where T : Component
    {
        bool exists = target.HasComponent<T>();
        if (ImGui.MenuItem(name, string.Empty, false, !exists))
            state.Undo?.Execute(state, $"Add {name}", () => target.AddComponent(factory()));
        if (exists && ImGui.IsItemHovered()) ImGui.SetTooltip("This component is already attached.");
    }

    private static void DrawComponentProperties(EditorState state, EditorProjectContext project, Component component)
    {
        ImGui.Indent();
        if (component is Camera2D camera)
        {
            float oldZoom = camera.Zoom;
            float zoom = oldZoom;
            bool changed = ImGui.DragFloat($"Zoom##{component.GetHashCode()}", ref zoom, .01f, .01f, 100f);
            if (changed) camera.Zoom = zoom;
            TrackItem(state, "Change Camera Zoom", changed, () => camera.Zoom = oldZoom, () => camera.Zoom = zoom);
        }
        else if (component is SpriteRenderer sprite)
        {
            string label = TextureLabel(sprite.TextureReference, project.AssetDatabase);
            ImGui.Button($"Texture: {label}##{component.GetHashCode()}", new Vector2(-55f, 0f));
            if (ImGui.BeginDragDropTarget())
            {
                Guid? guid = AssetDragDrop.Accept();
                if (guid.HasValue && project.AssetDatabase.TryGetAsset(guid.Value, out AssetRecord? asset) && asset?.Type == AssetType.Texture2D)
                {
                    state.Undo?.Execute(state, "Change Sprite Texture", () =>
                    {
                        var reference = new AssetReference(asset.Guid, asset.ProjectPath);
                        sprite.TextureReference = reference;
                        sprite.Texture = project.Assets.LoadTexture(reference);
                    });
                }
                ImGui.EndDragDropTarget();
            }
            ImGui.SameLine();
            if (ImGui.SmallButton($"X##texture{component.GetHashCode()}"))
                state.Undo?.Execute(state, "Clear Sprite Texture", () => { sprite.TextureReference = null; sprite.Texture = null; });

            Vector4 oldTint = sprite.Tint;
            Vector4 tint = oldTint;
            bool tintChanged = ImGui.ColorEdit4($"Tint##{component.GetHashCode()}", ref tint);
            if (tintChanged) sprite.Tint = tint;
            TrackItem(state, "Change Sprite Tint", tintChanged, () => sprite.Tint = oldTint, () => sprite.Tint = tint);

            bool oldVisible = sprite.Visible;
            bool visible = oldVisible;
            bool visibleChanged = ImGui.Checkbox($"Visible##{component.GetHashCode()}", ref visible);
            if (visibleChanged) sprite.Visible = visible;
            TrackItem(state, "Set Sprite Visibility", visibleChanged, () => sprite.Visible = oldVisible, () => sprite.Visible = visible);

            int oldOrder = sprite.OrderInLayer;
            int order = oldOrder;
            bool orderChanged = ImGui.DragInt($"Order In Layer##{component.GetHashCode()}", ref order, 1f);
            if (orderChanged) sprite.OrderInLayer = order;
            TrackItem(state, "Change Sprite Order", orderChanged, () => sprite.OrderInLayer = oldOrder, () => sprite.OrderInLayer = order);
        }
        ImGui.Unindent();
    }

    private static void TrackItem(EditorState state, string name, bool changed, Action restore, Action apply)
    {
        if (ImGui.IsItemActivated())
        {
            restore();
            state.Undo?.BeginGesture(state, name);
            apply();
        }
        if (changed && state.Undo == null) state.MarkDirty();
        if (ImGui.IsItemDeactivatedAfterEdit()) state.Undo?.CommitGesture(state);
    }

    private static string TextureLabel(AssetReference? reference, AssetDatabase database)
    {
        if (reference == null || reference.IsEmpty) return "None";
        return database.Resolve(reference) is AssetRecord asset ? Path.GetFileName(asset.ProjectPath) : "Missing Asset";
    }

    private static void DrawAssetOrEmpty(EditorState state, EditorProjectContext project)
    {
        AssetRecord? asset = null;
        if (state.SelectedAssetId.HasValue) project.AssetDatabase.TryGetAsset(state.SelectedAssetId.Value, out asset);
        if (asset == null && state.SelectedAssetPath != null) project.AssetDatabase.TryGetAsset(state.SelectedAssetPath, out asset);
        if (asset == null) { ImGui.TextDisabled(state.SelectedAssetPath == null ? "Select a GameObject or asset." : "Missing Asset"); return; }
        ImGui.Text(Path.GetFileName(asset.ProjectPath));
        ImGui.TextDisabled(asset.ProjectPath);
        ImGui.TextDisabled($"GUID: {asset.Guid}");
        if (asset.Type != AssetType.Texture2D) return;
        ImGui.SeparatorText("Texture Import Settings");
        TextureFilter filter = asset.Metadata.Importer.Filter;
        if (ImGui.BeginCombo("Filter", filter.ToString()))
        {
            foreach (TextureFilter option in Enum.GetValues<TextureFilter>())
            {
                bool selected = filter == option;
                if (ImGui.Selectable(option.ToString(), selected)) project.AssetDatabase.SetTextureFilter(asset.Guid, option);
                if (selected) ImGui.SetItemDefaultFocus();
            }
            ImGui.EndCombo();
        }
    }
}
