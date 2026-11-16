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

        context.RegisterSimpleComponent<WildlingSlotMarker3D>(
            "WildlingSlotMarker3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Wildling Slot Maker (Ring)",
                Category: "Wildlings",
                Description: "Coloured ground ring parented to the trainer. Drag it in the scene view to author where that wildling follows; the ring's offset is the slot distance.",
                SearchKeywords: "wildlings formation ring marker slot target follow gizmo"));

        context.RegisterSimpleComponent<WildlingArenaSpawner3D>(
            "WildlingArenaSpawner3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Wildling Arena Spawner 3D",
                Category: "Wildlings",
                Description: "Survivors enemy feed: spawns chase-and-touch hostiles around the controlled creature up to a cap.",
                SearchKeywords: "wildlings survivors arena spawn enemies waves"));

        context.RegisterSimpleComponent<WildlingRun3D>(
            "WildlingRun3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Wildling Run Director 3D",
                Category: "Wildlings",
                Description: "Survivors run clock, kill credit, experience/level progression and the fail state.",
                SearchKeywords: "wildlings survivors run timer level xp kills"));

        context.RegisterSimpleComponent<WildlingExperienceOrb3D>(
            "WildlingExperienceOrb3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Wildling XP Orb 3D",
                Category: "Wildlings",
                Description: "Experience pickup: homes in on the player when close and grants run XP on contact.",
                SearchKeywords: "wildlings xp orb pickup magnet experience"));

        context.RegisterSimpleComponent<WildlingLevelUpDraft3D>(
            "WildlingLevelUpDraft3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Wildling Level-Up Draft 3D",
                Category: "Wildlings",
                Description: "Pauses the world on level-up and shows the authored upgrade cards to pick from.",
                SearchKeywords: "wildlings level up draft cards pause upgrade"));

        context.RegisterSimpleComponent<WildlingUpgradeCard3D>(
            "WildlingUpgradeCard3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Wildling Upgrade Card 3D",
                Category: "Wildlings",
                Description: "Authored draft card: title, description, effect numbers and the text objects that show it.",
                SearchKeywords: "wildlings upgrade card draft stats"));

        context.RegisterSimpleComponent<WildlingDodge3D>(
            "WildlingDodge3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Wildling Dodge 3D",
                Category: "Wildlings",
                Description: "Burst dodge with i-frames: key, distance, duration and cooldown are all properties.",
                SearchKeywords: "wildlings dodge dash roll iframe movement"));

        context.RegisterSimpleComponent<WildlingProfile3D>(
            "WildlingProfile3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Wildling Profile 3D",
                Category: "Wildlings",
                Description: "Persistent profile: coins, essence, owned creatures and run history saved as JSON.",
                SearchKeywords: "wildlings profile save coins essence collection"));

        context.RegisterSimpleComponent<WildlingSanctuary3D>(
            "WildlingSanctuary3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Wildling Sanctuary 3D",
                Category: "Wildlings",
                Description: "Menu-driven hub: shows the profile, freezes the arena and starts a fresh run on a key.",
                SearchKeywords: "wildlings sanctuary hub menu start run coins profile"));

        context.RegisterSimpleComponent<WildlingBond3D>(
            "WildlingBond3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Wildling Bond 3D",
                Category: "Wildlings",
                Description: "Capture window: a weakened nearby creature can be bonded for species essence.",
                SearchKeywords: "wildlings bond capture essence recruit weakened"));

        context.RegisterSimpleComponent<WildlingEnemyDefinition3D>(
            "WildlingEnemyDefinition3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Wildling Enemy Kind 3D",
                Category: "Wildlings",
                Description: "Authored enemy kind: stats, colour, species identity and essence value for the arena spawner.",
                SearchKeywords: "wildlings enemy kind definition stats species"));

        context.RegisterSimpleComponent<WildlingEnemy3D>(
            "WildlingEnemy3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Wildling Enemy Identity 3D",
                Category: "Wildlings",
                Description: "Runtime identity on a spawned hostile, used by capture.",
                SearchKeywords: "wildlings enemy identity species capture"));

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

        context.RegisterSimpleComponent<WildlingDamagePopup3D>(
            "WildlingDamagePopup3D",
            new ByteEnginePluginComponentMetadata(
                DisplayName: "Wildling Damage Popup 3D",
                Category: "Wildlings",
                Description: "Floating damage numbers: pooled authored UiText objects driven by HealthComponent damage events.",
                SearchKeywords: "wildlings damage popup floating numbers combat feedback"));

        WildlingsEvents.Register(context);
    }
}
