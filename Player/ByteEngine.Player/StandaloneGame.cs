using ByteEngine.Core;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;

namespace ByteEngine.Player;

internal sealed class StandaloneGame : ByteEngineApplication
{
    private readonly GameProjectRuntime _project;
    private readonly bool _smokeTest;
    private readonly bool _validateOnly;
    private int _smokeFrames;
    private readonly System.Diagnostics.Stopwatch _smokeTimer=new();
    protected override bool CloseOnEscape => false;

    public StandaloneGame(GameProjectRuntime project, bool smokeTest = false, bool validateOnly = false)
        : base(project.Project.Window.Width, project.Project.Window.Height, project.Project.Name)
    {
        _project = project;
        _smokeTest=smokeTest;
        _validateOnly=validateOnly;
        if(smokeTest||validateOnly)IsVisible=false;
    }

    protected override void OnEngineStart()
    {
        foreach (var variable in _project.Project.GlobalVariables)
            Scenes.GlobalVariables.Set(variable.Name, variable.Value.Clone());
        var currentPath=_project.Project.StartupScene;
        Scenes.ShowLoadingScreen=true;Scenes.SceneFactory=path=>{var fresh=_project.LoadScene(path);currentPath=path;return fresh;};
        var scene = _project.LoadStartupScene();
        Scenes.RestartSceneFactory = () =>
        {
            Scenes.GlobalVariables.Clear();
            foreach (var variable in _project.Project.GlobalVariables)
                Scenes.GlobalVariables.Set(variable.Name, variable.Value.Clone());
            return _project.LoadScene(currentPath);
        };
        Scenes.LoadScene(scene);
        if(_smokeTest)
        {
            foreach(var surface in scene.GameObjects.SelectMany(o=>o.Components).OfType<ByteEngine.Core.Characters.HeightfieldCollider3D>())
            {
                if(!surface.TrySampleWorld(surface.Transform.WorldPosition,out var point,out var normal)||!float.IsFinite(point.Y))
                    throw new InvalidDataException("Packaged heightfield cannot provide ground collision at its centre.");
                ByteEngine.Core.Diagnostics.CrashDebugLog.Write($"SMOKE terrain {surface.GetType().Name}: grid={surface.Columns}x{surface.Rows}; ground={point.Y:0.00}; normal={normal}");
            }
            _smokeTimer.Start();
        }
        if(_validateOnly)
        {
            ByteEngine.Core.Diagnostics.CrashDebugLog.Write($"Package validation passed: {_project.Project.Name}; assets={_project.Database.Assets.Count}");
            Close();
        }
        bool grab = scene.GameObjects.Any(item =>
            item.GetComponent<PlayerController3D>() is { AcceptLookInput: true });
        CaptureGameInput(grab);
    }

    protected override void OnEngineUpdate()
    {
        if (!IsGameInputCaptured && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            bool grab = Scenes.ActiveScene?.GameObjects.Any(item =>
                item.GetComponent<PlayerController3D>() is { AcceptLookInput: true }) == true;
            CaptureGameInput(grab);
        }
    }

    protected override void OnEngineRender()
    {
        if(!_smokeTest)return;
        var error=OpenTK.Graphics.OpenGL4.GL.GetError();
        if(error!=OpenTK.Graphics.OpenGL4.ErrorCode.NoError)throw new InvalidOperationException("Packaged rendering error: "+error);
        ++_smokeFrames;
        if(_smokeFrames==30)_smokeTimer.Restart();
        if(_smokeFrames>=150)
        {
            ByteEngine.Core.Diagnostics.CrashDebugLog.Write($"SMOKE PASS: 150 native packaged frames; final 120 frames elapsed={_smokeTimer.Elapsed.TotalSeconds:0.00}s");
            Close();
        }
    }

    protected override void OnEngineShutdown()
    {
        ReleaseGameInput();
        // Release GPU assets while the window still owns its live graphics context.
        _project.Dispose();
    }
}
