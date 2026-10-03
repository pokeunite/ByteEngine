using System.Text.Json;
using ByteEngine.Core.Plugins;
namespace ByteEngine.Mcp.Authoring;
public sealed partial class AuthoringSession
{
    private object PluginTool(string op, JsonElement? args)
    {
        _ = Project;
        if (op == "list") return new { ok = true,
            installed = ByteEnginePluginPackageManager.ListInstalled(Root),
            loaded = _plugins?.LoadedPlugins.Select(p => new { p.Manifest.Id, p.Manifest.Version }).ToArray(),
            messages = _pluginMessages.ToArray() };
        if (op == "import")
        {
            var result = ByteEnginePluginPackageManager.Import(Root, Request.Required(args, "path"));
            return new { ok = true, plugin = result.Plugin, message = result.Message, reopen_required = true };
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
}
