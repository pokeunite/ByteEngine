using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.Scene;

public abstract class Component
{
    private GameObject? _gameObject;

    private bool _started;

    public GameObject GameObject
    {
        get
        {
            return _gameObject
                ?? throw new InvalidOperationException(
                    "Component is not attached to a GameObject."
                );
        }
    }

    public Transform Transform =>
        GameObject.Transform;

    public bool Enabled { get; set; } = true;

    public virtual int? RenderOrder => null;
	public virtual int UpdateOrder => 0;

    internal void Attach(
        GameObject gameObject)
    {
        if (_gameObject != null)
        {
            throw new InvalidOperationException(
                "This component is already attached to a GameObject."
            );
        }

        _gameObject = gameObject;
    }

    internal void StartInternal()
    {
        if (_started)
        {
            return;
        }

        _started = true;

        OnStart();
    }

    internal void UpdateInternal()
    {
        if (!Enabled)
        {
            return;
        }

        if (!_started)
        {
            StartInternal();
        }

        OnUpdate();
    }

    internal void RenderInternal(
        RenderContext context)
    {
        if (!Enabled)
        {
            return;
        }

        if (!_started)
        {
            StartInternal();
        }

        OnRender(context);
    }

    internal void RenderEditorInternal(
        RenderContext context)
    {
        if (!Enabled)
        {
            return;
        }

        OnRender(context);
    }

    internal void StopInternal()
    {
        if (!_started)
        {
            return;
        }

        OnStop();

        _started = false;
    }

    internal void DestroyInternal()
    {
        StopInternal();
        OnDestroy();
        _gameObject = null;
    }

    protected virtual void OnStart()
    {
    }

    protected virtual void OnUpdate()
    {
    }

    protected virtual void OnRender(
        RenderContext context)
    {
    }

    protected virtual void OnDestroy()
    {
    }

    protected virtual void OnStop()
    {
    }
}
