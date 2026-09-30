using System.Text.Json;

namespace ByteEngine.Core.Construction;

/// <summary>A versioned, physics-independent snapshot of a modular assembly.</summary>
public sealed record AssemblyBlueprint<T>(
    int Version, Guid Core, AssemblyPart<T>[] Parts, AssemblyLink[] Links)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        IncludeFields = true,
        WriteIndented = true
    };

    public static AssemblyBlueprint<T> Capture(AssemblyGraph<T> graph, Guid core)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (!graph.Parts.Any(part => part.Id == core))
            throw new ArgumentException("Core is not part of the graph.", nameof(core));
        return new AssemblyBlueprint<T>(1, core, graph.Parts.ToArray(), graph.Links.ToArray());
    }

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static AssemblyBlueprint<T> FromJson(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        AssemblyBlueprint<T> snapshot = JsonSerializer.Deserialize<AssemblyBlueprint<T>>(json, JsonOptions)
            ?? throw new JsonException("Assembly blueprint is empty.");
        if (snapshot.Version != 1 || snapshot.Parts is null || snapshot.Links is null)
            throw new JsonException("Unsupported or incomplete assembly blueprint.");
        // Rebuild through the graph API so invalid channels, sockets, and duplicate IDs fail closed.
        _ = snapshot.Instantiate();
        return snapshot;
    }

    public AssemblyGraph<T> Instantiate()
    {
        if (Version != 1 || Parts is null || Links is null)
            throw new InvalidOperationException("Unsupported or incomplete assembly blueprint.");
        var graph = new AssemblyGraph<T>();
        foreach (AssemblyPart<T> part in Parts) graph.Add(part);
        if (!Parts.Any(part => part.Id == Core))
            throw new InvalidOperationException("Blueprint has no core part.");
        foreach (AssemblyLink link in Links)
        {
            AssemblyLink created = graph.Connect(link.PartA, link.SocketA,
                link.PartB, link.SocketB, link.BreakForce, link.BreakTorque, link.Id);
            if (!string.Equals(created.Channel, link.Channel, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Blueprint link channel differs from its sockets.");
        }
        return graph;
    }
}
