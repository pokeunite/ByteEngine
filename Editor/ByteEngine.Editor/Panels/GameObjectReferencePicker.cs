using ByteEngine.Core.Scene;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class GameObjectReferencePicker
{
    private string _search =
        string.Empty;
    private string _selectionSet = "Selection";

    public bool DrawPopup(
        string popupId,
        EditorState state,
        GameObject? selfContext,
        out string? selectedToken)
    {
        selectedToken =
            null;

        if (!ImGui.BeginPopup(
                popupId))
        {
            return false;
        }

        ImGui.SetNextItemWidth(
            300.0f);

        ImGui.InputTextWithHint(
            "##ObjectSearch",
            "Search objects...",
            ref _search,
            128);

        ImGui.Separator();

        bool changed =
            false;

        if (Matches(
                "Self"))
        {
            string selfLabel =
                selfContext != null
                    ? $"Self (preview: {selfContext.Name})"
                    : "Self";

            if (ImGui.Selectable(
                    selfLabel))
            {
                selectedToken =
                    "Self";

                changed =
                    true;

                ImGui.CloseCurrentPopup();
            }
        }

        if (Matches("Picked selection " + _selectionSet))
        {
            ImGui.SeparatorText("Picked objects");
            ImGui.SetNextItemWidth(280);
            ImGui.InputTextWithHint("##SelectionSet","Selection set name",ref _selectionSet,128);
            if(!string.IsNullOrWhiteSpace(_selectionSet)&&ImGui.Selectable("Use objects from this selection"))
            {selectedToken=_selectionSet=="Selection"?"Picked":"Picked:"+_selectionSet;changed=true;ImGui.CloseCurrentPopup();}
        }

        if (Matches("Last Ray Hit Weapon Shot Raycast"))
        {
            if (ImGui.Selectable("Last Ray Hit (raycast or weapon shot)"))
            {
                selectedToken = "Last Ray Hit";
                changed = true;
                ImGui.CloseCurrentPopup();
            }
        }

        if (state.EditorScene.GameObjects.Count >
            0)
        {
            ImGui.SeparatorText(
                "Scene Objects");

            foreach (GameObject gameObject
                     in state.EditorScene.GameObjects
                         .OrderBy(
                             item =>
                                 item.Name,
                             StringComparer.OrdinalIgnoreCase))
            {
                if (!Matches(
                        gameObject.Name) &&
                    !Matches(
                        gameObject.Id.ToString()))
                {
                    continue;
                }

                string label =
                    $"{gameObject.Name}##{gameObject.Id}";

                if (ImGui.Selectable(
                        label))
                {
                    selectedToken =
                        $"id:{gameObject.Id}";

                    changed =
                        true;

                    ImGui.CloseCurrentPopup();
                }
            }
        }

        ImGui.EndPopup();

        return changed;
    }

    public static string GetDisplayName(
        string token,
        EditorState? state,
        GameObject? selfContext)
    {
        if (string.IsNullOrWhiteSpace(
                token) ||
            string.Equals(
                token,
                "Self",
                StringComparison.OrdinalIgnoreCase))
        {
            return selfContext != null
                ? $"Self (preview: {selfContext.Name})"
                : "Self";
        }

        if (state != null &&
            token.StartsWith(
                "id:",
                StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(
                token[3..],
                out Guid objectId))
        {
            GameObject? target =
                state.EditorScene.FindGameObject(
                    objectId);

            if (target != null)
            {
                return target.Name;
            }

            return $"Missing Object ({objectId})";
        }

        return token;
    }

    private bool Matches(
        string value)
    {
        return string.IsNullOrWhiteSpace(
                   _search) ||
               value.Contains(
                   _search,
                   StringComparison.OrdinalIgnoreCase);
    }
}
