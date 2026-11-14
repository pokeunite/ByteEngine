using ByteEngine.Core.Plugins;

namespace Wildlings;

/// <summary>
/// Wildling Adventures gameplay plugin. Every component is data-driven so the
/// scene stays authored in the editor and the GDD numbers live in code.
/// </summary>
public sealed class WildlingsPlugin : IByteEnginePlugin
{
    public void Register(ByteEnginePluginContext context)
    {
        context.RegisterSimpleComponent<TeamManager3D>(
            "TeamManager3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Team Manager 3D",
                Category: "Wildlings",
                Description: "Owns the three-slot party, hands player input between members and keeps one active camera.",
                SearchKeywords: "wildlings party team switch roster"));

        context.RegisterSimpleComponent<TrainerFollower3D>(
            "TrainerFollower3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Trainer Follower 3D",
                Category: "Wildlings",
                Description: "Noncombat trainer that trails the active party member.",
                SearchKeywords: "wildlings trainer follower companion"));

        context.RegisterSimpleComponent<WildlingStats3D>(
            "WildlingStats3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Wildling Stats 3D",
                Category: "Wildlings",
                Description: "Species identity, quality multiplier and the derived HP / speed / damage values.",
                SearchKeywords: "wildlings species stats health speed"));

        context.RegisterSimpleComponent<WildlingCombat3D>(
            "WildlingCombat3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Wildling Combat 3D",
                Category: "Wildlings",
                Description: "Basic attack, species special and utility with cooldowns for player or AI control.",
                SearchKeywords: "wildlings combat ability firebolt special cooldown"));

        context.RegisterSimpleComponent<WildlingFollower3D>(
            "WildlingFollower3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Wildling Follower 3D",
                Category: "Wildlings",
                Description: "Exploration follow: stable travel heading, loose slots, hysteresis, catch-up and trail routing.",
                SearchKeywords: "wildlings follower formation explore trail"));

        context.RegisterSimpleComponent<WildlingStatus3D>(
            "WildlingStatus3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Wildling Status 3D",
                Category: "Wildlings",
                Description: "Burning, root and slow status effects shared by creatures and hostiles.",
                SearchKeywords: "wildlings status burning root slow"));

        context.RegisterSimpleComponent<WildlingHud3D>(
            "WildlingHud3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Wildling HUD 3D",
                Category: "Wildlings",
                Description: "Party HP, ability cooldowns and control reminder overlay.",
                SearchKeywords: "wildlings hud ui party health cooldown"));

        WildlingsEvents.Register(context);
    }
}
