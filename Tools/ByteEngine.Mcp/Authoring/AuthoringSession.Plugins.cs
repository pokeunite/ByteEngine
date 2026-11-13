using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ByteEngine.Core.Plugins;

namespace ByteEngine.Mcp.Authoring;

public sealed partial class AuthoringSession
{
    private object PluginTool(string op, JsonElement? args)
    {
        _ = Project;
        if (op == "list")
            return new { ok = true,
                installed = ByteEnginePluginPackageManager.ListInstalled(Root),
                loaded = _plugins?.LoadedPlugins.Select(p => new { Id = p.Manifest?.Id, Version = p.Manifest?.Version }).ToArray(),
                sources = ListPluginSources(),
                messages = _pluginMessages.ToArray() };
        if (op == "create") return CreatePluginTool(args);
        if (op == "build") return BuildPluginTool(args);
        if (op == "import" || op == "install")
        {
            string package = Request.String(args, "package") ?? Request.Required(args, "path");
            var result = ByteEnginePluginPackageManager.Import(Root, package);
            return new { ok = true, plugin = result.Plugin, message = result.Message, reopen_required = true };
        }
        if (op == "update")
        {
            string package = Request.String(args, "package") ?? Request.Required(args, "path");
            string updateMessage = ReplacePackage(package);
            return new { ok = true, message = updateMessage, reopen_required = true };
        }
        string id = Request.Required(args, "id");
        var plugin = ByteEnginePluginPackageManager.ListInstalled(Root)
            .FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase))
            ?? throw new McpFault("NOT_FOUND", "Plugin is not installed.");
        string message = op switch {
            "enable" => ByteEnginePluginPackageManager.SetEnabled(plugin, true),
            "disable" => ByteEnginePluginPackageManager.SetEnabled(plugin, false),
            "remove" => ByteEnginePluginPackageManager.Remove(plugin),
            _ => throw new McpFault("UNSUPPORTED_CAPABILITY") };
        return new { ok = true, message, reopen_required = true };
    }

    private string ReplacePackage(string package)
    {
        string? id = ReadPackageId(package);
        if (id != null)
        {
            var installed = ByteEnginePluginPackageManager.ListInstalled(Root)
                .FirstOrDefault(p => p.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
            if (installed != null) ByteEnginePluginPackageManager.Remove(installed);
        }
        return ByteEnginePluginPackageManager.Import(Root, package).Message;
    }

    private static string? ReadPackageId(string package)
    {
        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(package);
            var entry = archive.GetEntry("plugin.json");
            if (entry == null) return null;
            using var reader = new StreamReader(entry.Open());
            using var document = JsonDocument.Parse(reader.ReadToEnd());
            return document.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
        }
        catch (Exception) { return null; }
    }
    private object CreatePluginTool(JsonElement? args)
    {
        string name = Request.Required(args, "name").Trim();
        if (!Regex.IsMatch(name, "^[A-Za-z][A-Za-z0-9_.]*$"))
            throw new McpFault("INVALID_REQUEST", "Plugin name must start with a letter and contain only letters, digits, '_' or '.'.");
        string id = (Request.String(args, "id") ?? "game." + name.ToLowerInvariant()).Trim();
        if (!Regex.IsMatch(id, "^[a-z][a-z0-9_.-]*$"))
            throw new McpFault("INVALID_REQUEST", "Plugin id must be lower-case letters, digits, '.', '_' or '-'.");
        string namespaceName = Request.String(args, "namespace") ?? name;
        string component = Request.String(args, "component") ?? name + "System3D";
        if (!Regex.IsMatch(component, "^[A-Za-z][A-Za-z0-9_]*$") || !Regex.IsMatch(namespaceName, "^[A-Za-z][A-Za-z0-9_.]*$"))
            throw new McpFault("INVALID_REQUEST", "Component and namespace must be valid C# identifiers.");

        string? repository = FindRepositoryRoot()
            ?? throw new McpFault("REPOSITORY_NOT_FOUND", "Set BYTEENGINE_ROOT to the ByteEngine checkout.");
        string pluginsRoot = Path.Combine(repository, "Plugins");
        string directory = Request.String(args, "path") is { } custom
            ? Path.GetFullPath(custom)
            : Path.Combine(pluginsRoot, name);
        if (!string.Equals(Path.GetDirectoryName(directory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)), pluginsRoot, StringComparison.OrdinalIgnoreCase))
            throw new McpFault("INVALID_REQUEST", "Plugin source must be a direct child of the engine Plugins folder so ../BytePlugin.targets resolves.");
        if (Directory.Exists(directory) && Directory.EnumerateFileSystemEntries(directory).Any() && !Request.Bool(args, "force"))
            throw new McpFault("ALREADY_EXISTS", $"Plugin folder is not empty: {directory}");
        Directory.CreateDirectory(directory);

        string projectFile = Path.Combine(directory, name + ".Plugin.csproj");
        string manifestFile = Path.Combine(directory, "plugin.json");
        string sourceFile = Path.Combine(directory, name + "Plugin.cs");
        File.WriteAllText(projectFile, PluginScaffolding.CSharpProject(name, namespaceName));
        File.WriteAllText(manifestFile, PluginScaffolding.Manifest(id, name, namespaceName));
        File.WriteAllText(sourceFile, PluginScaffolding.ComponentSource(name, namespaceName, component));
        File.WriteAllText(Path.Combine(directory, "README.md"), PluginScaffolding.Readme(name, id, component));

        return new
        {
            ok = true,
            name,
            id,
            directory,
            project = projectFile,
            manifest = manifestFile,
            source = sourceFile,
            component,
            next = "Edit the C# source, then call be_plugin(op:\"build\", args:{\"plugin\":\"" + name + "\",\"install\":true})."
        };
    }

    private object BuildPluginTool(JsonElement? args)
    {
        string plugin = Request.Required(args, "plugin");
        string configuration = Request.String(args, "configuration") ?? "Release";
        string project = ResolvePluginProject(plugin);
        string repository = FindRepositoryRoot() ?? Path.GetDirectoryName(project)!;
        var stopwatch = Stopwatch.StartNew();
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = repository,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        // MSBuild node-reuse children would otherwise inherit our pipes and the
        // stdio transport's stdin, keeping this call alive after the build exits.
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        start.ArgumentList.Add("build");
        start.ArgumentList.Add(project);
        start.ArgumentList.Add("-c");
        start.ArgumentList.Add(configuration);
        start.ArgumentList.Add("--nologo");
        start.ArgumentList.Add("-v");
        start.ArgumentList.Add("m");
        start.ArgumentList.Add("-nodeReuse:false");
        using Process process = Process.Start(start)
            ?? throw new McpFault("IO_ERROR", "Could not start dotnet build.");
        process.StandardInput.Close();
        var captured = new StringBuilder();
        var gate = new object();
        process.OutputDataReceived += (_, line) =>
        {
            if (line.Data == null) return;
            lock (gate) captured.AppendLine(line.Data);
        };
        process.ErrorDataReceived += (_, line) =>
        {
            if (line.Data == null) return;
            lock (gate) captured.AppendLine(line.Data);
        };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();
        if (!process.WaitForExit(300000))
        {
            try { process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { }
            throw new McpFault("BUILD_TIMEOUT", "Plugin build exceeded five minutes.");
        }
        try
        {
            process.CancelOutputRead();
            process.CancelErrorRead();
        }
        catch (InvalidOperationException) { }
        string output;
        lock (gate) output = captured.ToString();
        stopwatch.Stop();
        string? package = ResolvePackagePath(project, output);
        if (process.ExitCode != 0 || package == null)
            return new { ok = false, code = "BUILD_FAILED", exit_code = process.ExitCode,
                elapsed_ms = stopwatch.ElapsedMilliseconds, output = Tail(output) };
        object? install = null;
        if (Request.Bool(args, "install"))
        {
            string message = ReplacePackage(package);
            install = new { package, message, reopen_required = true };
        }
        return new { ok = true, package, exit_code = process.ExitCode, elapsed_ms = stopwatch.ElapsedMilliseconds,
            install, output = Tail(output) };
    }


    private static string ResolvePluginProject(string name)
    {
        if (File.Exists(name) && name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)) return Path.GetFullPath(name);
        string? repository = FindRepositoryRoot()
            ?? throw new McpFault("REPOSITORY_NOT_FOUND", "Set BYTEENGINE_ROOT to the ByteEngine checkout.");
        string plugins = Path.Combine(repository, "Plugins");
        var candidates = Directory.Exists(plugins)
            ? Directory.EnumerateDirectories(plugins).SelectMany(directory => Directory.EnumerateFiles(directory, "*.Plugin.csproj")).ToArray()
            : [];
        string? match = candidates.FirstOrDefault(path =>
            Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(path)).Equals(name, StringComparison.OrdinalIgnoreCase) ||
            Path.GetFileName(Path.GetDirectoryName(path)!).Equals(name, StringComparison.OrdinalIgnoreCase));
        if (match == null)
        {
            match = candidates.FirstOrDefault(path =>
            {
                string manifest = Path.Combine(Path.GetDirectoryName(path)!, "plugin.json");
                return File.Exists(manifest) && ReadManifestId(manifest)?.Equals(name, StringComparison.OrdinalIgnoreCase) == true;
            });
        }
        return match ?? throw new McpFault("NOT_FOUND", $"No plugin source named '{name}' under {plugins}.");
    }

    private static string? ReadManifestId(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            return document.RootElement.TryGetProperty("id", out var id) ? id.GetString() : null;
        }
        catch (Exception) { return null; }
    }

    private static string? ResolvePackagePath(string project, string output)
    {
        foreach (Match match in Regex.Matches(output, @"BYTEPLUGIN CREATED:\s*(.+)"))
        {
            string candidate = match.Groups[1].Value.Trim();
            if (File.Exists(candidate)) return candidate;
        }
        string directory = Path.GetDirectoryName(project)!;
        string name = ReadCsprojValue(project, "BytePluginName") ?? Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(project));
        string? repository = FindRepositoryRoot();
        string fallback = Path.Combine(repository ?? Path.GetFullPath(Path.Combine(directory, "..", "..")), "artifacts", "plugins", name + ".byteplugin");
        return File.Exists(fallback) ? fallback : null;
    }

    private static string? ReadCsprojValue(string project, string property)
    {
        try
        {
            string text = File.ReadAllText(project);
            var match = Regex.Match(text, $"<{property}>([^<]+)</{property}>");
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }
        catch (Exception) { return null; }
    }

    private static string Tail(string output) => output.Length <= 4000 ? output : output[^4000..];

    private static object[] ListPluginSources()
    {
        string? repository = FindRepositoryRoot();
        string plugins = Path.Combine(repository ?? "", "Plugins");
        if (!Directory.Exists(plugins)) return [];
        var result = new List<object>();
        foreach (string directory in Directory.EnumerateDirectories(plugins).Order(StringComparer.OrdinalIgnoreCase))
        {
            string? project = Directory.EnumerateFiles(directory, "*.Plugin.csproj").FirstOrDefault();
            if (project == null) continue;
            string manifest = Path.Combine(directory, "plugin.json");
            string? id = File.Exists(manifest) ? ReadManifestId(manifest) : null;
            string? version = null;
            if (File.Exists(manifest))
            {
                try
                {
                    using var document = JsonDocument.Parse(File.ReadAllText(manifest));
                    version = document.RootElement.TryGetProperty("version", out var value) ? value.GetString() : null;
                }
                catch (Exception) { }
            }
            result.Add(new { name = Path.GetFileName(directory), id, version, project });
        }
        return result.ToArray();
    }

}
