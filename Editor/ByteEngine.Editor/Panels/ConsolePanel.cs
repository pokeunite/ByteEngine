using System.Numerics;

using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class ConsolePanel
{
    public bool IsOpen { get; set; } =
        true;

    public void Draw(
        EditorLog log)
    {
        bool isOpen =
            IsOpen;

        ImGui.Begin(
            "Console",
            ref isOpen
        );

        IsOpen =
            isOpen;

        if (ImGui.SmallButton("Clear"))
        {
            log.Clear();
        }

        ImGui.SameLine();

        ImGui.TextDisabled(
            $"{log.Entries.Count} messages"
        );

        ImGui.Separator();
        string selectableText = string.Join(Environment.NewLine,
            log.Entries.Select(entry =>
                $"[{entry.Timestamp:HH:mm:ss}] [{entry.Level}] {entry.Message}"));
        if (ImGui.SmallButton("Copy All"))
        {
            ImGui.SetClipboardText(selectableText);
        }

        // Read-only input supports mouse selection and Ctrl+C, unlike TextWrapped.
        ImGui.InputTextMultiline("##ConsoleText", ref selectableText,
            (uint)Math.Max(selectableText.Length + 1, 1),
            new Vector2(-1, Math.Max(1, ImGui.GetContentRegionAvail().Y)),
            ImGuiInputTextFlags.ReadOnly);



        ImGui.End();
    }
}
