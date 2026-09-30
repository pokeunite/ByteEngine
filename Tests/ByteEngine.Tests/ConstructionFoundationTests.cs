using System.Numerics;
using ByteEngine.Core.Construction;

namespace ByteEngine.Tests;

internal static class ConstructionFoundationTests
{
    public static void Run()
    {
        static void Check(bool ok, string message)
        {
            if (!ok) throw new Exception("Construction: " + message);
        }
        static AssemblyPart<string> Part(string name, Guid socket) =>
            new(Guid.NewGuid(), name,
                new[] { new AssemblySocket(socket, "Universal", Matrix4x4.Identity) });

        var graph = new AssemblyGraph<string>();
        var cockpit = Part("Cockpit", Guid.NewGuid());
        var wheel = Part("Wheel", Guid.NewGuid());
        var debris = Part("Debris", Guid.NewGuid());
        graph.Add(cockpit);
        graph.Add(wheel);
        graph.Add(debris);
        AssemblyLink link = graph.Connect(cockpit.Id, cockpit.Sockets[0].Id,
            wheel.Id, wheel.Sockets[0].Id, 10, 5);
        Check(graph.Component(cockpit.Id).SetEquals(new[] { cockpit.Id, wheel.Id }),
            "linked component traversal");
        Check(graph.DetachedFrom(cockpit.Id).Count == 1, "orphan component");
        AssemblyBlueprint<string> blueprint = AssemblyBlueprint<string>.Capture(graph, cockpit.Id);
        AssemblyBlueprint<string> restored = AssemblyBlueprint<string>.FromJson(blueprint.ToJson());
        AssemblyGraph<string> restoredGraph = restored.Instantiate();
        Check(restored.Core == cockpit.Id && restoredGraph.Links.Single().Id == link.Id &&
              restoredGraph.Component(cockpit.Id).SetEquals(new[] { cockpit.Id, wheel.Id }),
            "versioned blueprint preserves part/socket/link identities and topology");
        try
        {
            graph.Connect(cockpit.Id, cockpit.Sockets[0].Id,
                debris.Id, debris.Sockets[0].Id);
            throw new Exception("Construction: occupied socket accepted");
        }
        catch (InvalidOperationException) { }

        var adapter = new FakeAdapter();
        using (var integrity = new AssemblyIntegrity<string>(graph, adapter, cockpit.Id))
        using (var bus = new AssemblyEventBus<string>(graph, integrity))
        {
            var detached = new List<IReadOnlySet<Guid>>();
            var events = new List<AssemblyEvent<string>>();
            bus.Published += events.Add;
            var payloadProbe = Part("Spring Pod", Guid.NewGuid());
            bus.Add(payloadProbe);
            Check(events.Any(item => item.Kind == AssemblyEventKind.PartAdded &&
                     item.PartId == payloadProbe.Id && item.Payload == "Spring Pod"),
                "typed payload survives the generic part-added event");
            bus.Remove(payloadProbe.Id);
            integrity.Detached += detached.Add;
            Check(adapter.Created == 1, "existing constraint registration");
            integrity.SetBuildingMode(true);
            Check(adapter.Frozen.Count == 3, "building mode freezes every part");
            adapter.Loads[link.Id] = new AssemblyLoad(11, 0);
            Check(integrity.Evaluate() == 1, "measured overload breaks connection");
            Check(graph.Component(cockpit.Id).Count == 1, "break detaches wheel");
            Check(adapter.Destroyed == 1, "native constraint destroyed");
            Check(detached.Count == 1 && detached[0].Contains(wheel.Id) &&
                  !detached[0].Contains(debris.Id),
                "only newly detached parts raise detach event");
            Check(events.Any(item => item.Kind == AssemblyEventKind.JointBroken &&
                     item.LinkId == link.Id && item.Payload == "Wheel") &&
                  events.Any(item => item.Kind == AssemblyEventKind.Detached &&
                     item.DetachedParts!.Contains(wheel.Id)),
                "typed event bus relays broken link and detached component");
        }

        Matrix4x4 target = Matrix4x4.CreateTranslation(10, 0, 0);
        Matrix4x4 socket = Matrix4x4.CreateTranslation(2, 0, 0);
        Matrix4x4 moving = Matrix4x4.CreateTranslation(1, 0, 0);
        Matrix4x4 placed = SocketPlacement.Align(target, socket, moving, Matrix4x4.Identity);
        Check(Vector3.Distance(Vector3.Transform(Vector3.Zero, moving * placed),
            new Vector3(12, 0, 0)) < .0001f, "socket frames align");
        var existingBodies = new[]
        {
            new AssemblyPlacementBody(cockpit.Id, Matrix4x4.Identity,
                new Vector3(2, 1, 2), 10)
        };
        Check(!AssemblyPlacementRules.Validate(
                new AssemblyPlacementBody(Guid.NewGuid(), Matrix4x4.Identity,
                    new Vector3(1, 1, 1), 2), existingBodies, 20, out _),
            "overlapping body is rejected");
        Check(!AssemblyPlacementRules.Validate(
                new AssemblyPlacementBody(Guid.NewGuid(), Matrix4x4.CreateTranslation(3, 0, 0),
                    new Vector3(1, 1, 1), 12), existingBodies, 20, out _),
            "overweight body is rejected");
        Check(AssemblyPlacementRules.Validate(
                new AssemblyPlacementBody(Guid.NewGuid(), Matrix4x4.CreateTranslation(3, 0, 0),
                    new Vector3(1, 1, 1), 2), existingBodies, 20, out _),
            "free, in-budget body may be placed");

        var field = new SwarmFlowField(16, 16, 1, Vector2.Zero);
        field.SetBlocked(2, 1, true);
        field.Rebuild(8, 1);
        Check(field.Sample(new Vector2(1.5f, 1.5f)) != Vector2.UnitX,
            "flow route avoids obstacle");
        var agents = new Vector2[500];
        Array.Fill(agents, new Vector2(1.5f, 1.5f));
        field.Step(agents, 2, .1f);
        Check(agents.All(position => position != new Vector2(1.5f, 1.5f)),
            "500 agents consume one field");
        var horde = new SwarmHorde(500);
        SwarmHandle first = horde.Spawn(new Vector2(1, 1), 1);
        for (int i = 1; i < 500; i++) horde.Spawn(new Vector2(10 + i, 10), 1);
        Check(horde.Count == 500, "500 pooled units spawned");
        int dropped = 0;
        horde.Squished += _ => dropped++;
        Check(horde.Squish(Vector2.Zero, new Vector2(2, 2), .5f, 1, 2, 1) == 0,
            "slow contact does not squish");
        Check(horde.Squish(Vector2.Zero, new Vector2(2, 2), .5f, 3, 2, 1) == 1 &&
              dropped == 1 && !horde.TryGet(first, out _), "swept impact kills one enemy");
        SwarmHandle replacement = horde.Spawn(new Vector2(1, 1), 1);
        Check(replacement.Slot == first.Slot && replacement.Generation != first.Generation,
            "pooled slot invalidates stale handle");
        var duel = new SwarmHorde(2);
        duel.Spawn(new Vector2(0, 0), 0);
        duel.Spawn(new Vector2(.1f, 0), 1);
        Check(duel.ResolveClashes(.2f, 1) == 1 && duel.Count == 0,
            "opposing factions clash through shared spatial cells");
        var particles = new ImpactParticles3D();
        particles.Emit(Vector3.Zero, 1200);
        Check(particles.ActiveCount == 1024, "impact debris uses a fixed particle pool");
        particles.Advance(2);
        Check(particles.ActiveCount == 0, "expired impact debris slots return to pool");
        Console.WriteLine("Construction graph, integrity boundary, placement, and 500-agent swarm checks passed.");
    }

    private sealed class FakeAdapter : IAssemblyConstraintAdapter
    {
        public int Created, Destroyed;
        public readonly HashSet<Guid> Frozen = new();
        public readonly Dictionary<Guid, AssemblyLoad> Loads = new();
        public void Create(AssemblyLink link) => Created++;
        public void Destroy(Guid linkId) => Destroyed++;
        public void SetBuildingMode(Guid partId, bool frozen)
        {
            if (frozen) Frozen.Add(partId);
            else Frozen.Remove(partId);
        }
        public AssemblyLoad Measure(Guid linkId) =>
            Loads.TryGetValue(linkId, out AssemblyLoad load) ? load : default;
    }
}
