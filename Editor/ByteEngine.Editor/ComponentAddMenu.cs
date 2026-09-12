using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Scene;
using ImGuiNET;

namespace ByteEngine.Editor;

internal static class ComponentAddMenu
{
    public static void Draw(GameObject target, string search, Action<Component, string> add)
    {
        IEnumerable<Type> candidates = ComponentMetadataRegistry.RegisteredTypes
            .Where(type => type != typeof(BlueprintInstance) && typeof(Component).IsAssignableFrom(type))
            .Where(type => ComponentMetadataRegistry.Matches(type, search));

        if (!string.IsNullOrWhiteSpace(search))
        {
            foreach (Type type in candidates.OrderBy(type => ComponentMetadataRegistry.DisplayName(type)))
                DrawItem(target, type, add);
            return;
        }

        foreach (string category in ComponentMetadataRegistry.CategoryOrder)
        {
            Type[] entries = candidates
                .Where(type => ComponentMetadataRegistry.Get(type).Category == category)
                .OrderBy(type => ComponentMetadataRegistry.DisplayName(type))
                .ToArray();
            if (!ImGui.BeginMenu(category)) continue;
            if (entries.Length == 0)
                ImGui.MenuItem("No components available", string.Empty, false, false);
            foreach (Type type in entries) DrawItem(target, type, add);
            ImGui.EndMenu();
        }
    }

    private static void DrawItem(GameObject target, Type type, Action<Component, string> add)
    {
        ComponentMetadata metadata = ComponentMetadataRegistry.Get(type);
        bool exists = target.Components.Any(component => component.GetType() == type);
        if (ImGui.MenuItem(metadata.DisplayName, string.Empty, false, !exists) &&
            Activator.CreateInstance(type) is Component component)
        {
            add(component, metadata.DisplayName);
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip(exists ? "This component is already attached." : metadata.Description);
    }
}
