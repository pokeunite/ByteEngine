using System.Numerics;
using ByteEngine.Core.Construction;
using ByteEngine.Core.Scene;

namespace ByteEngine.Sandbox.Goblin;

internal static class GoblinPhysicsTests
{
    public static void Run()
    {
        var scene = new Scene("Goblin vehicle physics test");
        GameObject chassis = scene.CreateGameObject("Chassis");
        chassis.Transform.WorldPosition = new Vector3(0, 1.1f, 0);
        GameObject wheel = scene.CreateGameObject("Wheel");
        wheel.Transform.WorldPosition = new Vector3(.85f, .5f, .6f);
        Guid chassisSocket = Guid.NewGuid(), wheelSocket = Guid.NewGuid();
        var core = new AssemblyPart<string>(chassis.Id, "Core",
            new[] { new AssemblySocket(chassisSocket, "WheelAxle", Matrix4x4.Identity) });
        var tire = new AssemblyPart<string>(wheel.Id, "Wheel",
            new[] { new AssemblySocket(wheelSocket, "WheelAxle", Matrix4x4.Identity) });
        var graph = new AssemblyGraph<string>();
        graph.Add(core);
        graph.Add(tire);
        graph.Connect(core.Id, chassisSocket, tire.Id, wheelSocket);
        using var physics = new GoblinPhysicsWorld();
        physics.AddBox(core.Id, chassis, new Vector3(1.4f, .35f, 1.8f), 12);
        physics.AddWheel(tire.Id, wheel, .5f, .3f, 2);
        using var integrity = new AssemblyIntegrity<string>(graph, physics, core.Id);
        integrity.SetBuildingMode(true);
        Vector3 frozen = physics.Position(core.Id);
        physics.Step(1f);
        Check(Vector3.Distance(frozen, physics.Position(core.Id)) < .0001f,
            "building mode stops vehicle simulation");
        integrity.SetBuildingMode(false);
        physics.SetWheelSpeed(10);
        for (int i = 0; i < 120; i++) physics.Step(1f / 60f);
        Check(Vector3.Distance(frozen, physics.Position(core.Id)) > .01f,
            "jointed vehicle responds to gravity and wheel motor");
        Check(float.IsFinite(physics.Position(tire.Id).X) &&
              float.IsFinite(physics.Position(tire.Id).Y),
            "wheel remains numerically stable");
        Check(graph.Component(core.Id).Contains(tire.Id), "wheel still attached");
        RunBreakage();
        RunFourWheelDrive();
        RunProjectile();
        RunSpring();
        Console.WriteLine("Goblin BEPU hinge/weld bridge headless checks passed.");
    }

    private static void RunSpring()
    {
        var scene = new Scene("Spring constraint test");
        GameObject a = scene.CreateGameObject("A");
        GameObject b = scene.CreateGameObject("B");
        a.Transform.WorldPosition = new Vector3(0, 2, 0);
        b.Transform.WorldPosition = new Vector3(0, 2, -1.2f);
        Guid aSocket = Guid.NewGuid(), bSocket = Guid.NewGuid();
        var graph = new AssemblyGraph<string>();
        graph.Add(new AssemblyPart<string>(a.Id, "A",
            new[] { new AssemblySocket(aSocket, "Spring", Matrix4x4.Identity) }));
        graph.Add(new AssemblyPart<string>(b.Id, "B",
            new[] { new AssemblySocket(bSocket, "Spring", Matrix4x4.Identity) }));
        graph.Connect(a.Id, aSocket, b.Id, bSocket);
        using var physics = new GoblinPhysicsWorld();
        physics.AddBox(a.Id, a, new Vector3(.5f), 2);
        physics.AddBox(b.Id, b, new Vector3(.5f), 2);
        using var integrity = new AssemblyIntegrity<string>(graph, physics, a.Id);
        for (int i = 0; i < 120; i++) physics.Step(1f / 60f);
        Check(float.IsFinite(physics.Position(a.Id).X) &&
              float.IsFinite(physics.Position(b.Id).Z),
            "spring constraint stays numerically stable");
    }

    private static void RunProjectile()
    {
        var scene = new Scene("Catapult stone test");
        GameObject stone = scene.CreateGameObject("Stone");
        stone.Transform.WorldPosition = new Vector3(0, 2, 0);
        using var physics = new GoblinPhysicsWorld();
        physics.AddBall(stone.Id, stone, .2f, 1, new Vector3(0, 6, -14));
        for (int i = 0; i < 30; i++) physics.Step(1f / 60f);
        Check(physics.Position(stone.Id).Z < -2 &&
              float.IsFinite(physics.Position(stone.Id).Y),
            "catapult stone follows finite ballistic motion");
        physics.RemoveBody(stone.Id);
    }

