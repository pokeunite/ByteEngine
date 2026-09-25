using ByteEngine.Core.Graphics;
using ByteEngine.Core.Variables;
using ByteEngine.Core.Classification;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Physics;

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

    /// <summary>
    /// Scene-owned 3D physics world. It steps after component updates so
    /// gameplay can apply forces or move kinematic objects first.
    /// </summary>
    public PhysicsWorld3D Physics { get; } =
        new();

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

    /// <summary>
    /// Places a newly reparented child at the end of its sibling group in the
    /// serialized Scene order. This keeps the visible Hierarchy order stable
    /// after save/reload.
    /// </summary>
    internal void MoveObjectToEndOfSiblingGroup(
        GameObject gameObject)
    {
        if (!_gameObjects.Contains(gameObject) ||
            gameObject.Parent == null)
        {
            return;
        }

        GameObject parent =
            gameObject.Parent;

        _gameObjects.Remove(gameObject);

        int insertAfter =
            _gameObjects.IndexOf(parent);

        for (int index = 0;
             index < _gameObjects.Count;
             index++)
        {
            if (ReferenceEquals(
                    _gameObjects[index].Parent,
                    parent))
            {
                insertAfter =
                    index;
            }
        }

        _gameObjects.Insert(
            Math.Clamp(insertAfter + 1, 0, _gameObjects.Count),
            gameObject);
    }

    /// <summary>
    /// Keeps the Scene's serialized object order aligned with Hierarchy sibling
    /// order. GameObject validates that both objects are actual siblings.
    /// </summary>
    internal bool MoveObjectRelative(
        GameObject moving,
        GameObject sibling,
        bool after)
    {
        ArgumentNullException.ThrowIfNull(moving);
        ArgumentNullException.ThrowIfNull(sibling);

        if (ReferenceEquals(moving, sibling) ||
            !_gameObjects.Contains(moving) ||
            !_gameObjects.Contains(sibling))
        {
            return false;
        }

        _gameObjects.Remove(moving);

        int siblingIndex =
            _gameObjects.IndexOf(sibling);

        int insertIndex =
            after
                ? siblingIndex + 1
                : siblingIndex;

        _gameObjects.Insert(
            Math.Clamp(insertIndex, 0, _gameObjects.Count),
            moving);

        return true;
    }

    public bool DestroyGameObject(
        GameObject gameObject)
    {
        ArgumentNullException.ThrowIfNull(
            gameObject);

        CrashDebugLog.Write(
            $"Scene.DestroyGameObject: request scene='{Name}' object='{gameObject.Name}' id={gameObject.Id} " +
            $"components=[{string.Join(", ", gameObject.Components.Select(component => component.GetType().FullName))}] " +
            $"isUpdating={_isUpdating}.");

        if (!_gameObjects.Contains(
                gameObject))
        {
            CrashDebugLog.Write(
                "Scene.DestroyGameObject: object is not in this scene; returning false.");

            return false;
        }

        if (_isUpdating)
        {
            _pendingDestroy.Add(
                gameObject.Id);

            gameObject.Active =
                false;

            CrashDebugLog.Write(
                "Scene.DestroyGameObject: queued because scene is updating.");

            return true;
        }

        CrashDebugLog.Write(
            "Scene.DestroyGameObject: entering immediate destruction.");

        bool result =
            DestroyGameObjectImmediate(
                gameObject);

        CrashDebugLog.Write(
            $"Scene.DestroyGameObject: immediate destruction returned {result}.");

        return result;
    }

    public GameObject? FindGameObject(string name) =>
        _gameObjects.FirstOrDefault(item => item.Name == name);

    public GameObject? FindGameObject(Guid id) =>
        _gameObjects.FirstOrDefault(item => item.Id == id);

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
        foreach (GameObject gameObject in _gameObjects)
        {
            if (!gameObject.ActiveInHierarchy) continue;

            T? component =
                gameObject.GetComponent<T>();

            if (component != null && component.Enabled) return component;
        }

        return null;
    }

    internal void LoadInternal()
    {
        if (_loaded) return;

        _loaded = true;

        Console.WriteLine($"Loading scene: {Name}");

        foreach (GameObject gameObject in _gameObjects)
            gameObject.StartInternal();
    }

    internal void UpdateInternal()
    {
        if (!_loaded) return;

        _isUpdating = true;

        try
        {
            GameObject[] frameObjects =
                _gameObjects.ToArray();

            foreach (GameObject gameObject in frameObjects)
            {
                if (_pendingDestroy.Contains(gameObject.Id)) continue;
                gameObject.UpdateInternal();
            }

            /*
             * Physics runs while _isUpdating is still true. Collision/trigger
             * callbacks are therefore allowed to destroy GameObjects safely:
             * destruction is queued until the complete gameplay+physics step
             * has finished.
             */
            Physics.Step(
                this,
                (float)Time.DeltaTime);

            /*
             * LateUpdate is a distinct, scene-wide pass after normal gameplay
             * updates and physics. This gives camera rigs and other post-motion
             * systems the final transforms for the frame instead of making them
             * depend on GameObject/component ordering.
             *
             * The same frame snapshot is used intentionally: objects created
             * during Update start normally but enter the update/late-update pair
             * on the following frame. Objects queued for destruction are skipped.
             */
            foreach (GameObject gameObject in frameObjects)
            {
                if (_pendingDestroy.Contains(gameObject.Id)) continue;
                gameObject.LateUpdateInternal();
            }

            SkeletalAttachmentService.UpdateScene(this);
        }
        finally
        {
            _isUpdating = false;
            FlushPendingDestroy();
        }
    }

    internal void RenderInternal(RenderContext context)
    {
        if (!_loaded) return;

        context.Begin3DFrame();
        SkeletalAttachmentService.UpdateScene(this);

        foreach (GameObject gameObject in GetRenderOrder())
            gameObject.RenderInternal(context);

        context.Flush3D();
    }

    internal void RenderEditorInternal(RenderContext context, EditorSkeletalPreviewCache? skeletalPreview = null)
    {
        try
        {
            skeletalPreview?.Prepare(this);
            context.Begin3DFrame();
            SkeletalAttachmentService.UpdateScene(this);

            foreach (GameObject gameObject in GetRenderOrder())
                gameObject.RenderEditorInternal(context);

            skeletalPreview?.Render(context);
            context.Flush3D();
        }
        finally
        {
            skeletalPreview?.RestoreStaticMeshes();
        }
    }

    internal void UnloadInternal()
    {
        if (!_loaded) return;

        Console.WriteLine($"Unloading scene: {Name}");

        for (int index = _gameObjects.Count - 1; index >= 0; index--)
            _gameObjects[index].StopInternal();

        _pendingDestroy.Clear();
        Physics.Reset();
        _isUpdating = false;
        _loaded = false;
    }

    private bool DestroyGameObjectImmediate(
        GameObject gameObject)
    {
        CrashDebugLog.Write(
            $"Scene.DestroyGameObjectImmediate: BEGIN object='{gameObject.Name}' id={gameObject.Id}.");

        if (!_gameObjects.Contains(gameObject))
        {
            CrashDebugLog.Write(
                "Scene.DestroyGameObjectImmediate: object already absent.");

            return false;
        }

        foreach (GameObject child in gameObject.Children.ToArray())
        {
            CrashDebugLog.Write(
                $"Scene.DestroyGameObjectImmediate: destroying child '{child.Name}' id={child.Id}.");

            DestroyGameObjectImmediate(child);
        }

        CrashDebugLog.Write(
            "Scene.DestroyGameObjectImmediate: removing pending-destroy id.");

        _pendingDestroy.Remove(gameObject.Id);

        CrashDebugLog.Write(
            "Scene.DestroyGameObjectImmediate: removing object from scene list.");

        _gameObjects.Remove(gameObject);

        CrashDebugLog.Write(
            "Scene.DestroyGameObjectImmediate: unregistering classification.");

        UnregisterClassification(gameObject);

        CrashDebugLog.Write(
            "Scene.DestroyGameObjectImmediate: detaching object from scene.");

        gameObject.DetachFromScene();

        CrashDebugLog.Write(
            "Scene.DestroyGameObjectImmediate: calling GameObject.DestroyInternal.");

        gameObject.DestroyInternal();

        CrashDebugLog.Write(
            $"Scene.DestroyGameObjectImmediate: COMPLETE object='{gameObject.Name}' id={gameObject.Id}.");

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
        if (_pendingDestroy.Count == 0) return;

        Guid[] pending =
            _pendingDestroy.ToArray();

        _pendingDestroy.Clear();

        foreach (Guid id in pending)
        {
            GameObject? gameObject =
                FindGameObject(id);

            if (gameObject != null)
                DestroyGameObjectImmediate(gameObject);
        }
    }

    private IEnumerable<GameObject> GetRenderOrder()
    {
        return _gameObjects
            .Select((gameObject, index) => new { gameObject, index })
            .OrderBy(item => item.gameObject.RenderOrder)
            .ThenBy(item => item.index)
            .Select(item => item.gameObject);
    }
}
