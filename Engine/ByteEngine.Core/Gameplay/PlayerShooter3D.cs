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

        if (player == null || scene == null)
        {
            return;
        }

        if (Input.IsGameViewHovered && Input.IsGameViewFocused)
        {
            AimAtGameViewPointer(player, scene);
        }

        bool fire =
            Automatic
                ? Input.IsMouseButtonDown(MouseButton.Left)
                : Input.IsMouseButtonPressed(MouseButton.Left);

        if (!fire || !Input.IsGameViewHovered || !Input.IsGameViewFocused)
        {
            return;
        }

        player.GetComponent<ProjectileLauncher3D>()?.Fire();
    }

    private static void AimAtGameViewPointer(
        GameObject player,
        RuntimeScene scene)
    {
        Camera3D? camera = scene.FindComponent<Camera3D>();

        if (camera == null)
        {
            return;
        }

        float aspect = Input.GameViewSize.X / Math.Max(Input.GameViewSize.Y, 1f);
        (Vector3 rayOrigin, Vector3 rayDirection) = camera.ScreenPointToRay(Input.GameViewPointerNormalized, aspect);
        float aimPlaneY = player.Transform.WorldPosition.Y;
        Vector3 aimPoint;
        if (GameplayQuery3D.Raycast(scene, rayOrigin, rayDirection, out RaycastHit3D hit, 1000f, player))
            aimPoint = hit.Point;
        else
        {
            if (MathF.Abs(rayDirection.Y) <= .0001f) return;
            float distance = (aimPlaneY - rayOrigin.Y) / rayDirection.Y;
            if (distance <= 0f) return;
            aimPoint = rayOrigin + rayDirection * distance;
        }

        Vector3 direction =
            aimPoint -
            player.Transform.WorldPosition;

        direction.Y = 0f;

        if (direction.LengthSquared() <= .0001f)
        {
            return;
        }

        direction = Vector3.Normalize(direction);

        Vector3 euler = player.Transform.EulerAngles;

        euler.Y =
            MathF.Atan2(
                -direction.X,
                -direction.Z) *
            180f /
            MathF.PI;

        player.Transform.EulerAngles = euler;
    }
}
