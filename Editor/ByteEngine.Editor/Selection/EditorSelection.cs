using ByteEngine.Core.Scene;

namespace ByteEngine.Editor.Selection;

internal sealed class EditorSelection
{
    private readonly List<GameObject> _objects = new();
    public IReadOnlyList<GameObject> Objects => _objects;
    public GameObject? Primary => _objects.LastOrDefault();
    public int Count => _objects.Count;
    public bool Contains(GameObject gameObject) => _objects.Contains(gameObject);

    public void Set(GameObject? gameObject)
    {
        _objects.Clear();
        if (gameObject != null) _objects.Add(gameObject);
    }

    public void Set(IEnumerable<GameObject> gameObjects)
    {
        _objects.Clear();
        foreach (GameObject gameObject in gameObjects.Distinct()) _objects.Add(gameObject);
    }

    public void Toggle(GameObject gameObject)
    {
        if (!_objects.Remove(gameObject)) _objects.Add(gameObject);
    }

    public void Clear() => _objects.Clear();
}
