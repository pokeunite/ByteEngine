using System.Numerics;
using ByteEngine.Core.Diagnostics;

using ImGuiNET;

namespace ByteEngine.Editor;

internal sealed class EditorLayout
{
    private bool _layoutChecked;

    private bool _resetRequested;

    public void RequestReset()
    {
        _resetRequested =
            true;
    }

    public void DrawDockSpace()
    {
        /*
         * EditorApplication draws the primary main menu immediately before the
         * dock space. Dear ImGui supports appending to the same menu-bar window
         * with another Begin/End pair during the same frame, which lets the
         * debug visibility controls live here without coupling them to the
         * editor application's already-large menu implementation.
         */
        DrawDebugMenu();

        ImGuiViewportPtr viewport =
            ImGui.GetMainViewport();

        ImGui.SetNextWindowPos(
            viewport.WorkPos
        );

        ImGui.SetNextWindowSize(
            viewport.WorkSize
        );

        ImGui.SetNextWindowViewport(
            viewport.ID
        );

        ImGui.PushStyleVar(
            ImGuiStyleVar.WindowRounding,
            0.0f
        );

        ImGui.PushStyleVar(
            ImGuiStyleVar.WindowBorderSize,
            0.0f
        );

        ImGui.PushStyleVar(
            ImGuiStyleVar.WindowPadding,
            Vector2.Zero
        );

        ImGuiWindowFlags windowFlags =
            ImGuiWindowFlags.NoDocking |
            ImGuiWindowFlags.NoTitleBar |
            ImGuiWindowFlags.NoCollapse |
            ImGuiWindowFlags.NoResize |
            ImGuiWindowFlags.NoMove |
            ImGuiWindowFlags.NoBringToFrontOnFocus |
            ImGuiWindowFlags.NoNavFocus |
            ImGuiWindowFlags.NoSavedSettings;

        ImGui.PushStyleColor(
            ImGuiCol.WindowBg,
            new Vector4(
                0.070f,
                0.082f,
                0.102f,
                1.0f));

        ImGui.Begin(
            "ByteEngine Dock Host",
            windowFlags
        );

        ImGui.PopStyleColor();
        ImGui.PopStyleVar(3);

        uint dockSpaceId =
            ImGui.GetID(
                "ByteEngine DockSpace"
            );

        if (!_layoutChecked)
        {
            if (!ImGuiDockBuilder.NodeExists(
                    dockSpaceId))
            {
                ImGuiDockBuilder.BuildDefaultLayout(
                    dockSpaceId,
                    viewport.WorkSize
                );
            }

            _layoutChecked =
                true;
        }

        if (_resetRequested)
        {
            ImGuiDockBuilder.BuildDefaultLayout(
                dockSpaceId,
                viewport.WorkSize
            );

            _resetRequested =
                false;
        }

        ImGui.DockSpace(
            dockSpaceId,
            Vector2.Zero,
            ImGuiDockNodeFlags.PassthruCentralNode
        );

        ImGui.End();
    }

    private static void DrawDebugMenu()
    {
        if (!ImGui.BeginMainMenuBar())
        {
            return;
        }

        if (ImGui.BeginMenu("Debug"))
        {
            if (ImGui.BeginMenu("Console Debug Boxes"))
            {
                bool showFootIk =
                    EditorPreferences.ShowDebugFootIk;

                if (ImGui.MenuItem(
                        "Foot IK",
                        string.Empty,
                        showFootIk))
                {
                    EditorPreferences.ShowDebugFootIk =
                        !showFootIk;

                    if (showFootIk)
                        RuntimeDiagnostics.DebugFootIk = false;
                }

                bool showBlueprint =
                    EditorPreferences.ShowDebugBlueprintVisibility;

                if (ImGui.MenuItem(
                        "Blueprint Visibility",
                        string.Empty,
                        showBlueprint))
                {
                    EditorPreferences.ShowDebugBlueprintVisibility =
                        !showBlueprint;

                    if (showBlueprint)
                        RuntimeDiagnostics.DebugBlueprintVisibility = false;
                }

                bool showTps =
                    EditorPreferences.ShowDebugTpsJitter;

                if (ImGui.MenuItem(
                        "TPS Jitter",
                        string.Empty,
                        showTps))
                {
                    EditorPreferences.ShowDebugTpsJitter =
                        !showTps;

                    if (showTps)
                        RuntimeDiagnostics.DebugTpsJitter = false;
                }

                bool showWeapons =
                    EditorPreferences.ShowDebugWeaponRaycast;

                if (ImGui.MenuItem(
                        "Weapons / Raycasts",
                        string.Empty,
                        showWeapons))
                {
                    EditorPreferences.ShowDebugWeaponRaycast =
                        !showWeapons;

                    if (showWeapons)
                        RuntimeDiagnostics.DebugWeaponRaycast = false;
                }

                ImGui.Separator();

                if (ImGui.MenuItem("Show All"))
                {
                    EditorPreferences.ShowDebugFootIk = true;
                    EditorPreferences.ShowDebugBlueprintVisibility = true;
                    EditorPreferences.ShowDebugTpsJitter = true;
                    EditorPreferences.ShowDebugWeaponRaycast = true;
                }

                if (ImGui.MenuItem("Hide All"))
                {
                    EditorPreferences.ShowDebugFootIk = false;
                    EditorPreferences.ShowDebugBlueprintVisibility = false;
                    EditorPreferences.ShowDebugTpsJitter = false;
                    EditorPreferences.ShowDebugWeaponRaycast = false;
                    RuntimeDiagnostics.DebugFootIk = false;
                    RuntimeDiagnostics.DebugBlueprintVisibility = false;
                    RuntimeDiagnostics.DebugTpsJitter = false;
                    RuntimeDiagnostics.DebugWeaponRaycast = false;
                }

                ImGui.EndMenu();
            }

            ImGui.TextDisabled(
                "Choose which controls are shown in Console > Debug.");

            ImGui.EndMenu();
        }

        ImGui.EndMainMenuBar();
    }
}
