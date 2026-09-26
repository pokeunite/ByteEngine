using System.Numerics;
using System.Text;
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
        bool anyVisible =
            EditorPreferences.ShowDebugFootIk ||
            EditorPreferences.ShowDebugBlueprintVisibility ||
            EditorPreferences.ShowDebugTpsJitter ||
            EditorPreferences.ShowDebugWeaponRaycast;

        if (!anyVisible)
        {
            ImGui.TextDisabled(
                "No debug boxes are visible. Use Debug > Console Debug Boxes from the top menu.");
            return;
        }

        bool firstControl =
            true;

        if (EditorPreferences.ShowDebugFootIk)
        {
            bool enabled = RuntimeDiagnostics.DebugFootIk;
            if (ImGui.Checkbox("Debug Foot IK", ref enabled))
                RuntimeDiagnostics.DebugFootIk = enabled;
            firstControl = false;
        }

        if (EditorPreferences.ShowDebugBlueprintVisibility)
        {
            if (!firstControl) ImGui.SameLine();

            bool enabled = RuntimeDiagnostics.DebugBlueprintVisibility;
            if (ImGui.Checkbox("Debug Blueprint Visibility", ref enabled))
                RuntimeDiagnostics.DebugBlueprintVisibility = enabled;
            firstControl = false;
        }

        if (EditorPreferences.ShowDebugTpsJitter)
        {
            if (!firstControl) ImGui.SameLine();

            bool enabled = RuntimeDiagnostics.DebugTpsJitter;
            if (ImGui.Checkbox("Debug TPS Jitter", ref enabled))
                RuntimeDiagnostics.DebugTpsJitter = enabled;
            firstControl = false;
        }

        if (EditorPreferences.ShowDebugWeaponRaycast)
        {
            if (!firstControl) ImGui.SameLine();

            bool enabled = RuntimeDiagnostics.DebugWeaponRaycast;
            if (ImGui.Checkbox("Debug Weapons / Raycasts", ref enabled))
                RuntimeDiagnostics.DebugWeaponRaycast = enabled;
        }

        var description =
            new List<string>();

        if (EditorPreferences.ShowDebugFootIk)
            description.Add("Foot IK records during Play.");

        if (EditorPreferences.ShowDebugBlueprintVisibility)
            description.Add("Blueprint Visibility samples the Scene View while open.");

        if (EditorPreferences.ShowDebugTpsJitter)
            description.Add("TPS Jitter records one end-of-frame sample after movement, physics and camera LateUpdate.");

        if (EditorPreferences.ShowDebugWeaponRaycast)
            description.Add("Weapons / Raycasts records Event Sheet ray origins/directions plus projectile and PlayerShooter aim/velocity data. Projectile shots also draw a short cyan launch-direction line while enabled.");

        description.Add("Untick a debug option to freeze its trace, then copy it.");

        ImGui.TextWrapped(
            string.Join(" ", description));

        string trace =
            BuildVisibleDebugTrace();

        if (ImGui.SmallButton("Copy Debug"))
            ImGui.SetClipboardText(trace);

        ImGui.SameLine();

        if (ImGui.SmallButton("Clear Debug"))
        {
            ClearVisibleDebugTrace();
            trace = string.Empty;
        }

        ImGui.Separator();

        ImGui.InputTextMultiline("##RuntimeDebugText", ref trace,
            (uint)Math.Max(trace.Length + 1, 1),
            new Vector2(-1, Math.Max(1, ImGui.GetContentRegionAvail().Y)),
            ImGuiInputTextFlags.ReadOnly);
    }

    private static string BuildVisibleDebugTrace()
    {
        var text =
            new StringBuilder();

        void Append(
            string title,
            string value)
        {
            if (text.Length > 0)
                text.AppendLine().AppendLine();

            text.Append('[')
                .Append(title)
                .AppendLine("]")
                .Append(value);
        }

        if (EditorPreferences.ShowDebugFootIk)
            Append("Foot IK", RuntimeDiagnostics.GetFootIkTrace());

        if (EditorPreferences.ShowDebugBlueprintVisibility)
            Append("Blueprint Visibility", RuntimeDiagnostics.GetBlueprintVisibilityTrace());

        if (EditorPreferences.ShowDebugTpsJitter)
            Append("TPS Jitter", RuntimeDiagnostics.GetTpsJitterTrace());

        if (EditorPreferences.ShowDebugWeaponRaycast)
            Append("Weapons / Raycasts", RuntimeDiagnostics.GetWeaponRaycastTrace());

        return text.ToString();
    }

    private static void ClearVisibleDebugTrace()
    {
        if (EditorPreferences.ShowDebugFootIk)
            RuntimeDiagnostics.ClearFootIkTrace();

        if (EditorPreferences.ShowDebugBlueprintVisibility)
            RuntimeDiagnostics.ClearBlueprintVisibilityTrace();

        if (EditorPreferences.ShowDebugTpsJitter)
            RuntimeDiagnostics.ClearTpsJitterTrace();

        if (EditorPreferences.ShowDebugWeaponRaycast)
            RuntimeDiagnostics.ClearWeaponRaycastTrace();
    }
}
