using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Gameplay;

public sealed class PlayerShooter3D : Component
{
    public bool Automatic { get; set; } = true;
    public override int UpdateOrder => -90;

    protected override void OnUpdate()
    {
        GameObject? player = AttachedGameObject;
        RuntimeScene? scene = player?.Scene;
        if (player == null || scene == null || !Input.IsGameInputCaptured) return;
        Camera3D? camera = scene.ActiveCamera;
        if (camera == null) return;

        Vector3 aimDirection = CalculateAimDirection(player, camera, scene,
            Input.GameViewSize.X / Math.Max(Input.GameViewSize.Y, 1f));
        FaceAimDirection(player, camera.Transform.Forward);

        bool fire = Automatic
            ? Input.IsMouseButtonDown(MouseButton.Left)
            : Input.IsMouseButtonPressed(MouseButton.Left);
        if (fire) player.GetComponent<ProjectileLauncher3D>()?.Fire(aimDirection);
    }

    public static Vector3 CalculateAimDirection(GameObject player, Camera3D camera, RuntimeScene scene, float aspectRatio)
    {
        (Vector3 rayOrigin, Vector3 rayDirection) = camera.ScreenPointToRay(new Vector2(.5f), aspectRatio);
        Vector3 muzzle = player.Transform.WorldPosition;
        ProjectileLauncher3D? launcher = player.GetComponent<ProjectileLauncher3D>();
        if (launcher != null) muzzle = Vector3.Transform(launcher.MuzzleOffset, player.Transform.WorldMatrix);

        if (GameplayQuery3D.Raycast(scene, rayOrigin, rayDirection, out RaycastHit3D hit, 1000f, player) &&
            hit.GameObject.GetComponent<ByteEngine.Core.Characters.GroundSurface>() == null)
        {
            Vector3 towardHit = hit.Point - muzzle;
            if (towardHit.LengthSquared() > .000001f) return Vector3.Normalize(towardHit);
        }

        Vector3 distantPoint = rayOrigin + rayDirection * 1000f;
        Vector3 fallback = distantPoint - muzzle;
        return fallback.LengthSquared() > .000001f ? Vector3.Normalize(fallback) : rayDirection;
    }

    private static void FaceAimDirection(GameObject player, Vector3 cameraForward)
    {
        cameraForward.Y = 0f;
        if (cameraForward.LengthSquared() <= .0001f) return;
        cameraForward = Vector3.Normalize(cameraForward);
        Vector3 euler = player.Transform.EulerAngles;
        euler.Y = MathF.Atan2(-cameraForward.X, -cameraForward.Z) * 180f / MathF.PI;
        player.Transform.EulerAngles = euler;
    }
}
