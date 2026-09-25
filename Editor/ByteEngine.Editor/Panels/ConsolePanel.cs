using System.Numerics;
using ByteEngine.Core.Diagnostics;

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

        if (ImGui.BeginTabBar("##ConsoleTabs"))
        {
            if (ImGui.BeginTabItem("Console"))
            {
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
                ImGui.EndTabItem();
            }
            if (ImGui.BeginTabItem("Debug"))
            {
                DrawDebug();
                ImGui.EndTabItem();
            }
            ImGui.EndTabBar();
        }



        ImGui.End();
    }

    private static void DrawDebug()
    {
        bool enabled = RuntimeDiagnostics.DebugFootIk;
        if (ImGui.Checkbox("Debug Foot IK", ref enabled))
            RuntimeDiagnostics.DebugFootIk = enabled;

        ImGui.SameLine();

        bool blueprintEnabled = RuntimeDiagnostics.DebugBlueprintVisibility;
        if (ImGui.Checkbox("Debug Blueprint Visibility", ref blueprintEnabled))
            RuntimeDiagnostics.DebugBlueprintVisibility = blueprintEnabled;

        ImGui.SameLine();

        bool tpsJitterEnabled = RuntimeDiagnostics.DebugTpsJitter;
        if (ImGui.Checkbox("Debug TPS Jitter", ref tpsJitterEnabled))
            RuntimeDiagnostics.DebugTpsJitter = tpsJitterEnabled;

        ImGui.TextWrapped(
            "Foot IK records during Play. Blueprint Visibility samples the Scene View while open. " +
            "TPS Jitter records one end-of-frame sample after movement, physics and camera LateUpdate. " +
            "Untick a debug option to freeze its trace, then copy it.");

        string trace =
            $"[Foot IK]{Environment.NewLine}{RuntimeDiagnostics.GetFootIkTrace()}" +
            $"{Environment.NewLine}{Environment.NewLine}[Blueprint Visibility]{Environment.NewLine}{RuntimeDiagnostics.GetBlueprintVisibilityTrace()}" +
            $"{Environment.NewLine}{Environment.NewLine}[TPS Jitter]{Environment.NewLine}{RuntimeDiagnostics.GetTpsJitterTrace()}";

        if (ImGui.SmallButton("Copy Debug"))
            ImGui.SetClipboardText(trace);

        ImGui.SameLine();

        if (ImGui.SmallButton("Clear Debug"))
        {
            RuntimeDiagnostics.ClearFootIkTrace();
            RuntimeDiagnostics.ClearBlueprintVisibilityTrace();
            RuntimeDiagnostics.ClearTpsJitterTrace();
            trace = string.Empty;
        }

        ImGui.Separator();

        ImGui.InputTextMultiline("##RuntimeDebugText", ref trace,
            (uint)Math.Max(trace.Length + 1, 1),
            new Vector2(-1, Math.Max(1, ImGui.GetContentRegionAvail().Y)),
            ImGuiInputTextFlags.ReadOnly);
    }
}
