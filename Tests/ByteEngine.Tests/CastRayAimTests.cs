using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Tests;

internal static class CastRayAimTests
{
    public static void Run()
    {
        TestTopDownCursor();
        TestCrosshair("ThirdPersonCrosshair", new Vector3(1f, 2f, 5f),
            new Vector3(1f, 2f, -5f), new Vector3(0f, 1f, 0f),
            new Vector3(.2f, 1.2f, -1f), new Vector3(.5f, 2f, .5f));
        TestCrosshair("FirstPersonCrosshair", new Vector3(0f, 1.6f, 0f),
            new Vector3(0f, 1.6f, -5f), new Vector3(.2f, 1.4f, 0f),
            new Vector3(.2f, 1.4f, -.5f), new Vector3(.2f, .3f, .2f));
        TestMuzzleDirection("Forward", new Vector3(0f, 0f, -5f));
        TestMuzzleDirection("Right", new Vector3(5f, 0f, 0f));
        TestMuzzleDirection("Left", new Vector3(-5f, 0f, 0f));
        TestLegacyMuzzleDirection();
        TestDebugRayStaysSmallAndBounded();
        TestCameraTargetingIgnoresPlayerMatrixAndExtendsSearch();
    }

    private static void TestTopDownCursor()
    {
        var scene = new Scene("Top Down Cast Ray");
        GameObject owner = CreateOwnerAndMuzzle(scene, new Vector3(0f, 1f, 0f));
        GameObject cameraObject = scene.CreateGameObject("Camera");
        cameraObject.Transform.WorldPosition = new Vector3(0f, 10f, 0f);
        cameraObject.Transform.EulerAngles = new Vector3(-90f, 0f, 0f);
        Camera3D camera = cameraObject.AddComponent(new Camera3D { FieldOfView = 60f });
        scene.SetActiveCamera(camera);
        Assert(camera.Transform.Forward.Y < -.99f, "Top-down camera faces down");
        GameObject target = AddBox(scene, "Enemy", new Vector3(2f, 1f, 0f),
            new Vector3(1f, 2f, 1f));
        AddBox(scene, "Ground", new Vector3(0f, -1.5f, 0f),
            new Vector3(30f, 1f, 30f));
        Input.SetGameViewPointer(new Vector2(.6732f, .5f), new Vector2(1000f), true);
        try
        {
            EventExecutionContext context = Fire(scene, owner, "TopDownCursor");
            Assert(context.LastRaycastHit?.GameObject == target,
                "Top-down cursor aims at the elevated enemy instead of ground");
        }
        finally
        {
            Input.SetGameViewPointer(new Vector2(.5f), new Vector2(1000f), false);
        }
    }

    private static void TestCrosshair(string mode, Vector3 cameraPosition,
        Vector3 targetPosition, Vector3 muzzlePosition, Vector3 wallPosition,
        Vector3 wallSize)
    {
        var scene = new Scene(mode + " Cast Ray");
        GameObject owner = CreateOwnerAndMuzzle(scene, muzzlePosition);
        GameObject cameraObject = scene.CreateGameObject("Camera");
        cameraObject.Transform.WorldPosition = cameraPosition;
        Camera3D camera = cameraObject.AddComponent(new Camera3D());
        scene.SetActiveCamera(camera);
        GameObject enemy = AddBox(scene, "Enemy", targetPosition, Vector3.One);
        Input.SetGameViewPointer(new Vector2(.9f, .1f), new Vector2(1000f), true);
        EventExecutionContext clearShot = Fire(scene, owner, mode);
        Assert(clearShot.LastRaycastHit?.GameObject == enemy,
            mode + " uses screen center and reaches the visible enemy");
        GameObject wall = AddBox(scene, "Near Wall", wallPosition, wallSize);
        EventExecutionContext blockedShot = Fire(scene, owner, mode);
        Assert(blockedShot.LastRaycastHit?.GameObject == wall,
            mode + " muzzle ray is blocked despite a clear camera target");
    }

    private static void TestDebugRayStaysSmallAndBounded()
    {
        var scene = new Scene("Debug Ray Cast");
        GameObject owner = CreateOwnerAndMuzzle(scene, new Vector3(0f, 0f, -.2f));
        GameObject cameraObject = scene.CreateGameObject("Camera");
        cameraObject.Transform.WorldPosition = Vector3.Zero;
        Camera3D camera = cameraObject.AddComponent(new Camera3D());
        scene.SetActiveCamera(camera);
        GameObject target = AddBox(scene, "Target", new Vector3(0f, 0f, -5f), Vector3.One);
        Guid instructionId = Guid.NewGuid();

        for (int i = 0; i < 20; i++)
        {
            EventExecutionContext context = Fire(scene, owner, "FirstPersonCrosshair",
                drawDebug: true, instructionId: instructionId);
            Assert(context.LastRaycastHit?.GameObject == target,
                "Debug visuals do not change the actual hit");
        }

        GameObject[] markers = scene.GameObjects
            .Where(item => item.Name.StartsWith("__DebugRay_", StringComparison.Ordinal))
            .ToArray();
        Assert(markers.Length == 2,
            "Repeated shots reuse one camera and one muzzle marker");
        GameObject cameraMarker = markers.Single(item => item.Name.Contains("Camera_"));
        Vector3 markerDirection = Vector3.Transform(Vector3.UnitZ,
            cameraMarker.Transform.WorldRotation);
        Vector3 visibleStart = cameraMarker.Transform.WorldPosition -
            markerDirection * cameraMarker.Transform.LocalScale.Z * .5f;
        float visibleDepth = Vector3.Dot(visibleStart -
            camera.Transform.WorldPosition, camera.Transform.Forward);
        Assert(visibleDepth >= .49f,
            "Camera debug marker starts beyond the near plane");
    }

