using System.Numerics;

using ImGuiNET;

namespace ByteEngine.Editor;

internal static class EditorUi
{
    public static bool PrimaryButton(string label, Action? action = null)
    {
        ImGui.PushStyleColor(ImGuiCol.Button, EditorTheme.Accent);
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, EditorTheme.AccentHover);
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(.20f, .46f, .72f, 1.0f));
        bool pressed = ImGui.Button(label);
        ImGui.PopStyleColor(3);
        if (pressed) action?.Invoke();
        return pressed;
    }

    public static void LabelValue(string label, string value)
    {
        ImGui.TextDisabled(label);
        ImGui.SameLine(Math.Max(110.0f, ImGui.GetCursorPosX()));
        ImGui.TextWrapped(value);
    }
}