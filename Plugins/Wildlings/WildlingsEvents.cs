using ByteEngine.Core.Plugins;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;

namespace Wildlings;

/// <summary>
/// Exposes the Wildlings systems to the ByteGraph Event Sheet.
///
/// The per-frame runtime (input ownership, camera handover, projectile physics,
/// cooldowns) stays in the components; these nodes let Event Modules read that
/// state and command it, so rules can be authored without touching C#.
/// </summary>
internal static class WildlingsEvents
{
    private const string Prefix = "studio.wildlings.";

    internal static void Register(ByteEnginePluginContext context)
    {
        RegisterTeam(context);
        RegisterCreature(context);
    }

    private static void RegisterTeam(ByteEnginePluginContext context)
    {
        VisualArgumentDefinition target = Arg("target", VariableValue.FromString("Self"));
        VisualArgumentDefinition slot = Arg("slot", VariableValue.FromNumber(1));
        VisualArgumentDefinition count = Arg("count", VariableValue.FromNumber(3));

        context.RegisterAction(new VisualActionDefinition
        {
            Id = Prefix + "team.enterCombat",
            Category = "Wildlings",
            DisplayName = "Give Control To The Wildlings",
            TargetComponent = nameof(TeamManager3D),
            Arguments = [target],
            Execute = (i, e) => Team(i, e)?.SetCombat(true)
        });

        context.RegisterAction(new VisualActionDefinition
        {
            Id = Prefix + "team.leaveCombat",
            Category = "Wildlings",
            DisplayName = "Give Control Back To The Trainer",
            TargetComponent = nameof(TeamManager3D),
            Arguments = [target],
            Execute = (i, e) => Team(i, e)?.SetCombat(false)
        });

        context.RegisterAction(new VisualActionDefinition
        {
            Id = Prefix + "team.toggleCombat",
            Category = "Wildlings",
            DisplayName = "Toggle Combat Control",
            TargetComponent = nameof(TeamManager3D),
            Arguments = [target],
            Execute = (i, e) =>
            {
                if (Team(i, e) is { } team)
                {
                    team.SetCombat(!team.InCombat);
                }
            }
        });

        context.RegisterAction(new VisualActionDefinition
        {
            Id = Prefix + "team.commandSlot",
            Category = "Wildlings",
            DisplayName = "Command Wildling Slot",
            TargetComponent = nameof(TeamManager3D),
            Arguments = [target, slot],
            Execute = (i, e) => Team(i, e)?.CommandSlot((int)MathF.Round((float)EventValueResolver.GetNumber(i, "slot", e, 1f)))
        });

        context.RegisterCondition(new VisualConditionDefinition
        {
            Id = Prefix + "team.inCombat",
            Category = "Wildlings",
            DisplayName = "Team Is In Combat",
            TargetComponent = nameof(TeamManager3D),
            Arguments = [target],
            Evaluate = (i, e) => Team(i, e) is { InCombat: true }
        });

        context.RegisterCondition(new VisualConditionDefinition
        {
            Id = Prefix + "team.isExploring",
            Category = "Wildlings",
            DisplayName = "Team Is Exploring",
            TargetComponent = nameof(TeamManager3D),
            Arguments = [target],
            Evaluate = (i, e) => Team(i, e) is { InCombat: false }
        });

        context.RegisterCondition(new VisualConditionDefinition
        {
            Id = Prefix + "team.activeSlotIs",
            Category = "Wildlings",
            DisplayName = "Commanded Slot Is",
            TargetComponent = nameof(TeamManager3D),
            Arguments = [target, slot],
            Evaluate = (i, e) => Team(i, e) is { } team &&
                                 team.ActiveSlot == (int)MathF.Round((float)EventValueResolver.GetNumber(i, "slot", e, 1f))
        });

        context.RegisterCondition(new VisualConditionDefinition
        {
            Id = Prefix + "team.livingCountAtLeast",
            Category = "Wildlings",
            DisplayName = "Living Wildlings At Least",
            TargetComponent = nameof(TeamManager3D),
            Arguments = [target, count],
            Evaluate = (i, e) => Team(i, e) is { } team &&
                                 team.LivingCount >= (int)MathF.Round((float)EventValueResolver.GetNumber(i, "count", e, 1f))
        });
    }