    private static void RunBreakage()
    {
        var scene = new Scene("Physical joint break test");
        GameObject core = scene.CreateGameObject("Core");
        core.Transform.WorldPosition = new Vector3(0, 2, 0);
        GameObject armor = scene.CreateGameObject("Armor");
        armor.Transform.WorldPosition = new Vector3(0, 2, -1);
        Guid coreSocket = Guid.NewGuid(), armorSocket = Guid.NewGuid();
        var graph = new AssemblyGraph<string>();
        graph.Add(new AssemblyPart<string>(core.Id, "Core",
            new[] { new AssemblySocket(coreSocket, "Universal", Matrix4x4.Identity) }));
        graph.Add(new AssemblyPart<string>(armor.Id, "Armor",
            new[] { new AssemblySocket(armorSocket, "Universal", Matrix4x4.Identity) }));
        graph.Connect(core.Id, coreSocket, armor.Id, armorSocket, .001f, float.PositiveInfinity);
        using var physics = new GoblinPhysicsWorld();
        physics.AddBox(core.Id, core, new Vector3(1, 1, 1), 10);
        physics.AddBox(armor.Id, armor, new Vector3(.5f, .5f, .5f), 2);
        using var integrity = new AssemblyIntegrity<string>(graph, physics, core.Id);
        bool detached = false;
        integrity.Detached += group => detached = group.Contains(armor.Id);
        for (int i = 0; i < 180 && graph.Links.Count > 0; i++)
        {
            physics.Step(1f / 60f);
            integrity.Evaluate();
        }
        Check(graph.Links.Count == 0 && detached,
            "measured physical load breaks weld and detaches armor");
        graph.Remove(armor.Id);
        physics.RemoveBody(armor.Id);
        Check(graph.Parts.Count == 1, "detached physical part can be removed");
    }

    private static void RunFourWheelDrive()
    {
        var scene = new Scene("Four wheel drive test");
        GameObject chassis = scene.CreateGameObject("Cockpit");
        chassis.Transform.WorldPosition = new Vector3(0, 1.05f, 0);
        Vector3[] offsets =
        {
            new(-.95f, -.52f, -.75f), new(.95f, -.52f, -.75f),
            new(-.95f, -.52f, .75f), new(.95f, -.52f, .75f)
        };
        var chassisSockets = offsets.Select(offset => new AssemblySocket(
            Guid.NewGuid(), "WheelAxle", Matrix4x4.CreateTranslation(offset))).ToList();
        var sawMount = new AssemblySocket(Guid.NewGuid(), "SawAxle",
            Matrix4x4.CreateTranslation(0, 0, -1.6f));
        chassisSockets.Add(sawMount);
        var graph = new AssemblyGraph<string>();
        graph.Add(new AssemblyPart<string>(chassis.Id, "Cockpit", chassisSockets));
        using var physics = new GoblinPhysicsWorld();
        physics.AddBox(chassis.Id, chassis, new Vector3(1.5f, .45f, 2.2f), 14);
        for (int i = 0; i < 4; i++)
        {
            GameObject wheel = scene.CreateGameObject("Wheel");
            wheel.Transform.WorldPosition = chassis.Transform.WorldPosition + offsets[i];
            Guid socket = Guid.NewGuid();
            graph.Add(new AssemblyPart<string>(wheel.Id, "Wheel",
                new[] { new AssemblySocket(socket, "WheelAxle", Matrix4x4.Identity) }));
            physics.AddWheel(wheel.Id, wheel, .46f, .32f, 2);
            graph.Connect(chassis.Id, chassisSockets[i].Id, wheel.Id, socket, 900, 900);
        }
        using var integrity = new AssemblyIntegrity<string>(graph, physics, chassis.Id);
        physics.SetWheelSpeed(12);
        for (int i = 0; i < 300; i++)
        {
            physics.Step(1f / 60f);
            integrity.Evaluate();
        }
        Vector3 result = physics.Position(chassis.Id);
        Check(float.IsFinite(result.Z) && MathF.Abs(result.Z) > 1,
            $"four driven wheels move chassis across ground; actual Z={result.Z:0.###}");
        Check(graph.Component(chassis.Id).Count == 5,
            "normal driving does not snap off wheels");
        GameObject saw = scene.CreateGameObject("Saw");
        saw.Transform.WorldPosition = physics.Position(chassis.Id) +
            Vector3.Transform(new Vector3(0, 0, -1.6f), physics.Orientation(chassis.Id));
        Guid sawSocket = Guid.NewGuid();
        graph.Add(new AssemblyPart<string>(saw.Id, "Saw",
            new[] { new AssemblySocket(sawSocket, "SawAxle", Matrix4x4.Identity) }));
        physics.AddWheel(saw.Id, saw, .5f, .18f, 3);
        graph.Connect(chassis.Id, sawMount.Id, saw.Id, sawSocket, 1000, 1000);
        physics.SetSawSpeed(22);
        for (int i = 0; i < 120; i++) physics.Step(1f / 60f);
        Check(float.IsFinite(physics.Position(saw.Id).X) &&
              graph.Component(chassis.Id).Contains(saw.Id),
            "late-attached saw hinge remains stable under motor load");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception("Goblin physics: " + message);
    }
}
