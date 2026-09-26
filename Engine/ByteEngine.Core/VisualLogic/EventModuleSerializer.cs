using System.Numerics;
using System.Text.Json;

using ByteEngine.Core.Serialization;
using ByteEngine.Core.Variables;

namespace ByteEngine.Core.VisualLogic;

public sealed class EventModuleSerializer
{
    private const int CurrentVersion =
        2;

    public void Save(
        EventModuleDefinition module,
        string path)
    {
        ArgumentNullException.ThrowIfNull(
            module);

        Normalize(module);

        JsonSerialization.WriteAtomic(
            path,
            module);
    }

    public EventModuleDefinition Load(
        string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "ByteEngine Event Module was not found.",
                path);
        }

        try
        {
            EventModuleDefinition module =
                JsonSerializer.Deserialize<EventModuleDefinition>(
                    File.ReadAllText(path),
                    JsonSerialization.Options)
                ?? throw new InvalidDataException(
                    "Event Module contained no data.");

            Normalize(module);

            return module;
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Invalid ByteEngine Event Module '{path}': {exception.Message}",
                exception);
        }
    }

    private static void Normalize(
        EventModuleDefinition module)
    {
        if (module.Id ==
            Guid.Empty)
        {
            module.Id =
                Guid.NewGuid();
        }

        if (module.Version <=
            0)
        {
            module.Version =
                1;
        }

        int sourceVersion =
            module.Version;

        bool migrateLegacyRayForward =
            sourceVersion <
            CurrentVersion;

        module.Name =
            string.IsNullOrWhiteSpace(
                module.Name)
                ? "Event Module"
                : module.Name.Trim();

        module.RequiredComponents ??=
            new List<string>();

        module.Rules ??=
            new List<EventRuleDefinition>();

        module.EditorGroups ??=
            new List<EventGraphGroupDefinition>();

        foreach (EventGraphGroupDefinition group
                 in module.EditorGroups)
        {
            if (group.Id ==
                Guid.Empty)
            {
                group.Id =
                    Guid.NewGuid();
            }

            group.MemberIds ??=
                new List<Guid>();

            group.Title =
                string.IsNullOrWhiteSpace(
                    group.Title)
                    ? "Comment"
                    : group.Title;
        }

        for (int index = 0; index < module.Rules.Count; index++)
        {
            NormalizeRule(
                module.Rules[index],
                $"Event {index + 1}",
                migrateLegacyRayForward);
        }

        if (module.Version <
            CurrentVersion)
        {
            module.Version =
                CurrentVersion;
        }
    }

    private static void NormalizeRule(
        EventRuleDefinition rule,
        string fallbackName,
        bool migrateLegacyRayForward)
    {
        if (rule.Id ==
            Guid.Empty)
        {
            rule.Id =
                Guid.NewGuid();
        }

        rule.DisplayName =
            !string.IsNullOrWhiteSpace(rule.DisplayName) &&
            !rule.DisplayName.Equals("New Event", StringComparison.Ordinal)
                ? rule.DisplayName.Trim()
                : !string.IsNullOrWhiteSpace(rule.EditorTitle)
                    ? rule.EditorTitle.Trim()
                    : fallbackName;

        // EditorTitle is retained only for backward compatibility with v0.8 files.
        rule.EditorTitle = rule.DisplayName;

        rule.Conditions ??=
            new List<VisualInstruction>();

        rule.Actions ??=
            new List<VisualInstruction>();

        rule.SubEvents ??=
            new List<EventRuleDefinition>();

        rule.ConnectedConditionIds ??=
            new List<Guid>();

        foreach (VisualInstruction instruction
                 in rule.Conditions.Concat(
                     rule.Actions))
        {
            if (instruction.InstanceId ==
                Guid.Empty)
            {
                instruction.InstanceId =
                    Guid.NewGuid();
            }

            instruction.Arguments ??=
                new Dictionary<string, EventValue>(
                    StringComparer.OrdinalIgnoreCase);

            instruction.ConditionInputIds ??=
                new List<Guid>();

            if (migrateLegacyRayForward)
            {
                MigrateLegacyRayForwardDirection(
                    instruction);
            }
        }

        /*
         * Remove stale Condition references when a Condition was deleted from
         * the module but an older file still contains its id in graph wiring.
         */
        HashSet<Guid> validConditionIds =
            rule.Conditions
                .Select(
                    condition =>
                        condition.InstanceId)
                .ToHashSet();

        rule.ConnectedConditionIds.RemoveAll(
            id =>
                !validConditionIds.Contains(
                    id));

        foreach (VisualInstruction condition
                 in rule.Conditions)
        {
            condition.ConditionInputIds.RemoveAll(
                id =>
                    !validConditionIds.Contains(
                        id) ||
                    id ==
                        condition.InstanceId);
        }

        /*
         * Remove stale Action links. The runtime also protects itself, but
         * normalizing here keeps serialized ByteGraph data internally clean.
         */
        HashSet<Guid> validActionIds =
            rule.Actions
                .Select(
                    action =>
                        action.InstanceId)
                .ToHashSet();

        if (rule.FirstActionId.HasValue &&
            !validActionIds.Contains(
                rule.FirstActionId.Value))
        {
            rule.FirstActionId =
                null;
        }

        foreach (VisualInstruction action
                 in rule.Actions)
        {
            if (action.NextActionId.HasValue &&
                !validActionIds.Contains(
                    action.NextActionId.Value))
            {
                action.NextActionId =
                    null;
            }

            if (action.TrueActionId.HasValue &&
                !validActionIds.Contains(
                    action.TrueActionId.Value))
            {
                action.TrueActionId =
                    null;
            }

            if (action.FalseActionId.HasValue &&
                !validActionIds.Contains(
                    action.FalseActionId.Value))
            {
                action.FalseActionId =
                    null;
            }
        }

        for (int index = 0; index < rule.SubEvents.Count; index++)
        {
            NormalizeRule(
                rule.SubEvents[index],
                $"{rule.DisplayName} Event {index + 1}",
                migrateLegacyRayForward);
        }
    }

    /// <summary>
    /// Event Modules authored before version 2 used local +Z as the default
    /// Cast Ray forward direction. ByteEngine's Transform.Forward convention is
    /// local -Z, so those old default rays point exactly backward.
    ///
    /// Migrate only the exact legacy default: constant local +Z with world-space
    /// disabled. Custom local vectors, world-space vectors and variable-backed
    /// values are left untouched.
    /// </summary>
    private static void MigrateLegacyRayForwardDirection(
        VisualInstruction instruction)
    {
        if (!instruction.Id.Equals(
                "physics.castRay",
                StringComparison.OrdinalIgnoreCase) &&
            !instruction.Id.Equals(
                "physics.rayHitsAnything",
                StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!instruction.Arguments.TryGetValue(
                "direction",
                out EventValue? directionValue) ||
            directionValue.Kind !=
                EventValueKind.Constant ||
            directionValue.Constant.Type !=
                VariableType.Vector3)
        {
            return;
        }

        if (instruction.Arguments.TryGetValue(
                "worldSpace",
                out EventValue? worldSpaceValue))
        {
            if (worldSpaceValue.Kind !=
                    EventValueKind.Constant ||
                worldSpaceValue.Constant.Type !=
                    VariableType.Boolean ||
                worldSpaceValue.Constant.Boolean)
            {
                return;
            }
        }

        Vector3 direction =
            directionValue.Constant.Vector3;

        if (!float.IsFinite(direction.X) ||
            !float.IsFinite(direction.Y) ||
            !float.IsFinite(direction.Z) ||
            Vector3.DistanceSquared(
                direction,
                Vector3.UnitZ) >
                .000001f)
        {
            return;
        }

        instruction.Arguments["direction"] =
            EventValue.Vector3(
                -Vector3.UnitZ);
    }
}
