using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Mcp.Authoring;

public sealed partial class AuthoringSession : IDisposable
{
    private static readonly JsonSerializerOptions CompactJson = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    private readonly object _gate = new();
    private readonly HandleTable _handles = new();
    private readonly Dictionary<string, string> _changed = new(StringComparer.OrdinalIgnoreCase);
    private readonly ProjectSerializer _projects = new();
    private readonly BlueprintSerializer _blueprints = new();
    private readonly EventModuleSerializer _modules = new();
    private readonly VisualLogicRegistry _logic = VisualLogicRegistry.CreateDefault();
    private ProjectData? _project;
    private string? _projectFile;
    private string? _root;
    private AssetDatabase? _database;
    private AssetManager? _assets;
    private ComponentSerializer? _components;
    private SceneSerializer? _scenes;

    internal ProjectData Project => _project ?? throw new McpFault("PROJECT_NOT_OPEN");
    internal string Root => _root ?? throw new McpFault("PROJECT_NOT_OPEN");
    internal AssetDatabase Database => _database ?? throw new McpFault("PROJECT_NOT_OPEN");
    internal AssetManager Assets => _assets ?? throw new McpFault("PROJECT_NOT_OPEN");
    internal ComponentSerializer Components => _components ?? throw new McpFault("PROJECT_NOT_OPEN");
    internal SceneSerializer Scenes => _scenes ?? throw new McpFault("PROJECT_NOT_OPEN");
    internal BlueprintSerializer Blueprints => _blueprints;
    internal EventModuleSerializer Modules => _modules;
    internal VisualLogicRegistry LogicRegistry => _logic;
    internal HandleTable Handles => _handles;

    public string Dispatch(string domain, string op, JsonElement? args = null)
    {
        lock (_gate)
        {
            try
            {
                object result = domain.ToLowerInvariant() switch
                {
                    "project" => ProjectTool(op, args),
                    "catalog" => CatalogTool(op, args),
                    "asset" => AssetTool(op, args),
                    "scene" => SceneTool(op, args),
                    "blueprint" => BlueprintTool(op, args),
                    "logic" => LogicTool(op, args),
                    "runtime" => RuntimeTool(op, args),
                    "changes" => ChangesTool(op, args),
                    _ => throw new McpFault("UNSUPPORTED_CAPABILITY")
                };
                return JsonSerializer.Serialize(result, CompactJson);
            }
            catch (McpFault fault)
            {
                return JsonSerializer.Serialize(new { ok = false, code = fault.Code, hint = fault.Hint }, CompactJson);
            }
            catch (Exception exception)
            {
                return JsonSerializer.Serialize(new { ok = false, code = "IO_ERROR", hint = exception.Message }, CompactJson);
            }
        }
    }

    internal void Open(string path)
    {
        if (!Path.IsPathFullyQualified(path) || !path.EndsWith(".byteproject", StringComparison.OrdinalIgnoreCase))
            throw new McpFault("INVALID_REQUEST", "Use an absolute .byteproject path.");
        string full = Path.GetFullPath(path);
        if (!File.Exists(full)) throw new McpFault("NOT_FOUND", "Project file does not exist.");
        ProjectData project = _projects.Load(full);
        string root = Path.GetDirectoryName(full)!;
        AssetDatabase database = new(root, [project.AssetDirectory, project.SceneDirectory]);
        AssetManager assets = new(database);
        ComponentSerializer components = new(root, database, assets);
        SceneSerializer scenes = new(components, project.Classification);
        SkyEnvironmentExposureSerialization.Register(components);
        PhysicsSerializationRegistrar.Register(components);
        AudioSerializationRegistrar.Register(components);
        AnimationSerializationRegistrar.Register(components);
        _assets?.Dispose();
        _database?.Dispose();
        _project = project;
        _projectFile = full;
        _root = root;
        _database = database;
        _assets = assets;
        _components = components;
        _scenes = scenes;
        _handles.Clear();
        _changed.Clear();
    }

    internal string Inside(string relative, string? extension = null)
    {
        if (Path.IsPathRooted(relative) || relative.Split('/', '\\').Contains(".."))
            throw new McpFault("INVALID_REQUEST", "Use a project-relative path without '..'.");
        string full = Database.ResolveProjectPath(relative);
        if (extension != null && !full.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            throw new McpFault("INVALID_REQUEST", $"Expected {extension}.");
        return full;
    }
    internal string Relative(string full) => Path.GetRelativePath(Root, full).Replace('\\', '/');
    internal static string Rev(string full)
    {
        if (!File.Exists(full)) return "new";
        using var stream = File.Open(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        return Convert.ToHexString(SHA256.HashData(stream))[..12].ToLowerInvariant();
    }
    internal string CheckRevision(string full, JsonElement? args)
    {
        string current = Rev(full);
        string? expected = Request.String(args, "expected_rev");
        if (expected == null) throw new McpFault("CONFLICT", $"Current revision: {current}. Inspect first.");
        if (!string.Equals(expected, current, StringComparison.OrdinalIgnoreCase))
            throw new McpFault("CONFLICT", $"Current revision: {current}.");
        return current;
    }
    internal void MarkChanged(string full) => _changed[Relative(full)] = Rev(full);
    internal AssetRecord Asset(string input, AssetType? required = null)
    {
        char prefix = input.Length > 1 && char.IsAsciiDigit(input[1]) ? input[0] : 'a';
        string key = _handles.Resolve(prefix, input);
        AssetRecord? record = Guid.TryParse(key, out Guid id) && Database.TryGetAsset(id, out var byId)
            ? byId : Database.TryGetAsset(key, out var byPath) ? byPath : null;
        if (record == null) throw new McpFault("NOT_FOUND", "Find the asset with be_asset.");
        if (required.HasValue && record.Type != required) throw new McpFault("INVALID_ASSET_TYPE");
        return record;
    }
    internal string AssetHandle(AssetRecord asset) =>
        _handles.Get(asset.Type == AssetType.Blueprint ? 'b' : asset.Type == AssetType.EventModule ? 'e' : 'a',
            asset.Guid.ToString());

    private object ChangesTool(string op, JsonElement? args) => op switch
    {
        "status" => new { ok = true, dirty = _changed.Select(x => new { path = x.Key, rev = x.Value }).ToArray() },
        _ => throw new McpFault("UNSUPPORTED_CAPABILITY", "Undo is not available in V1.")
    };

    public void Dispose()
    {
        _assets?.Dispose();
        _database?.Dispose();
    }
}
