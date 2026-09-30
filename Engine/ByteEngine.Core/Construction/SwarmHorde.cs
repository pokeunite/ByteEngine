using System.Numerics;

namespace ByteEngine.Core.Construction;

public readonly record struct SwarmHandle(int Slot, int Generation);
public readonly record struct SwarmSquish(Vector2 Position, byte Team);

/// <summary>Fixed-capacity, pooled infantry positions without one GameObject per unit.</summary>
public sealed class SwarmHorde
{
    private readonly Vector2[] _positions;
    private readonly byte[] _teams;
    private readonly int[] _generations;
    private readonly bool[] _active;
    private readonly int[] _free;
    private readonly int[] _nextInCell;
    private readonly Dictionary<(int X, int Y), int> _enemyCells = new();
    private int _next;
    private int _freeCount;

    public int Capacity => _positions.Length;
    public int Count { get; private set; }
    public event Action<SwarmSquish>? Squished;

    public SwarmHorde(int capacity)
    {
        if (capacity <= 0 || capacity > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(capacity));
        _positions = new Vector2[capacity];
        _teams = new byte[capacity];
        _generations = new int[capacity];
        _active = new bool[capacity];
        _free = new int[capacity];
        _nextInCell = new int[capacity];
    }

    public SwarmHandle Spawn(Vector2 position, byte team)
    {
        if (!float.IsFinite(position.X) || !float.IsFinite(position.Y))
            throw new ArgumentException("Position must be finite.", nameof(position));
        if (_freeCount == 0 && _next == Capacity)
            throw new InvalidOperationException("Swarm capacity reached.");
        int slot = _freeCount > 0 ? _free[--_freeCount] : _next++;
        _positions[slot] = position;
        _teams[slot] = team;
        _active[slot] = true;
        Count++;
        return new SwarmHandle(slot, _generations[slot]);
    }

    public bool TryGet(SwarmHandle handle, out Vector2 position)
    {
        if (handle.Slot >= 0 && handle.Slot < _next &&
            _active[handle.Slot] && _generations[handle.Slot] == handle.Generation)
        {
            position = _positions[handle.Slot];
            return true;
        }
        position = default;
        return false;
    }

    public bool TryGetSlot(int slot, out Vector2 position, out byte team)
    {
        if ((uint)slot < (uint)_next && _active[slot])
        {
            position = _positions[slot];
            team = _teams[slot];
            return true;
        }
        position = default;
        team = 0;
        return false;
    }

    public bool TryGetSlot(int slot, out SwarmHandle handle, out Vector2 position, out byte team)
    {
        if (TryGetSlot(slot, out position, out team))
        {
            handle = new SwarmHandle(slot, _generations[slot]);
            return true;
        }
        handle = default;
        return false;
    }

    public bool Despawn(SwarmHandle handle)
    {
        if (!TryGet(handle, out _)) return false;
        Release(handle.Slot);
        return true;
    }

    public void Step(SwarmFlowField red, SwarmFlowField green, float speed, float deltaTime)
    {
        ArgumentNullException.ThrowIfNull(red);
        ArgumentNullException.ThrowIfNull(green);
        if (!float.IsFinite(speed) || speed < 0 || !float.IsFinite(deltaTime) || deltaTime < 0)
            throw new ArgumentOutOfRangeException(nameof(speed));
        float distance = speed * deltaTime;
        for (int i = 0; i < _next; i++)
            if (_active[i])
                _positions[i] += (_teams[i] == 0 ? red : green).Sample(_positions[i]) * distance;
    }

    /// <summary>Opposing units cancel at close range using a reusable spatial grid.</summary>
    public int ResolveClashes(float radius, int maximumPairs)
    {
        if (!float.IsFinite(radius) || radius <= 0 || maximumPairs < 0)
            throw new ArgumentOutOfRangeException(nameof(radius));
        _enemyCells.Clear();
        float inverseCell = 1f / radius;
        for (int i = 0; i < _next; i++)
        {
            if (!_active[i] || _teams[i] != 1) continue;
            var cell = Cell(_positions[i], inverseCell);
            _nextInCell[i] = _enemyCells.GetValueOrDefault(cell, -1);
            _enemyCells[cell] = i;
        }
        int pairs = 0;
        float radiusSquared = radius * radius;
        for (int i = 0; i < _next && pairs < maximumPairs; i++)
        {
            if (!_active[i] || _teams[i] != 0) continue;
            var cell = Cell(_positions[i], inverseCell);
            bool found = false;
            for (int dy = -1; dy <= 1 && !found; dy++)
                for (int dx = -1; dx <= 1 && !found; dx++)
                {
                    int candidate = _enemyCells.GetValueOrDefault((cell.X + dx, cell.Y + dy), -1);
                    while (candidate >= 0)
                    {
                        int next = _nextInCell[candidate];
                        if (_active[candidate] &&
                            Vector2.DistanceSquared(_positions[i], _positions[candidate]) <= radiusSquared)
                        {
                            Release(i);
                            Release(candidate);
                            pairs++;
                            found = true;
                            break;
                        }
                        candidate = next;
                    }
                }
        }
        return pairs;
    }

    private static (int X, int Y) Cell(Vector2 point, float inverseCell) =>
        ((int)MathF.Floor(point.X * inverseCell), (int)MathF.Floor(point.Y * inverseCell));

    /// <summary>Swept vehicle footprint catches units crossed between frames.</summary>
    public int Squish(Vector2 from, Vector2 to, float radius, float vehicleSpeed,
        float minimumSpeed, byte targetTeam)
    {
        if (!float.IsFinite(radius) || radius < 0 ||
            !float.IsFinite(vehicleSpeed) || vehicleSpeed < 0 ||
            !float.IsFinite(minimumSpeed) || minimumSpeed < 0)
            throw new ArgumentOutOfRangeException(nameof(radius));
        if (vehicleSpeed < minimumSpeed) return 0;
        Vector2 travel = to - from;
        float lengthSquared = travel.LengthSquared();
        float radiusSquared = radius * radius;
        int killed = 0;
        for (int i = 0; i < _next; i++)
        {
            if (!_active[i] || _teams[i] != targetTeam) continue;
            float fraction = lengthSquared > 0
                ? Math.Clamp(Vector2.Dot(_positions[i] - from, travel) / lengthSquared, 0, 1)
                : 0;
            if (Vector2.DistanceSquared(_positions[i], from + fraction * travel) > radiusSquared)
                continue;
            SwarmSquish result = new(_positions[i], _teams[i]);
            Release(i);
            Squished?.Invoke(result);
            killed++;
        }
        return killed;
    }

    private void Release(int slot)
    {
        _active[slot] = false;
        _generations[slot]++;
        _free[_freeCount++] = slot;
        Count--;
    }
}
