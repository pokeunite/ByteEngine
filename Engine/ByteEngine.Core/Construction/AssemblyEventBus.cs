namespace ByteEngine.Core.Construction;

public enum AssemblyEventKind { PartAdded, PartRemoved, Linked, Unlinked, JointBroken, Detached }

public readonly record struct AssemblyEvent<T>(
    AssemblyEventKind Kind, Guid PartId, T? Payload,
    Guid LinkId = default, IReadOnlySet<Guid>? DetachedParts = null);

/// <summary>One subscription point for presentation, scoring, sound, and telemetry.</summary>
public sealed class AssemblyEventBus<T> : IDisposable
{
    private readonly AssemblyGraph<T> _graph;
    private readonly AssemblyIntegrity<T>? _integrity;
    private bool _disposed;

    public event Action<AssemblyEvent<T>>? Published;

    public AssemblyEventBus(AssemblyGraph<T> graph, AssemblyIntegrity<T>? integrity = null)
    {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        _integrity = integrity;
        graph.Linked += OnLinked;
        graph.Unlinked += OnUnlinked;
        if (integrity is not null)
        {
            integrity.Broken += OnBroken;
            integrity.Detached += OnDetached;
        }
    }

    public void Add(AssemblyPart<T> part)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _graph.Add(part);
        Published?.Invoke(new AssemblyEvent<T>(AssemblyEventKind.PartAdded, part.Id, part.Payload));
    }

    public bool Remove(Guid id)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AssemblyPart<T>? part = _graph.Parts.FirstOrDefault(p => p.Id == id);
        if (part is null) return false;
        if (!_graph.Remove(id)) return false;
        Published?.Invoke(new AssemblyEvent<T>(AssemblyEventKind.PartRemoved, id, part.Payload));
        return true;
    }

    private void OnLinked(AssemblyLink link) =>
        Published?.Invoke(new AssemblyEvent<T>(AssemblyEventKind.Linked,
            link.PartB, Payload(link.PartB), link.Id));

    private void OnUnlinked(AssemblyLink link) =>
        Published?.Invoke(new AssemblyEvent<T>(AssemblyEventKind.Unlinked,
            link.PartB, Payload(link.PartB), link.Id));

    private void OnBroken(AssemblyLink link) =>
        Published?.Invoke(new AssemblyEvent<T>(AssemblyEventKind.JointBroken,
            link.PartB, Payload(link.PartB), link.Id));

    private void OnDetached(IReadOnlySet<Guid> parts) =>
        Published?.Invoke(new AssemblyEvent<T>(AssemblyEventKind.Detached,
            Guid.Empty, default, DetachedParts: parts));

    private T? Payload(Guid id) =>
        _graph.Parts.FirstOrDefault(part => part.Id == id) is { } part
            ? part.Payload : default;

    public void Dispose()
    {
        if (_disposed) return;
        _graph.Linked -= OnLinked;
        _graph.Unlinked -= OnUnlinked;
        if (_integrity is not null)
        {
            _integrity.Broken -= OnBroken;
            _integrity.Detached -= OnDetached;
        }
        _disposed = true;
    }
}
