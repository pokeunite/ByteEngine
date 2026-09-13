using ByteEngine.Core.Classification;
using ImGuiNET;

namespace ByteEngine.Editor;

internal static class ClassificationPickers
{
    public static bool DrawLayer(string label, ClassificationSettings settings, ref int layer)
    {
        string preview = settings.FindLayer(layer)?.Name ?? "Default";
        bool changed = false;
        if (!ImGui.BeginCombo(label, preview)) return false;
        foreach (ObjectLayerDefinition option in settings.Layers.OrderBy(item => item.Index))
        {
            bool selected = option.Index == layer;
            if (ImGui.Selectable($"{option.Index}  {option.Name}##{label}{option.Index}", selected))
            {
                layer = option.Index;
                changed = true;
            }
            if (selected) ImGui.SetItemDefaultFocus();
        }
        ImGui.EndCombo();
        return changed;
    }

    public static bool DrawLayerMask(string label, ClassificationSettings settings, ref LayerMask mask)
    {
        bool changed = false;
        LayerMask current = mask;
        string preview = current == LayerMask.All ? "All" : current == LayerMask.None ? "None" :
            string.Join(", ", settings.Layers.Where(item => current.Contains(item.Index)).Select(item => item.Name));
        if (!ImGui.BeginCombo(label, preview)) return false;
        if (ImGui.SmallButton("All")) { mask = LayerMask.All; changed = true; }
        ImGui.SameLine();
        if (ImGui.SmallButton("None")) { mask = LayerMask.None; changed = true; }
        foreach (ObjectLayerDefinition layer in settings.Layers.OrderBy(item => item.Index))
        {
            bool included = mask.Contains(layer.Index);
            if (ImGui.Checkbox($"{layer.Name}##{label}{layer.Index}", ref included))
            {
                mask = included ? mask.Add(layer.Index) : mask.Remove(layer.Index);
                changed = true;
            }
        }
        ImGui.EndCombo();
        return changed;
    }

    public static bool DrawTag(string label, ClassificationSettings settings, ref Guid tagId, bool allowNone = true)
    {
        string preview = settings.FindTag(tagId)?.Name ?? (tagId == Guid.Empty ? "None" : "Missing Tag");
        bool changed = false;
        if (!ImGui.BeginCombo(label, preview)) return false;
        if (allowNone && ImGui.Selectable("None", tagId == Guid.Empty)) { tagId = Guid.Empty; changed = true; }
        foreach (TagDefinition tag in settings.Tags.OrderBy(item => item.Name))
        {
            bool selected = tag.Id == tagId;
            if (ImGui.Selectable($"{tag.Name}##{tag.Id}", selected)) { tagId = tag.Id; changed = true; }
            if (selected) ImGui.SetItemDefaultFocus();
        }
        ImGui.EndCombo();
        return changed;
    }
}
