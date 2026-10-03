using ByteEngine.Core.Plugins;
using ByteEngine.Core.Scene;

namespace SpaceScraper.Plugin;

public sealed class SpaceScraperPlugin : IByteEnginePlugin
{
    public void Register(ByteEnginePluginContext context)
    {
        context.RegisterSimpleComponent<SpaceBuilder3D>(
            "SpaceBuilder3D",
            new ByteEnginePluginComponentMetadata(
                "Space Builder 3D",
                "Gameplay",
                "Native Space Scraper construction root.",
                "space scraper ship builder construction"));
    }
}

public sealed class SpaceBuilder3D : Component
{
    public float BuildRadius { get; set; } = 20f;
    public int MaxParts { get; set; } = 150;
    public bool StartInBuildMode { get; set; } = true;

    protected override void OnStart()
    {
        // Space Scraper code goes here without modifying ByteEngine.Core.
    }
}
