using System.Diagnostics;
using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Runtime;
using ImGuiNET;

namespace ByteEngine.Editor.Panels;

internal sealed class GameExportPanel
{
    public bool IsOpen { get; set; }
    private string _startup = string.Empty;
    private string _output = string.Empty;
    private Guid _project;
    private bool _web;
    private Task<GamePackageResult>? _task;
    private volatile string _progress = string.Empty;
    private string _error = string.Empty;
    private GamePackageResult? _result;

    public void Open(EditorProjectContext project, bool web = false)
    {
        if (_task != null) { IsOpen = true; return; }
        _web = web;
        _project = project.Project.ProjectId;
        _startup = project.Project.StartupScene;
        _output = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "ByteEngine Games");
        _error = string.Empty;
        _result = null;
        IsOpen = true;
    }

    public void Draw(EditorProjectContext project, bool dirty, bool playing, EditorLog log)
    {
        if (_task?.IsCompleted == true)
        {
            try
            {
                _result = _task.GetAwaiter().GetResult();
                log.Info($"Game exported: {_result.Executable}");
            }
            catch (Exception error) { _error = error.Message; log.Error("Game export failed: " + error.Message); }
            _task = null;
        }
        if (!IsOpen) return;
        ImGui.SetNextWindowSize(new Vector2(660, 420), ImGuiCond.FirstUseEver);
        bool open = IsOpen;
        bool visible = ImGui.Begin(_web ? "Export Web Game" : "Export Windows Game", ref open);
        IsOpen = open;
        if (!visible) { ImGui.End(); return; }
        ImGui.TextWrapped(_web ? "Desktop-browser WebGL 2 game. Includes a ready-to-upload itch.io HTML ZIP. No SDK is required by players." : "Windows x64 standalone game. Includes .NET and native libraries; no editor or SDK is required to play.");
        ImGui.Separator();
        bool running = _task != null;
        ImGui.BeginDisabled(running);
        if (ImGui.BeginCombo("Startup Scene", _startup))
        {
            foreach (AssetRecord asset in project.AssetDatabase.Assets.Where(a => a.Type == AssetType.Scene)
                .OrderBy(a => a.ProjectPath, StringComparer.OrdinalIgnoreCase))
            {
                if (ImGui.Selectable(asset.ProjectPath, asset.ProjectPath == _startup)) _startup = asset.ProjectPath;
            }
            ImGui.EndCombo();
        }
        ImGui.InputText("Export Folder", ref _output, 1024);
        if (ImGui.Button("Choose Folder..."))
        {
            using var dialog = new FolderBrowserDialog { Description = _web ? "Choose where to save the web game" : "Choose where to save the Windows game",
                UseDescriptionForTitle = true, SelectedPath = _output };
            if (dialog.ShowDialog() == DialogResult.OK) _output = dialog.SelectedPath;
        }
        ImGui.TextWrapped(_web ? "Browser graphics are reduced: no desktop shadows, IBL or post-processing parity. Sprites and the legacy Arena HUD are not supported. Saved content is compressed into Game.bytepak; models are prepared during export. Test on a private itch.io page first." : "Microsoft Visual C++ x64 runtime and a compatible graphics driver are required. An official prerequisite link is included. A new dated build folder is created here. Existing builds are never overwritten. Saved assets and scenes are compressed into Game.bytepak.");
        ImGui.EndDisabled();
        if (dirty) ImGui.TextWrapped("Save the scene and all open asset documents before exporting.");
        if (playing) ImGui.TextWrapped("Stop Play mode before exporting.");
        if (running) ImGui.TextWrapped(_progress);
        if (_project != project.Project.ProjectId && !running)
            ImGui.TextWrapped("The open project changed. Close and reopen this export window.");
        ImGui.BeginDisabled(running || dirty || playing || string.IsNullOrWhiteSpace(_output) ||
            _project != project.Project.ProjectId);
        if (ImGui.Button(_web ? "Export Web Game" : "Export Windows Game"))
        {
            _error = string.Empty; _result = null; _progress = "Preparing export...";
            project.SaveProject();
            string file = project.ProjectFilePath, output = _output, startup = _startup;
            bool web = _web;
            string runtime = web ? FindWebRuntime() : FindRuntime();
            _task = Task.Run(() => web
                ? WebGamePackageExporter.Export(file, runtime, output, startup,
                    new ExportProgress(message => _progress = message))
                : GamePackageExporter.Export(file, runtime, output, startup,
                    new ExportProgress(message => _progress = message)));
        }
        ImGui.EndDisabled();
        if (!string.IsNullOrWhiteSpace(_error)) ImGui.TextWrapped(_error);
        if (_result != null)
        {
            ImGui.Separator();
            ImGui.TextWrapped("Export complete: " + _result.Directory);
            ImGui.Text($"{_result.ContentFiles} content files / {_result.ContentBytes / (1024d * 1024d):F1} MB");
            if (ImGui.Button("Open Export Folder"))
                Process.Start(new ProcessStartInfo(_result.Directory) { UseShellExecute = true });
            ImGui.TextWrapped(_web ? "Upload the generated ZIP as an HTML game on itch.io. To test locally, serve the site folder over HTTP. Click the game to activate input/audio; Escape releases the mouse." : "Zip and share the entire build folder, not just the executable. Test the build on another Windows x64 PC before distributing it.");
        }
        ImGui.End();
    }

    private static string FindWebRuntime()
    {
        string bundled = Path.Combine(AppContext.BaseDirectory, "BrowserRuntime");
        if (File.Exists(Path.Combine(bundled, "_framework", "dotnet.js"))) return bundled;
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ByteEngine.sln")))
                return Path.Combine(directory.FullName, "Player", "ByteEngine.Browser", "bin", "Release", "net9.0", "publish", "wwwroot");
            directory = directory.Parent;
        }
        return bundled;
    }

    private static string FindRuntime()
    {
        string bundled = Path.Combine(AppContext.BaseDirectory, "PlayerRuntime");
        if (File.Exists(Path.Combine(bundled, "ByteEngine.Player.exe"))) return bundled;
        // Source checkout fallback. Packaged editor always uses its adjacent PlayerRuntime.
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ByteEngine.sln")))
                return Path.Combine(directory.FullName, "Player", "ByteEngine.Player", "bin", "Release",
                    "net9.0-windows", "win-x64", "publish");
            directory = directory.Parent;
        }
        return bundled;
    }

    private sealed class ExportProgress(Action<string> report) : IProgress<string>
    {
        public void Report(string value) => report(value);
    }
}
