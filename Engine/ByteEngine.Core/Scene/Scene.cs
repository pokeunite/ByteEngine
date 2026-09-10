using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.Scene;

public sealed class Scene
{
    private readonly List<GameObject> _gameObjects = new();
    private bool _loaded;

    public Guid Id { get; private set; }
    public string Name { get; set; }
    public IReadOnlyList<GameObject> GameObjects => _gameObjects;
    public int GameObjectCount => _gameObjects.Count;
    public bool IsLoaded => _loaded;

    public Scene(string name) : this(Guid.NewGuid(), name) { }
    public Scene(Guid id, string name)
    {
        if (id == Guid.Empty) throw new ArgumentException("Scene ID cannot be empty.", nameof(id));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Scene name cannot be empty.", nameof(name));
        Id = id;
        Name = name;
    }

    public GameObject CreateGameObject(string name = "GameObject")
    {
        var gameObject = new GameObject(name);
        AddGameObject(gameObject);
        return gameObject;
    }

    public void AddGameObject(GameObject gameObject)
    {
        ArgumentNullException.ThrowIfNull(gameObject);
        if (_gameObjects.Contains(gameObject)) return;
        _gameObjects.Add(gameObject);
        gameObject.AttachToScene(this);
        if (_loaded) gameObject.StartInternal();
    }

    public bool DestroyGameObject(GameObject gameObject)
    {
        ArgumentNullException.ThrowIfNull(gameObject);
        if (!_gameObjects.Contains(gameObject)) return false;
        foreach (GameObject child in gameObject.Children.ToArray()) DestroyGameObject(child);
        _gameObjects.Remove(gameObject);
        gameObject.DetachFromScene();
        gameObject.DestroyInternal();
        return true;
    }

    public GameObject? FindGameObject(string name) => _gameObjects.FirstOrDefault(item => item.Name == name);
    public GameObject? FindGameObject(Guid id) => _gameObjects.FirstOrDefault(item => item.Id == id);

    public T? FindComponent<T>() where T : Component
    {
        foreach (GameObject gameObject in _gameObjects)
        {
            if (!gameObject.ActiveInHierarchy) continue;
            T? component = gameObject.GetComponent<T>();
            if (component != null && component.Enabled) return component;
        }
        return null;
    }

    internal void LoadInternal()
    {
        if (_loaded) return;
        _loaded = true;
        Console.WriteLine($"Loading scene: {Name}");
        foreach (GameObject gameObject in _gameObjects) gameObject.StartInternal();
    }

    internal void UpdateInternal()
    {
        if (!_loaded) return;
        foreach (GameObject gameObject in _gameObjects) gameObject.UpdateInternal();
    }

    internal void RenderInternal(Renderer2D renderer)
    {
        if (!_loaded) return;
        foreach (GameObject gameObject in GetRenderOrder()) gameObject.RenderInternal(renderer);
    }

    internal void RenderEditorInternal(Renderer2D renderer)
    {
        foreach (GameObject gameObject in GetRenderOrder()) gameObject.RenderEditorInternal(renderer);
    }

    private IEnumerable<GameObject> GetRenderOrder() =>
        _gameObjects.Select((gameObject, index) => new { gameObject, index })
            .OrderBy(item => item.gameObject.GetComponent<SpriteRenderer>()?.OrderInLayer ?? 0)
            .ThenBy(item => item.index)
            .Select(item => item.gameObject);

    internal void UnloadInternal()
    {
        if (!_loaded) return;
        Console.WriteLine($"Unloading scene: {Name}");
        for (int i = _gameObjects.Count - 1; i >= 0; i--) _gameObjects[i].StopInternal();
        _loaded = false;
    }
}
