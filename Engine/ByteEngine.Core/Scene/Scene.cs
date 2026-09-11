using ByteEngine.Core.Graphics;
using ByteEngine.Core.Variables;

namespace ByteEngine.Core.Scene;

public sealed class Scene
{
    private readonly List<GameObject> _gameObjects =
        new();

    private readonly HashSet<Guid> _pendingDestroy =
        new();

    private bool _loaded;
    private bool _isUpdating;

    public Guid Id { get; private set; }
    public string Name { get; set; }

    public IReadOnlyList<GameObject> GameObjects =>
        _gameObjects;

    public int GameObjectCount =>
        _gameObjects.Count;

    public bool IsLoaded =>
        _loaded;

    public VariableStore Variables { get; } =
        new();

    internal VariableStore? RuntimeGlobals { get; set; }

    public Scene(
        string name)
        : this(
            Guid.NewGuid(),
            name)
    {
    }

    public Scene(
        Guid id,
        string name)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "Scene ID cannot be empty.",
                nameof(id));
        }

        if (string.IsNullOrWhiteSpace(
                name))
        {
            throw new ArgumentException(
                "Scene name cannot be empty.",
                nameof(name));
        }

        Id = id;
        Name = name;
    }

    public GameObject CreateGameObject(
        string name = "GameObject")
    {
        var gameObject =
            new GameObject(
                name);

        AddGameObject(
            gameObject);

        return gameObject;
    }

    public void AddGameObject(
        GameObject gameObject)
    {
        ArgumentNullException.ThrowIfNull(
            gameObject);

        if (_gameObjects.Contains(
                gameObject))
        {
            return;
        }

        _gameObjects.Add(
            gameObject);

        gameObject.AttachToScene(
            this);

        if (_loaded)
        {
            gameObject.StartInternal();
        }
    }

    public bool DestroyGameObject(
        GameObject gameObject)
    {
        ArgumentNullException.ThrowIfNull(
            gameObject);

        if (!_gameObjects.Contains(
                gameObject))
        {
            return false;
        }

        /*
         * Visual Logic can destroy an object while Scene.UpdateInternal
         * is iterating the scene. Removing from the list immediately
         * would invalidate that iteration and could crash play mode.
         *
         * Queue the destruction until the current update finishes.
         */
        if (_isUpdating)
        {
            _pendingDestroy.Add(
                gameObject.Id);

            /*
             * Disable it immediately so it cannot continue rendering
             * or participate in later hierarchy activity this frame.
             */
            gameObject.Active =
                false;

            return true;
        }

        return DestroyGameObjectImmediate(
            gameObject);
    }

    public GameObject? FindGameObject(
        string name)
    {
        return _gameObjects.FirstOrDefault(
            item =>
                item.Name ==
                name);
    }

    public GameObject? FindGameObject(
        Guid id)
    {
        return _gameObjects.FirstOrDefault(
            item =>
                item.Id ==
                id);
    }

    public T? FindComponent<T>()
        where T : Component
    {
        foreach (GameObject gameObject
                 in _gameObjects)
        {
            if (!gameObject.ActiveInHierarchy)
            {
                continue;
            }

            T? component =
                gameObject.GetComponent<T>();

            if (component != null &&
                component.Enabled)
            {
                return component;
            }
        }

        return null;
    }

    internal void LoadInternal()
    {
        if (_loaded)
        {
            return;
        }

        _loaded =
            true;

        Console.WriteLine(
            $"Loading scene: {Name}");

        foreach (GameObject gameObject
                 in _gameObjects)
        {
            gameObject.StartInternal();
        }
    }

    internal void UpdateInternal()
    {
        if (!_loaded)
        {
            return;
        }

        _isUpdating =
            true;

        try
        {
            /*
             * Snapshot the list so objects created during an update
             * begin updating on the next frame, not halfway through
             * the current frame.
             */
            foreach (GameObject gameObject
                     in _gameObjects.ToArray())
            {
                if (_pendingDestroy.Contains(
                        gameObject.Id))
                {
                    continue;
                }

                gameObject.UpdateInternal();
            }
        }
        finally
        {
            _isUpdating =
                false;

            FlushPendingDestroy();
        }
    }

    internal void RenderInternal(
        RenderContext context)
    {
        if (!_loaded)
        {
            return;
        }

        foreach (GameObject gameObject
                 in GetRenderOrder())
        {
            gameObject.RenderInternal(
                context);
        }
    }

    internal void RenderEditorInternal(
        RenderContext context)
    {
        foreach (GameObject gameObject
                 in GetRenderOrder())
        {
            gameObject.RenderEditorInternal(
                context);
        }
    }

    internal void UnloadInternal()
    {
        if (!_loaded)
        {
            return;
        }

        Console.WriteLine(
            $"Unloading scene: {Name}");

        for (int index =
                 _gameObjects.Count -
                 1;
             index >=
             0;
             index--)
        {
            _gameObjects[index]
                .StopInternal();
        }

        _pendingDestroy.Clear();
        _isUpdating =
            false;
        _loaded =
            false;
    }

    private bool DestroyGameObjectImmediate(
        GameObject gameObject)
    {
        if (!_gameObjects.Contains(
                gameObject))
        {
            return false;
        }

        foreach (GameObject child
                 in gameObject.Children.ToArray())
        {
            DestroyGameObjectImmediate(
                child);
        }

        _pendingDestroy.Remove(
            gameObject.Id);

        _gameObjects.Remove(
            gameObject);

        gameObject.DetachFromScene();
        gameObject.DestroyInternal();

        return true;
    }

    private void FlushPendingDestroy()
    {
        if (_pendingDestroy.Count ==
            0)
        {
            return;
        }

        Guid[] pending =
            _pendingDestroy.ToArray();

        _pendingDestroy.Clear();

        foreach (Guid id
                 in pending)
        {
            GameObject? gameObject =
                FindGameObject(
                    id);

            if (gameObject != null)
            {
                DestroyGameObjectImmediate(
                    gameObject);
            }
        }
    }

    private IEnumerable<GameObject> GetRenderOrder()
    {
        return _gameObjects
            .Select(
                (gameObject, index) =>
                    new
                    {
                        gameObject,
                        index
                    })
            .OrderBy(
                item =>
                    item.gameObject.RenderOrder)
            .ThenBy(
                item =>
                    item.index)
            .Select(
                item =>
                    item.gameObject);
    }
}
