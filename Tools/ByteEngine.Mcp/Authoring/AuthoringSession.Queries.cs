using System.Text.Json;
using ByteEngine.Core.Assets;

namespace ByteEngine.Mcp.Authoring;

public sealed partial class AuthoringSession
{
    private static readonly HashSet<string> ReadOperations = new(StringComparer.Ordinal)
    {
        "project/info", "project/tags", "project/layers", "project/templates",
        "catalog/help", "catalog/components", "catalog/component", "catalog/conditions", "catalog/actions", "catalog/asset_types",
        "asset/find", "asset/inspect", "scene/list", "scene/inspect", "scene/inspect_object", "scene/find_objects",
        "blueprint/list", "blueprint/inspect", "logic/list", "logic/inspect", "logic/inspect_rule",
        "plugin/list", "changes/status"
    };

    private object BatchRead(JsonElement? args)
    {
        JsonElement requests = Request.Get(args, "requests");
        if (requests.ValueKind != JsonValueKind.Array || requests.GetArrayLength() is < 1 or > 16)
            throw new McpFault("INVALID_REQUEST", "Provide requests: 1..16 {tool, op, args} objects. Read-only; no nested batches.");
        // Validate the entire envelope before executing any request.
        foreach (JsonElement request in requests.EnumerateArray())
        {
            string tool = Request.Required(request, "tool");
            string op = Request.Required(request, "op");
            if (!tool.StartsWith("be_", StringComparison.Ordinal) || !ReadOperations.Contains($"{tool[3..]}/{op}"))
                throw new McpFault("INVALID_REQUEST", $"{tool}/{op} is not available in batch_read.");
        }
        var results = new List<object>();
        bool success = true;
        foreach (JsonElement request in requests.EnumerateArray())
        {
            string tool = Request.Required(request, "tool");
            string op = Request.Required(request, "op");
            using JsonDocument document = JsonDocument.Parse(Dispatch(tool[3..], op, Request.Get(request, "args")));
            JsonElement result = document.RootElement.Clone();
            success &= result.GetProperty("ok").GetBoolean();
            results.Add(new { tool, op, result });
        }
        return new { ok = success, results };
    }

    private object FindSceneObjects(AssetRecord asset, JsonElement? args)
    {
        string search = Request.String(args, "search") ?? "";
        string? component = Request.String(args, "component");
        Type? componentType = component == null ? null : FindComponentType(component)
            ?? throw new McpFault("INVALID_COMPONENT", $"Unknown component '{component}'.");
        int offset = Math.Max(0, Request.Int(args, "offset"));
        int limit = Math.Clamp(Request.Int(args, "limit", 20), 1, 100);
        var scene = Scenes.Load(asset.FullPath);
        var matches = scene.GameObjects.Where(o => o.Name.Contains(search, StringComparison.OrdinalIgnoreCase) &&
            (componentType == null || o.Components.Any(c => componentType.IsInstanceOfType(c))))
            .OrderBy(o => o.Id).ToArray();
        return new
        {
            ok = true, rev = Rev(asset.FullPath), total = matches.Length, offset,
            next_offset = (long)offset + limit < matches.Length ? (int?)(offset + limit) : null,
            objects = matches.Skip(offset).Take(limit).Select(o => new
            {
                id = o.Id, o.Name, parent = o.Parent?.Id, active = o.Active,
                components = o.Components.Select(c => c.GetType().Name).ToArray()
            }).ToArray()
        };
    }

    private static object WorkflowHelp(JsonElement? args)
    {
        string topic = Request.String(args, "topic") ?? "overview";
        string[] steps = topic switch
        {
            "overview" => ["Open an absolute .byteproject with be_project/open, then be_project/info.",
                "Use be_catalog/help topic: scenes, gameplay or debugging for focused recipes.",
                "Combine 1..16 independent reads with be_project/batch_read {requests:[{tool:be_project,op:info,args:{}}]}.",
                "Read-only batch results are ordered and include individual errors. No writes, builds, validation or nested batches.",
                "Revision tokens and compact handles belong to this server session. Exports require explicit user authorization."],
            "scenes" => ["be_scene/list {search,limit}; be_scene/find_objects {scene,search,component,offset,limit}.",
                "find_objects: case-insensitive name substring, optional registered component name/codec; filters combine with AND; default limit 20, maximum 100; stable ID order.",
                "Use next_offset until absent. be_scene/inspect_object {scene,object:<id>} retrieves properties only for the chosen object.",
                "be_catalog/component {name} returns codec and default properties.",
                "be_scene/apply {scene,expected_rev,dry_run,edits:[...]} batches edits; use $aliases after create_object with as.",
                "Edit arguments: create_object {name,parent?,as?}; set_component {target,type,properties}; set_transform {target,position?,rotation?,scale?}; delete_object {target}."],
            "gameplay" => ["be_plugin/list identifies installed packages, source projects and load messages.",
                "be_catalog/components {search,limit}; component {name}; actions or conditions {search,limit} discovers gameplay capabilities and event arguments.",
                "be_asset/find {search,type,offset,limit}; inspect {asset,summary_only:true} locates models and clips.",
                "Edit canonical Plugins/<Game> C# for custom behavior. MCP does not implement arbitrary gameplay code.",
                "be_plugin/build {plugin,install:true}; reopen with be_project/open {path} before querying new codecs.",
                "Follow project-specific version/package/PluginSource rules in AGENTS.md; build/install does not replace those requirements."],
            "debugging" => ["Batch be_project/info, be_plugin/list and be_changes/status for project identity, load warnings and session writes.",
                "be_runtime/validate checks all scenes, Blueprints and Event Modules; run separately after edits.",
                "be_scene/find_objects plus inspect_object narrows serialized-state problems without reading whole scenes.",
                "Engine implementation: Engine/ByteEngine.Core; editor: Editor/ByteEngine.Editor; custom console tests: Tests/ByteEngine.Tests.",
                "Use the named test suites and build/publish rules in MemoryBank/techContext.md and AGENTS.md for engine changes.",
                "MCP has no Play, input injection, screenshots or undo. be_editor/open enables human visual verification; validation does not prove gameplay."],
            _ => throw new McpFault("INVALID_REQUEST", "Topics: overview, scenes, gameplay, debugging.")
        };
        return new { ok = true, topic, steps };
    }
}
