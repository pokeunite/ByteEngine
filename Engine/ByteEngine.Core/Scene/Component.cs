using ByteEngine.Core.Graphics;
using ByteEngine.Core.Physics;

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

    protected GameObject? AttachedGameObject => _gameObject;

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

    internal void LateUpdateInternal()
    {
        if (!Enabled)
        {
            return;
        }

        if (!_started)
        {
            StartInternal();
        }

        OnLateUpdate();
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

    internal void CollisionEnterInternal(
        PhysicsContact3D contact)
    {
        if (Enabled)
        {
            OnCollisionEnter(contact);
        }
    }

    internal void CollisionStayInternal(
        PhysicsContact3D contact)
    {
        if (Enabled)
        {
            OnCollisionStay(contact);
        }
    }

    internal void CollisionExitInternal(
        PhysicsContact3D contact)
    {
        if (Enabled)
        {
            OnCollisionExit(contact);
        }
    }

    internal void TriggerEnterInternal(
        PhysicsContact3D contact)
    {
        if (Enabled)
        {
            OnTriggerEnter(contact);
        }
    }

    internal void TriggerStayInternal(
        PhysicsContact3D contact)
    {
        if (Enabled)
        {
            OnTriggerStay(contact);
        }
    }

    internal void TriggerExitInternal(
        PhysicsContact3D contact)
    {
        if (Enabled)
        {
            OnTriggerExit(contact);
        }
    }

    protected virtual void OnStart()
    {
    }

    protected virtual void OnUpdate()
    {
    }

    /// <summary>
    /// Called after the Scene has completed its normal component update pass and
    /// physics step for the frame. Camera follow, procedural pose corrections,
    /// and other systems that depend on final gameplay transforms belong here.
    /// </summary>
    protected virtual void OnLateUpdate()
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

    /// <summary>
    /// Called on enabled components when a solid physics contact begins.
    /// Contact.Normal points away from the other collider toward this object.
    /// </summary>
    protected virtual void OnCollisionEnter(
        PhysicsContact3D contact)
    {
    }

    /// <summary>
    /// Called while a solid physics contact remains active.
    /// </summary>
    protected virtual void OnCollisionStay(
        PhysicsContact3D contact)
    {
    }

    /// <summary>
    /// Called when a solid physics contact ends.
    /// </summary>
    protected virtual void OnCollisionExit(
        PhysicsContact3D contact)
    {
    }

    protected virtual void OnTriggerEnter(
        PhysicsContact3D contact)
    {
    }

    protected virtual void OnTriggerStay(
        PhysicsContact3D contact)
    {
    }

    protected virtual void OnTriggerExit(
        PhysicsContact3D contact)
    {
    }
}
