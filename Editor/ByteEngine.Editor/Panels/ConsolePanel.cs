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

        foreach (EditorLogEntry entry
                 in log.Entries)
        {
            Vector4 color =
                entry.Level switch
                {
                    EditorLogLevel.Warning =>
                        new Vector4(
                            1.0f,
                            0.72f,
                            0.2f,
                            1.0f
                        ),
                    EditorLogLevel.Error =>
                        new Vector4(
                            1.0f,
                            0.3f,
                            0.3f,
                            1.0f
                        ),
                    _ =>
                        new Vector4(
                            0.82f,
                            0.85f,
                            0.9f,
                            1.0f
                        )
                };

            ImGui.PushStyleColor(
                ImGuiCol.Text,
                color
            );

            ImGui.TextWrapped(
                $"[{entry.Timestamp:HH:mm:ss}] [{entry.Level}] {entry.Message}"
            );

            ImGui.PopStyleColor();
        }

        ImGui.End();
    }
}
