using ByteEngine.Core.Graphics;
using ByteEngine.Core.Variables;
using ByteEngine.Core.Classification;

namespace ByteEngine.Core.Scene;

public sealed class Scene
{
    private readonly List<GameObject> _gameObjects =
        new();

    private readonly HashSet<Guid> _pendingDestroy =
        new();
    private readonly Dictionary<Guid, HashSet<GameObject>> _tagIndex = new();
    private readonly HashSet<GameObject>[] _layerIndex = Enumerable.Range(0, 32).Select(_ => new HashSet<GameObject>()).ToArray();

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
    public ClassificationSettings Classification { get; }

    public Camera3D? ActiveCamera
    {
        get
        {
            Camera3D? selected = _gameObjects
                .Where(item => item.ActiveInHierarchy)
                .SelectMany(item => item.Components.OfType<Camera3D>())
                .FirstOrDefault(camera => camera.Enabled && camera.ActiveGameCamera);

            return selected ?? _gameObjects
                .Where(item => item.ActiveInHierarchy)
                .SelectMany(item => item.Components.OfType<Camera3D>())
                .FirstOrDefault(camera => camera.Enabled);
        }
    }

    public void SetActiveCamera(Camera3D? camera)
    {
        if (camera != null && camera.GameObject.Scene != this)
            throw new InvalidOperationException("The active camera must belong to this scene.");

        foreach (Camera3D candidate in _gameObjects.SelectMany(item => item.Components.OfType<Camera3D>()))
            candidate.SetActiveGameCameraWithoutNotification(ReferenceEquals(candidate, camera));
    }

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

    public Scene(string name, ClassificationSettings classification)
        : this(Guid.NewGuid(), name, classification)
    {
    }

    public Scene(
        Guid id,
        string name,
        ClassificationSettings? classification = null)
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
        Classification = classification ?? ClassificationSettings.CreateDefault();
        Classification.EnsureValid();
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
        RegisterClassification(gameObject);

        Camera3D? requestedCamera = gameObject.Components.OfType<Camera3D>()
            .FirstOrDefault(camera => camera.ActiveGameCamera);
        if (requestedCamera != null) SetActiveCamera(requestedCamera);

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

    public GameObject? FindFirstWithTag(Guid tagId) =>
        _tagIndex.TryGetValue(tagId, out HashSet<GameObject>? objects) ? objects.FirstOrDefault(item => item.ActiveInHierarchy) : null;
    public GameObject? FindFirstWithTag(string name) => Classification.FindTag(name) is { } tag ? FindFirstWithTag(tag.Id) : null;
    public IReadOnlyCollection<GameObject> FindGameObjectsWithTag(Guid tagId) =>
        _tagIndex.TryGetValue(tagId, out HashSet<GameObject>? objects) ? objects : Array.Empty<GameObject>();
    public IReadOnlyCollection<GameObject> FindGameObjectsWithTag(string name) =>
        Classification.FindTag(name) is { } tag ? FindGameObjectsWithTag(tag.Id) : Array.Empty<GameObject>();
    public int CountWithTag(Guid tagId) => _tagIndex.TryGetValue(tagId, out HashSet<GameObject>? objects) ? objects.Count : 0;
    public int CountWithTag(string name) => Classification.FindTag(name) is { } tag ? CountWithTag(tag.Id) : 0;
    public IReadOnlyCollection<GameObject> FindGameObjectsOnLayer(int layer) =>
        layer is >= 0 and < 32 ? _layerIndex[layer] : Array.Empty<GameObject>();
    public IEnumerable<GameObject> FindGameObjects(LayerMask mask)
    {
        for (int layer = 0; layer < 32; layer++)
            if (mask.Contains(layer))
                foreach (GameObject gameObject in _layerIndex[layer]) yield return gameObject;
    }

    internal void OnTagAdded(GameObject gameObject, Guid tagId)
    {
        if (!_tagIndex.TryGetValue(tagId, out HashSet<GameObject>? objects)) _tagIndex[tagId] = objects = new();
        objects.Add(gameObject);
    }
    internal void OnTagRemoved(GameObject gameObject, Guid tagId)
    {
        if (!_tagIndex.TryGetValue(tagId, out HashSet<GameObject>? objects)) return;
        objects.Remove(gameObject);
        if (objects.Count == 0) _tagIndex.Remove(tagId);
    }
    internal void OnLayerChanged(GameObject gameObject, int previous, int current)
    {
        if (previous is >= 0 and < 32) _layerIndex[previous].Remove(gameObject);
        _layerIndex[current].Add(gameObject);
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

        UnregisterClassification(gameObject);

        gameObject.DetachFromScene();
        gameObject.DestroyInternal();

        return true;
    }

    private void RegisterClassification(GameObject gameObject)
    {
        _layerIndex[gameObject.Layer].Add(gameObject);
        foreach (Guid tag in gameObject.Tags) OnTagAdded(gameObject, tag);
    }

    private void UnregisterClassification(GameObject gameObject)
    {
        _layerIndex[gameObject.Layer].Remove(gameObject);
        foreach (Guid tag in gameObject.Tags) OnTagRemoved(gameObject, tag);
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
