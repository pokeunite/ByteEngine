using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ByteEngine.Editor.Panels;

namespace ByteEngine.Editor;

internal enum CameraActivationPromptKind
{
    UseAsFirstCamera,
    ReplaceActiveCamera
}

internal readonly record struct CameraActivationRequest(
    CameraActivationPromptKind Kind,
    Camera3D Camera,
    Camera3D? PreviousActiveCamera);

internal static class CameraActivationPrompt
{
    public static CameraActivationRequest Create(Camera3D camera, Camera3D? previousActive) =>
        new(previousActive == null
                ? CameraActivationPromptKind.UseAsFirstCamera
                : CameraActivationPromptKind.ReplaceActiveCamera,
            camera,
            previousActive);

    public static void Apply(Scene? scene, CameraActivationRequest request, bool makeActive)
    {
        if (!makeActive || scene == null) return;
        scene.SetActiveCamera(request.Camera);
    }
}

internal sealed class CameraActivationPromptState
{
    private readonly PopupInteractionState _interaction = new();
    public CameraActivationRequest? Request { get; private set; }
    public bool IsOpen => _interaction.IsOpen;

    public void Begin(CameraActivationRequest request)
    {
        if (!_interaction.Request()) return;
        Request = request;
    }

    public bool ConsumeOpenRequest() => _interaction.ConsumeOpenRequest();
    public void MarkVisible() => _interaction.MarkVisible();

    public void RecoverWhenNotVisible()
    {
        _interaction.RecoverWhenNotVisible();
        if (!_interaction.IsOpen) Request = null;
    }

    public void Reset()
    {
        Request = null;
        _interaction.Reset();
    }
}
