using ByteEngine.Core.Graphics;

namespace ByteEngine.Core.Scene;

public sealed class SceneManager
{
    public Scene? ActiveScene { get; private set; }

    public bool HasActiveScene =>
        ActiveScene != null;

    public void LoadScene(
        Scene scene)
    {
        ArgumentNullException.ThrowIfNull(
            scene
        );

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
        }

        ActiveScene = scene;

        ActiveScene
            .LoadInternal();

        Console.WriteLine(
            $"Active scene: {scene.Name}"
        );
    }

    public void UnloadScene()
    {
        if (ActiveScene == null)
        {
            return;
        }

        ActiveScene
            .UnloadInternal();

        ActiveScene = null;
    }

    internal void SetEditorScene(
        Scene scene)
    {
        ArgumentNullException.ThrowIfNull(
            scene
        );

        if (ActiveScene?.IsLoaded == true)
        {
            ActiveScene
                .UnloadInternal();
        }

        ActiveScene =
            scene;
    }

    internal void UpdateInternal()
    {
        ActiveScene?
            .UpdateInternal();
    }

    internal void RenderInternal(
        Renderer2D renderer)
    {
        ActiveScene?
            .RenderInternal(
                renderer
            );
    }

    internal void ShutdownInternal()
    {
        UnloadScene();
    }
}
