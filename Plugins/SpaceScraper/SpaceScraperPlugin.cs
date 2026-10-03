using ByteEngine.Core.Variables;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Core.Scene;

namespace SpaceScraper.Plugin;

public sealed class SpaceScraperPlugin : IByteEnginePlugin
{
    public void Register(ByteEnginePluginContext context)
    {
        context.RegisterSimpleComponent<SpaceBuilder3D>(
            "SpaceBuilder3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Space Builder 3D",
                Category: "Gameplay",
                Description: "Root component for the Space Scraper construction system.",
                SearchKeywords: "space scraper ship builder construction"));
        context.RegisterCondition(new VisualConditionDefinition {
            Id = "bytebard.spacescraper.buildEnabled", Category = "SpaceScraper", DisplayName = "Build Mode Enabled",
            Evaluate = (_, execution) => execution.Self.GetComponent<SpaceBuilder3D>()?.StartInBuildMode == true });
        context.RegisterAction(new VisualActionDefinition {
            Id = "bytebard.spacescraper.setBuildMode", Category = "SpaceScraper", DisplayName = "Set Build Mode",
            Arguments = [new("enabled", "Enabled", VariableValue.FromBoolean(true))],
            Execute = (instruction, execution) => {
                var builder = execution.Self.GetComponent<SpaceBuilder3D>();
                if (builder != null) builder.StartInBuildMode = EventValueResolver.GetBoolean(instruction, "enabled", execution);
            } });

    }
}

public sealed class SpaceBuilder3D : Component
{
    public float BuildRadius { get; set; } = 20.0f;

    public int MaxParts { get; set; } = 150;

    public bool StartInBuildMode { get; set; } = true;

    protected override void OnStart()
    {
        // V0.1 smoke-test component.
        // Real construction logic comes after plugin import/load is proven.
    }
}
