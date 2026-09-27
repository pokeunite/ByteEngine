using System.Numerics;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Characters;
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

        Vector3 aimDirection = CalculateAimDirection(
            player,
            camera,
            scene,
            Input.GameViewSize.X / Math.Max(Input.GameViewSize.Y, 1f),
            AimAtPointer);

        if (AimAtPointer)
        {
            InputActionState look = InputActions.Get("Look");

            if (look.ActiveSource == nameof(InputBindingType.GamepadStick) &&
                look.Axis2D.LengthSquared() > .04f)
            {
                aimDirection =
                    CalculateStickAimDirection(
                        camera,
                        look.Axis2D);
            }
        }

        /*
         * PlayerController3D is the authoritative owner of a normal TPS
         * character's body rotation.
         *
         * Previously PlayerController3D updated the character first (-300),
         * then PlayerShooter3D ran later (-90) and rewrote the SAME root
         * rotation every frame from camera aim. Besides defeating FaceMovement
         * free-orbit/turn-in-place, the old shooter path also round-tripped
         * through Transform.EulerAngles. The two writers could therefore fight
         * each other and produce visible alternating yaw jitter, especially
         * while moving/jumping.
         *
         * Keep the shooter focused on aim/fire. It may own body rotation only
         * when there is no active PlayerController3D, or when that controller
         * explicitly declares its rotation Independent.
         */
        PlayerController3D? playerController =
            player.GetComponent<PlayerController3D>();

        bool shooterOwnsRotation =
            playerController?.Enabled != true ||
            playerController.CharacterRotation ==
                CharacterRotationMode.Independent;

        if (shooterOwnsRotation)
        {
            FaceAimDirection(
                player,
                AimAtPointer
                    ? aimDirection
                    : camera.Transform.Forward);
        }

        bool fire =
            Automatic
                ? InputActions.IsDown("Fire") ||
                  Input.IsMouseButtonDown(MouseButton.Left)
                : InputActions.WasPressed("Fire") ||
                  Input.IsMouseButtonPressed(MouseButton.Left);

        if (fire)
        {
            float aspectRatio =
                Input.GameViewSize.X /
                Math.Max(
                    Input.GameViewSize.Y,
                    1f);

            Vector2 debugScreenPoint =
                AimAtPointer
                    ? Input.GameViewPointerNormalized
                    : new Vector2(.5f);

            (Vector3 debugRayOrigin, Vector3 debugRayDirection) =
                camera.ScreenPointToRay(
                    debugScreenPoint,
                    aspectRatio);

            ProjectileLauncher3D? launcher =
                player.GetComponent<ProjectileLauncher3D>();

            Vector3 muzzle =
                launcher != null
                    ? Vector3.Transform(
                        launcher.MuzzleOffset,
                        player.Transform.WorldMatrix)
                    : player.Transform.WorldPosition;

            bool cameraTargetFound = GameplayQuery3D.Raycast(
                scene,
                debugRayOrigin,
                debugRayDirection,
                out RaycastHit3D cameraTarget,
                1000f,
                player,
                null,
                source: null,
                bypassCollisionMatrix: true,
                includeTriggers: false);

            bool muzzlePredictionFound = GameplayQuery3D.Raycast(
                scene,
                muzzle,
                aimDirection,
                out RaycastHit3D muzzlePrediction,
                1000f,
                player,
                null,
                source: null,
                bypassCollisionMatrix: true,
                includeTriggers: false);

            string playerMatrixAllowsTarget =
                cameraTargetFound
                    ? scene.Classification.CollisionMatrix.ShouldInteract(
                        player.Layer,
                        cameraTarget.GameObject.Layer).ToString()
                    : "n/a";

            RuntimeDiagnostics.RecordWeaponRaycast(
                $"PLAYER AIM player=\"{player.Name}\" id={player.Id:N} aimAtPointer={AimAtPointer} automatic={Automatic} launcher={(launcher != null)} " +
                $"inputCaptured={Input.IsGameInputCaptured} pointerOver={Input.IsPointerOverGameView} pointerFocused={Input.IsGameViewFocused} " +
                $"screen=({debugScreenPoint.X:0.000},{debugScreenPoint.Y:0.000}) view=({Input.GameViewSize.X:0},{Input.GameViewSize.Y:0}) " +
                $"playerPos={DebugV3(player.Transform.WorldPosition)} playerFwd={DebugV3(player.Transform.Forward)} playerLayer={LayerName(scene, player.Layer)} " +
                $"cameraPos={DebugV3(camera.Transform.WorldPosition)} cameraFwd={DebugV3(camera.Transform.Forward)} " +
                $"cameraRayOrigin={DebugV3(debugRayOrigin)} cameraRayDir={DebugV3(debugRayDirection)} " +
                $"cameraTarget={DescribeHit(scene, cameraTargetFound, cameraTarget)} playerMatrixAllowsTarget={playerMatrixAllowsTarget} " +
                $"muzzleOffset={DebugV3(launcher?.MuzzleOffset ?? Vector3.Zero)} muzzle={DebugV3(muzzle)} resolvedAimDir={DebugV3(aimDirection)} " +
                $"muzzlePrediction={DescribeHit(scene, muzzlePredictionFound, muzzlePrediction)}");

            launcher?.Fire(
                aimDirection);
        }
    }

    public static Vector3 CalculateAimDirection(
        GameObject player,
        Camera3D camera,
        RuntimeScene scene,
        float aspectRatio,
        bool aimAtPointer = false)
    {
        Vector2 screenPoint =
            aimAtPointer
                ? Input.GameViewPointerNormalized
                : new Vector2(.5f);

        (Vector3 rayOrigin, Vector3 rayDirection) =
            camera.ScreenPointToRay(
                screenPoint,
                aspectRatio);

        Vector3 muzzle =
            player.Transform.WorldPosition;

        ProjectileLauncher3D? launcher =
            player.GetComponent<ProjectileLauncher3D>();

        if (launcher != null)
        {
            muzzle =
                Vector3.Transform(
                    launcher.MuzzleOffset,
                    player.Transform.WorldMatrix);
        }

        if (aimAtPointer)
        {
            Vector3 target;

            // Pointer targeting is a screen-space visibility query. It must not
            // inherit the player's collision matrix or an enemy can be skipped
            // and the ground behind it becomes the selected target instead.
            if (GameplayQuery3D.Raycast(
                    scene,
                    rayOrigin,
                    rayDirection,
                    out RaycastHit3D pointerHit,
                    1000f,
                    player,
                    null,
                    source: null,
                    bypassCollisionMatrix: true,
                    includeTriggers: false))
            {
                target =
                    pointerHit.Point;
            }
            else if (MathF.Abs(rayDirection.Y) >
                     .0001f)
            {
                target =
                    rayOrigin +
                    rayDirection *
                    Math.Max(
                        0f,
                        (muzzle.Y - rayOrigin.Y) /
                        rayDirection.Y);
            }
            else
            {
                target =
                    rayOrigin +
                    rayDirection *
                    1000f;
            }

            // Keep the complete 3D hit point for firing. FaceAimDirection
            // flattens its own copy for body yaw, so projectile/ray accuracy no
            // longer has to be sacrificed to keep a top-down character upright.
            Vector3 directionToTarget =
                target -
                muzzle;

            if (directionToTarget.LengthSquared() >
                .000001f)
            {
                return Vector3.Normalize(
                    directionToTarget);
            }
        }

        if (GameplayQuery3D.Raycast(
                scene,
                rayOrigin,
                rayDirection,
                out RaycastHit3D hit,
                1000f,
                player,
                null,
                player) &&
            hit.GameObject.GetComponent<
                ByteEngine.Core.Characters.GroundSurface>() ==
            null)
        {
            Vector3 towardHit =
                hit.Point -
                muzzle;

            if (towardHit.LengthSquared() >
                .000001f)
            {
                return Vector3.Normalize(
                    towardHit);
            }
        }

        Vector3 distantPoint =
            rayOrigin +
            rayDirection *
            1000f;

        Vector3 fallback =
            distantPoint -
            muzzle;

        return fallback.LengthSquared() >
               .000001f
            ? Vector3.Normalize(fallback)
            : rayDirection;
    }

    public static Vector3 CalculateStickAimDirection(
        Camera3D camera,
        Vector2 stick)
    {
        Vector3 right =
            camera.Transform.Right;

        right.Y =
            0f;

        if (right.LengthSquared() <
            .000001f)
        {
            right =
                Vector3.UnitX;
        }

        Vector3 forward =
            camera.Transform.Forward;

        forward.Y =
            0f;

        if (forward.LengthSquared() <
            .000001f)
        {
            forward =
                -Vector3.UnitZ;
        }

        Vector3 direction =
            Vector3.Normalize(right) *
            stick.X +
            Vector3.Normalize(forward) *
            stick.Y;

        return direction.LengthSquared() >
               .000001f
            ? Vector3.Normalize(direction)
            : Vector3.Normalize(forward);
    }

    private static string DescribeHit(
        RuntimeScene scene,
        bool found,
        RaycastHit3D hit)
    {
        if (!found)
            return "MISS";

        GameObject? healthOwner =
            FindHealthOwner(hit.GameObject);

        return
            $"HIT(object=\"{hit.GameObject.Name}\",id={hit.GameObject.Id:N},layer={LayerName(scene, hit.GameObject.Layer)}," +
            $"collider={hit.Collider.GetType().Name},point={DebugV3(hit.Point)},normal={DebugV3(hit.Normal)},distance={hit.Distance:0.000}," +
            $"ground={IsGround(hit.GameObject)},healthOwner=\"{healthOwner?.Name ?? "<none>"}\")";
    }

    private static string LayerName(
        RuntimeScene scene,
        int layer) =>
        $"{layer}:{scene.Classification.FindLayer(layer)?.Name ?? "<missing>"}";

    private static bool IsGround(
        GameObject gameObject)
    {
        for (GameObject? current = gameObject;
             current != null;
             current = current.Parent)
        {
            if (current.GetComponent<GroundSurface>() != null)
                return true;
        }

        return false;
    }

    private static GameObject? FindHealthOwner(
        GameObject gameObject)
    {
        for (GameObject? current = gameObject;
             current != null;
             current = current.Parent)
        {
            if (current.GetComponent<HealthComponent>() != null)
                return current;
        }

        return null;
    }

    private static string DebugV3(
        Vector3 value) =>
        $"({value.X:0.000},{value.Y:0.000},{value.Z:0.000})";

    private static void FaceAimDirection(
        GameObject player,
        Vector3 cameraForward)
    {
        cameraForward.Y =
            0f;

        if (cameraForward.LengthSquared() <=
            .0001f)
        {
            return;
        }

        cameraForward =
            Vector3.Normalize(
                cameraForward);

        /*
         * Fallback rotation path for shooter-only characters.
         *
         * Do not read/modify/write Transform.EulerAngles here. Build the
         * intended world-Y rotation directly so this path cannot reintroduce
         * quaternion -> Euler -> quaternion feedback jitter.
         */
        float yaw =
            PlayerController3D.YawFromDirection(
                cameraForward);

        player.Transform.WorldRotation =
            Quaternion.CreateFromAxisAngle(
                Vector3.UnitY,
                yaw *
                MathF.PI /
                180f);
    }
}
