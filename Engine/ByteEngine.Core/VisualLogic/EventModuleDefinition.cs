using ByteEngine.Core.Scene;

namespace ByteEngine.Core.VisualLogic;

public sealed class EventModuleDefinition
{
    public Guid Id { get; set; } =
        Guid.NewGuid();

    public int Version { get; set; } =
        1;

    public string Name { get; set; } =
        "Event Module";

    /// <summary>
    /// Optional Blueprint this Event Module was authored against.
    /// Generic Event Modules leave this null.
    /// </summary>
    public Guid? TargetBlueprintGuid { get; set; }

    public string? TargetBlueprintPath { get; set; }

    public List<string> RequiredComponents { get; set; } =
        new();

    public List<EventRuleDefinition> Rules { get; set; } =
        new();

    public IReadOnlyList<string> Validate(
        GameObject target)
    {
        ArgumentNullException.ThrowIfNull(target);

        HashSet<string> components =
            target.Components
                .Select(component =>
                    component.GetType().Name)
                .ToHashSet(
                    StringComparer.OrdinalIgnoreCase);

        return RequiredComponents
            .Where(required =>
                !components.Contains(required))
            .Select(required =>
                $"{Name} requires {required}.")
            .ToList();
    }

    public IReadOnlyList<string> Validate(
        IEnumerable<string> componentTypes)
    {
        ArgumentNullException.ThrowIfNull(
            componentTypes);

        HashSet<string> components =
            componentTypes.ToHashSet(
                StringComparer.OrdinalIgnoreCase);

        return RequiredComponents
            .Where(required =>
                !components.Contains(required))
            .Select(required =>
                $"{Name} requires {required}.")
            .ToList();
    }
}

public sealed class EventRuleDefinition
{
    public Guid Id { get; set; } =
        Guid.NewGuid();

    public bool Enabled { get; set; } =
        true;

    public List<VisualInstruction> Conditions { get; set; } =
        new();

    public List<VisualInstruction> Actions { get; set; } =
        new();

    public List<EventRuleDefinition> SubEvents { get; set; } =
        new();
}

public sealed class VisualInstruction
{
    public Guid InstanceId { get; set; } =
        Guid.NewGuid();

    /// <summary>
    /// Registry identifier.
    /// Example:
    /// input.keyHeld
    /// character.jump
    /// variable.set
    /// </summary>
    public string Id { get; set; } =
        string.Empty;

    public Dictionary<string, EventValue> Arguments { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}