using System.Numerics;
using ByteEngine.Core.Characters;

namespace ByteEngine.Core.Physics;

public sealed partial class PhysicsWorld3D
{
    // Only immovable primitive pairs are retained. Dynamic/kinematic bodies,
    // meshes and heightfields continue through the normal narrow phase.
    private readonly Dictionary<Collider3D, PrimitiveState> _primitiveStates = new();
    private readonly Dictionary<(Collider3D, Collider3D), CachedPrimitiveContact> _primitiveContacts = new();
    private readonly List<(Collider3D, Collider3D)> _expiredPrimitiveContacts = new();
    public int LastCachedContactHits { get; private set; }
    public int LastStaticSolveReuseCount { get; private set; }
    private readonly List<StaticSolveState> _staticSolveStates = new();
    private readonly Dictionary<ContactKey, PhysicsContactPair3D> _staticSolveContacts = new();
    private readonly uint[] _staticSolveMatrix = new uint[32];
    private bool _staticSolveValid;
    private readonly record struct StaticSolveState(Collider3D Collider, PrimitiveState Shape,
        Rigidbody3D? Body, int Layer, bool UseProjectMatrix, uint Mask);

    private StaticSolveState CaptureStaticState(ColliderEntry entry) => new(entry.Collider,
        _primitiveStates[entry.Collider], entry.Body, entry.GameObject.Layer,
        entry.Collider.UseProjectMatrix, entry.Collider.CollisionMask.Bits);

    private bool TryReuseStaticSolve(List<ColliderEntry> entries, uint[] matrix)
    {
        if (!_staticSolveValid || _primitiveStates.Count != entries.Count || matrix.Length != 32 ||
            entries.Count != _staticSolveStates.Count) return false;
        for (int i = 0; i < 32; i++) if (matrix[i] != _staticSolveMatrix[i]) return false;
        for (int i = 0; i < entries.Count; i++)
            if (CaptureStaticState(entries[i]) != _staticSolveStates[i]) return false;
        foreach (var contact in _staticSolveContacts) _frameContacts[contact.Key] = contact.Value;
        LastCachedContactHits += _staticSolveContacts.Count;
        LastStaticSolveReuseCount++;
        return true;
    }

    private void BeginStaticSolve(List<ColliderEntry> entries, uint[] matrix)
    {
        _staticSolveValid = _primitiveStates.Count == entries.Count && matrix.Length == 32;
        _staticSolveStates.Clear();
        _staticSolveContacts.Clear();
        if (!_staticSolveValid) return;
        foreach (var entry in entries) _staticSolveStates.Add(CaptureStaticState(entry));
        Array.Copy(matrix, _staticSolveMatrix, 32);
    }

    private readonly record struct PrimitiveState(Matrix4x4 World, Vector3 Center,
        Vector3 Size, bool Trigger, int Ordinal);
    private readonly record struct CachedPrimitiveContact(PrimitiveState First,
        PrimitiveState Second, bool Intersects, PhysicsContactPair3D Pair, ContactKey Key);

    private void PreparePrimitiveStates(List<ColliderEntry> entries)
    {
        _primitiveStates.Clear();
        foreach (var entry in entries)
        {
            if (entry.Body is { BodyType: not RigidbodyBodyType3D.Static } ||
                entry.Collider is not (BoxCollider3D or CapsuleCollider3D)) continue;
            var collider = entry.Collider;
            _primitiveStates[collider] = new(collider.Transform.WorldMatrix, collider.Center,
                collider.Size, collider.IsTrigger, ComponentOrdinal(entry.GameObject, collider));
        }
        // Removed/disabled colliders must not be retained by the cache.
        _expiredPrimitiveContacts.Clear();
        foreach (var key in _primitiveContacts.Keys)
            if (!_primitiveStates.ContainsKey(key.Item1) || !_primitiveStates.ContainsKey(key.Item2))
                _expiredPrimitiveContacts.Add(key);
        foreach (var key in _expiredPrimitiveContacts) _primitiveContacts.Remove(key);
    }

    private bool TryCachedContact(ColliderEntry first, ColliderEntry second)
    {
        if (!_primitiveStates.TryGetValue(first.Collider, out var a) ||
            !_primitiveStates.TryGetValue(second.Collider, out var b) ||
            !_primitiveContacts.TryGetValue((first.Collider, second.Collider), out var cached) ||
            cached.First != a || cached.Second != b) return false;
        LastCachedContactHits++;
        if (cached.Intersects)
        {
            _frameContacts[cached.Key] = cached.Pair;
            if (_staticSolveValid) _staticSolveContacts[cached.Key] = cached.Pair;
        }
        return true;
    }

    private void CacheContact(ColliderEntry first, ColliderEntry second, bool intersects,
        PhysicsContactPair3D pair = default, ContactKey key = default)
    {
        if (_primitiveStates.TryGetValue(first.Collider, out var a) &&
            _primitiveStates.TryGetValue(second.Collider, out var b))
        {
            var cacheKey=(first.Collider,second.Collider);
            if(_primitiveContacts.Count<32768 || _primitiveContacts.ContainsKey(cacheKey))
                _primitiveContacts[cacheKey] = new(a, b, intersects, pair, key);
        }
    }
}
