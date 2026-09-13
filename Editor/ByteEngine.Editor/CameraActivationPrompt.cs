using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

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
