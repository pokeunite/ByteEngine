using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.Scene;

public sealed class SceneManager
{
    public Variables.VariableStore GlobalVariables { get; } =
        new();

    public Scene? ActiveScene { get; private set; }

    public bool HasActiveScene =>
        ActiveScene != null;

    public void LoadScene(
        Scene scene)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        if (ReferenceEquals(
                ActiveScene,
                scene))
        {
            return;
        }

        if (ActiveScene != null)
        {
            ActiveScene
                .UnloadInternal();

            ActiveScene.RuntimeGlobals =
                null;
        }

        ActiveScene =
            scene;

        /*
         * Runtime Scenes receive access to the SceneManager's
         * global variable store.
         *
         * This lets runtime systems such as EventModuleComponent
         * resolve:
         *
         * Global.Score
         * Global.Difficulty
         * Global.PlayerName
         *
         * without making global variables belong to the Scene.
         */
        ActiveScene.RuntimeGlobals =
            GlobalVariables;

        ActiveScene
            .LoadInternal();

        Console.WriteLine(
            $"Active scene: {scene.Name}");
    }

    public void UnloadScene()
    {
        if (ActiveScene == null)
        {
            return;
        }

        ActiveScene
            .UnloadInternal();

        ActiveScene.RuntimeGlobals =
            null;

        ActiveScene =
            null;
    }

    internal void SetEditorScene(
        Scene scene)
    {
        ArgumentNullException.ThrowIfNull(
            scene);

        if (ActiveScene?.IsLoaded == true)
        {
            ActiveScene
                .UnloadInternal();
        }

        if (ActiveScene != null)
        {
            ActiveScene.RuntimeGlobals =
                null;
        }

        ActiveScene =
            scene;

        /*
         * Editor scenes must NOT have runtime global state.
         *
         * Visual Event Modules execute only when the runtime
         * scene is loaded in Play Mode.
         */
        ActiveScene.RuntimeGlobals =
            null;
    }

    internal void UpdateInternal()
    {
        ActiveScene?.UpdateInternal();

        /*
         * TPS jitter diagnostics are sampled here, after Scene.UpdateInternal
         * has completed gameplay updates, physics, LateUpdate camera follow and
         * the final skeletal-attachment pass. This gives the trace one coherent
         * end-of-frame snapshot instead of mixing pre/post movement state.
         */
        if (ActiveScene != null)
        {
            RuntimeDiagnostics.SampleTpsJitter(
                ActiveScene);
        }
    }

    internal void RenderInternal(
        RenderContext context)
    {
        ActiveScene?
            .RenderInternal(
                context);
    }

    internal void ShutdownInternal()
    {
        UnloadScene();
    }
}
