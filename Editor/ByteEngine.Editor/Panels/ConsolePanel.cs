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

        bool visible=ImGui.Begin(
            "Console",
            ref isOpen
        );

        IsOpen =
            isOpen;
        if(!visible){ImGui.End();return;}

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
                if(Environment.TickCount64>=_nextConsoleRefresh){_nextConsoleRefresh=Environment.TickCount64+500;_consolePreview=string.Join(Environment.NewLine,log.Entries.TakeLast(160).Select(entry=>$"[{entry.Timestamp:HH:mm:ss}] [{entry.Level}] {entry.Message}"));if(_consolePreview.Length>24000)_consolePreview=_consolePreview[^24000..];}
                string selectableText = _consolePreview;
                if (ImGui.SmallButton("Copy All"))
                {
                    ImGui.SetClipboardText(string.Join(Environment.NewLine,log.Entries.Select(entry=>$"[{entry.Timestamp:HH:mm:ss}] [{entry.Level}] {entry.Message}")));
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

    private static string _debugSaveStatus="",_debugPreview="",_consolePreview="";
    private static long _nextDebugRefresh,_nextConsoleRefresh;

    private static void DrawDebug()
    {
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

        bool constructionEnabled = ConstructionDiagnostics.Enabled;
        if (ImGui.Checkbox("Debug Building / Contraptions", ref constructionEnabled))
            ConstructionDiagnostics.Enabled = constructionEnabled;

        bool performance=SurfacePerformanceDiagnostics.PerformanceEnabled;
        if(ImGui.Checkbox("Debug Performance / Frame Pacing",ref performance)){SurfacePerformanceDiagnostics.PerformanceEnabled=performance;_nextDebugRefresh=0;}
        ImGui.SameLine();
        bool sand=SurfacePerformanceDiagnostics.SandEnabled;
        if(ImGui.Checkbox("Debug Interactive Sand",ref sand)){SurfacePerformanceDiagnostics.SandEnabled=sand;_nextDebugRefresh=0;}

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

        description.Add("Building / Contraptions records build snapshots, placement, connections, save/load and sampled wheel/hinge physics during Play.");

        description.Add("Performance captures CPU/GPU render times, presentation waits, allocations and GC pauses. Interactive Sand records deformation depth, stamps and shader state during Play.");

        description.Add("Untick a debug option to freeze its trace, then copy it.");

        ImGui.TextWrapped(
            string.Join(" ", description));

        if(Environment.TickCount64>=_nextDebugRefresh){_nextDebugRefresh=Environment.TickCount64+500;string full=BuildVisibleDebugTrace();_debugPreview=full.Length>24000?"Live view shows the latest 24KB. Copy/Save includes the full trace.\n"+full[^24000..]:full;}
        string trace=_debugPreview;

        if (ImGui.SmallButton("Copy Debug"))
            ImGui.SetClipboardText(BuildVisibleDebugTrace());

        ImGui.SameLine();

        if (ImGui.SmallButton("Save Debug…"))
        {
            string? path=EditorDialogs.ChooseDebugTraceSave();
            if(path!=null)
            {
                try {File.WriteAllText(path,BuildVisibleDebugTrace(),Encoding.UTF8);_debugSaveStatus="Saved: "+path;}
                catch(Exception e) when(e is IOException or UnauthorizedAccessException){_debugSaveStatus="Could not save: "+e.Message;}
            }
        }
        ImGui.SameLine();
        if (ImGui.SmallButton("Clear Debug"))
        {
            ClearVisibleDebugTrace();
            trace = _debugPreview=string.Empty;_nextDebugRefresh=0;
        }

        if(_debugSaveStatus.Length>0)ImGui.TextWrapped(_debugSaveStatus);
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

        Append("Building / Contraptions", ConstructionDiagnostics.GetTrace());
        Append("Performance / Frame Pacing",SurfacePerformanceDiagnostics.GetPerformanceTrace());
        Append("Interactive Sand",SurfacePerformanceDiagnostics.GetSandTrace());
        return text.ToString();
    }

    private static void ClearVisibleDebugTrace()
    {
        SurfacePerformanceDiagnostics.Clear();
        ConstructionDiagnostics.Clear();
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
