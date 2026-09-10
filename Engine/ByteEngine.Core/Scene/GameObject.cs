namespace ByteEngine.Core.Scene;

public sealed class GameObject
{
    private readonly List<Component> _components = new();
    private readonly List<GameObject> _children = new();
    private bool _started;
    private Scene? _scene;

    public Guid Id { get; private set; }
    public string Name { get; set; }
    public bool Active { get; set; } = true;
    public bool ActiveInHierarchy => Active && (Parent?.ActiveInHierarchy ?? true);
    public Transform2D Transform { get; }
    public IReadOnlyList<Component> Components => _components;
    public GameObject? Parent { get; private set; }
    public IReadOnlyList<GameObject> Children => _children;

    public GameObject(string name = "GameObject") : this(Guid.NewGuid(), name) { }

    public GameObject(Guid id, string name = "GameObject")
    {
        if (id == Guid.Empty) throw new ArgumentException("GameObject ID cannot be empty.", nameof(id));
        Id = id;
        Name = name;
        Transform = new Transform2D(this);
    }

    public bool SetParent(GameObject? parent, bool worldPositionStays = true)
    {
        if (ReferenceEquals(parent, this) || (parent != null && parent.IsDescendantOf(this))) return false;
        if (parent != null && _scene != null && parent._scene != _scene) return false;
        if (ReferenceEquals(Parent, parent)) return true;

        var position = Transform.Position;
        float rotation = Transform.Rotation;
        var size = Transform.Size;
        Parent?._children.Remove(this);
        Parent = parent;
        Parent?._children.Add(this);
        if (worldPositionStays)
        {
            Transform.Position = position;
            Transform.Rotation = rotation;
            Transform.Size = size;
        }
        return true;
    }

    public bool IsDescendantOf(GameObject possibleAncestor)
    {
        for (GameObject? current = Parent; current != null; current = current.Parent)
            if (ReferenceEquals(current, possibleAncestor)) return true;
        return false;
    }

    internal void AttachToScene(Scene scene) => _scene = scene;
    internal void DetachFromScene() { SetParent(null); _scene = null; }

    public T AddComponent<T>(T component) where T : Component
    {
        ArgumentNullException.ThrowIfNull(component);
        component.Attach(this);
        _components.Add(component);
        if (_started) component.StartInternal();
        return component;
    }

    public T? GetComponent<T>() where T : Component => _components.OfType<T>().FirstOrDefault();
    public bool TryGetComponent<T>(out T? component) where T : Component { component = GetComponent<T>(); return component != null; }
    public bool HasComponent<T>() where T : Component => GetComponent<T>() != null;

    public bool RemoveComponent(Component component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (!_components.Remove(component)) return false;
        component.DestroyInternal();
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
        foreach (Component component in _components) component.UpdateInternal();
    }

    internal void RenderInternal(Graphics.Renderer2D renderer)
    {
        if (!ActiveInHierarchy) return;
        if (!_started) StartInternal();
        foreach (Component component in _components) component.RenderInternal(renderer);
    }

    internal void RenderEditorInternal(Graphics.Renderer2D renderer)
    {
        if (!ActiveInHierarchy) return;
        foreach (Component component in _components) component.RenderEditorInternal(renderer);
    }

    internal void StopInternal()
    {
        for (int i = _components.Count - 1; i >= 0; i--) _components[i].StopInternal();
        _started = false;
    }

    internal void DestroyInternal()
    {
        for (int i = _components.Count - 1; i >= 0; i--) _components[i].DestroyInternal();
        _components.Clear();
        _started = false;
    }
}
