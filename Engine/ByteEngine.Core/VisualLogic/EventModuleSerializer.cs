using System.Text.Json;

using ByteEngine.Core.Serialization;

namespace ByteEngine.Core.VisualLogic;

public sealed class EventModuleSerializer
{
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

        foreach (EventRuleDefinition rule
                 in module.Rules)
        {
            NormalizeRule(
                rule);
        }
    }

    private static void NormalizeRule(
        EventRuleDefinition rule)
    {
        if (rule.Id ==
            Guid.Empty)
        {
            rule.Id =
                Guid.NewGuid();
        }

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

        foreach (EventRuleDefinition child
                 in rule.SubEvents)
        {
            NormalizeRule(
                child);
        }
    }
}
