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

        if (state.SelectedObject == null)
        {
            DrawAssetOrEmpty(state, project);
            ImGui.End();
            return;
        }

        if (state.Mode != EditorMode.Edit)
            ImGui.TextColored(new Vector4(1f, .75f, .2f, 1f), "Runtime changes are discarded on Stop.");

        GameObject selected = state.SelectedObject;
        string name = selected.Name;
        if (ImGui.InputText("Name", ref name, 256))
        {
            selected.Name = string.IsNullOrWhiteSpace(name) ? "GameObject" : name;
            state.MarkDirty();
        }

        bool active = selected.Active;
        if (ImGui.Checkbox("Active", ref active)) { selected.Active = active; state.MarkDirty(); }

        ImGui.SeparatorText("Transform");
        Vector2 position = selected.Transform.Position;
        if (ImGui.DragFloat2("Position", ref position, 1f)) { selected.Transform.Position = position; state.MarkDirty(); }
        float rotation = selected.Transform.Rotation;
        if (ImGui.DragFloat("Rotation", ref rotation, .25f)) { selected.Transform.Rotation = rotation; state.MarkDirty(); }
        Vector2 size = selected.Transform.Size;
        if (ImGui.DragFloat2("Size", ref size, 1f, 1f, 10000f))
        {
            selected.Transform.Size = new Vector2(Math.Max(1f, size.X), Math.Max(1f, size.Y));
            state.MarkDirty();
        }

        Component? remove = null;
        foreach (Component component in selected.Components.ToArray())
        {
            ImGui.SeparatorText(component.GetType().Name);
            bool enabled = component.Enabled;
            if (ImGui.Checkbox($"Enabled##{component.GetHashCode()}", ref enabled)) { component.Enabled = enabled; state.MarkDirty(); }
            ImGui.SameLine();
            if (ImGui.SmallButton($"Remove##{component.GetHashCode()}")) remove = component;
            DrawComponentProperties(state, project, component);
        }

        if (remove != null && selected.RemoveComponent(remove)) state.MarkDirty();

        ImGui.Separator();
        if (ImGui.Button("Add Component")) ImGui.OpenPopup("Add Component Popup");
        if (ImGui.BeginPopup("Add Component Popup"))
        {
            DrawAddComponentItem<Camera2D>("Camera2D", selected, state, () => new Camera2D());
            DrawAddComponentItem<SpriteRenderer>("SpriteRenderer", selected, state,
                () => new SpriteRenderer());
            ImGui.EndPopup();
        }

        ImGui.End();
    }

    private static void DrawAddComponentItem<T>(string name, GameObject target, EditorState state, Func<T> factory) where T : Component
    {
        bool exists = target.HasComponent<T>();
        if (ImGui.MenuItem(name, string.Empty, false, !exists))
        {
            target.AddComponent(factory());
            state.MarkDirty();
        }
        if (exists && ImGui.IsItemHovered()) ImGui.SetTooltip("This component is already attached.");
    }

    private static void DrawComponentProperties(EditorState state, EditorProjectContext project, Component component)
    {
        ImGui.Indent();
        if (component is Camera2D camera)
        {
            float zoom = camera.Zoom;
            if (ImGui.DragFloat($"Zoom##{component.GetHashCode()}", ref zoom, .01f, .01f, 100f)) { camera.Zoom = zoom; state.MarkDirty(); }
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
                    var reference = new AssetReference(asset.Guid, asset.ProjectPath);
                    sprite.TextureReference = reference;
                    sprite.Texture = project.Assets.LoadTexture(reference);
                    state.MarkDirty();
                }
                ImGui.EndDragDropTarget();
            }
            ImGui.SameLine();
            if (ImGui.SmallButton($"X##texture{component.GetHashCode()}"))
            {
                sprite.TextureReference = null;
                sprite.Texture = null;
                state.MarkDirty();
            }

            Vector4 tint = sprite.Tint;
            if (ImGui.ColorEdit4($"Tint##{component.GetHashCode()}", ref tint)) { sprite.Tint = tint; state.MarkDirty(); }
            bool visible = sprite.Visible;
            if (ImGui.Checkbox($"Visible##{component.GetHashCode()}", ref visible)) { sprite.Visible = visible; state.MarkDirty(); }
        }
        ImGui.Unindent();
    }

    private static string TextureLabel(AssetReference? reference, AssetDatabase database)
    {
        if (reference == null || reference.IsEmpty) return "None";
        AssetRecord? asset = database.Resolve(reference);
        return asset == null ? "Missing Asset" : Path.GetFileName(asset.ProjectPath);
    }

    private static void DrawAssetOrEmpty(EditorState state, EditorProjectContext project)
    {
        AssetRecord? asset = null;
        if (state.SelectedAssetId.HasValue) project.AssetDatabase.TryGetAsset(state.SelectedAssetId.Value, out asset);
        if (asset == null && state.SelectedAssetPath != null) project.AssetDatabase.TryGetAsset(state.SelectedAssetPath, out asset);
        if (asset == null)
        {
            ImGui.TextDisabled(state.SelectedAssetPath == null ? "Select a GameObject or asset." : "Missing Asset");
            return;
        }

        ImGui.Text(Path.GetFileName(asset.ProjectPath));
        ImGui.TextDisabled(asset.ProjectPath);
        ImGui.TextDisabled($"GUID: {asset.Guid}");
        if (asset.Type != AssetType.Texture2D) return;

        ImGui.SeparatorText("Texture Import Settings");
        TextureFilter filter = asset.Metadata.Importer.Filter;
        string preview = filter.ToString();
        if (ImGui.BeginCombo("Filter", preview))
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
