namespace ByteEngine.Core.Scene;

public sealed class GameObject
{
    private readonly List<Component> _components =
        new();

    private bool _started;

    public Guid Id { get; private set; }

    public string Name { get; set; }

    public bool Active { get; set; } =
        true;

    public Transform2D Transform { get; }

    public IReadOnlyList<Component> Components =>
        _components;

    public GameObject(
        string name = "GameObject")
        : this(
            Guid.NewGuid(),
            name
        )
    {
    }

    public GameObject(
        Guid id,
        string name = "GameObject")
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException(
                "GameObject ID cannot be empty.",
                nameof(id)
            );
        }

        Id = id;
        Name = name;

        Transform =
            new Transform2D();
    }

    public T AddComponent<T>(
        T component)
        where T : Component
    {
        ArgumentNullException.ThrowIfNull(
            component
        );

        component.Attach(
            this
        );

        _components.Add(
            component
        );

        if (_started)
        {
            component.StartInternal();
        }

        return component;
    }

    public T? GetComponent<T>()
        where T : Component
    {
        foreach (Component component in _components)
        {
            if (component is T result)
            {
                return result;
            }
        }

        return null;
    }

    public bool TryGetComponent<T>(
        out T? component)
        where T : Component
    {
        component =
            GetComponent<T>();

        return component != null;
    }

    public bool HasComponent<T>()
        where T : Component
    {
        return GetComponent<T>() != null;
    }

    internal void StartInternal()
    {
        if (_started)
        {
            return;
        }

        _started = true;

        for (int i = 0;
             i < _components.Count;
             i++)
        {
            _components[i]
                .StartInternal();
        }
    }

    internal void UpdateInternal()
    {
        if (!Active)
        {
            return;
        }

        if (!_started)
        {
            StartInternal();
        }

        for (int i = 0;
             i < _components.Count;
             i++)
        {
            _components[i]
                .UpdateInternal();
        }
    }

    internal void RenderInternal(
        Graphics.Renderer2D renderer)
    {
        if (!Active)
        {
            return;
        }

        if (!_started)
        {
            StartInternal();
        }

        for (int i = 0;
             i < _components.Count;
             i++)
        {
            _components[i]
                .RenderInternal(
                    renderer
                );
        }
    }

    public bool RemoveComponent(Component component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (!_components.Remove(component)) return false;
        component.DestroyInternal();
        return true;
    }

    internal void RenderEditorInternal(
        Graphics.Renderer2D renderer)
    {
        if (!Active)
        {
            return;
        }

        for (int i = 0;
             i < _components.Count;
             i++)
        {
            _components[i]
                .RenderEditorInternal(
                    renderer
                );
        }
    }

    internal void StopInternal()
    {
        for (int i = _components.Count - 1;
             i >= 0;
             i--)
        {
            _components[i]
                .StopInternal();
        }

        _started = false;
    }

    internal void DestroyInternal()
    {
        for (int i = _components.Count - 1;
             i >= 0;
             i--)
        {
            _components[i]
                .DestroyInternal();
        }

        _components.Clear();

        _started = false;
    }
}
