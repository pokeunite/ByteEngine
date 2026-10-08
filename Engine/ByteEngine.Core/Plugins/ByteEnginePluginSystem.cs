using System.Numerics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using System.Text.Json.Nodes;

using ByteEngine.Core.Assets;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Core.Plugins;

/// <summary>
/// Project-local trusted-code plugin entry point.
/// A plugin is loaded from &lt;Project&gt;/Plugins/**/plugin.json.
/// </summary>
public interface IByteEnginePlugin
{
    void Register(ByteEnginePluginContext context);

    void Shutdown()
    {
    }
}

public enum ByteEnginePluginLoadMode
{
    Editor,
    Runtime
}

public sealed class ByteEnginePluginManifest
{
    public int SchemaVersion { get; set; } = 1;
    public string? MinimumEngineVersion { get; set; }
    public string? MaximumEngineVersion { get; set; }
    public List<ByteEnginePluginDependency> Dependencies { get; set; } = new();
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = "0.1.0";
    public string Assembly { get; set; } = string.Empty;
    public string EntryPoint { get; set; } = string.Empty;
    public bool Editor { get; set; } = true;
    public bool Runtime { get; set; } = true;
}

public sealed record ByteEnginePluginDependency
{
    public string Id { get; set; } = "";
    public string? MinimumVersion { get; set; }
}

public sealed record ByteEnginePluginComponentMetadata(
    string DisplayName,
    string Category = "Gameplay",
    string Description = "",
    string SearchKeywords = "",
    bool BeginnerVisible = true,
    bool Advanced = false);

public sealed record ByteEnginePluginComponentRegistration(
    string PluginId,
    Type ComponentType,
    IComponentCodec Codec,
    ByteEnginePluginComponentMetadata Metadata);

