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

    /// <summary>
    /// Human-readable event name. This never participates in graph identity.
    /// </summary>
    public string DisplayName { get; set; } =
        "New Event";

    public bool Enabled { get; set; } =
        true;

    public List<VisualInstruction> Conditions { get; set; } =
        new();

    public List<VisualInstruction> Actions { get; set; } =
        new();

    public List<EventRuleDefinition> SubEvents { get; set; } =
        new();

    /*
     * Runtime condition-flow data.
     *
     * Older Event Modules treated every Condition in Conditions as connected
     * to the Event. Once ByteGraph initializes explicit condition flow, only
     * ids in ConnectedConditionIds participate in the Event's AND test.
     */
    public bool HasExplicitConditionFlow { get; set; }

    public List<Guid> ConnectedConditionIds { get; set; } =
        new();

    /*
     * Runtime execution-flow data.
     *
     * Old Event Modules did not store explicit action links. When this is
     * false, runtime execution falls back to Actions list order so existing
     * .byteevents files remain compatible.
     *
     * ByteGraph initializes an explicit chain the first time an older module
     * is opened. From then on, FirstActionId and each action's NextActionId
     * define the orange execution flow.
     */
    public bool HasExplicitExecutionFlow { get; set; }

    public Guid? FirstActionId { get; set; }

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
     * Runtime condition-graph inputs.
     *
     * Used by advanced logic Conditions such as logic.and and logic.or.
     * Ordinary Conditions leave this list empty.
     */
    public List<Guid> ConditionInputIds { get; set; } =
        new();

    /*
     * Runtime execution-flow data for Action instructions.
     *
     * Ordinary Actions use NextActionId.
     * Flow Branch uses TrueActionId / FalseActionId.
     */
    public Guid? NextActionId { get; set; }

    public Guid? TrueActionId { get; set; }

    public Guid? FalseActionId { get; set; }

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
