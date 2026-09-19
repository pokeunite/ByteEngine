using System.Numerics;

using ImGuiNET;

namespace ByteEngine.Editor;

internal enum EditorStatusKind
{
    Neutral,
    Success,
    Warning,
    Error
}

internal static class EditorUi
{
    public static bool BeginToolbar(string id, float height = EditorTheme.ToolbarHeight)
    {
        ImGui.PushStyleColor(ImGuiCol.ChildBg, EditorTheme.ToolbarBackground);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Vector2(EditorTheme.S, 3.0f));
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new Vector2(EditorTheme.XS, 4.0f));
        return ImGui.BeginChild(id, new Vector2(0.0f, height), ImGuiChildFlags.Borders,
            ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse);
    }

    public static void EndToolbar()
    {
        ImGui.EndChild();
        ImGui.PopStyleVar(2);
        ImGui.PopStyleColor();
    }

    public static bool ToolbarButton(string label, string? tooltip = null, bool enabled = true)
    {
        ImGui.BeginDisabled(!enabled);
        bool pressed = ImGui.Button(label, new Vector2(0.0f, EditorTheme.StandardControlHeight));
        ImGui.EndDisabled();
        Tooltip(tooltip);
        return pressed;
    }

    public static bool ToolbarToggle(string label, bool active, string? tooltip = null)
    {
        if (active)
        {
            ImGui.PushStyleColor(ImGuiCol.Button, EditorTheme.Selection);
            ImGui.PushStyleColor(ImGuiCol.ButtonHovered, EditorTheme.SelectionHover);
            ImGui.PushStyleColor(ImGuiCol.ButtonActive, EditorTheme.AccentActive);
        }
        bool pressed = ImGui.Button(label, new Vector2(0.0f, EditorTheme.StandardControlHeight));
        if (active) ImGui.PopStyleColor(3);
        Tooltip(tooltip);
        return pressed;
    }

    public static void ToolbarSeparator()
    {
        ImGui.SameLine();
        ImGui.Dummy(new Vector2(EditorTheme.XS, 1.0f));
        ImGui.SameLine();
        ImGui.TextDisabled("|");
        ImGui.SameLine();
        ImGui.Dummy(new Vector2(EditorTheme.XS, 1.0f));
        ImGui.SameLine();
    }

    public static bool PrimaryButton(string label, Action? action = null, Vector2 size = default)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, EditorTheme.Accent);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, EditorTheme.AccentHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, EditorTheme.AccentActive);
        bool pressed = ImGui.Button(label, size);
        ImGui.PopStyleColor(3);
        if (pressed) action?.Invoke();
        return pressed;
    }

    public static bool SecondaryButton(string label, Action? action = null, Vector2 size = default)
    {
        bool pressed = ImGui.Button(label, size);
        if (pressed) action?.Invoke();
        return pressed;
    }

    public static bool DestructiveButton(string label, Action? action = null, Vector2 size = default)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.43f, 0.15f, 0.15f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.58f, 0.19f, 0.18f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.70f, 0.22f, 0.20f, 1.0f));
        bool pressed = ImGui.Button(label, size);
        ImGui.PopStyleColor(3);
        if (pressed) action?.Invoke();
        return pressed;
    }

    public static bool SectionHeader(string label, bool defaultOpen = true)
    {
        ImGui.Spacing();
        ImGui.PushStyleColor(ImGuiCol.Header, EditorTheme.HeaderBackground);
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, EditorTheme.PanelHover);
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, EditorTheme.Selection);
        bool open = ImGui.CollapsingHeader(label.ToUpperInvariant(),
            defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
        ImGui.PopStyleColor(3);
        return open;
    }

    public static void PropertyRow(string label, Action drawControl, string? tooltip = null)
    {
        float startX = ImGui.GetCursorPosX();
        ImGui.AlignTextToFramePadding();
        ImGui.TextColored(EditorTheme.TextSecondary, label);
        Tooltip(tooltip);
        ImGui.SameLine(startX + EditorTheme.InspectorLabelWidth);
        ImGui.SetNextItemWidth(-1.0f);
        drawControl();
    }

    public static void LabelValue(string label, string value)
    {
        float startX = ImGui.GetCursorPosX();
        ImGui.TextColored(EditorTheme.TextMuted, label);
        ImGui.SameLine(startX + EditorTheme.InspectorLabelWidth);
        ImGui.TextWrapped(value);
    }

    public static void MutedText(string text) => ImGui.TextColored(EditorTheme.TextMuted, text);

    public static void StatusBadge(string text, EditorStatusKind kind = EditorStatusKind.Neutral)
    {
        Vector4 color = kind switch
        {
            EditorStatusKind.Success => EditorTheme.Success,
            EditorStatusKind.Warning => EditorTheme.Warning,
            EditorStatusKind.Error => EditorTheme.Error,
            _ => EditorTheme.TextSecondary
        };
        ImGui.PushStyleColor(ImGuiCol.Button, color with { W = 0.18f });
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, color with { W = 0.18f });
        ImGui.PushStyleColor(ImGuiCol.Text, color);
        ImGui.SmallButton(text);
        ImGui.PopStyleColor(3);
    }

    public static void EmptyState(string title, string? guidance = null)
    {
        ImGui.Dummy(new Vector2(0.0f, EditorTheme.L));
        float width = ImGui.GetContentRegionAvail().X;
        float titleWidth = ImGui.CalcTextSize(title).X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max((width - titleWidth) * 0.5f, 0.0f));
        ImGui.TextColored(EditorTheme.TextSecondary, title);
        if (string.IsNullOrWhiteSpace(guidance)) return;
        float guidanceWidth = ImGui.CalcTextSize(guidance).X;
        ImGui.SetCursorPosX(ImGui.GetCursorPosX() + Math.Max((width - guidanceWidth) * 0.5f, 0.0f));
        ImGui.TextColored(EditorTheme.TextMuted, guidance);
    }

    public static bool SearchField(string id, ref string value, uint capacity = 256, float width = 240.0f)
    {
        ImGui.SetNextItemWidth(width);
        return ImGui.InputTextWithHint(id, "Search...", ref value, capacity);
    }

    public static bool IconButton(string label, string tooltip, Vector2 size = default)
    {
        bool pressed = ImGui.Button(label, size);
        Tooltip(tooltip);
        return pressed;
    }

    public static void Tooltip(string? text)
    {
        if (!string.IsNullOrWhiteSpace(text) && ImGui.IsItemHovered(ImGuiHoveredFlags.DelayShort))
            ImGui.SetTooltip(text);
    }
}