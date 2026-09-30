using System.ComponentModel;
using System.Text.Json;
using ByteEngine.Mcp.Authoring;
using ModelContextProtocol.Server;

namespace ByteEngine.Mcp.Mcp;

/// <summary>Eight compact domain tools; semantic operation details live in AuthoringSession.</summary>
[McpServerToolType]
public sealed class ByteTools(AuthoringSession session)
{
    private string Call(string domain, string op, JsonElement? args) =>
        session.Dispatch(domain, op, args);

    [McpServerTool(Name = "be_project"), Description("Open, inspect, configure, or validate the active ByteEngine project.")]
    public string Project(string op, JsonElement? args = null) => Call("project", op, args);

    [McpServerTool(Name = "be_catalog"), Description("Search engine component, logic, tag, layer, and asset-type capabilities.")]
    public string Catalog(string op, JsonElement? args = null) => Call("catalog", op, args);

    [McpServerTool(Name = "be_asset"), Description("Find or inspect project assets using compact handles.")]
    public string Asset(string op, JsonElement? args = null) => Call("asset", op, args);

    [McpServerTool(Name = "be_scene"), Description("List, inspect, create, or batch-edit scenes.")]
    public string Scene(string op, JsonElement? args = null) => Call("scene", op, args);

    [McpServerTool(Name = "be_blueprint"), Description("List, inspect, create, or batch-edit Blueprints.")]
    public string Blueprint(string op, JsonElement? args = null) => Call("blueprint", op, args);

    [McpServerTool(Name = "be_logic"), Description("List, inspect, create, or author Event Module rules.")]
    public string Logic(string op, JsonElement? args = null) => Call("logic", op, args);

    [McpServerTool(Name = "be_runtime"), Description("Validate a ByteEngine project or run a controlled build.")]
    public string Runtime(string op, JsonElement? args = null) => Call("runtime", op, args);

    [McpServerTool(Name = "be_changes"), Description("Inspect MCP session mutations and revision receipts.")]
    public string Changes(string op, JsonElement? args = null) => Call("changes", op, args);
}
