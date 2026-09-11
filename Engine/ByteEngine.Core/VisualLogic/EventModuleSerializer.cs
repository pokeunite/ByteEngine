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
        if (module.Id == Guid.Empty)
        {
            module.Id =
                Guid.NewGuid();
        }

        if (module.Version <= 0)
        {
            module.Version =
                1;
        }

        module.Name =
            string.IsNullOrWhiteSpace(module.Name)
                ? "Event Module"
                : module.Name.Trim();

        module.RequiredComponents ??=
            new List<string>();

        module.Rules ??=
            new List<EventRuleDefinition>();

        foreach (EventRuleDefinition rule
                 in module.Rules)
        {
            NormalizeRule(rule);
        }
    }

    private static void NormalizeRule(
        EventRuleDefinition rule)
    {
        if (rule.Id == Guid.Empty)
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
        }

        foreach (EventRuleDefinition child
                 in rule.SubEvents)
        {
            NormalizeRule(child);
        }
    }
}