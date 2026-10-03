

using ByteEngine.Core.Plugins;

namespace ByteEngine.Editor;

/// <summary>
/// Keeps plugin component types inside the editor's existing Add Component /
/// Inspector metadata path without changing any built-in component metadata.
/// </summary>
internal static class PluginEditorBridge
{
    private static readonly HashSet<Type> InjectedTypes = new();

    public static void SyncPluginComponents()
    {
        foreach (Type type in InjectedTypes)
            ComponentMetadataRegistry.RemovePlugin(type);

        InjectedTypes.Clear();

        foreach (ByteEnginePluginComponentRegistration registration
                 in ByteEnginePluginRegistry.Components)
        {
            ByteEnginePluginComponentMetadata source = registration.Metadata;

            // ComponentAddMenu currently enumerates a stable built-in category
            // list. Keep V1 additive by mapping custom categories to Gameplay
            // rather than rewriting Codex's menu/metadata system.
            string category =
                ComponentMetadataRegistry.CategoryOrder.Contains(
                    source.Category,
                    StringComparer.OrdinalIgnoreCase)
                    ? ComponentMetadataRegistry.CategoryOrder.First(item =>
                        string.Equals(item, source.Category, StringComparison.OrdinalIgnoreCase))
                    : "Gameplay";

            ComponentMetadataRegistry.RegisterPlugin(registration.ComponentType,
                new ComponentMetadata(
                    source.DisplayName,
                    category,
                    string.IsNullOrWhiteSpace(source.Description)
                        ? $"{source.DisplayName} plugin component."
                        : source.Description,
                    source.SearchKeywords,
                    source.BeginnerVisible,
                    source.Advanced));

            InjectedTypes.Add(registration.ComponentType);
        }
    }
}
