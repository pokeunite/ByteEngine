using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Audio;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Core.Runtime;

/// <summary>Standalone bootstrap. Uses the same codecs and asset identities as editor Play.</summary>
public sealed class GameProjectRuntime : IDisposable
{
    public ProjectData Project { get; }
    public string Root { get; }
    public AssetDatabase Database { get; }
    public AssetManager Assets { get; }
    public SceneSerializer Serializer { get; }
    private readonly Guid _animation;
    private readonly Guid _audio;
    private readonly Guid _spawner;
    private readonly ByteEnginePluginSession _plugins;
    private bool _disposed;

    public GameProjectRuntime(string projectFile, Action<string>? warning = null)
    {
        Project = new ProjectSerializer().Load(projectFile);
        Root = Path.GetDirectoryName(Path.GetFullPath(projectFile))!;
        GamePackageExporter.ResolveInside(Root, Project.AssetDirectory);
        GamePackageExporter.ResolveInside(Root, Project.SceneDirectory);
        GamePackageExporter.ResolveInside(Root, Project.StartupScene);
        if (!File.Exists(Path.Combine(Root, Project.StartupScene)))
            throw new FileNotFoundException($"Startup scene is missing: {Project.StartupScene}");

        _plugins = ByteEnginePluginManager.LoadProjectPlugins(
            Root,
            ByteEnginePluginLoadMode.Runtime,
            warning);

        Database = new AssetDatabase(Root, new[] { Project.AssetDirectory, Project.SceneDirectory },
            warning, warning, readOnly: true);
        Assets = new AssetManager(Database, warning);
        Project.InputMap ??= InputMap.CreateDefault();
        Project.InputMap.EnsureValid();
        InputActions.Configure(Project.InputMap);

        var components = new ComponentSerializer(Root, Database, Assets, warning);
        ByteEnginePluginRegistry.ApplyComponentCodecs(components, warning);

        Serializer = new SceneSerializer(components, Project.Classification);
        SkyEnvironmentExposureSerialization.Register(components);
        PhysicsSerializationRegistrar.Register(components);
        AudioSerializationRegistrar.Register(components);
        AnimationSerializationRegistrar.Register(components);
        _animation = AnimationRuntimeAssets.Configure(Assets);
        _audio = AudioRuntimeAssets.Configure(Database, warning);
        _spawner = RuntimeSpawnService.ConfigureBlueprintSpawner((scene, blueprint, position) =>
            BlueprintRuntimeFactory.Spawn(scene, blueprint, position, Database, Serializer, warning));
    }

    public Scene.Scene LoadStartupScene() =>
        Serializer.Load(GamePackageExporter.ResolveInside(Root, Project.StartupScene));

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        RuntimeSpawnService.ClearBlueprintSpawner(_spawner);
        AnimationRuntimeAssets.Clear(_animation);
        AudioRuntimeAssets.Clear(_audio);
        Assets.Dispose();
        Database.Dispose();
        _plugins.Dispose();
    }
}
