using ByteEngine.Core.Graphics;
using ByteEngine.Core.Physics;

namespace ByteEngine.Core.Scene;

public abstract class Component
{
    private sealed record Callbacks(bool Update, bool Late, bool Fixed,bool Render,bool RenderOrder,int Contacts);
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Type, Callbacks> CallbackTypes = new();
    private Callbacks? _callbacks;
    private Callbacks RuntimeCallbacks => _callbacks ??= CallbackTypes.GetValue(GetType(), static type =>
    {
        bool Overrides(string name) => type.GetMethod(name,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, Type.EmptyTypes, null)?.DeclaringType != typeof(Component);
        bool render=type.GetMethod(nameof(OnRender),System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,[typeof(RenderContext)],null)?.DeclaringType!=typeof(Component);
        bool Contact(string name)=>type.GetMethod(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic,null,[typeof(PhysicsContact3D)],null)?.DeclaringType!=typeof(Component);
        int contacts=(Contact(nameof(OnCollisionEnter))?1:0)|(Contact(nameof(OnCollisionStay))?2:0)|(Contact(nameof(OnCollisionExit))?4:0)|(Contact(nameof(OnTriggerEnter))?8:0)|(Contact(nameof(OnTriggerStay))?16:0)|(Contact(nameof(OnTriggerExit))?32:0);
        bool renderOrder=type.GetProperty(nameof(RenderOrder))?.GetMethod?.DeclaringType!=typeof(Component);
        return new(Overrides(nameof(OnUpdate)), Overrides(nameof(OnLateUpdate)), Overrides(nameof(OnFixedUpdate)),render,renderOrder,contacts);
    });
    internal bool HasUpdateCallback => RuntimeCallbacks.Update;
    internal bool HasLateUpdateCallback => RuntimeCallbacks.Late;
    internal bool HasFixedUpdateCallback => RuntimeCallbacks.Fixed;
    internal bool HasRenderCallback => RuntimeCallbacks.Render;
    internal bool HasRenderOrderOverride => RuntimeCallbacks.RenderOrder;
    internal bool HasContactCallbacks=>RuntimeCallbacks.Contacts!=0;
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

    private bool _enabled=true;
    public bool Enabled {get=>_enabled;set{if(_enabled==value)return;_enabled=value;if(Scene.AffectsPhysics(this))_gameObject?.Scene?.InvalidateQueryBounds();}}

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

        var profiler = _gameObject?.Scene?.ComponentProfiler;
        if (profiler is not { Enabled: true }) { OnUpdate(); return; }
        var start = profiler.Begin(this);
        try { OnUpdate(); } finally { profiler.End(this, "Update", start); }
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

        var profiler = _gameObject?.Scene?.ComponentProfiler;
        if (profiler is not { Enabled: true }) { OnLateUpdate(); return; }
        var start = profiler.Begin(this);
        try { OnLateUpdate(); } finally { profiler.End(this, "LateUpdate", start); }
    }

    internal void FixedUpdateInternal()
    {
        if (!Enabled) return;
        if (!_started) StartInternal();
        var profiler = _gameObject?.Scene?.ComponentProfiler;
        if (profiler is not { Enabled: true }) { OnFixedUpdate(); return; }
        var start = profiler.Begin(this);
        try { OnFixedUpdate(); } finally { profiler.End(this, "FixedUpdate", start); }
    }

    /// <summary>Runs before each fixed physics tick in scenes that opt into fixed simulation.</summary>
    protected virtual void OnFixedUpdate() { }

    internal void RenderInternal(
        RenderContext context)
    {
        if (!Enabled || !HasRenderCallback)
        {
            return;
        }

        if (!_started)
        {
            StartInternal();
        }

        var profiler = _gameObject?.Scene?.ComponentProfiler;
        if (profiler is not { Enabled: true }) { OnRender(context); return; }
        var start = profiler.Begin(this);
        try { OnRender(context); } finally { profiler.End(this, "Render", start); }
    }

    internal void RenderEditorInternal(
        RenderContext context)
    {
        if (!Enabled || !HasRenderCallback)
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
        if (Enabled&&(RuntimeCallbacks.Contacts&1)!=0)
        {
            OnCollisionEnter(contact);
        }
    }

    internal void CollisionStayInternal(
        PhysicsContact3D contact)
    {
        if (Enabled&&(RuntimeCallbacks.Contacts&2)!=0)
        {
            OnCollisionStay(contact);
        }
    }

    internal void CollisionExitInternal(
        PhysicsContact3D contact)
    {
        if (Enabled&&(RuntimeCallbacks.Contacts&4)!=0)
        {
            OnCollisionExit(contact);
        }
    }

    internal void TriggerEnterInternal(
        PhysicsContact3D contact)
    {
        if (Enabled&&(RuntimeCallbacks.Contacts&8)!=0)
        {
            OnTriggerEnter(contact);
        }
    }

    internal void TriggerStayInternal(
        PhysicsContact3D contact)
    {
        if (Enabled&&(RuntimeCallbacks.Contacts&16)!=0)
        {
            OnTriggerStay(contact);
        }
    }

    internal void TriggerExitInternal(
        PhysicsContact3D contact)
    {
        if (Enabled&&(RuntimeCallbacks.Contacts&32)!=0)
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
