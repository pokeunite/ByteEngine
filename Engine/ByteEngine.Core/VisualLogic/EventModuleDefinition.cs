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

    /*
     * ByteGraph editor metadata.
     *
     * This does not affect runtime execution.
     */
    public float EditorNodeScale { get; set; } =
        0.72f;

    public List<EventGraphGroupDefinition> EditorGroups { get; set; } =
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

    /*
     * ByteGraph editor metadata.
     */
    public string EditorTitle { get; set; } =
        string.Empty;

    public bool EditorCollapsed { get; set; }

    public bool EditorLayoutInitialized { get; set; }

    public float EditorX { get; set; }

    public float EditorY { get; set; }
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

    /*
     * ByteGraph editor metadata.
     */
    public bool EditorLayoutInitialized { get; set; }

    public float EditorX { get; set; }

    public float EditorY { get; set; }
}

public sealed class EventGraphGroupDefinition
{
    public Guid Id { get; set; } =
        Guid.NewGuid();

    public string Title { get; set; } =
        "Comment";

    /*
     * A group tracks node ids rather than storing a fixed box.
     * The editor recomputes the box from its member nodes, so the
     * comment box follows nodes when they are moved.
     */
    public List<Guid> MemberIds { get; set; } =
        new();
}
