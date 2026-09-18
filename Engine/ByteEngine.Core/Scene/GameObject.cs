using ByteEngine.Core.Graphics;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Physics;

namespace ByteEngine.Core.Scene;

public sealed class GameObject
{
    private readonly List<Component> _components = new();
    private readonly List<GameObject> _children = new();
    private readonly HashSet<Guid> _tags = new();
    private int _layer;
    private bool _started;
    private Scene? _scene;

    public Guid Id { get; private set; }
    public string Name { get; set; }
    public bool Active { get; set; } = true;
    public bool ActiveInHierarchy => Active && (Parent?.ActiveInHierarchy ?? true);
    public Transform Transform { get; }
    public IReadOnlyList<Component> Components => _components;
    internal int RenderOrder =>
        _components.Select(component => component.RenderOrder)
            .FirstOrDefault(order => order.HasValue) ?? 0;
    public GameObject? Parent { get; private set; }
    public IReadOnlyList<GameObject> Children => _children;
    public Scene? Scene => _scene;
    public IReadOnlyCollection<Guid> Tags => _tags;

    public int Layer
    {
        get => _layer;
        set
        {
            int safe = value is >= 0 and < 32 ? value : 0;
            if (_layer == safe) return;
            int previous = _layer;
            _layer = safe;
            _scene?.OnLayerChanged(this, previous, safe);
        }
    }

    public Variables.VariableStore Variables { get; } = new();

    public GameObject(string name = "GameObject") : this(Guid.NewGuid(), name) { }

    public GameObject(Guid id, string name = "GameObject")
    {
        if (id == Guid.Empty) throw new ArgumentException("GameObject ID cannot be empty.", nameof(id));
        Id = id;
        Name = name;
        Transform = new Transform(this);
    }

    public bool SetParent(GameObject? parent, bool worldPositionStays = true)
    {
        if (ReferenceEquals(parent, this) || (parent != null && parent.IsDescendantOf(this))) return false;
        if (parent != null && _scene != null && parent._scene != _scene) return false;
        if (ReferenceEquals(Parent, parent)) return true;

        var position = Transform.WorldPosition;
        var rotation = Transform.WorldRotation;
        var scale = Transform.WorldScale;
        Parent?._children.Remove(this);
        Parent = parent;
        Parent?._children.Add(this);

        _scene?.MoveObjectToEndOfSiblingGroup(this);

        if (worldPositionStays)
        {
            Transform.WorldPosition = position;
            Transform.WorldRotation = rotation;
            Transform.WorldScale = scale;
        }

        return true;
    }

    /// <summary>
    /// Moves this object immediately before a sibling in Hierarchy order.
    /// Both objects must belong to the same Scene and have the same parent.
    /// </summary>
    public bool MoveBefore(GameObject sibling) =>
        MoveRelative(sibling, after: false);

    /// <summary>
    /// Moves this object immediately after a sibling in Hierarchy order.
    /// Both objects must belong to the same Scene and have the same parent.
    /// </summary>
    public bool MoveAfter(GameObject sibling) =>
        MoveRelative(sibling, after: true);

    private bool MoveRelative(GameObject sibling, bool after)
    {
        ArgumentNullException.ThrowIfNull(sibling);

        if (ReferenceEquals(this, sibling) ||
            _scene == null ||
            !ReferenceEquals(_scene, sibling._scene) ||
            !ReferenceEquals(Parent, sibling.Parent))
        {
            return false;
        }

        if (Parent != null)
        {
            List<GameObject> siblings = Parent._children;

            if (!siblings.Contains(this) || !siblings.Contains(sibling))
            {
                return false;
            }

            siblings.Remove(this);

            int siblingIndex = siblings.IndexOf(sibling);
            int insertIndex = after ? siblingIndex + 1 : siblingIndex;

            siblings.Insert(
                Math.Clamp(insertIndex, 0, siblings.Count),
                this);
        }

        return _scene.MoveObjectRelative(this, sibling, after);
    }

    public bool IsDescendantOf(GameObject possibleAncestor)
    {
        for (GameObject? current = Parent; current != null; current = current.Parent)
            if (ReferenceEquals(current, possibleAncestor)) return true;
        return false;
    }

    public bool HasTag(Guid tagId) => tagId != Guid.Empty && _tags.Contains(tagId);
    public bool HasTag(string name) => _scene?.Classification.FindTag(name) is { } tag && HasTag(tag.Id);

    public bool AddTag(Guid tagId)
    {
        if (tagId == Guid.Empty || !_tags.Add(tagId)) return false;
        _scene?.OnTagAdded(this, tagId);
        return true;
    }

    public bool AddTag(string name) => _scene?.Classification.FindTag(name) is { } tag && AddTag(tag.Id);

    public bool RemoveTag(Guid tagId)
    {
        if (!_tags.Remove(tagId)) return false;
        _scene?.OnTagRemoved(this, tagId);
        return true;
    }

