using ByteEngine.Core.Graphics;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Physics;

namespace ByteEngine.Core.Scene;

public sealed class GameObject
{
    private readonly List<Component> _components = new();
    private Component[]? _contactComponents;
    private Component[] ContactComponents => _contactComponents ??= _components.ToArray();
    private readonly List<GameObject> _children = new();
    private readonly HashSet<Guid> _tags = new();
    private int _layer;
    private bool _started;
    private Scene? _scene;
    private bool _hasUpdateCallbacks, _hasLateCallbacks, _hasFixedCallbacks;
    private Dictionary<Type, Component?>? _componentLookup;
    private Component[]? _updateSchedule;
    private Component[] _lateSchedule = [], _fixedSchedule = [];
    private int[] _scheduledOrders = [];

    private void InvalidateComponentCaches()
    {
        _componentLookup?.Clear();
        _updateSchedule = null;
        _lateSchedule = _fixedSchedule = [];
        _scheduledOrders = [];
    }

    private void RefreshCallbacks()
    {
        _hasUpdateCallbacks = _hasLateCallbacks = _hasFixedCallbacks = false;
        foreach (var component in _components)
        {
            _hasUpdateCallbacks |= component.HasUpdateCallback;
            _hasLateCallbacks |= component.HasLateUpdateCallback;
            _hasFixedCallbacks |= component.HasFixedUpdateCallback;
        }
    }

    public Guid Id { get; private set; }
    public string Name { get; set; }
    public bool Active { get; set; } = true;
    public bool ActiveInHierarchy => Active && (Parent?.ActiveInHierarchy ?? true);
    public Transform Transform { get; }
    public IReadOnlyList<Component> Components => _components;
    internal int RenderOrder
    {
        get
        {
            for (int i = 0; i < _components.Count; i++)
                if (_components[i].RenderOrder is int order) return order;
            return 0;
        }
    }
    public GameObject? Parent { get; private set; }
    public string ParentSocket { get; set; } = string.Empty;
    public AttachmentTransformRule AttachmentLocationRule { get; set; } = AttachmentTransformRule.KeepRelative;
    public AttachmentTransformRule AttachmentRotationRule { get; set; } = AttachmentTransformRule.KeepRelative;
    public AttachmentTransformRule AttachmentScaleRule { get; set; } = AttachmentTransformRule.KeepRelative;
    public System.Numerics.Vector3 AttachmentPosition { get; set; }
    public System.Numerics.Quaternion AttachmentRotation { get; set; } = System.Numerics.Quaternion.Identity;
    public System.Numerics.Vector3 AttachmentScale { get; set; } = System.Numerics.Vector3.One;
    public bool IsAttached => Parent != null && !string.IsNullOrWhiteSpace(ParentSocket);
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
        if (parent == null) ParentSocket = string.Empty;
        Parent?._children.Add(this);
        _scene?.InvalidateRenderHierarchy();

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
        if(component is Graphics.ThreeD.MeshLodGroup or Assets.ModelHierarchyInstance)_scene?.InvalidateRenderHierarchy();
        InvalidateComponentCaches();
        _hasUpdateCallbacks |= component.HasUpdateCallback;
        _hasLateCallbacks |= component.HasLateUpdateCallback;
        _hasFixedCallbacks |= component.HasFixedUpdateCallback;
        _contactComponents=null;
        _scene?.InvalidateComponentRegistries();

        if (component is Camera3D { ActiveGameCamera: true } camera && _scene != null)
            _scene.SetActiveCamera(camera);

