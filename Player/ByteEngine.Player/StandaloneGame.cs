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
    private Action? _firstFrame;
    private readonly System.Diagnostics.Stopwatch _smokeTimer=new();
    private IDisposable? _displayLease;
    protected override bool CloseOnEscape => false;
    private string _recorderMessage = string.Empty;
    private DateTime _recorderNoticeUntil = DateTime.UtcNow.AddSeconds(10);

    protected override void ShowVideoRecorderSettings()
    {
        bool captured = IsGameInputCaptured;
        bool grabbed = CursorState == OpenTK.Windowing.Common.CursorState.Grabbed;
        ReleaseGameInput();
        using (var dialog = new VideoRecorderDialog(Recorder)) dialog.ShowDialog();
        if (captured) CaptureGameInput(grabbed);
    }

    protected override void DrawVideoRecorderOverlay()
    {
        if (_smokeTest || _validateOnly) return;
        if (_recorderMessage != Recorder.Message)
        {
            _recorderMessage = Recorder.Message;
            _recorderNoticeUntil = DateTime.UtcNow.AddSeconds(10);
        }
        if (!Recorder.IsActive && Recorder.State != VideoRecorderState.Saving && DateTime.UtcNow > _recorderNoticeUntil) return;
        BeginScreenOverlay();
        string message = Recorder.IsActive
            ? $"REC {Recorder.Elapsed:mm\\:ss} | F9: stop | Shift+F9: settings"
            : Recorder.Message;
        float width = Math.Max(100, Math.Min(WindowWidth - 24, 900));
        Renderer.DrawQuad(new System.Numerics.Vector2(12 + width / 2, 34), new(width, 44), new(.03f, .04f, .06f, .9f));
        Renderer.DrawText(message, null, 18, new(20, 21), new(1, .8f, .8f, 1), width - 16);
    }

    public StandaloneGame(GameProjectRuntime project, bool smokeTest = false, bool validateOnly = false, Action? firstFrame = null)
        : base(project.Project.Window.Width, project.Project.Window.Height, project.Project.Name)
    {
        Icon = ByteEngine.Branding.NativeIdentity.LoadWindowIcon();
        _project = project;
        _firstFrame = firstFrame;
        _smokeTest=smokeTest;
        _validateOnly=validateOnly;
        if(smokeTest||validateOnly)IsVisible=false;

    }

    protected override void OnEngineStart()
    {
        // Plugins drive windowed/borderless/fullscreen through Core; the host owns the window.
        if (!_smokeTest && !_validateOnly)
            _displayLease = GameWindowDisplay.Attach(ApplyDisplayMode, _project.Project.Window.StartFullscreen ? GameWindowMode.Fullscreen : GameWindowMode.Windowed);
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
        bool grab = WantsMouseLook(scene);
        CaptureGameInput(grab);
    }

    private NetworkLobbyForm? networkLobby;
    protected override void OnEngineUpdate()
    {        System.Windows.Forms.Application.DoEvents();
        if(!Scenes.ActiveScene!.GameObjects.Any(o=>o.Components.Any(c=>c.GetType().Name=="PvpMatch3D"))&&Input.IsKeyPressed(Key.F10) && Scenes.ActiveScene?.GameObjects.Select(o=>o.GetComponent<ByteEngine.Core.Net.NetworkSettings>()).FirstOrDefault(s=>s!=null) is {} networkSettings){
            if(networkLobby is null || networkLobby.IsDisposed)networkLobby=new NetworkLobbyForm(networkSettings);
            networkLobby.Show();networkLobby.Activate();ReleaseGameInput();
        }
        // Menu closure can change mouse-look ownership after startup captured a free cursor.
        if (IsGameInputCaptured && Scenes.ActiveScene is { } activeScene)
            CaptureGameInput(WantsMouseLook(activeScene));

        if (!IsGameInputCaptured && Input.IsMouseButtonPressed(MouseButton.Left))
        {
            bool grab = Scenes.ActiveScene is {} scene && WantsMouseLook(scene);
            CaptureGameInput(grab);
        }
    }

    private static bool WantsMouseLook(Scene scene) =>
        scene.ActiveCamera?.GameObject.GetComponent<TopDownCamera3D>()?.Enabled != true &&
        (scene.GameObjects.Any(item => item.ActiveInHierarchy && item.Components.Any(c=>c.Enabled && c.GetType().Name=="PvpMatch3D")) && ByteEngine.Core.Net.NetworkRuntime.Session is {IsActive:true} ||
        scene.GameObjects.Any(item => item.ActiveInHierarchy &&
            item.GetComponent<PlayerController3D>() is { Enabled: true, AcceptLookInput: true }));

    protected override void OnEngineRender()
    {
        _firstFrame?.Invoke();
        _firstFrame = null;
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

    private void ApplyDisplayMode(GameWindowMode mode)
    {
        var monitor = OpenTK.Windowing.Desktop.Monitors.GetMonitorFromWindow(this);
        var area = monitor.ClientArea;
        if (mode == GameWindowMode.Fullscreen)
        {
            WindowState = OpenTK.Windowing.Common.WindowState.Fullscreen;
            return;
        }

        WindowState = OpenTK.Windowing.Common.WindowState.Normal;
        WindowBorder = mode == GameWindowMode.Borderless
            ? OpenTK.Windowing.Common.WindowBorder.Hidden
            : OpenTK.Windowing.Common.WindowBorder.Resizable;
        if (mode == GameWindowMode.Borderless)
        {
            ClientSize = area.Size;
            Location = area.Min;
        }
        else
        {
            ClientSize = new(
                Math.Min(_project.Project.Window.Width, Math.Max(640, area.Size.X - 80)),
                Math.Min(_project.Project.Window.Height, Math.Max(480, area.Size.Y - 100)));
            CenterWindow();
        }
    }

    protected override void OnEngineShutdown()
    {
        _displayLease?.Dispose();
        _displayLease = null;
        ReleaseGameInput();
        // Release GPU assets while the window still owns its live graphics context.
        _project.Dispose();
    }
}
