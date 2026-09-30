namespace ByteEngine.Core.Construction;

/// <summary>Measured load from a real physics constraint, not an estimated impact.</summary>
public readonly record struct AssemblyLoad(float Force, float Torque);

/// <summary>
/// Boundary between logical construction and a physics engine with true joints.
/// ByteEngine's current linear-only PhysicsWorld3D cannot implement this honestly yet.
/// </summary>
public interface IAssemblyConstraintAdapter
{
    void Create(AssemblyLink link);
    void Destroy(Guid linkId);
    void SetBuildingMode(Guid partId, bool frozen);
    AssemblyLoad Measure(Guid linkId);
}

/// <summary>Breaks overloaded links and reports components detached from the core.</summary>
public sealed class AssemblyIntegrity<T> : IDisposable
{
    private readonly AssemblyGraph<T> _graph;
    private readonly IAssemblyConstraintAdapter _physics;
    private readonly Guid _core;
    private bool _disposed;

    public event Action<AssemblyLink>? Broken;
    public event Action<IReadOnlySet<Guid>>? Detached;

    public AssemblyIntegrity(AssemblyGraph<T> graph, IAssemblyConstraintAdapter physics, Guid core)
    {
        _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        _physics = physics ?? throw new ArgumentNullException(nameof(physics));
        if (!graph.Parts.Any(p => p.Id == core)) throw new ArgumentException("Unknown core.", nameof(core));
        _core = core;
        graph.Linked += OnLinked;
        graph.Unlinked += OnUnlinked;
        foreach (AssemblyLink link in graph.Links) physics.Create(link);
    }

    public void SetBuildingMode(bool building)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        foreach (AssemblyPart<T> part in _graph.Parts)
            _physics.SetBuildingMode(part.Id, building);
    }

    public int Evaluate()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        HashSet<Guid> previouslyAttached = new(_graph.Component(_core));
        int count = 0;
        foreach (AssemblyLink link in _graph.Links.ToArray())
        {
            AssemblyLoad load = _physics.Measure(link.Id);
            if (!float.IsFinite(load.Force) || !float.IsFinite(load.Torque) ||
                MathF.Abs(load.Force) < link.BreakForce && MathF.Abs(load.Torque) < link.BreakTorque)
                continue;
            _graph.Disconnect(link.Id);
            Broken?.Invoke(link);
            count++;
        }
        if (count > 0)
            foreach (IReadOnlySet<Guid> group in _graph.DetachedFrom(_core))
                if (group.Any(previouslyAttached.Contains))
                    Detached?.Invoke(group);
        return count;
    }

    private void OnLinked(AssemblyLink link) => _physics.Create(link);
    private void OnUnlinked(AssemblyLink link) => _physics.Destroy(link.Id);

    public void Dispose()
    {
        if (_disposed) return;
        _graph.Linked -= OnLinked;
        _graph.Unlinked -= OnUnlinked;
        foreach (AssemblyLink link in _graph.Links) _physics.Destroy(link.Id);
        _disposed = true;
    }
}