    public bool RemoveTag(string name) => _scene?.Classification.FindTag(name) is { } tag && RemoveTag(tag.Id);

    internal void SetTags(IEnumerable<Guid>? tags)
    {
        foreach (Guid oldTag in _tags.ToArray()) RemoveTag(oldTag);
        if (tags == null) return;
        foreach (Guid tag in tags) AddTag(tag);
    }

    internal void AttachToScene(Scene scene) => _scene = scene;
    internal void DetachFromScene() { SetParent(null); _scene = null; }

    public T AddComponent<T>(T component) where T : Component
    {
        ArgumentNullException.ThrowIfNull(component);
        component.Attach(this);
        _components.Add(component);

        if (component is Camera3D { ActiveGameCamera: true } camera && _scene != null)
            _scene.SetActiveCamera(camera);

        if (_started) component.StartInternal();
        return component;
    }

    public T? GetComponent<T>() where T : Component => _components.OfType<T>().FirstOrDefault();

    public bool TryGetComponent<T>(out T? component)
        where T : Component
    {
        component = GetComponent<T>();
        return component != null;
    }

    public bool HasComponent<T>()
        where T : Component =>
        GetComponent<T>() != null;

    public bool RemoveComponent(Component component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (!_components.Remove(component)) return false;

        CrashDebugLog.Write(
            $"GameObject.RemoveComponent: object='{Name}' component='{component.GetType().FullName}' BEGIN.");

        component.DestroyInternal();

        CrashDebugLog.Write(
            $"GameObject.RemoveComponent: object='{Name}' component='{component.GetType().FullName}' COMPLETE.");

        return true;
    }

    internal void StartInternal()
    {
        if (_started) return;
        _started = true;
        foreach (Component component in _components) component.StartInternal();
    }

    internal void UpdateInternal()
    {
        if (!ActiveInHierarchy) return;

        if (!_started) StartInternal();

        foreach (Component component
                 in _components.OrderBy(component => component.UpdateOrder))
        {
            component.UpdateInternal();
        }
    }

    internal void LateUpdateInternal()
    {
        if (!ActiveInHierarchy) return;

        if (!_started) StartInternal();

        foreach (Component component
                 in _components.OrderBy(component => component.UpdateOrder))
        {
            component.LateUpdateInternal();
        }
    }

    internal void RenderInternal(Graphics.RenderContext context)
    {
        if (!ActiveInHierarchy) return;
        if (!_started) StartInternal();
        foreach (Component component in _components) component.RenderInternal(context);
    }

    internal void RenderEditorInternal(Graphics.RenderContext context)
    {
        if (!ActiveInHierarchy) return;
        foreach (Component component in _components) component.RenderEditorInternal(context);
    }

    internal void StopInternal()
    {
        for (int i = _components.Count - 1; i >= 0; i--)
            _components[i].StopInternal();

        _started = false;
    }


    internal void DispatchCollisionEnter(
        PhysicsContact3D contact)
    {
        foreach (Component component
                 in _components.ToArray())
        {
            component.CollisionEnterInternal(
                contact);
        }
    }

    internal void DispatchCollisionStay(
        PhysicsContact3D contact)
    {
        foreach (Component component
                 in _components.ToArray())
        {
            component.CollisionStayInternal(
                contact);
        }
    }

    internal void DispatchCollisionExit(
        PhysicsContact3D contact)
    {
        foreach (Component component
                 in _components.ToArray())
        {
            component.CollisionExitInternal(
                contact);
        }
    }

    internal void DispatchTriggerEnter(
        PhysicsContact3D contact)
    {
        foreach (Component component
                 in _components.ToArray())
        {
            component.TriggerEnterInternal(
                contact);
        }
    }

    internal void DispatchTriggerStay(
        PhysicsContact3D contact)
    {
        foreach (Component component
                 in _components.ToArray())
        {
            component.TriggerStayInternal(
                contact);
        }
    }

    internal void DispatchTriggerExit(
        PhysicsContact3D contact)
    {
        foreach (Component component
                 in _components.ToArray())
        {
            component.TriggerExitInternal(
                contact);
        }
    }

    internal void DestroyInternal()
    {
        CrashDebugLog.Write(
            $"GameObject.DestroyInternal: BEGIN object='{Name}' id={Id} componentCount={_components.Count}.");

        for (int i = _components.Count - 1; i >= 0; i--)
        {
            Component component =
                _components[i];

            CrashDebugLog.Write(
                $"GameObject.DestroyInternal: destroying component index={i} type='{component.GetType().FullName}'.");

            component.DestroyInternal();

            CrashDebugLog.Write(
                $"GameObject.DestroyInternal: destroyed component index={i} type='{component.GetType().FullName}'.");
        }

        CrashDebugLog.Write(
            "GameObject.DestroyInternal: clearing component collection.");

        _components.Clear();
        _started = false;

        CrashDebugLog.Write(
            $"GameObject.DestroyInternal: COMPLETE object='{Name}' id={Id}.");
    }
}
