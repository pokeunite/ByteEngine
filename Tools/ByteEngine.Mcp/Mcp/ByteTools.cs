using System.ComponentModel;
using System.Text.Json;
using ByteEngine.Mcp.Authoring;
using ModelContextProtocol.Server;

namespace ByteEngine.Mcp.Mcp;

/// <summary>Nine compact domain tools; semantic operation details live in AuthoringSession.</summary>
[McpServerToolType]
public sealed class ByteTools(AuthoringSession session)
{
    private string Call(string domain, string op, JsonElement? args) =>
        session.Dispatch(domain, op, args);

    [McpServerTool(Name = "be_plugin"), Description("List, scaffold, build, install, update, enable, disable, or remove project plugins.")]
    public string Plugin(string op, JsonElement? args = null) => Call("plugin", op, args);

    [McpServerTool(Name = "be_project"), Description("Create, open, inspect, tag, or configure the active ByteEngine project.")]
    public string Project(string op, JsonElement? args = null) => Call("project", op, args);

    [McpServerTool(Name = "be_catalog"), Description("Inspect engine component, logic, tag, layer, and asset-type capabilities.")]
    public string Catalog(string op, JsonElement? args = null) => Call("catalog", op, args);

    [McpServerTool(Name = "be_asset"), Description("Import files, inspect model sub-assets, change import settings, reimport, or find assets.")]
    public string Asset(string op, JsonElement? args = null) => Call("asset", op, args);

    [McpServerTool(Name = "be_scene"), Description("List, inspect, create, or batch-edit scenes (transform, duplicate, tags, variables, components).")]
    public string Scene(string op, JsonElement? args = null) => Call("scene", op, args);

    [McpServerTool(Name = "be_blueprint"), Description("List, inspect, extract from a scene, create, or batch-edit Blueprints.")]
    public string Blueprint(string op, JsonElement? args = null) => Call("blueprint", op, args);

    [McpServerTool(Name = "be_logic"), Description("List, inspect, create, or author Event Module rules.")]
    public string Logic(string op, JsonElement? args = null) => Call("logic", op, args);

    [McpServerTool(Name = "be_runtime"), Description("Validate every scene, Blueprint and Event Module in the active project.")]
    public string Runtime(string op, JsonElement? args = null) => Call("runtime", op, args);

    [McpServerTool(Name = "be_export"), Description("Analyze web compatibility or export the active project to Windows or Web. Exports never run automatically.")]
    public string Export(string op, JsonElement? args = null) => Call("export", op, args);

    [McpServerTool(Name = "be_editor"), Description("Open the ByteEngine editor on the active or given project so a human can Play and visually verify.")]
    public string Editor(string op, JsonElement? args = null) => Call("editor", op, args);

    [McpServerTool(Name = "be_changes"), Description("Inspect MCP session mutations and revision receipts.")]
    public string Changes(string op, JsonElement? args = null) => Call("changes", op, args);
}