    private static void RegisterCreature(ByteEnginePluginContext context)
    {
        VisualArgumentDefinition target = Arg("target", VariableValue.FromString("Self"));
        VisualArgumentDefinition species = Arg("species", VariableValue.FromString(WildlingSpeciesDatabase.FireLizardId));
        VisualArgumentDefinition enabled = Arg("enabled", VariableValue.FromBoolean(true));

        context.RegisterAction(new VisualActionDefinition
        {
            Id = Prefix + "creature.setCombatEnabled",
            Category = "Wildlings",
            DisplayName = "Allow / Stop Creature Attacks",
            TargetComponent = nameof(WildlingStats3D),
            Arguments = [target, enabled],
            Execute = (i, e) =>
            {
                if (Resolve(i, e)?.GetComponent<WildlingCombat3D>() is { } combat)
                {
                    combat.CombatEnabled = EventValueResolver.GetBoolean(i, "enabled", e, true);
                }
            }
        });

        context.RegisterAction(new VisualActionDefinition
        {
            Id = Prefix + "creature.setSpecies",
            Category = "Wildlings",
            DisplayName = "Set Creature Species",
            TargetComponent = nameof(WildlingStats3D),
            Arguments = [target, species],
            Execute = (i, e) =>
            {
                if (Resolve(i, e)?.GetComponent<WildlingStats3D>() is { } stats)
                {
                    stats.Species = EventValueResolver.GetString(i, "species", e, WildlingSpeciesDatabase.FireLizardId);
                    stats.ApplyToBody();
                }
            }
        });

        context.RegisterCondition(new VisualConditionDefinition
        {
            Id = Prefix + "creature.speciesIs",
            Category = "Wildlings",
            DisplayName = "Creature Species Is",
            TargetComponent = nameof(WildlingStats3D),
            Arguments = [target, species],
            Evaluate = (i, e) => Resolve(i, e)?.GetComponent<WildlingStats3D>() is { } stats &&
                                 string.Equals(stats.Species,
                                     EventValueResolver.GetString(i, "species", e, WildlingSpeciesDatabase.FireLizardId),
                                     StringComparison.OrdinalIgnoreCase)
        });

        context.RegisterCondition(new VisualConditionDefinition
        {
            Id = Prefix + "creature.isAiControlled",
            Category = "Wildlings",
            DisplayName = "Creature Is On Companion AI",
            TargetComponent = nameof(WildlingStats3D),
            Arguments = [target],
            Evaluate = (i, e) => Resolve(i, e)?.GetComponent<WildlingCombat3D>() is { AiControlled: true }
        });
    }

    private static VisualArgumentDefinition Arg(string name, VariableValue value) =>
        new(name, char.ToUpper(name[0]) + name[1..], value);

    private static TeamManager3D? Team(VisualInstruction instruction, EventExecutionContext context)
    {
        GameObject? resolved = Resolve(instruction, context);
        if (resolved?.GetComponent<TeamManager3D>() is { } direct)
        {
            return direct;
        }

        foreach (GameObject candidate in context.Scene.GameObjects)
        {
            if (candidate.GetComponent<TeamManager3D>() is { } team)
            {
                return team;
            }
        }

        return null;
    }

    private static GameObject? Resolve(VisualInstruction instruction, EventExecutionContext context)
    {
        string token = EventValueResolver.GetString(instruction, "target", context, "Self");

        if (string.IsNullOrWhiteSpace(token) || token.Equals("Self", StringComparison.OrdinalIgnoreCase))
        {
            return context.Self;
        }

        if (token.Equals("Last Ray Hit", StringComparison.OrdinalIgnoreCase))
        {
            return context.LastRaycastHit?.GameObject;
        }

        if (token.StartsWith("id:", StringComparison.OrdinalIgnoreCase) && Guid.TryParse(token[3..], out Guid id))
        {
            return context.Scene.FindGameObject(id);
        }

        return context.Scene.FindGameObject(token);
    }
}