/// <summary>
/// Process-local registrations contributed by the currently loaded project plugins.
/// Built-in ByteEngine registrations are never removed or replaced.
/// </summary>
public static class ByteEnginePluginRegistry
{
    private sealed class PluginRegistration
    {
        public readonly List<ByteEnginePluginComponentRegistration> Components = new();
        public readonly List<VisualActionDefinition> Actions = new();
        public readonly List<VisualConditionDefinition> Conditions = new();
        public readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase);
    }

    private static int _revision;
    public static int Revision => System.Threading.Volatile.Read(ref _revision);
    private static readonly object Sync = new();
    private static readonly Dictionary<string, PluginRegistration> ByPlugin =
        new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<ByteEnginePluginComponentRegistration> Components
    {
        get
        {
            lock (Sync)
                return ByPlugin.Values.SelectMany(item => item.Components).ToArray();
        }
    }

    internal static void RegisterAction(string id, VisualActionDefinition action)
    {
        lock (Sync)
        {
            if (!action.Id.StartsWith(id + ".", StringComparison.OrdinalIgnoreCase) ||
                ByPlugin.Values.SelectMany(p => p.Actions).Any(a => a.Id.Equals(action.Id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Plugin action must have a unique plugin-prefixed id.");
            ByPlugin[id].Actions.Add(action);
        }
    }
    internal static void RegisterCondition(string id, VisualConditionDefinition condition)
    {
        lock (Sync)
        {
            if (!condition.Id.StartsWith(id + ".", StringComparison.OrdinalIgnoreCase) ||
                ByPlugin.Values.SelectMany(p => p.Conditions).Any(a => a.Id.Equals(condition.Id, StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("Plugin condition must have a unique plugin-prefixed id.");
            ByPlugin[id].Conditions.Add(condition);
        }
    }
    internal static void RegisterAlias(string id, string alias, string canonical)
    {
        lock (Sync)
        {
            if (string.IsNullOrWhiteSpace(alias) || !ByPlugin[id].Components.Any(c => c.Codec.TypeName == canonical) ||
                ByPlugin.Values.Any(p => p.Aliases.ContainsKey(alias)))
                throw new InvalidOperationException("Invalid or duplicate serialized component alias.");
            ByPlugin[id].Aliases.Add(alias, canonical);
        }
    }
    public static void ApplyVisualLogic(VisualLogicRegistry registry)
    {
        lock (Sync)
            foreach (var plugin in ByPlugin.Values)
            {
                foreach (var action in plugin.Actions) if (!registry.TryGetAction(action.Id, out _)) registry.RegisterAction(action);
                foreach (var condition in plugin.Conditions) if (!registry.TryGetCondition(condition.Id, out _)) registry.RegisterCondition(condition);
            }
    }

    internal static bool ContainsPlugin(string pluginId)
    {
        lock (Sync)
            return ByPlugin.ContainsKey(pluginId);
    }

    internal static void BeginPlugin(string pluginId)
    {
        lock (Sync)
        {
            if (ByPlugin.ContainsKey(pluginId))
                throw new InvalidOperationException($"Plugin '{pluginId}' is already registered.");

            ByPlugin.Add(pluginId, new PluginRegistration());
            System.Threading.Interlocked.Increment(ref _revision);
        }
    }

    internal static void RemovePlugin(string pluginId)
    {
        lock (Sync)
        {
            if (ByPlugin.Remove(pluginId)) System.Threading.Interlocked.Increment(ref _revision);
        }
    }

    internal static void RegisterComponent(
        string pluginId,
        IComponentCodec codec,
        ByteEnginePluginComponentMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(codec);
        ArgumentNullException.ThrowIfNull(metadata);

        if (!typeof(Component).IsAssignableFrom(codec.ComponentType))
            throw new InvalidOperationException(
                $"Plugin component '{codec.ComponentType.FullName}' does not derive from ByteEngine Component.");

        if (codec.ComponentType.Assembly == typeof(Component).Assembly)
            throw new InvalidOperationException("Plugins cannot replace built-in engine components.");

        if (codec.ComponentType.IsAbstract)
            throw new InvalidOperationException(
                $"Plugin component '{codec.ComponentType.FullName}' cannot be abstract.");

        string prefix = pluginId + ".";
        if (!codec.TypeName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Plugin codec type name '{codec.TypeName}' must start with '{prefix}'.");

        lock (Sync)
        {
            if (!ByPlugin.TryGetValue(pluginId, out PluginRegistration? registration))
                throw new InvalidOperationException($"Plugin '{pluginId}' is not being registered.");

            if (ByPlugin.Values.SelectMany(item => item.Components)
                .Any(item => item.ComponentType == codec.ComponentType))
            {
                throw new InvalidOperationException(
                    $"Component type '{codec.ComponentType.FullName}' is already registered by a plugin.");
            }

            if (ByPlugin.Values.SelectMany(item => item.Components)
                .Any(item => string.Equals(item.Codec.TypeName, codec.TypeName,
                    StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException(
                    $"Serialized plugin component name '{codec.TypeName}' is already registered.");
            }

            registration.Components.Add(new ByteEnginePluginComponentRegistration(
                pluginId, codec.ComponentType, codec, metadata));
        }
    }

    /// <summary>
    /// Applies plugin codecs after ByteEngine built-ins have registered.
    /// Existing built-in component runtime types are deliberately protected.
    /// </summary>
    public static void ApplyComponentCodecs(
        ComponentSerializer serializer,
        Action<string>? warningSink = null)
    {
        ArgumentNullException.ThrowIfNull(serializer);

        foreach (ByteEnginePluginComponentRegistration registration in Components)
        {
            if (serializer.RegisteredComponentTypes.Contains(registration.ComponentType))
            {
                warningSink?.Invoke(
                    $"Plugin '{registration.PluginId}' attempted to replace built-in component " +
                    $"'{registration.ComponentType.FullName}'. The plugin registration was ignored.");
                continue;
            }

            serializer.Register(registration.Codec);
            lock (Sync)
                foreach (var alias in ByPlugin[registration.PluginId].Aliases.Where(a => a.Value == registration.Codec.TypeName))
                {
                    try { serializer.RegisterAlias(alias.Key, registration.Codec.TypeName); }
                    catch (InvalidOperationException e) { warningSink?.Invoke($"Plugin '{registration.PluginId}' alias '{alias.Key}' ignored: {e.Message}"); }
                }
        }
    }
}

/// <summary>
/// Registration surface intentionally kept small for Plugin System V1.
/// </summary>
public sealed class ByteEnginePluginContext
{
    public string PluginId { get; }

    internal ByteEnginePluginContext(string pluginId)
    {
        PluginId = pluginId;
    }

    public void RegisterComponent(
        IComponentCodec codec,
        ByteEnginePluginComponentMetadata metadata)
    {
        ByteEnginePluginRegistry.RegisterComponent(PluginId, codec, metadata);
    }

    public void RegisterAction(VisualActionDefinition definition) => ByteEnginePluginRegistry.RegisterAction(PluginId, definition);
    public void RegisterCondition(VisualConditionDefinition definition) => ByteEnginePluginRegistry.RegisterCondition(PluginId, definition);
    public void RegisterSerializedAlias(string legacyName, string canonicalName) => ByteEnginePluginRegistry.RegisterAlias(PluginId, legacyName, canonicalName);

    /// <summary>
    /// Registers a component whose authorable state consists only of the normal
    /// ByteEngine inspector-friendly scalar/vector/enum/AssetReference types.
    /// </summary>
    public void RegisterSimpleComponent<T>(
        string serializedName,
        ByteEnginePluginComponentMetadata metadata)
        where T : Component, new()
    {
        if (string.IsNullOrWhiteSpace(serializedName))
            throw new ArgumentException("Serialized component name cannot be empty.", nameof(serializedName));

        string safeName = serializedName.Trim();
        if (safeName.Contains('.') || safeName.Contains(':'))
            throw new ArgumentException(
                "RegisterSimpleComponent expects a short serialized name without namespace separators.",
                nameof(serializedName));

        RegisterComponent(
            new ReflectionPluginComponentCodec<T>($"{PluginId}.{safeName}"),
            metadata);
    }
}

/// <summary>
/// Small reflection codec for ordinary plugin components.
/// Complex component state can still provide a custom IComponentCodec.
/// </summary>
public sealed class ReflectionPluginComponentCodec<T> : IComponentCodec
    where T : Component, new()
{
    private static readonly PropertyInfo[] Properties =
        typeof(T)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property =>
                property.GetIndexParameters().Length == 0 &&
                property.GetMethod?.IsPublic == true &&
                property.SetMethod?.IsPublic == true &&
                property.DeclaringType != typeof(Component) &&
                property.Name is not ("UpdateOrder" or "RenderOrder") &&
                Supported(property.PropertyType))
            .ToArray();

    public string TypeName { get; }
    public Type ComponentType => typeof(T);

    public ReflectionPluginComponentCodec(string typeName)
    {
        TypeName = !string.IsNullOrWhiteSpace(typeName)
            ? typeName
            : throw new ArgumentException("Plugin component type name cannot be empty.", nameof(typeName));
    }

    public ComponentData Serialize(Component component, ComponentSerializationContext context)
    {
        var typed = (T)component;
        var properties = new JsonObject();

        foreach (PropertyInfo property in Properties)
            properties[ToCamel(property.Name)] = WriteValue(property.GetValue(typed));

        return new ComponentData
        {
            Type = TypeName,
            Properties = properties
        };
    }

    public Component Deserialize(ComponentData data, ComponentSerializationContext context)
    {
        var component = new T();

        foreach (PropertyInfo property in Properties)
        {
            JsonNode? node = data.Properties[ToCamel(property.Name)];
            if (node == null)
                continue;

            if (TryReadValue(node, property.PropertyType, context, out object? value))
                property.SetValue(component, value);
        }

        return component;
    }

    private static bool Supported(Type type) =>
        type == typeof(bool) ||
        type == typeof(int) ||
        type == typeof(float) ||
        type == typeof(double) ||
        type == typeof(string) ||
        type == typeof(Guid) ||
        type == typeof(Vector2) ||
        type == typeof(Vector3) ||
        type == typeof(Vector4) ||
        type == typeof(AssetReference) ||
        type.IsEnum;

    private static JsonNode? WriteValue(object? value)
    {
        return value switch
        {
            null => null,
            bool boolean => JsonValue.Create(boolean),
            int integer => JsonValue.Create(integer),
            float number => JsonValue.Create(float.IsFinite(number) ? number : 0f),
            double number => JsonValue.Create(double.IsFinite(number) ? number : 0d),
            string text => JsonValue.Create(text),
            Guid guid => JsonValue.Create(guid.ToString()),
            Vector2 vector => new JsonArray(vector.X, vector.Y),
            Vector3 vector => new JsonArray(vector.X, vector.Y, vector.Z),
            Vector4 vector => new JsonArray(vector.X, vector.Y, vector.Z, vector.W),
            AssetReference reference => new JsonObject
            {
                ["guid"] = reference.Guid.ToString(),
                ["path"] = reference.CachedProjectPath
            },
            Enum enumeration => JsonValue.Create(enumeration.ToString()),
            _ => null
        };
    }

    private static bool TryReadValue(
        JsonNode node,
        Type type,
        ComponentSerializationContext context,
        out object? value)
    {
        value = null;

        try
        {
            if (type == typeof(bool)) value = node.GetValue<bool>();
            else if (type == typeof(int)) value = node.GetValue<int>();
            else if (type == typeof(float)) value = node.GetValue<float>();
            else if (type == typeof(double)) value = node.GetValue<double>();
            else if (type == typeof(string)) value = node.GetValue<string>();
            else if (type == typeof(Guid))
            {
                if (!Guid.TryParse(node.GetValue<string>(), out Guid guid))
                    return false;
                value = guid;
            }
            else if (type == typeof(Vector2) && node is JsonArray v2 && v2.Count >= 2)
                value = new Vector2(v2[0]?.GetValue<float>() ?? 0, v2[1]?.GetValue<float>() ?? 0);
            else if (type == typeof(Vector3) && node is JsonArray v3 && v3.Count >= 3)
                value = new Vector3(v3[0]?.GetValue<float>() ?? 0, v3[1]?.GetValue<float>() ?? 0,
                    v3[2]?.GetValue<float>() ?? 0);
            else if (type == typeof(Vector4) && node is JsonArray v4 && v4.Count >= 4)
                value = new Vector4(v4[0]?.GetValue<float>() ?? 0, v4[1]?.GetValue<float>() ?? 0,
                    v4[2]?.GetValue<float>() ?? 0, v4[3]?.GetValue<float>() ?? 0);
            else if (type == typeof(AssetReference) && node is JsonObject asset)
            {
                Guid.TryParse(asset["guid"]?.GetValue<string>(), out Guid guid);
                string? path = asset["path"]?.GetValue<string>();
                value = guid != Guid.Empty
                    ? new AssetReference(guid, path)
                    : !string.IsNullOrWhiteSpace(path)
                        ? context.AssetDatabase.ResolveReference(path)
                        : AssetReference.Empty;
            }
            else if (type.IsEnum)
            {
                string name = node.GetValue<string>();
                if (!Enum.TryParse(type, name, true, out object? parsed))
                    return false;
                value = parsed;
            }
            else
                return false;

            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static string ToCamel(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];
}

public sealed record LoadedByteEnginePlugin(
    ByteEnginePluginManifest Manifest,
    string Directory,
    IByteEnginePlugin Instance);

/// <summary>
/// One project's plugin lifetime. Assemblies intentionally remain loaded for the
/// process lifetime in V1; registrations and plugin state are still removed when
/// the project closes.
/// </summary>
public sealed class ByteEnginePluginSession : IDisposable
{
    private readonly List<LoadedByteEnginePlugin> _plugins;
    private bool _disposed;

    internal ByteEnginePluginSession(List<LoadedByteEnginePlugin> plugins)
    {
        _plugins = plugins;
    }

    public IReadOnlyList<LoadedByteEnginePlugin> LoadedPlugins => _plugins;

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        for (int index = _plugins.Count - 1; index >= 0; index--)
        {
            LoadedByteEnginePlugin plugin = _plugins[index];

            try
            {
                plugin.Instance.Shutdown();
            }
            catch
            {
                // Project shutdown must not be blocked by plugin cleanup.
            }

            ByteEnginePluginRegistry.RemovePlugin(plugin.Manifest.Id);
        }

        _plugins.Clear();
    }
}

public static class ByteEnginePluginManager
{
    private static readonly JsonSerializerOptions ManifestJson = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    /// <summary>Register a statically linked browser plugin. Browser WASM cannot load desktop assemblies.</summary>
    public static void RegisterLinkedPlugin(string id, IByteEnginePlugin plugin){ByteEnginePluginRegistry.BeginPlugin(id);try{plugin.Register(new ByteEnginePluginContext(id));}catch{ByteEnginePluginRegistry.RemovePlugin(id);throw;}}

    public static ByteEnginePluginSession LoadProjectPlugins(
        string projectRoot,
        ByteEnginePluginLoadMode mode,
        Action<string>? warningSink = null)
    {
        projectRoot = Path.GetFullPath(projectRoot);

        string pluginsRoot = Path.Combine(projectRoot, "Plugins");
        var loaded = new List<LoadedByteEnginePlugin>();

        if (!Directory.Exists(pluginsRoot))
            return new ByteEnginePluginSession(loaded);

        if (OperatingSystem.IsBrowser())
        {
            warningSink?.Invoke(
                "Browser runtime uses statically linked plugins.");
            return new ByteEnginePluginSession(loaded);
        }

        var installations = ByteEnginePluginPackageManager.ListInstalled(projectRoot);
        var pending = installations.Where(p => p.Enabled).OrderBy(p => p.Id, StringComparer.OrdinalIgnoreCase).ToList();
        while (pending.Count > 0)
        {
            bool progressed = false;
            foreach (var installation in pending.ToArray())
            {
                ByteEnginePluginManifest candidate;
                try { candidate = ByteEnginePluginPackageManager.ReadManifest(File.ReadAllText(installation.ManifestPath)); }
                catch (Exception e) { warningSink?.Invoke($"Invalid plugin '{installation.Id}': {e.Message}"); pending.Remove(installation); progressed = true; continue; }
                if (candidate.Dependencies.Any(d => pending.Any(p => p.Id.Equals(d.Id, StringComparison.OrdinalIgnoreCase)))) continue;
                pending.Remove(installation); progressed = true;
                LoadInstallation(installation, candidate);
            }
            if (!progressed)
            {
                foreach (var item in pending) warningSink?.Invoke($"Plugin '{item.Id}' has cyclic dependencies and was skipped.");
                break;
            }
        }
        return new ByteEnginePluginSession(loaded);

        void LoadInstallation(InstalledByteEnginePlugin installation, ByteEnginePluginManifest candidate)
        {
            string manifestFile = installation.ManifestPath;
            string pluginDirectory = Path.GetDirectoryName(Path.GetFullPath(manifestFile))!;

            try
            {
                ByteEnginePluginManifest manifest =
                    JsonSerializer.Deserialize<ByteEnginePluginManifest>(
                        File.ReadAllText(manifestFile), ManifestJson)
                    ?? throw new InvalidDataException("Plugin manifest is empty.");

                ValidateManifest(manifest);
                Version engineVersion = Version.Parse(ByteEngineInfo.Version);
                if (manifest.MinimumEngineVersion != null && (!Version.TryParse(manifest.MinimumEngineVersion, out var minimumEngine) || engineVersion < minimumEngine))
                    throw new InvalidDataException($"Requires engine {manifest.MinimumEngineVersion}; installed engine is {engineVersion}.");
                if (manifest.MaximumEngineVersion != null && (!Version.TryParse(manifest.MaximumEngineVersion, out var maximumEngine) || engineVersion > maximumEngine))
                    throw new InvalidDataException($"Supports engine through {manifest.MaximumEngineVersion}; installed engine is {engineVersion}.");
                foreach (var dependency in manifest.Dependencies)
                {
                    var found = loaded.FirstOrDefault(p => p.Manifest.Id.Equals(dependency.Id, StringComparison.OrdinalIgnoreCase));
                    if (found == null) throw new InvalidDataException($"Dependency '{dependency.Id}' is missing, disabled or failed to load.");
                    if (dependency.MinimumVersion != null && (!Version.TryParse(dependency.MinimumVersion, out var minimum) || !Version.TryParse(found.Manifest.Version, out var actual) || actual < minimum))
                        throw new InvalidDataException($"Dependency '{dependency.Id}' requires version {dependency.MinimumVersion}.");
                }

                if (mode == ByteEnginePluginLoadMode.Runtime && !manifest.Runtime)
                    return;

                if (mode == ByteEnginePluginLoadMode.Editor && !manifest.Editor && !manifest.Runtime)
                    return;

                if (ByteEnginePluginRegistry.ContainsPlugin(manifest.Id))
                {
                    warningSink?.Invoke(
                        $"Plugin '{manifest.Id}' is already loaded. Duplicate manifest '{manifestFile}' was skipped.");
                    return;
                }

                string assemblyPath = ResolveInsidePlugin(pluginDirectory, manifest.Assembly);

                if (!File.Exists(assemblyPath))
                    throw new FileNotFoundException(
                        $"Plugin assembly was not found: {manifest.Assembly}", assemblyPath);

                var dependencies = loaded.Where(p => manifest.Dependencies.Any(d => d.Id.Equals(p.Manifest.Id, StringComparison.OrdinalIgnoreCase)))
                    .Select(p => p.Instance.GetType().Assembly);
                var loadContext = new ProjectPluginLoadContext(assemblyPath, dependencies);
                // Embedded hosts and diagnostics may already load this exact assembly in
                // the default context. Reuse only a byte-identical image to preserve type identity.
                Assembly? shared = AssemblyLoadContext.Default.Assemblies.FirstOrDefault(a =>
                    !a.IsDynamic && a.GetName().Name == AssemblyName.GetAssemblyName(assemblyPath).Name &&
                    !string.IsNullOrWhiteSpace(a.Location) && SameAssemblyImage(a.Location, assemblyPath));
                Assembly assembly = shared ?? loadContext.LoadFromAssemblyPath(assemblyPath);

                Type? entryType = ResolveEntryPoint(assembly, manifest.EntryPoint);
                if (entryType == null)
                    throw new InvalidDataException(
                        string.IsNullOrWhiteSpace(manifest.EntryPoint)
                            ? "Plugin assembly contains no concrete IByteEnginePlugin entry point."
                            : $"Plugin entry point '{manifest.EntryPoint}' was not found.");

                if (Activator.CreateInstance(entryType) is not IByteEnginePlugin plugin)
                    throw new InvalidDataException(
                        $"Plugin entry point '{entryType.FullName}' could not be created.");

                ByteEnginePluginRegistry.BeginPlugin(manifest.Id);

                try
                {
                    plugin.Register(new ByteEnginePluginContext(manifest.Id));
                }
                catch
                {
                    ByteEnginePluginRegistry.RemovePlugin(manifest.Id);
                    try { plugin.Shutdown(); } catch { }
                    throw;
                }

                loaded.Add(new LoadedByteEnginePlugin(manifest, pluginDirectory, plugin));

                warningSink?.Invoke(
                    $"Loaded ByteEngine plugin '{manifest.Name}' ({manifest.Id}) v{manifest.Version}.");
            }
            catch (Exception error)
            {
                warningSink?.Invoke(
                    $"Could not load ByteEngine plugin '{manifestFile}': {error.Message}");
            }
        }

    }

    private static bool SameAssemblyImage(string first, string second)
    {
        using var a = File.OpenRead(first); using var b = File.OpenRead(second);
        return a.Length == b.Length && System.Security.Cryptography.SHA256.HashData(a).AsSpan()
            .SequenceEqual(System.Security.Cryptography.SHA256.HashData(b));
    }

    private static void ValidateManifest(ByteEnginePluginManifest manifest)
    {
        if (string.IsNullOrWhiteSpace(manifest.Id) ||
            manifest.Id.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '.' or '-' or '_')))
        {
            throw new InvalidDataException(
                "Plugin id must contain only letters, digits, '.', '-' or '_'.");
        }

        if (string.IsNullOrWhiteSpace(manifest.Name))
            manifest.Name = manifest.Id;

        if (string.IsNullOrWhiteSpace(manifest.Assembly))
            throw new InvalidDataException("Plugin manifest must specify an assembly.");
    }

    private static Type? ResolveEntryPoint(Assembly assembly, string configured)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            Type? exact = assembly.GetType(configured, false, false);
            if (exact != null &&
                !exact.IsAbstract &&
                typeof(IByteEnginePlugin).IsAssignableFrom(exact))
                return exact;

            return null;
        }

        return assembly
            .GetTypes()
            .FirstOrDefault(type =>
                type.IsClass &&
                !type.IsAbstract &&
                type.GetConstructor(Type.EmptyTypes) != null &&
                typeof(IByteEnginePlugin).IsAssignableFrom(type));
    }

    private static string ResolveInsidePlugin(string pluginDirectory, string relativePath)
    {
        if (Path.IsPathRooted(relativePath))
            throw new InvalidDataException("Plugin assembly path must be relative to its plugin directory.");

        string root = Path.GetFullPath(pluginDirectory);
        string full = Path.GetFullPath(Path.Combine(root,
            relativePath.Replace('/', Path.DirectorySeparatorChar)));

        string prefix = Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar;
        if (!full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Plugin assembly path escapes its plugin directory.");

        return full;
    }

    private sealed class ProjectPluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;
        private readonly string _directory;
        private readonly Dictionary<string, Assembly> _dependencies;

        public ProjectPluginLoadContext(string pluginAssembly, IEnumerable<Assembly> dependencies)
            : base($"ByteEnginePlugin:{Path.GetFileNameWithoutExtension(pluginAssembly)}:{Guid.NewGuid():N}",
                isCollectible: false)
        {
            _resolver = new AssemblyDependencyResolver(pluginAssembly);
            _directory = Path.GetDirectoryName(pluginAssembly)!;
            _dependencies = dependencies.GroupBy(a => a.GetName().Name!, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // ByteEngine.Core must always resolve from the host/default context so
            // plugin Component/IComponentCodec identity matches the running engine.
            string coreName = typeof(IByteEnginePlugin).Assembly.GetName().Name ?? "ByteEngine.Core";
            if (string.Equals(assemblyName.Name, coreName, StringComparison.OrdinalIgnoreCase))
                return null;

            if (assemblyName.Name != null && _dependencies.TryGetValue(assemblyName.Name, out var dependency))
                return dependency;
            string? path = _resolver.ResolveAssemblyToPath(assemblyName);
            if (path == null && !string.IsNullOrWhiteSpace(assemblyName.Name))
            {
                string local = ResolveInsidePlugin(_directory, assemblyName.Name + ".dll");
                if (File.Exists(local)) path = local;
            }
            return path == null ? null : LoadFromAssemblyPath(path);
        }

        protected override nint LoadUnmanagedDll(string unmanagedDllName)
        {
            string? path = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path == null ? nint.Zero : LoadUnmanagedDllFromPath(path);
        }
    }
}
