using ByteEngine.Core;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;

namespace ByteEngine.Player;

internal sealed class StandaloneGame : ByteEngineApplication
{
    private readonly GameProjectRuntime _project;
    protected override bool CloseOnEscape => false;

    public StandaloneGame(GameProjectRuntime project)
        : base(project.Project.Window.Width, project.Project.Window.Height, project.Project.Name)
    {
        _project = project;
    }

    protected override void OnEngineStart()
    {
        foreach (var variable in _project.Project.GlobalVariables)
            Scenes.GlobalVariables.Set(variable.Name, variable.Value.Clone());
        var scene = _project.LoadStartupScene();
        Scenes.RestartSceneFactory = () =>
        {
            Scenes.GlobalVariables.Clear();
            foreach (var variable in _project.Project.GlobalVariables)
                Scenes.GlobalVariables.Set(variable.Name, variable.Value.Clone());
            return _project.LoadStartupScene();
        };
        Scenes.LoadScene(scene);
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

    protected override void OnEngineShutdown()
    {
        ReleaseGameInput();
        // Release GPU assets while the window still owns its live graphics context.
        _project.Dispose();
    }
}
