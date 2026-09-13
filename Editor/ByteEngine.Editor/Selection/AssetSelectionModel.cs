using System.Numerics;

namespace ByteEngine.Editor.Selection;

internal readonly record struct AssetSelectionBounds(string Path, Vector2 Minimum, Vector2 Maximum)
{
    public bool Intersects(Vector2 minimum, Vector2 maximum) =>
        Minimum.X <= maximum.X && Maximum.X >= minimum.X &&
        Minimum.Y <= maximum.Y && Maximum.Y >= minimum.Y;
}

internal sealed class AssetSelectionModel
{
    private readonly HashSet<string> _selected = new(StringComparer.OrdinalIgnoreCase);
    private int? _anchorIndex;

    public IReadOnlyCollection<string> Paths => _selected;
    public string? PrimaryPath { get; private set; }
    public int Count => _selected.Count;
    public bool Contains(string path) => _selected.Contains(path);

    public void Click(IReadOnlyList<string> orderedPaths, int index, bool control, bool shift)
    {
        if (index < 0 || index >= orderedPaths.Count) return;
        string path = orderedPaths[index];
        if (shift && _anchorIndex.HasValue)
        {
            if (!control) _selected.Clear();
            int start = Math.Min(_anchorIndex.Value, index);
            int end = Math.Max(_anchorIndex.Value, index);
            for (int item = start; item <= end; item++) _selected.Add(orderedPaths[item]);
            PrimaryPath = path;
        }
        else if (control)
        {
            if (!_selected.Remove(path))
            {
                _selected.Add(path);
                PrimaryPath = path;
            }
            else if (string.Equals(PrimaryPath, path, StringComparison.OrdinalIgnoreCase))
            {
                PrimaryPath = _selected.LastOrDefault();
            }
            _anchorIndex = index;
        }
        else
        {
            _selected.Clear();
            _selected.Add(path);
            PrimaryPath = path;
            _anchorIndex = index;
        }
    }

    public void Marquee(IEnumerable<AssetSelectionBounds> bounds, Vector2 start, Vector2 end, bool additive)
    {
        if (!additive) _selected.Clear();
        Vector2 minimum = Vector2.Min(start, end);
        Vector2 maximum = Vector2.Max(start, end);
        foreach (AssetSelectionBounds item in bounds)
        {
            if (!item.Intersects(minimum, maximum)) continue;
            _selected.Add(item.Path);
            PrimaryPath = item.Path;
        }
        if (_selected.Count == 0) PrimaryPath = null;
    }

    public void Retain(IEnumerable<string> available)
    {
        HashSet<string> valid = available.ToHashSet(StringComparer.OrdinalIgnoreCase);
        _selected.RemoveWhere(path => !valid.Contains(path));
        if (PrimaryPath != null && !_selected.Contains(PrimaryPath))
            PrimaryPath = _selected.LastOrDefault();
    }

    public void Clear()
    {
        _selected.Clear();
        PrimaryPath = null;
        _anchorIndex = null;
    }
}
