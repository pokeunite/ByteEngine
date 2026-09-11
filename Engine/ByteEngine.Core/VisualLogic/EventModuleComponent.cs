using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.VisualLogic;

public sealed class EventModuleComponent
    : Component
{
    private sealed class RuntimeModule
    {
        public required AssetReference Reference { get; init; }

        public required EventModuleDefinition Definition { get; init; }

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

        if (ContainsReference(reference))
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

        _runtimeModules.RemoveAll(
            item => SameReference(
                item.Reference,
                reference));

        _runtimeModules.Add(
            new RuntimeModule
            {
                Reference =
                    reference,

                Definition =
                    definition
            });
    }

    public bool RemoveModule(
        AssetReference reference)
    {
        ArgumentNullException.ThrowIfNull(
            reference);

        bool removed =
            _modules.RemoveAll(
                item => SameReference(
                    item,
                    reference)) > 0;

        _runtimeModules.RemoveAll(
            item => SameReference(
                item.Reference,
                reference));

        return removed;
    }

    public void ClearModules()
    {
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
            IReadOnlyList<string> problems =
                module.Definition.Validate(
                    GameObject);

            foreach (string problem
                     in problems)
            {
                Warn(
                    problem);
            }
        }
    }

    protected override void OnUpdate()
    {
        ByteEngine.Core.Scene.Scene? scene =
            GameObject.Scene;

        if (scene == null ||
            scene.RuntimeGlobals == null)
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
            item => SameReference(
                item,
                reference));
    }

    private static bool SameReference(
        AssetReference left,
        AssetReference right)
    {
        if (left.Guid != Guid.Empty &&
            right.Guid != Guid.Empty)
        {
            return left.Guid ==
                   right.Guid;
        }

        return string.Equals(
            left.CachedProjectPath,
            right.CachedProjectPath,
            StringComparison.OrdinalIgnoreCase);
    }

    private void Warn(
        string message)
    {
        if (_warningSink != null)
        {
            _warningSink(
                $"Visual Logic [{GameObject.Name}]: {message}");

            return;
        }

        Console.WriteLine(
            $"Visual Logic [{GameObject.Name}]: {message}");
    }
}