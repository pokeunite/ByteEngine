using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.VisualLogic;

public sealed class EventModuleComponent
    : Component
{
    private sealed class RuntimeModule
    {
        public required AssetReference Reference { get; init; }

        public required EventModuleDefinition Definition { get; set; }

        public EventModuleRuntime Runtime { get; } =
            new();
    }

    private readonly List<AssetReference> _modules =
        new();

    private readonly List<RuntimeModule> _runtimeModules =
        new();

    private Action<string>? _warningSink;

    public IReadOnlyList<AssetReference> Modules =>
        _modules;

    /*
     * Visual events should execute before gameplay components
     * such as CharacterController3D consume commands for the frame.
     */
    public override int UpdateOrder =>
        -1000;

    public void AddModuleReference(
        AssetReference reference)
    {
        ArgumentNullException.ThrowIfNull(
            reference);

        if (reference.IsEmpty)
        {
            return;
        }

        if (ContainsReference(
                reference))
        {
            return;
        }

        _modules.Add(
            reference);
    }

    public void AddResolvedModule(
        AssetReference reference,
        EventModuleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(
            reference);

        ArgumentNullException.ThrowIfNull(
            definition);

        AddModuleReference(
            reference);

        foreach (RuntimeModule existing in _runtimeModules
                     .Where(item => SameReference(item.Reference, reference)).ToArray())
        {
            existing.Runtime.Reset();
            _runtimeModules.Remove(existing);
        }

        _runtimeModules.Add(
            new RuntimeModule
            {
                Reference =
                    reference,

                Definition =
                    definition
            });
    }

    /// <summary>
    /// Replaces the runtime definition for an Event Module that is already
    /// attached to this GameObject.
    ///
    /// Used by the ByteEngine editor for live ByteGraph hot reload while
    /// Play mode is running.
    /// </summary>
    public bool TryReloadResolvedModule(
        AssetReference reference,
        EventModuleDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(
            reference);

        ArgumentNullException.ThrowIfNull(
            definition);

        if (!ContainsReference(
                reference))
        {
            return false;
        }

        RuntimeModule? runtimeModule =
            _runtimeModules.FirstOrDefault(
                item =>
                    SameReference(
                        item.Reference,
                        reference));

        if (runtimeModule ==
            null)
        {
            _runtimeModules.Add(
                new RuntimeModule
                {
                    Reference =
                        reference,

                    Definition =
                        definition
                });

            ValidateDefinition(
                definition);

            return true;
        }

        runtimeModule.Definition =
            definition;

        /*
         * Reset rule-local transient state such as Trigger Once latches.
         * The next runtime update begins cleanly against the new graph.
         */
        runtimeModule.Runtime.Reset();

        ValidateDefinition(
            definition);

        return true;
    }

    public bool RemoveModule(
        AssetReference reference)
    {
        ArgumentNullException.ThrowIfNull(
            reference);

        bool removed =
            _modules.RemoveAll(
                item =>
                    SameReference(
                        item,
                        reference)) >
            0;

        foreach (RuntimeModule existing in _runtimeModules
                     .Where(item => SameReference(item.Reference, reference)).ToArray())
        {
            existing.Runtime.Reset();
            _runtimeModules.Remove(existing);
        }

        return removed;
    }

    public void ClearModules()
    {
        foreach (RuntimeModule module in _runtimeModules) module.Runtime.Reset();
        _modules.Clear();
        _runtimeModules.Clear();
    }

    internal void SetWarningSink(
        Action<string>? warningSink)
    {
        _warningSink =
            warningSink;
    }

    protected override void OnStart()
    {
        foreach (RuntimeModule module
                 in _runtimeModules)
        {
            ValidateDefinition(
                module.Definition);
        }
    }

    protected override void OnUpdate()
    {
        ByteEngine.Core.Scene.Scene? scene =
            GameObject.Scene;

        if (scene ==
                null ||
            scene.RuntimeGlobals ==
                null)
        {
            return;
        }

        foreach (RuntimeModule module
                 in _runtimeModules)
        {
            module.Runtime.Update(
                module.Definition,
                scene.RuntimeGlobals,
                scene,
                GameObject,
                Warn);
        }
    }

    protected override void OnStop()
    {
        foreach (RuntimeModule module
                 in _runtimeModules)
        {
            module.Runtime.Reset();
        }
    }

    private bool ContainsReference(
        AssetReference reference)
    {
        return _modules.Any(
            item =>
                SameReference(
                    item,
                    reference));
    }

    private static bool SameReference(
        AssetReference left,
        AssetReference right)
    {
        if (left.Guid !=
                Guid.Empty &&
            right.Guid !=
                Guid.Empty)
        {
            return left.Guid ==
                   right.Guid;
        }

        return string.Equals(
            left.CachedProjectPath,
            right.CachedProjectPath,
            StringComparison.OrdinalIgnoreCase);
    }

    private void ValidateDefinition(
        EventModuleDefinition definition)
    {
        IReadOnlyList<string> problems =
            definition.Validate(
                GameObject);

        foreach (string problem
                 in problems)
        {
            Warn(
                problem);
        }
    }

    private void Warn(
        string message)
    {
        if (_warningSink !=
            null)
        {
            _warningSink(
                $"Visual Logic [{GameObject.Name}]: {message}");

            return;
        }

        Console.WriteLine(
            $"Visual Logic [{GameObject.Name}]: {message}");
    }
}
