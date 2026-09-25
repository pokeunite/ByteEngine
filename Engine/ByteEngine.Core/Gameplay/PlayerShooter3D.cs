using System.Numerics;
using ByteEngine.Core.InputSystem;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Gameplay;

public sealed class PlayerShooter3D : Component
{
    public bool Automatic { get; set; } = true;
    public bool AimAtPointer { get; set; }
    public override int UpdateOrder => -90;

    protected override void OnUpdate()
    {
        GameObject? player = AttachedGameObject;
        RuntimeScene? scene = player?.Scene;
        if (player == null || scene == null || !Input.IsGameInputCaptured) return;
        Camera3D? camera = scene.ActiveCamera;
        if (camera == null) return;

        Vector3 aimDirection = CalculateAimDirection(player, camera, scene,
            Input.GameViewSize.X / Math.Max(Input.GameViewSize.Y, 1f), AimAtPointer);
        if (AimAtPointer)
        {
            InputActionState look = InputActions.Get("Look");
            if (look.ActiveSource == nameof(InputBindingType.GamepadStick) &&
                look.Axis2D.LengthSquared() > .04f)
                aimDirection = CalculateStickAimDirection(camera, look.Axis2D);
        }
        FaceAimDirection(player, AimAtPointer ? aimDirection : camera.Transform.Forward);

        bool fire = Automatic
            ? InputActions.IsDown("Fire") || Input.IsMouseButtonDown(MouseButton.Left)
            : InputActions.WasPressed("Fire") || Input.IsMouseButtonPressed(MouseButton.Left);
        if (fire) player.GetComponent<ProjectileLauncher3D>()?.Fire(aimDirection);
    }

    public static Vector3 CalculateAimDirection(GameObject player, Camera3D camera, RuntimeScene scene, float aspectRatio, bool aimAtPointer = false)
    {
        Vector2 screenPoint = aimAtPointer ? Input.GameViewPointerNormalized : new Vector2(.5f);
        (Vector3 rayOrigin, Vector3 rayDirection) = camera.ScreenPointToRay(screenPoint, aspectRatio);
        Vector3 muzzle = player.Transform.WorldPosition;
        ProjectileLauncher3D? launcher = player.GetComponent<ProjectileLauncher3D>();
        if (launcher != null) muzzle = Vector3.Transform(launcher.MuzzleOffset, player.Transform.WorldMatrix);

        if (aimAtPointer)
        {
            Vector3 target;
            if (GameplayQuery3D.Raycast(scene, rayOrigin, rayDirection, out RaycastHit3D pointerHit,
                    1000f, player, null, player))
                target = pointerHit.Point;
            else if (MathF.Abs(rayDirection.Y) > .0001f)
                target = rayOrigin + rayDirection * Math.Max(0f, (muzzle.Y - rayOrigin.Y) / rayDirection.Y);
            else
                target = rayOrigin + rayDirection * 1000f;
            target.Y = muzzle.Y;
            Vector3 flatDirection = target - muzzle;
            if (flatDirection.LengthSquared() > .000001f) return Vector3.Normalize(flatDirection);
        }

        if (GameplayQuery3D.Raycast(scene, rayOrigin, rayDirection, out RaycastHit3D hit, 1000f,
                player, null, player) &&
            hit.GameObject.GetComponent<ByteEngine.Core.Characters.GroundSurface>() == null)
        {
            Vector3 towardHit = hit.Point - muzzle;
            if (towardHit.LengthSquared() > .000001f) return Vector3.Normalize(towardHit);
        }

        Vector3 distantPoint = rayOrigin + rayDirection * 1000f;
        Vector3 fallback = distantPoint - muzzle;
        return fallback.LengthSquared() > .000001f ? Vector3.Normalize(fallback) : rayDirection;
    }

    public static Vector3 CalculateStickAimDirection(Camera3D camera, Vector2 stick)
    {
        Vector3 right = camera.Transform.Right;
        right.Y = 0f;
        if (right.LengthSquared() < .000001f) right = Vector3.UnitX;
        Vector3 forward = camera.Transform.Forward;
        forward.Y = 0f;
        if (forward.LengthSquared() < .000001f) forward = -Vector3.UnitZ;
        Vector3 direction = Vector3.Normalize(right) * stick.X +
            Vector3.Normalize(forward) * stick.Y;
        return direction.LengthSquared() > .000001f
            ? Vector3.Normalize(direction) : Vector3.Normalize(forward);
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