        if (_started) component.StartInternal();
        return component;
    }

    public T? GetComponent<T>() where T : Component
    {
        int count = _components.Count;
        if (count == 0) return null;
        if (count == 1) return _components[0] as T;
        var type = typeof(T);
        if (_componentLookup != null && _componentLookup.TryGetValue(type, out var cached)) return (T?)cached;
        T? result = null;
        for (int i = 0; i < count; i++)
            if (_components[i] is T match) { result = match; break; }
        // Cache misses too: most ancestor searches ask for an absent engine component.
        // Do not root types from collectible editor plugin contexts through a negative cache.
        if (!type.Assembly.IsCollectible)
        {
            _componentLookup ??= new();
            if (_componentLookup.Count < 32) _componentLookup[type] = result;
        }
        return result;
    }

    public bool TryGetComponent<T>(out T? component)
        where T : Component
    {
        component = GetComponent<T>();
        return component != null;
    }

    public bool HasComponent<T>()
        where T : Component =>
        GetComponent<T>() != null;

    internal void ReorderComponents(IReadOnlyList<Component> ordered)
    {
        if(ordered.Count!=_components.Count||ordered.Distinct().Count()!=ordered.Count||ordered.Any(c=>!_components.Contains(c)))throw new ArgumentException("Component order must contain every attached component once.");
        _components.Clear();_contactComponents=null;
        _scene?.InvalidateComponentRegistries();_components.AddRange(ordered);
        if(_components.Any(c=>c is Graphics.ThreeD.MeshLodGroup or Assets.ModelHierarchyInstance))_scene?.InvalidateRenderHierarchy();
        InvalidateComponentCaches();
    }

    public bool RemoveComponent(Component component)
    {
        ArgumentNullException.ThrowIfNull(component);
        if (!_components.Remove(component)) return false;
        if(component is Graphics.ThreeD.MeshLodGroup or Assets.ModelHierarchyInstance)_scene?.InvalidateRenderHierarchy();
        InvalidateComponentCaches();
        RefreshCallbacks();
        _contactComponents=null;
        _scene?.InvalidateComponentRegistries();

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
        // Components may create renderers during OnStart. Added components start immediately.
        foreach (Component component in _components.ToArray()) component.StartInternal();
    }

    internal void UpdateInternal()
    {
        if (!ActiveInHierarchy) return;

        if (!_started) StartInternal();
        if (!_hasUpdateCallbacks) return;
        using var localClock=Time.EnterObjectScale(SimulationRate3D.Resolve(this));

        // A single component needs no ordered enumeration or sort buffer.
        if (_components.Count == 1)
        {
            _components[0].UpdateInternal();
            return;
        }
        if (_components.Count == 0) return;
        RunOrderedComponents(late:false);
    }

    internal void LateUpdateInternal()
    {
        if (!ActiveInHierarchy) return;

        if (!_started) StartInternal();
        if (!_hasLateCallbacks) return;
        using var localClock=Time.EnterObjectScale(SimulationRate3D.Resolve(this));

        // A single component needs no ordered enumeration or sort buffer.
        if (_components.Count == 1)
        {
            _components[0].LateUpdateInternal();
            return;
        }
        if (_components.Count == 0) return;
        RunOrderedComponents(late:true);
    }

    internal void FixedUpdateInternal()
    {
        if (!ActiveInHierarchy) return;
        if (!_started) StartInternal();
        if (!_hasFixedCallbacks) return;
        using var localClock=Time.EnterObjectScale(SimulationRate3D.Resolve(this));
        int count = _components.Count;
        if (count == 0) return;
        if (count == 1) { _components[0].FixedUpdateInternal(); return; }
        EnsureComponentSchedule();
        var snapshot = _fixedSchedule;
        for (int i = 0; i < snapshot.Length; i++) snapshot[i].FixedUpdateInternal();
    }

    private void RunOrderedComponents(bool late)
    {
        EnsureComponentSchedule();
        // Hold the old immutable snapshot if a callback changes component membership.
        var snapshot = late ? _lateSchedule : _updateSchedule!;
        for (int i = 0; i < snapshot.Length; i++)
            if (late) snapshot[i].LateUpdateInternal(); else snapshot[i].UpdateInternal();
    }

    private void EnsureComponentSchedule()
    {
        int count = _components.Count;
        if (_updateSchedule != null && _scheduledOrders.Length == count)
        {
            bool same = true;
            for (int i = 0; i < count; i++)
                if (_scheduledOrders[i] != _components[i].UpdateOrder) { same = false; break; }
            if (same) return;
        }
        _scheduledOrders = new int[count];
        var ordered = new (Component Item, int Order)[count];
        for (int i = 0; i < count; i++)
        {
            int order = _components[i].UpdateOrder;
            _scheduledOrders[i] = order;
            ordered[i] = (_components[i], order);
        }
        // Stable insertion order preserves equal-priority callbacks and authored order.
        for (int i = 1; i < count; i++)
        {
            var value = ordered[i]; int j = i - 1;
            while (j >= 0 && ordered[j].Order > value.Order) { ordered[j + 1] = ordered[j]; j--; }
            ordered[j + 1] = value;
        }
        var updates = new List<Component>(); var late = new List<Component>(); var fixedUpdates = new List<Component>();
        for (int i = 0; i < count; i++)
        {
            var component = ordered[i].Item;
            if (component.HasUpdateCallback) updates.Add(component);
            if (component.HasLateUpdateCallback) late.Add(component);
            if (component.HasFixedUpdateCallback) fixedUpdates.Add(component);
        }
        _updateSchedule = updates.ToArray(); _lateSchedule = late.ToArray(); _fixedSchedule = fixedUpdates.ToArray();
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
                 in ContactComponents)
        {
            component.CollisionEnterInternal(
                contact);
        }
    }

    internal void DispatchCollisionStay(
        PhysicsContact3D contact)
    {
        foreach (Component component
                 in ContactComponents)
        {
            component.CollisionStayInternal(
                contact);
        }
    }

    internal void DispatchCollisionExit(
        PhysicsContact3D contact)
    {
        foreach (Component component
                 in ContactComponents)
        {
            component.CollisionExitInternal(
                contact);
        }
    }

    internal void DispatchTriggerEnter(
        PhysicsContact3D contact)
    {
        foreach (Component component
                 in ContactComponents)
        {
            component.TriggerEnterInternal(
                contact);
        }
    }

    internal void DispatchTriggerStay(
        PhysicsContact3D contact)
    {
        foreach (Component component
                 in ContactComponents)
        {
            component.TriggerStayInternal(
                contact);
        }
    }

    internal void DispatchTriggerExit(
        PhysicsContact3D contact)
    {
        foreach (Component component
                 in ContactComponents)
        {
            component.TriggerExitInternal(
                contact);
        }
    }

    internal void DestroyInternal()
    {
        for (int i = _components.Count - 1; i >= 0; i--)
        {
            Component component =
                _components[i];
            component.DestroyInternal();
        }

        _components.Clear();_contactComponents=null;
        _scene?.InvalidateComponentRegistries();
        _started = false;

    }
}
