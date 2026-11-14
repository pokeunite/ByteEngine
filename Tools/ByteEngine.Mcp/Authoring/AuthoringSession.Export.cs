using System.Diagnostics;
using System.Text.Json;
using ByteEngine.Core.Runtime;

namespace ByteEngine.Mcp.Authoring;

public sealed partial class AuthoringSession
{
    private object ExportTool(string op, JsonElement? args)
    {
        _ = Project;
        if (op == "analyze_web")
        {
            var issues = WebGamePackageExporter.Analyze(_projectFile!);
            return new
            {
                ok = issues.All(issue => !issue.BlocksExport),
                blocking = issues.Count(issue => issue.BlocksExport),
                issues
            };
        }
        if (op != "windows" && op != "web")
            throw new McpFault("UNSUPPORTED_CAPABILITY", "Use analyze_web, windows or web.");

        string? repository = FindRepositoryRoot();
        string runtime = Request.String(args, "runtime") ?? (repository == null ? string.Empty :
            Path.Combine(repository, "Dist", "ByteEngine", op == "windows" ? "PlayerRuntime" : "BrowserRuntime"));
        if (runtime.Length == 0 || !Directory.Exists(runtime))
            throw new McpFault("NOT_FOUND",
                op == "windows"
                    ? "Published player runtime not found. Run Tools/PublishPlayer.ps1 first."
                    : "Published browser runtime not found. Run Tools/PublishBrowser.ps1 first.");
        string output = Request.String(args, "output") ?? (repository == null
            ? Path.Combine(Path.GetTempPath(), "byteengine-exports")
            : Path.Combine(repository, ".artifacts", "exports"));
        Directory.CreateDirectory(output);
        string scene = Request.String(args, "scene") ?? Project.StartupScene;
        GamePackageResult result = op == "windows"
            ? GamePackageExporter.Export(_projectFile!, runtime, output, scene)
            : WebGamePackageExporter.Export(_projectFile!, runtime, output, scene);
        return new
        {
            ok = true,
            runtime,
            directory = result.Directory,
            executable = result.Executable,
            content_files = result.ContentFiles,
            content_bytes = result.ContentBytes
        };
    }

    private object EditorTool(string op, JsonElement? args)
    {
        if (op != "open") throw new McpFault("UNSUPPORTED_CAPABILITY");
        string project = Request.String(args, "project") is { } given
            ? Path.GetFullPath(given)
            : _projectFile ?? throw new McpFault("PROJECT_NOT_OPEN");
        if (!File.Exists(project)) throw new McpFault("NOT_FOUND", "Project file does not exist.");
        string? repository = FindRepositoryRoot();
        string[] candidates = repository == null ? [] :
        [
            Path.Combine(repository, "Dist", "ByteEngine", "ByteEngine.Editor.exe"),
            Path.Combine(repository, "Editor", "ByteEngine.Editor", "bin", "Release", "net9.0-windows", "win-x64", "ByteEngine.Editor.exe"),
            Path.Combine(repository, "Editor", "ByteEngine.Editor", "bin", "Debug", "net9.0-windows", "win-x64", "ByteEngine.Editor.exe"),
            Path.Combine(repository, "Editor", "ByteEngine.Editor", "bin", "Release", "net9.0-windows", "ByteEngine.Editor.exe"),
            Path.Combine(repository, "Editor", "ByteEngine.Editor", "bin", "Debug", "net9.0-windows", "ByteEngine.Editor.exe")
        ];
        string executable = candidates.FirstOrDefault(File.Exists)
            ?? throw new McpFault("NOT_FOUND", "Publish or build the editor first.");
        var process = Process.Start(new ProcessStartInfo(executable)
        {
            UseShellExecute = true,
            WorkingDirectory = Path.GetDirectoryName(executable)!,
            Arguments = "\"" + project + "\""
        });
        return new { ok = true, executable, project, pid = process?.Id };
    }
}
