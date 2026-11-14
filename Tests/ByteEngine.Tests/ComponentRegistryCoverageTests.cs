using ByteEngine.Core.Assets;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Editor;

namespace ByteEngine.Tests;

/// <summary>
/// Enforces the single-registry contract introduced for component authoring:
/// the serializer, editor metadata, inspector property pipeline and browser
/// material contract must agree.
/// </summary>
internal static class ComponentRegistryCoverageTests
{
    public static void Run(string root)
    {
        string projectRoot = Path.Combine(root, "RegistryCoverage-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "Assets"));
        Directory.CreateDirectory(Path.Combine(projectRoot, "Scenes"));

        using var database = new AssetDatabase(projectRoot, ["Assets", "Scenes"]);
        using var assets = new AssetManager(database);

        _ = new ComponentSerializer(projectRoot, database, assets);

        IReadOnlyList<Type> registered = ComponentTypeRegistry.RegisteredTypes;
        Assert(registered.Count > 0, "component registry is populated");

        foreach (Type type in registered)
        {
            Assert(typeof(Component).IsAssignableFrom(type), $"registered type derives from Component: {type.FullName}");
            Assert(!string.IsNullOrWhiteSpace(ComponentTypeRegistry.NameOf(type)), $"registered type has a canonical name: {type.FullName}");
        }

        Type[] missingMetadata = ComponentMetadataRegistry.MissingSerializedMetadata().ToArray();
        Assert(missingMetadata.Length == 0,
            "every serialized component has editor metadata: " + string.Join(", ", missingMetadata.Select(t => t.FullName)));

        Type[] metadataWithoutSerializer = ComponentMetadataRegistry.MetadataWithoutSerializer().ToArray();
        Assert(metadataWithoutSerializer.Length == 0,
            "every editor component metadata entry has a serializer codec: " + string.Join(", ", metadataWithoutSerializer.Select(t => t.FullName)));

        Type[] unregistered = ComponentMetadataRegistry
            .UnregisteredComponentTypes(typeof(Component).Assembly)
            .Where(type => type != typeof(Component))
            .ToArray();
        Assert(unregistered.Length == 0,
            "every concrete engine component is serializable: " + string.Join(", ", unregistered.Select(t => t.FullName)));

        foreach (Type type in registered)
        {
            IReadOnlyList<ComponentPropertyDescriptor> descriptors =
                ComponentPropertyRenderer.Descriptors(type, PropertyEditorContext.Scene);
            Assert(descriptors != null, $"inspector descriptors resolve for {type.FullName}");
        }

        BrowserRenderContract.ValidateCoverage();

        foreach ((string alias, string canonical) in ComponentTypeRegistry.RegisteredAliases)
        {
            Assert(ComponentTypeRegistry.TryResolve(alias, out Type resolved), $"alias resolves: {alias}");
            Assert(ComponentTypeRegistry.NameOf(resolved) == canonical,
                $"alias points at its canonical type: {alias} -> {canonical}");
        }

        Console.WriteLine("Component registry coverage checks passed.");
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
    }
}
