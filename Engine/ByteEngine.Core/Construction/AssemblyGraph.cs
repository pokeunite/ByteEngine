using System.Numerics;

namespace ByteEngine.Core.Construction;

/// <summary>Physics-independent topology for modular assemblies.</summary>
public sealed class AssemblyGraph<T>
{
    private readonly Dictionary<Guid, AssemblyPart<T>> _parts = new();
    private readonly Dictionary<Guid, AssemblyLink> _links = new();
    private readonly Dictionary<Guid, Guid> _occupied = new();
    private readonly Dictionary<Guid, HashSet<Guid>> _adjacency = new();

    public IReadOnlyCollection<AssemblyPart<T>> Parts => _parts.Values;
    public IReadOnlyCollection<AssemblyLink> Links => _links.Values;
    public event Action<AssemblyLink>? Linked;
    public event Action<AssemblyLink>? Unlinked;

    public void Add(AssemblyPart<T> part)
    {
        ArgumentNullException.ThrowIfNull(part);
        if (part.Id == Guid.Empty || _parts.ContainsKey(part.Id))
            throw new ArgumentException("Duplicate or empty part ID.", nameof(part));
        var ids = new HashSet<Guid>();
        foreach (AssemblySocket socket in part.Sockets)
            if (socket.Id == Guid.Empty || !ids.Add(socket.Id) ||
                _parts.Values.Any(p => p.Sockets.Any(s => s.Id == socket.Id)))
                throw new ArgumentException("Duplicate or empty socket ID.", nameof(part));
        _parts.Add(part.Id, part);
        _adjacency.Add(part.Id, new HashSet<Guid>());
    }

    public AssemblyLink Connect(Guid a, Guid aSocket, Guid b, Guid bSocket,
        float breakForce = float.PositiveInfinity, float breakTorque = float.PositiveInfinity)
    {
        if (a == b || !_parts.TryGetValue(a, out var first) || !_parts.TryGetValue(b, out var second))
            throw new InvalidOperationException("Connect two distinct existing parts.");
        AssemblySocket left = first.Sockets.FirstOrDefault(s => s.Id == aSocket)
            ?? throw new InvalidOperationException("Socket A does not belong to part A.");
        AssemblySocket right = second.Sockets.FirstOrDefault(s => s.Id == bSocket)
            ?? throw new InvalidOperationException("Socket B does not belong to part B.");
        if (_occupied.ContainsKey(aSocket) || _occupied.ContainsKey(bSocket))
            throw new InvalidOperationException("Socket already occupied.");
        if (!string.Equals(left.Channel, right.Channel, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Socket channels differ.");
        if (breakForce <= 0 || breakTorque <= 0 || float.IsNaN(breakForce) || float.IsNaN(breakTorque))
            throw new ArgumentOutOfRangeException(nameof(breakForce));
        var link = new AssemblyLink(Guid.NewGuid(), a, aSocket, b, bSocket, breakForce, breakTorque);
        _links.Add(link.Id, link);
        _occupied.Add(aSocket, link.Id);
        _occupied.Add(bSocket, link.Id);
        _adjacency[a].Add(link.Id);
        _adjacency[b].Add(link.Id);
        Linked?.Invoke(link);
        return link;
    }

    public bool Disconnect(Guid id)
    {
        if (!_links.Remove(id, out var link)) return false;
        _occupied.Remove(link.SocketA);
        _occupied.Remove(link.SocketB);
        _adjacency[link.PartA].Remove(id);
        _adjacency[link.PartB].Remove(id);
        Unlinked?.Invoke(link);
        return true;
    }

    public bool Remove(Guid id)
    {
        if (!_parts.ContainsKey(id)) return false;
        foreach (Guid link in _adjacency[id].ToArray()) Disconnect(link);
        _adjacency.Remove(id);
        return _parts.Remove(id);
    }

    public IReadOnlySet<Guid> Component(Guid root)
    {
        if (!_parts.ContainsKey(root)) throw new KeyNotFoundException("Unknown part.");
        var found = new HashSet<Guid> { root };
        var queue = new Queue<Guid>();
        queue.Enqueue(root);
        while (queue.TryDequeue(out Guid current))
            foreach (Guid id in _adjacency[current])
            {
                AssemblyLink link = _links[id];
                Guid next = link.PartA == current ? link.PartB : link.PartA;
                if (found.Add(next)) queue.Enqueue(next);
            }
        return found;
    }

    public IReadOnlyList<IReadOnlySet<Guid>> DetachedFrom(Guid core)
    {
        var visited = new HashSet<Guid>(Component(core));
        var groups = new List<IReadOnlySet<Guid>>();
        foreach (Guid id in _parts.Keys)
        {
            if (visited.Contains(id)) continue;
            IReadOnlySet<Guid> group = Component(id);
            groups.Add(group);
            visited.UnionWith(group);
        }
        return groups;
    }
}

public sealed record AssemblySocket(Guid Id, string Channel, Matrix4x4 LocalTransform);
public sealed record AssemblyPart<T>(Guid Id, T Payload, IReadOnlyList<AssemblySocket> Sockets);
public sealed record AssemblyLink(Guid Id, Guid PartA, Guid SocketA, Guid PartB,
    Guid SocketB, float BreakForce, float BreakTorque);
