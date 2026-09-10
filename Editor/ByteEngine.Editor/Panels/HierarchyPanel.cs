using System.Numerics;
using ByteEngine.Core.Scene;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class HierarchyPanel
{
    private Guid? _renaming;
    private string _renameBuffer = string.Empty;
    private bool _focusRename;
    public bool IsOpen { get; set; } = true;

    public void Draw(
        EditorState state,
        Action createObject,
        Action deleteObjects,
        Action duplicateObjects,
        Action copyObjects,
        Action pasteObjects,
        Action<GameObject> createChild)
    {
        bool isOpen = IsOpen;
        ImGui.Begin("Hierarchy", ref isOpen);
        IsOpen = isOpen;
        ImGui.TextDisabled(state.DisplayedScene.Name);
        ImGui.SameLine();
        ImGui.TextColored(state.Mode == EditorMode.Edit ? new Vector4(.4f, .8f, 1f, 1f) : new Vector4(.4f, 1f, .5f, 1f), state.Mode.ToString());
        ImGui.Separator();

        foreach (GameObject root in state.DisplayedScene.GameObjects.Where(item => item.Parent == null))
            DrawNode(root, state, deleteObjects, duplicateObjects, copyObjects, pasteObjects, createChild);

        if (state.Mode == EditorMode.Edit)
        {
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, Math.Max(24f, ImGui.GetContentRegionAvail().Y)));
            if (ImGui.BeginDragDropTarget())
            {
                Guid? childId = GameObjectDragDrop.Accept();
                GameObject? child = childId.HasValue ? state.EditorScene.FindGameObject(childId.Value) : null;
                if (child != null) state.Undo?.Execute(state, "Unparent GameObject", () => child.SetParent(null));
                ImGui.EndDragDropTarget();
            }
            if (ImGui.BeginPopupContextItem("HierarchyBlankContext"))
            {
                if (ImGui.MenuItem("Create Empty")) createObject();
                if (ImGui.MenuItem("Paste", "Ctrl+V")) pasteObjects();
                ImGui.EndPopup();
            }
        }

        if (state.Mode == EditorMode.Edit && ImGui.BeginPopupContextWindow("HierarchyContext", ImGuiPopupFlags.MouseButtonRight | ImGuiPopupFlags.NoOpenOverItems))
        {
            if (ImGui.MenuItem("Create Empty")) createObject();
            if (ImGui.MenuItem("Paste", "Ctrl+V", false, true)) pasteObjects();
            ImGui.EndPopup();
        }

        if (state.Mode == EditorMode.Edit && state.Selection.Count > 0 && ImGui.IsWindowFocused() && ImGui.IsKeyPressed(ImGuiKey.Delete)) deleteObjects();
        ImGui.End();
    }

    private void DrawNode(GameObject gameObject, EditorState state, Action delete, Action duplicate, Action copy, Action paste, Action<GameObject> createChild)
    {
        if (_renaming == gameObject.Id)
        {
            if (_focusRename) { ImGui.SetKeyboardFocusHere(); _focusRename = false; }
            ImGui.SetNextItemWidth(-1f);
            bool enter = ImGui.InputText($"##rename{gameObject.Id}", ref _renameBuffer, 256, ImGuiInputTextFlags.EnterReturnsTrue);
            gameObject.Name = string.IsNullOrWhiteSpace(_renameBuffer) ? "GameObject" : _renameBuffer;
            if (enter || ImGui.IsItemDeactivatedAfterEdit()) { _renaming = null; state.Undo?.CommitGesture(state); }
            return;
        }

        ImGuiTreeNodeFlags flags = ImGuiTreeNodeFlags.OpenOnArrow | ImGuiTreeNodeFlags.SpanFullWidth;
        if (gameObject.Children.Count == 0) flags |= ImGuiTreeNodeFlags.Leaf;
        if (state.Selection.Contains(gameObject)) flags |= ImGuiTreeNodeFlags.Selected;
        bool open = ImGui.TreeNodeEx($"{gameObject.Name}##{gameObject.Id}", flags);

        if (ImGui.IsItemClicked(ImGuiMouseButton.Left) && !ImGui.IsItemToggledOpen())
        {
            if (ImGui.GetIO().KeyCtrl) state.Selection.Toggle(gameObject); else state.Selection.Set(gameObject);
            state.SelectedAssetId = null;
            state.SelectedAssetPath = null;
        }

        if (state.Mode == EditorMode.Edit && ImGui.BeginDragDropSource())
        {
            GameObjectDragDrop.Set(gameObject.Id);
            ImGui.Text(gameObject.Name);
            ImGui.EndDragDropSource();
        }

        if (state.Mode == EditorMode.Edit && ImGui.BeginDragDropTarget())
        {
            Guid? childId = GameObjectDragDrop.Accept();
            GameObject? child = childId.HasValue ? state.EditorScene.FindGameObject(childId.Value) : null;
            if (child != null && !ReferenceEquals(child, gameObject))
                state.Undo?.Execute(state, "Parent GameObject", () => child.SetParent(gameObject));
            ImGui.EndDragDropTarget();
        }

        if (state.Mode == EditorMode.Edit && ImGui.BeginPopupContextItem($"ObjectContext{gameObject.Id}"))
        {
            if (ImGui.MenuItem("Rename", "F2")) BeginRename(gameObject, state);
            if (ImGui.MenuItem("Duplicate", "Ctrl+D")) { state.Selection.Set(gameObject); duplicate(); }
            if (ImGui.MenuItem("Delete", "Delete")) { state.Selection.Set(gameObject); delete(); }
            if (ImGui.MenuItem("Create Child")) createChild(gameObject);
            if (ImGui.MenuItem("Copy", "Ctrl+C")) { state.Selection.Set(gameObject); copy(); }
            if (ImGui.MenuItem("Paste", "Ctrl+V")) paste();
            ImGui.EndPopup();
        }

        if (state.Selection.Primary == gameObject && ImGui.IsWindowFocused() && ImGui.IsKeyPressed(ImGuiKey.F2)) BeginRename(gameObject, state);
        if (open)
        {
            foreach (GameObject child in gameObject.Children) DrawNode(child, state, delete, duplicate, copy, paste, createChild);
            ImGui.TreePop();
        }
    }

    private void BeginRename(GameObject gameObject, EditorState state)
    {
        _renaming = gameObject.Id;
        _renameBuffer = gameObject.Name;
        _focusRename = true;
        state.Undo?.BeginGesture(state, "Rename GameObject");
    }
}
