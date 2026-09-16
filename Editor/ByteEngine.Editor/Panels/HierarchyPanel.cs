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
        Action<GameObject> createChild,
        Action<Guid, GameObject?> instantiateAsset)
    {
        bool isOpen = IsOpen;
        ImGui.Begin("Hierarchy", ref isOpen);
        IsOpen = isOpen;

        ImGui.PushStyleColor(
            ImGuiCol.ChildBg,
            new Vector4(
                0.105f,
                0.12f,
                0.15f,
                1.0f));

        if (ImGui.BeginChild(
                "##HierarchyHeader",
                new Vector2(
                    0.0f,
                    50.0f),
                ImGuiChildFlags.Borders,
                ImGuiWindowFlags.NoScrollbar))
        {
            ImGui.TextUnformatted(
                state.DisplayedScene.Name);

            ImGui.SameLine();

            ImGui.TextColored(
                state.Mode == EditorMode.Edit
                    ? new Vector4(
                        .4f,
                        .8f,
                        1f,
                        1f)
                    : new Vector4(
                        .4f,
                        1f,
                        .5f,
                        1f),
                state.Mode.ToString());

            ImGui.SameLine();

            ImGui.TextDisabled(
                $"{state.DisplayedScene.GameObjects.Count} objects");

            if (state.Mode == EditorMode.Edit)
            {
                float buttonWidth =
                    30.0f;

                ImGui.SameLine(
                    Math.Max(
                        ImGui.GetWindowWidth() -
                        buttonWidth -
                        12.0f,
                        ImGui.GetCursorPosX()));

                if (ImGui.Button(
                        "+",
                        new Vector2(
                            buttonWidth,
                            0.0f)))
                {
                    createObject();
                }

                if (ImGui.IsItemHovered())
                {
                    ImGui.SetTooltip(
                        "Create Empty GameObject");
                }
            }
        }

        ImGui.EndChild();
        ImGui.PopStyleColor();
        ImGui.Spacing();

        /*
         * Hierarchy commands are allowed to delete/reparent objects while the
         * panel is being drawn. Always iterate a snapshot so those mutations
         * cannot invalidate the scene's live GameObjects collection.
         */
        GameObject[] roots =
            state.DisplayedScene.GameObjects
                .Where(
                    item =>
                        item.Parent ==
                        null)
                .ToArray();

        DrawSiblingGroup(
            roots,
            state,
            deleteObjects,
            duplicateObjects,
            copyObjects,
            pasteObjects,
            createChild,
            instantiateAsset);

        if (state.Mode == EditorMode.Edit)
        {
            ImGui.Dummy(new Vector2(ImGui.GetContentRegionAvail().X, Math.Max(24f, ImGui.GetContentRegionAvail().Y)));
            if (ImGui.BeginDragDropTarget())
            {
                Guid? childId = GameObjectDragDrop.Accept();
                GameObject? child = childId.HasValue ? state.EditorScene.FindGameObject(childId.Value) : null;
                if (child != null)
                    ExecuteHierarchyEdit(state, "Unparent GameObject", () => child.SetParent(null));

                Guid? assetId = AssetDragDrop.Accept();
                if (assetId.HasValue) instantiateAsset(assetId.Value, null);
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

    private void DrawNode(GameObject gameObject, EditorState state, Action delete, Action duplicate, Action copy,
        Action paste, Action<GameObject> createChild, Action<Guid, GameObject?> instantiateAsset)
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

        ImGuiTreeNodeFlags flags =
            ImGuiTreeNodeFlags.OpenOnArrow |
            ImGuiTreeNodeFlags.SpanFullWidth |
            ImGuiTreeNodeFlags.FramePadding;

        if (gameObject.Children.Count == 0)
        {
            flags |=
                ImGuiTreeNodeFlags.Leaf;
        }

        if (state.Selection.Contains(gameObject))
        {
            flags |=
                ImGuiTreeNodeFlags.Selected;
        }

        ImGui.PushStyleVar(
            ImGuiStyleVar.FramePadding,
            new Vector2(
                4.0f,
                4.0f));

        bool open =
            ImGui.TreeNodeEx(
                $"  {gameObject.Name}##{gameObject.Id}",
                flags);

        Vector2 rowMinimum =
            ImGui.GetItemRectMin();

        Vector2 rowMaximum =
            ImGui.GetItemRectMax();

        ImGui.GetWindowDrawList()
            .AddCircleFilled(
                new Vector2(
                    rowMinimum.X +
                    18.0f,
                    (
                        rowMinimum.Y +
                        rowMaximum.Y
                    ) *
                    0.5f),
                3.0f,
                ImGui.GetColorU32(
                    gameObject.Active
                        ? new Vector4(
                            0.38f,
                            0.72f,
                            1.0f,
                            1.0f)
                        : new Vector4(
                            0.42f,
                            0.45f,
                            0.50f,
                            1.0f)));

        ImGui.PopStyleVar();

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

        /*
         * Dropping ON an object always means "make child".
         *
         * Reordering is deliberately handled by dedicated thin drop zones
         * BETWEEN sibling rows. The previous top/middle/bottom row split was
         * too ambiguous in ImGui and caused reorder attempts to become
         * parenting operations.
         */
        if (state.Mode == EditorMode.Edit && ImGui.BeginDragDropTarget())
        {
            Guid? childId = GameObjectDragDrop.Accept();
            GameObject? child = childId.HasValue ? state.EditorScene.FindGameObject(childId.Value) : null;
            if (child != null && !ReferenceEquals(child, gameObject))
            {
                ExecuteHierarchyEdit(
                    state,
                    "Parent GameObject",
                    () =>
                        child.SetParent(
                            gameObject));
            }

            Guid? assetId = AssetDragDrop.Accept();
            if (assetId.HasValue) instantiateAsset(assetId.Value, gameObject);
            ImGui.EndDragDropTarget();
        }

        bool deletedThisNode = false;

        GameObject[] siblings = GetSiblings(gameObject, state.DisplayedScene);
        int siblingIndex = Array.IndexOf(siblings, gameObject);

        if (state.Mode == EditorMode.Edit && ImGui.BeginPopupContextItem($"ObjectContext{gameObject.Id}"))
        {
            if (ImGui.MenuItem("Rename", "F2")) BeginRename(gameObject, state);
            if (ImGui.MenuItem("Duplicate", "Ctrl+D")) { state.Selection.Set(gameObject); duplicate(); }

            ImGui.BeginDisabled(siblingIndex <= 0);
            if (ImGui.MenuItem("Move Up"))
            {
                GameObject previous = siblings[siblingIndex - 1];
                ExecuteHierarchyEdit(state, "Move GameObject Up", () => gameObject.MoveBefore(previous));
            }
            ImGui.EndDisabled();

            ImGui.BeginDisabled(siblingIndex < 0 || siblingIndex >= siblings.Length - 1);
            if (ImGui.MenuItem("Move Down"))
            {
                GameObject next = siblings[siblingIndex + 1];
                ExecuteHierarchyEdit(state, "Move GameObject Down", () => gameObject.MoveAfter(next));
            }
            ImGui.EndDisabled();

            if (ImGui.MenuItem("Delete", "Delete"))
            {
                state.Selection.Set(gameObject);
                delete();
                deletedThisNode = true;
            }

            if (!deletedThisNode)
            {
                if (ImGui.MenuItem("Create Child")) createChild(gameObject);
                if (ImGui.MenuItem("Copy", "Ctrl+C")) { state.Selection.Set(gameObject); copy(); }
                if (ImGui.MenuItem("Paste", "Ctrl+V")) paste();
            }

            ImGui.EndPopup();
        }

        /*
         * Once a context-menu delete has destroyed this object, do not touch
         * the local GameObject again in the same ImGui frame.
         */
        if (deletedThisNode)
        {
            if (open)
            {
                ImGui.TreePop();
            }

            return;
        }

        if (state.Selection.Primary == gameObject && ImGui.IsWindowFocused() && ImGui.IsKeyPressed(ImGuiKey.F2)) BeginRename(gameObject, state);
        if (open)
        {
            /*
             * Child commands can also mutate the live child list. Iterate a
             * snapshot for the same reason as the root hierarchy.
             */
            DrawSiblingGroup(
                gameObject.Children.ToArray(),
                state,
                delete,
                duplicate,
                copy,
                paste,
                createChild,
                instantiateAsset);

            ImGui.TreePop();
        }
    }

    private void DrawSiblingGroup(
        GameObject[] siblings,
        EditorState state,
        Action delete,
        Action duplicate,
        Action copy,
        Action paste,
        Action<GameObject> createChild,
        Action<Guid, GameObject?> instantiateAsset)
    {
        if (siblings.Length ==
            0)
        {
            return;
        }

        for (int index =
                 0;
             index <
             siblings.Length;
             index++)
        {
            GameObject sibling =
                siblings[index];

            DrawReorderDropTarget(
                state,
                sibling,
                after:
                    false,
                $"Before:{sibling.Id}");

            DrawNode(
                sibling,
                state,
                delete,
                duplicate,
                copy,
                paste,
                createChild,
                instantiateAsset);
        }

        GameObject last =
            siblings[^1];

        DrawReorderDropTarget(
            state,
            last,
            after:
                true,
            $"After:{last.Id}");
    }

    private static void DrawReorderDropTarget(
        EditorState state,
        GameObject reference,
        bool after,
        string id)
    {
        if (state.Mode !=
            EditorMode.Edit)
        {
            return;
        }

        ImGui.PushID(
            id);

        float width =
            Math.Max(
                ImGui.GetContentRegionAvail().X,
                1.0f);

        /*
         * A real, separate ImGui item gives reorder drops an unambiguous
         * target. Five pixels is large enough to hit while dragging but small
         * enough not to make the hierarchy look widely spaced.
         */
        ImGui.InvisibleButton(
            "##HierarchyReorderDrop",
            new Vector2(
                width,
                5.0f));

        Vector2 minimum =
            ImGui.GetItemRectMin();

        Vector2 maximum =
            ImGui.GetItemRectMax();

        if (ImGui.IsItemHovered())
        {
            float lineY =
                (
                    minimum.Y +
                    maximum.Y
                ) *
                0.5f;

            ImGui.GetWindowDrawList()
                .AddLine(
                    new Vector2(
                        minimum.X,
                        lineY),
                    new Vector2(
                        maximum.X,
                        lineY),
                    ImGui.GetColorU32(
                        new Vector4(
                            0.30f,
                            0.72f,
                            1.0f,
                            1.0f)),
                    2.0f);
        }

        if (ImGui.BeginDragDropTarget())
        {
            Guid? movingId =
                GameObjectDragDrop.Accept();

            GameObject? moving =
                movingId.HasValue
                    ? state.EditorScene.FindGameObject(
                        movingId.Value)
                    : null;

            if (moving !=
                    null &&
                !ReferenceEquals(
                    moving,
                    reference) &&
                ReferenceEquals(
                    moving.Parent,
                    reference.Parent))
            {
                ExecuteHierarchyEdit(
                    state,
                    after
                        ? "Move GameObject Down"
                        : "Move GameObject Up",
                    () =>
                    {
                        if (after)
                        {
                            moving.MoveAfter(
                                reference);
                        }
                        else
                        {
                            moving.MoveBefore(
                                reference);
                        }
                    });
            }

            ImGui.EndDragDropTarget();
        }

        ImGui.PopID();
    }

    private static GameObject[] GetSiblings(GameObject gameObject, Scene scene)
    {
        return gameObject.Parent != null
            ? gameObject.Parent.Children.ToArray()
            : scene.GameObjects.Where(item => item.Parent == null).ToArray();
    }

    private static void ExecuteHierarchyEdit(EditorState state, string name, Action action)
    {
        if (state.Undo != null)
        {
            state.Undo.Execute(state, name, action);
            return;
        }

        action();
        state.MarkDirty();
    }

    private void BeginRename(GameObject gameObject, EditorState state)
    {
        _renaming = gameObject.Id;
        _renameBuffer = gameObject.Name;
        _focusRename = true;
        state.Undo?.BeginGesture(state, "Rename GameObject");
    }
}
