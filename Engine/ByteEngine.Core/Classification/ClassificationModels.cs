namespace ByteEngine.Core.Classification;

public sealed class TagDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Tag";
}

public sealed class ObjectLayerDefinition
{
    public int Index { get; set; }
    public string Name { get; set; } = string.Empty;
}

public readonly struct LayerMask : IEquatable<LayerMask>
{
    internal uint Bits { get; }
    internal LayerMask(uint bits) => Bits = bits;
    public static LayerMask None => new(0u);
    public static LayerMask All => new(uint.MaxValue);
    public bool Contains(int layer) => layer is >= 0 and < 32 && (Bits & (1u << layer)) != 0;
    public LayerMask Add(int layer) => layer is >= 0 and < 32 ? new(Bits | (1u << layer)) : this;
    public LayerMask Remove(int layer) => layer is >= 0 and < 32 ? new(Bits & ~(1u << layer)) : this;
    public static LayerMask FromLayers(params int[] layers)
    {
        LayerMask result = None;
        foreach (int layer in layers) result = result.Add(layer);
        return result;
    }
    internal static LayerMask FromBits(uint bits) => new(bits);
    public bool Equals(LayerMask other) => Bits == other.Bits;
    public override bool Equals(object? obj) => obj is LayerMask other && Equals(other);
    public override int GetHashCode() => Bits.GetHashCode();
    public static bool operator ==(LayerMask left, LayerMask right) => left.Equals(right);
    public static bool operator !=(LayerMask left, LayerMask right) => !left.Equals(right);
}

public sealed class CollisionMatrix
{
    public uint[] LayerMasks { get; set; } = Enumerable.Repeat(uint.MaxValue, 32).ToArray();
    public bool ShouldInteract(int first, int second) => first is >= 0 and < 32 && second is >= 0 and < 32 && (LayerMasks[first] & (1u << second)) != 0;
    public void SetInteraction(int first, int second, bool interacts)
    {
        if (first is < 0 or >= 32 || second is < 0 or >= 32) return;
        SetBit(first, second, interacts); SetBit(second, first, interacts);
    }
    public LayerMask GetMask(int layer) => layer is >= 0 and < 32 ? LayerMask.FromBits(LayerMasks[layer]) : LayerMask.None;
    public void EnsureValid()
    {
        if (LayerMasks == null || LayerMasks.Length != 32)
        {
            uint[] repaired = Enumerable.Repeat(uint.MaxValue, 32).ToArray();
            if (LayerMasks != null) Array.Copy(LayerMasks, repaired, Math.Min(32, LayerMasks.Length));
            LayerMasks = repaired;
        }
        for (int a = 0; a < 32; a++) for (int b = a + 1; b < 32; b++)
            if (((LayerMasks[a] >> b) & 1u) != ((LayerMasks[b] >> a) & 1u)) SetInteraction(a, b, ((LayerMasks[a] >> b) & 1u) != 0);
    }
    private void SetBit(int row, int column, bool value)
    {
        uint bit = 1u << column;
        LayerMasks[row] = value ? LayerMasks[row] | bit : LayerMasks[row] & ~bit;
    }
}

public sealed class ClassificationSettings
{
    public List<TagDefinition> Tags { get; set; } = CreateDefaultTags();
    public List<ObjectLayerDefinition> Layers { get; set; } = CreateDefaultLayers();
    public CollisionMatrix CollisionMatrix { get; set; } = new();
    public TagDefinition? FindTag(Guid id) => id == Guid.Empty ? null : Tags.FirstOrDefault(item => item.Id == id);
    public TagDefinition? FindTag(string? name) => string.IsNullOrWhiteSpace(name) ? null : Tags.FirstOrDefault(item => string.Equals(item.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    public ObjectLayerDefinition? FindLayer(int index) => Layers.FirstOrDefault(item => item.Index == index);
    public ObjectLayerDefinition? FindLayer(string? name) => string.IsNullOrWhiteSpace(name) ? null : Layers.FirstOrDefault(item => string.Equals(item.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    public TagDefinition? AddTag(string name)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (trimmed.Length == 0 || FindTag(trimmed) != null) return null;
        var definition = new TagDefinition { Name = trimmed }; Tags.Add(definition); return definition;
    }
    public bool RenameTag(Guid id, string name)
    {
        TagDefinition? tag = FindTag(id); string trimmed = name?.Trim() ?? string.Empty;
        if (tag == null || trimmed.Length == 0 || Tags.Any(item => item.Id != id && string.Equals(item.Name, trimmed, StringComparison.OrdinalIgnoreCase))) return false;
        tag.Name = trimmed; return true;
    }
    public bool TryRemoveTag(Guid id, IEnumerable<Scene.GameObject> objects, out int usageCount)
    {
        usageCount = objects.Count(item => item.HasTag(id));
        if (usageCount != 0) return false;
        return Tags.RemoveAll(item => item.Id == id) > 0;
    }
    public bool DefineLayer(int index, string name)
    {
        string trimmed = name?.Trim() ?? string.Empty;
        if (index is < 0 or >= 32 || trimmed.Length == 0 || Layers.Any(item => item.Index != index && string.Equals(item.Name, trimmed, StringComparison.OrdinalIgnoreCase))) return false;
        ObjectLayerDefinition? layer = FindLayer(index);
        if (layer == null) Layers.Add(new ObjectLayerDefinition { Index = index, Name = trimmed }); else layer.Name = trimmed;
        Layers.Sort((a, b) => a.Index.CompareTo(b.Index)); return true;
    }
    public bool TryClearLayer(int index, IEnumerable<Scene.GameObject> objects, out int usageCount)
    {
        usageCount = objects.Count(item => item.Layer == index);
        if (index == 0 || usageCount != 0) return false;
        return Layers.RemoveAll(item => item.Index == index) > 0;
    }
    public void EnsureValid()
    {
        Tags ??= new(); Layers ??= new(); CollisionMatrix ??= new();
        Tags.RemoveAll(item => item == null || item.Id == Guid.Empty || string.IsNullOrWhiteSpace(item.Name));
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase); Tags.RemoveAll(item => !names.Add(item.Name = item.Name.Trim()));
        Layers.RemoveAll(item => item == null || item.Index is < 0 or >= 32 || string.IsNullOrWhiteSpace(item.Name));
        Layers = Layers.GroupBy(item => item.Index).Select(group => group.First()).OrderBy(item => item.Index).ToList();
        ObjectLayerDefinition? defaultLayer = FindLayer(0);
        if (defaultLayer == null) Layers.Insert(0, new ObjectLayerDefinition { Index = 0, Name = "Default" }); else defaultLayer.Name = "Default";
        CollisionMatrix.EnsureValid();
    }
    public static ClassificationSettings CreateDefault() { var settings = new ClassificationSettings(); settings.EnsureValid(); return settings; }
    private static List<TagDefinition> CreateDefaultTags() => new[] { "Player", "Enemy", "Pickup", "Interactable" }.Select(name => new TagDefinition { Name = name }).ToList();
    private static List<ObjectLayerDefinition> CreateDefaultLayers() => new[] { "Default", "World", "Player", "Enemy", "PlayerProjectile", "EnemyProjectile", "Trigger" }.Select((name, index) => new ObjectLayerDefinition { Index = index, Name = name }).ToList();
}
