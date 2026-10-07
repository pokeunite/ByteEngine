using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.Scene;

public sealed class SceneManager
{
    public Variables.VariableStore GlobalVariables { get; } =
        new();

    public Scene? ActiveScene { get; private set; }

    /// <summary>Host-owned fresh scene factory used by Event Sheet scene.restart.</summary>
    public Func<Scene>? RestartSceneFactory { get; set; }
    public Func<string,Scene>? SceneFactory { get; set; }
    public Action? QuitHandler { get; set; }
    public bool ShowLoadingScreen {get;set;}
    string? _pendingLoad;

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
        _pendingLoad=null;
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
        if(_pendingLoad is {} pending){if(ActiveScene?.LoadingScreenPresented!=true)return;_pendingLoad=null;var watch=System.Diagnostics.Stopwatch.StartNew();try{if(SceneFactory is {} factory)LoadScene(factory(pending));}catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException){Console.WriteLine($"Could not load scene '{pending}': {e.Message}");}finally{if(ActiveScene!=null){ActiveScene.LoadingScreenVisible=false;ActiveScene.LoadingScreenPresented=false;}Console.WriteLine($"Scene transition: {watch.Elapsed.TotalMilliseconds:0} ms");}return;}
        ActiveScene?.UpdateInternal();

        if(ActiveScene?.QuitRequested==true){ActiveScene.ClearHostRequests();QuitHandler?.Invoke();return;}
        if(ActiveScene?.LoadRequested is { } path)
        {
            ActiveScene.ClearHostRequests();
            if(ShowLoadingScreen&&SceneFactory!=null){_pendingLoad=path;ActiveScene.LoadingScreenVisible=true;ActiveScene.LoadingScreenPresented=false;return;}
            try{if(SceneFactory is { } factory)LoadScene(factory(path));else Console.WriteLine("Scene load requested, but this host has no scene factory.");}
            catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException){Console.WriteLine($"Could not load scene '{path}': {e.Message}");}
        }

        if (ActiveScene?.RestartRequested == true)
        {
            if (RestartSceneFactory is { } factory)
                LoadScene(factory());
            else
                Console.WriteLine("Scene restart requested, but this host has no restart factory.");
        }

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
