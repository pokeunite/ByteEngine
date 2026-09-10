using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.Scene;

public sealed class Scene
{
    private readonly List<GameObject> _gameObjects =
        new();

    private bool _loaded;

    public Guid Id { get; private set; }

    public string Name { get; set; }

    public IReadOnlyList<GameObject> GameObjects =>
        _gameObjects;

    public int GameObjectCount =>
        _gameObjects.Count;

    public bool IsLoaded =>
        _loaded;

    public Scene(
        string name)
        : this(
            Guid.NewGuid(),
            name
        )
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
                nameof(id)
            );
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException(
                "Scene name cannot be empty.",
                nameof(name)
            );
        }

        Id = id;
        Name = name;
    }

    public GameObject CreateGameObject(
        string name = "GameObject")
    {
        GameObject gameObject =
            new(name);

        AddGameObject(
            gameObject
        );

        return gameObject;
    }

    public void AddGameObject(
        GameObject gameObject)
    {
        ArgumentNullException.ThrowIfNull(
            gameObject
        );

        if (_gameObjects.Contains(
                gameObject))
        {
            return;
        }

        _gameObjects.Add(
            gameObject
        );

        if (_loaded)
        {
            gameObject.StartInternal();
        }
    }

    public bool DestroyGameObject(
        GameObject gameObject)
    {
        ArgumentNullException.ThrowIfNull(
            gameObject
        );

        bool removed =
            _gameObjects.Remove(
                gameObject
            );

        if (!removed)
        {
            return false;
        }

        gameObject.DestroyInternal();

        return true;
    }

    public GameObject? FindGameObject(
        string name)
    {
        for (int i = 0;
             i < _gameObjects.Count;
             i++)
        {
            if (_gameObjects[i].Name == name)
            {
                return _gameObjects[i];
            }
        }

        return null;
    }

    public GameObject? FindGameObject(
        Guid id)
    {
        for (int i = 0;
             i < _gameObjects.Count;
             i++)
        {
            if (_gameObjects[i].Id == id)
            {
                return _gameObjects[i];
            }
        }

        return null;
    }

    public T? FindComponent<T>()
        where T : Component
    {
        for (int i = 0;
             i < _gameObjects.Count;
             i++)
        {
            if (!_gameObjects[i].Active)
            {
                continue;
            }

            T? component =
                _gameObjects[i]
                    .GetComponent<T>();

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

        _loaded = true;

        Console.WriteLine(
            $"Loading scene: {Name}"
        );

        for (int i = 0;
             i < _gameObjects.Count;
             i++)
        {
            _gameObjects[i]
                .StartInternal();
        }
    }

    internal void UpdateInternal()
    {
        if (!_loaded)
        {
            return;
        }

        for (int i = 0;
             i < _gameObjects.Count;
             i++)
        {
            _gameObjects[i]
                .UpdateInternal();
        }
    }

    internal void RenderInternal(
        Renderer2D renderer)
    {
        if (!_loaded)
        {
            return;
        }

        for (int i = 0;
             i < _gameObjects.Count;
             i++)
        {
            _gameObjects[i]
                .RenderInternal(
                    renderer
                );
        }
    }

    internal void RenderEditorInternal(
        Renderer2D renderer)
    {
        for (int i = 0;
             i < _gameObjects.Count;
             i++)
        {
            _gameObjects[i]
                .RenderEditorInternal(
                    renderer
                );
        }
    }

    internal void UnloadInternal()
    {
        if (!_loaded)
        {
            return;
        }

        Console.WriteLine(
            $"Unloading scene: {Name}"
        );

        for (int i = _gameObjects.Count - 1;
             i >= 0;
             i--)
        {
            _gameObjects[i]
                .StopInternal();
        }

        _loaded = false;
    }
}
