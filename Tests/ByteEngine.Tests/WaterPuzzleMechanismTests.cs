using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Gameplay.Hydraulics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;

namespace ByteEngine.Tests;
internal static class WaterPuzzleMechanismTests
{
    public static void Run()
    {
        var scene = new Scene("Reusable hydraulic mechanisms") { FixedSimulation = true };
        scene.Variables.Set("Pin", VariableValue.FromBoolean(true));
        var source = scene.CreateGameObject("Source").AddComponent(new WaterBasin3D { MinX = 0, MaxX = 2, Depth = 2, Floor = 1, InitialLevel = 5, Rim = 6 });
        var receiver = scene.CreateGameObject("Receiver").AddComponent(new WaterBasin3D { MinX = 5, MaxX = 15, Depth = 5, Floor = 0, InitialLevel = 0, Rim = 6 });
        var flow = scene.CreateGameObject("Flow").AddComponent(new WaterGate3D { Source = "Source", Destination = "Receiver", Conductance = .6f, OneWay = true, Overflow = true, CrestHeight = 1.5f });
        var siphon = scene.CreateGameObject("Siphon").AddComponent(new PrimedSiphon3D { SourceBasin = "Source", FlowGate = "Flow", PrimeHeight = 4.5f, AirBreakHeight = 1.6f });
        var network = scene.CreateGameObject("Network").AddComponent(new WaterNetwork3D());
        var deck = scene.CreateGameObject("Deck");
        var bridge = deck.AddComponent(new WaterCounterweightBridge3D { BallastBasin = "Receiver", CounterweightMass = 1000, KilogramsPerDegree = 100, AlignmentAngle = 58 });
        var latch = deck.AddComponent(new MechanicalLatch3D { CommandVariable = "Pin" });
        scene.LoadInternal();
        void Tick(int count) { for (int i = 0; i < count; i++) { Time.Update(1d / 60); scene.UpdateInternal(); } }
        void Require(bool ok, string message) { if (!ok) throw new Exception(message); }
        try
        {
            double total = network.TotalVolume;
            Tick(30); Require(siphon.Primed && !latch.Latched, "Priming or latch eligibility failed");
            Tick(150); Require(source.Level < 4.5f && siphon.Primed, "Siphon stopped below its prime line instead of retaining prime");
            Tick(1800); Require(!siphon.Primed && siphon.PrimeCount == 1, "Air break did not release prime exactly once");
            Require(Math.Abs(network.TotalVolume - total) < .0001 && receiver.Volume > 10, "Siphon failed conserved water transfer");
            latch.CanEngage = true; Tick(2); Require(latch.Latched, "Eligible latch did not engage");
            float held = bridge.Angle; receiver.Volume = 0; Tick(120);
            Require(Math.Abs(bridge.Angle - held) < .0001, "Latched bridge moved when ballast drained");
            scene.Variables["Pin"].Boolean = false; Tick(180);
            Require(!latch.Latched && bridge.Angle < held - 1, "Unlatched bridge failed to respond to drained ballast");
            Console.WriteLine("PASS reusable water systems: priming hysteresis, air break, conserved transfer, latch eligibility, ballast hold and release");
        }
        finally { scene.UnloadInternal(); }
    }
}