    private static void TestMuzzleDirection(string directionMode, Vector3 targetPosition)
    {
        var scene = new Scene("Muzzle " + directionMode);
        GameObject owner = CreateOwnerAndMuzzle(scene, Vector3.Zero);
        GameObject target = AddBox(scene, "Target", targetPosition, Vector3.One);
        EventExecutionContext context = Fire(scene, owner, "MuzzleDirection",
            directionMode: directionMode);
        Assert(context.LastRaycastHit?.GameObject == target,
            "Muzzle " + directionMode + " uses the muzzle's local axis");
    }

    private static void TestCameraTargetingIgnoresPlayerMatrixAndExtendsSearch()
    {
        var scene = new Scene("Camera target-selection filtering");
        GameObject owner = CreateOwnerAndMuzzle(scene, new Vector3(0f, 1f, 0f));
        int playerLayer = scene.Classification.FindLayer("Player")!.Index;
        int enemyLayer = scene.Classification.FindLayer("Enemy")!.Index;
        owner.Layer = playerLayer;
        scene.Classification.CollisionMatrix.SetInteraction(playerLayer, enemyLayer, false);

        GameObject cameraObject = scene.CreateGameObject("Camera");
        cameraObject.Transform.WorldPosition = new Vector3(0f, 10f, 0f);
        cameraObject.Transform.EulerAngles = new Vector3(-90f, 0f, 0f);
        Camera3D camera = cameraObject.AddComponent(new Camera3D());
        scene.SetActiveCamera(camera);
        GameObject target = AddBox(scene, "Enemy", Vector3.Zero, Vector3.One);
        target.Layer = enemyLayer;

        Input.SetGameViewPointer(new Vector2(.5f), new Vector2(1000f), true);
        try
        {
            EventExecutionContext context = Fire(scene, owner, "TopDownCursor",
                drawDebug: true, distance: 3f);
            Assert(context.LastRaycastHit == null,
                "Final muzzle hit still obeys the player collision matrix");
            GameObject cameraMarker = scene.GameObjects.Single(item =>
                item.Name.StartsWith("__DebugRay_Camera_", StringComparison.Ordinal));
            Vector3 markerDirection = Vector3.Transform(Vector3.UnitZ,
                cameraMarker.Transform.WorldRotation);
            Vector3 visibleEnd = cameraMarker.Transform.WorldPosition +
                markerDirection * cameraMarker.Transform.LocalScale.Z * .5f;
            Assert(Vector3.Distance(visibleEnd, target.Transform.WorldPosition) < 1f,
                "Camera target query reaches the enemy beyond weapon distance and bypasses player matrix");
        }
        finally
        {
            Input.SetGameViewPointer(new Vector2(.5f), new Vector2(1000f), false);
        }
    }

    private static void TestLegacyMuzzleDirection()
    {
        var scene = new Scene("Legacy Cast Ray");
        GameObject owner = CreateOwnerAndMuzzle(scene, Vector3.Zero);
        GameObject target = AddBox(scene, "Target", new Vector3(0f, 0f, -5f), Vector3.One);
        EventExecutionContext context = Fire(scene, owner, null, directionMode: null);
        Assert(context.LastRaycastHit?.GameObject == target,
            "Existing nodes without Aim Mode retain muzzle-direction behavior");
    }

    private static GameObject CreateOwnerAndMuzzle(Scene scene, Vector3 muzzlePosition)
    {
        GameObject owner = scene.CreateGameObject("Player");
        GameObject weapon = scene.CreateGameObject("Weapon");
        weapon.SetParent(owner, false);
        GameObject muzzle = scene.CreateGameObject("Muzzle");
        muzzle.SetParent(weapon, false);
        muzzle.Transform.WorldPosition = muzzlePosition;
        return owner;
    }

    private static GameObject AddBox(Scene scene, string name, Vector3 position, Vector3 size)
    {
        GameObject gameObject = scene.CreateGameObject(name);
        gameObject.Transform.WorldPosition = position;
        gameObject.AddComponent(new BoxCollider3D { Size = size });
        return gameObject;
    }

    private static EventExecutionContext Fire(Scene scene, GameObject owner, string? mode,
        bool drawDebug = false, Guid? instructionId = null, string? directionMode = "Forward",
        float distance = 100f)
    {
        var context = new EventExecutionContext
        {
            Globals = new VariableStore(),
            Scene = scene,
            Self = owner
        };
        var instruction = new VisualInstruction
        {
            Id = "physics.castRay",
            InstanceId = instructionId ?? Guid.NewGuid(),
            Arguments = new Dictionary<string, EventValue>
            {
                ["source"] = EventValue.String("Self"),
                ["muzzlePath"] = EventValue.String("Weapon/Muzzle"),
                ["direction"] = EventValue.Vector3(-Vector3.UnitZ),
                ["distance"] = EventValue.Number(distance)
            }
        };
        if (mode != null) instruction.Arguments["aimMode"] = EventValue.String(mode);
        if (directionMode != null)
            instruction.Arguments["directionMode"] = EventValue.String(directionMode);
        if (drawDebug)
        {
            instruction.Arguments["drawDebug"] = EventValue.Boolean(true);
            instruction.Arguments["debugDuration"] = EventValue.Number(4);
        }
        VisualLogicRegistry registry = VisualLogicRegistry.CreateDefault();
        Assert(registry.TryGetAction("physics.castRay", out VisualActionDefinition? action),
            "Cast Ray action registered");
        action!.Execute(instruction, context);
        return context;
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + message);
    }
}
