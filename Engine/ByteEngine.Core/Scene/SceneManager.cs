using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.Scene;

public sealed class SceneManager
{
    public Variables.VariableStore GlobalVariables { get; } =
        new();

    public Scene? ActiveScene { get; private set; }
    public Action<Scene>? SceneLoaded { get; set; }

    /// <summary>Host-owned fresh scene factory used by Event Sheet scene.restart.</summary>
    public Func<Scene>? RestartSceneFactory { get; set; }
    public Func<string,Scene>? SceneFactory { get; set; }
    public Action? QuitHandler { get; set; }
    public bool ShowLoadingScreen {get;set;}
    public Func<string,IEnumerable<SceneLoadStep>>? ProgressiveSceneFactory {get;set;}
    string? _pendingLoad;
    IEnumerator<SceneLoadStep>? _loadSteps;
    Scene? _preparedScene;
    bool _finishPresented;
    public bool IsLoading => _pendingLoad != null || _finishPresented;
    public float LoadingProgress => ActiveScene?.LoadingProgress ?? 0;

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
        SceneLoaded?.Invoke(scene);

        Console.WriteLine(
            $"Active scene: {scene.Name}");
    }

    public void UnloadScene()
    {
        _loadSteps?.Dispose();_loadSteps=null;_preparedScene=null;_finishPresented=false;
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
        if(_finishPresented){if(ActiveScene?.LoadingScreenPresented!=true)return;_finishPresented=false;if(ActiveScene!=null){ActiveScene.LoadingScreenVisible=false;ActiveScene.LoadingScreenPresented=false;}return;}
        if(_pendingLoad is {} pending){AdvanceLoading(pending);return;}
        ActiveScene?.UpdateInternal();

        if(ActiveScene?.QuitRequested==true){ActiveScene.ClearHostRequests();QuitHandler?.Invoke();return;}
        if(ActiveScene?.LoadRequested is { } path)
        {
            ActiveScene.ClearHostRequests();
            if(ShowLoadingScreen&&(SceneFactory!=null||ProgressiveSceneFactory!=null)){
                _pendingLoad=path;ActiveScene.LoadingScreenVisible=true;ActiveScene.LoadingScreenPresented=false;ActiveScene.LoadingProgress=0;
                ActiveScene.LoadingTitle=(ActiveScene.GameObjects.SelectMany(o=>o.Components).OfType<LoadingScreenStyle>().FirstOrDefault(s=>s.Enabled)??new LoadingScreenStyle()).Title(path);return;
            }
            try{if(SceneFactory is { } factory)LoadScene(factory(path));else Console.WriteLine("Scene load requested, but this host has no scene factory.");}
            catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException){Console.WriteLine($"Could not load scene '{path}': {e.Message}");}
        }

        if(ActiveScene?.PendingRestore is {} restore)
        {
            GlobalVariables.Clear();foreach(var pair in restore.Globals)GlobalVariables.Set(pair.Key,pair.Value.Clone());
            ActiveScene.PendingRestore=null;LoadScene(restore.Scene);
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

    private void AdvanceLoading(string path)
    {
        if(ActiveScene?.LoadingScreenPresented!=true)return;
        try{
            if(_preparedScene is {} ready){
                string title=ActiveScene.LoadingTitle;LoadScene(ready);_preparedScene=null;_pendingLoad=null;
                ActiveScene!.LoadingScreenVisible=true;ActiveScene.LoadingScreenPresented=false;ActiveScene.LoadingProgress=1;ActiveScene.LoadingTitle=title;_finishPresented=true;return;
            }
            if(ProgressiveSceneFactory is {} factory){
                _loadSteps??=factory(path).GetEnumerator();var budget=System.Diagnostics.Stopwatch.StartNew();
                do{
                    if(!_loadSteps.MoveNext())throw new InvalidDataException("Progressive scene loader finished without a scene.");
                    var step=_loadSteps.Current;if(float.IsFinite(step.Progress))ActiveScene.LoadingProgress=Math.Max(ActiveScene.LoadingProgress,Math.Clamp(step.Progress,0,.99f));
                    if(step.Scene!=null){_preparedScene=step.Scene;_loadSteps.Dispose();_loadSteps=null;break;}
                }while(budget.Elapsed.TotalMilliseconds<8);
            }else if(SceneFactory is {} sync){_preparedScene=sync(path);ActiveScene.LoadingProgress=.96f;}
            ActiveScene.LoadingScreenPresented=false;
        }catch(Exception e)when(e is IOException or InvalidDataException or ArgumentException or UnauthorizedAccessException){
            _loadSteps?.Dispose();_loadSteps=null;_preparedScene=null;_pendingLoad=null;
            ActiveScene.LoadingScreenVisible=false;ActiveScene.LoadingScreenPresented=false;Console.WriteLine($"Could not load scene '{path}': {e.Message}");
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
