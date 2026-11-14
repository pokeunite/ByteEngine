using System.Numerics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Gameplay.Hydraulics;

/// <summary>A commanded pin. The driving mechanism respects Latched, so holding never fights another transform writer.</summary>
public sealed class MechanicalLatch3D : Component
{
    public string CommandVariable { get; set; } = "";
    public bool CanEngage { get; set; }
    public string PinName { get; set; } = "";
    public Vector3 PinTravel { get; set; } = new(0, 0, .6f);
    public float PinSpeed { get; set; } = 4;
    public bool Latched { get; private set; }
    public float Engagement { get; private set; }
    GameObject? pin;
    Vector3 rest;
    protected override void OnStart() { pin = GameObject.Scene!.FindGameObject(PinName); rest = pin?.Transform.LocalPosition ?? Vector3.Zero; }
    protected override void OnFixedUpdate()
    {
        bool command = GameObject.Scene!.Variables.TryGet(CommandVariable, out var value) && value!.Boolean;
        if (!command) Latched = false;
        else if (CanEngage) Latched = true;
        Engagement += Math.Clamp((Latched ? 1 : 0) - Engagement, -Math.Max(0, PinSpeed) * (float)Time.FixedDeltaTime, Math.Max(0, PinSpeed) * (float)Time.FixedDeltaTime);
        if (pin != null) pin.Transform.LocalPosition = rest + PinTravel * Engagement;
    }
}

/// <summary>Hysteretic siphon controller. Its WaterGate performs all conserved transfer; no water is created here.</summary>
public sealed class PrimedSiphon3D : Component
{
    public string SourceBasin { get; set; } = "";
    public string FlowGate { get; set; } = "";
    public float PrimeHeight { get; set; } = 5;
    public float AirBreakHeight { get; set; } = 1;
    public bool Primed { get; private set; }
    public int PrimeCount { get; private set; }
    public float SourceLevel { get; private set; }
    protected override void OnFixedUpdate()
    {
        var scene = GameObject.Scene!;
        var basin = scene.FindGameObject(SourceBasin)?.GetComponent<WaterBasin3D>();
        var gate = scene.FindGameObject(FlowGate)?.GetComponent<WaterGate3D>();
        if (basin == null || gate == null) return;
        SourceLevel = basin.Level;
        float air = Math.Min(AirBreakHeight, PrimeHeight - .01f);
        if (Primed && SourceLevel <= air) Primed = false;
        else if (!Primed && SourceLevel >= PrimeHeight) { Primed = true; PrimeCount++; }
        gate.Opening = Primed ? 1 : 0;
    }
}

/// <summary>Damped water-ballast drawbridge with an editable alignment range and optional mechanical latch.</summary>
public sealed class WaterCounterweightBridge3D : Component
{
    public string BallastBasin { get; set; } = "";
    public float DryMass { get; set; } = 200;
    public float CounterweightMass { get; set; } = 2200;
    public float KilogramsPerDegree { get; set; } = 150;
    public float MinimumAngle { get; set; }
    public float MaximumAngle { get; set; } = 70;
    public float AlignmentAngle { get; set; } = 58;
    public float AlignmentTolerance { get; set; } = 4;
    public float Response { get; set; } = 2;
    public float MaximumDegreesPerSecond { get; set; } = 30;
    public Vector3 RotationAxis { get; set; } = Vector3.UnitZ;
    public float Angle { get; private set; }
    public float BallastMass { get; private set; }
    public bool IsAligned { get; private set; }
    Vector3 rest;
    protected override void OnStart() { rest = Transform.EulerAngles; }
    protected override void OnFixedUpdate()
    {
        var basin = GameObject.Scene!.FindGameObject(BallastBasin)?.GetComponent<WaterBasin3D>();
        if (basin == null) return;
        BallastMass = DryMass + (float)basin.Volume * 1000;
        if (GameObject.GetComponent<MechanicalLatch3D>() is not { Latched: true })
        {
            float target = Math.Clamp((BallastMass - CounterweightMass) / Math.Max(1, KilogramsPerDegree), MinimumAngle, Math.Max(MinimumAngle, MaximumAngle));
            float dt = (float)Time.FixedDeltaTime;
            float delta = (target - Angle) * (1 - MathF.Exp(-Math.Max(0, Response) * dt));
            Angle += Math.Clamp(delta, -Math.Max(0, MaximumDegreesPerSecond) * dt, Math.Max(0, MaximumDegreesPerSecond) * dt);
        }
        IsAligned = Math.Abs(Angle - AlignmentAngle) <= Math.Max(0, AlignmentTolerance);
        Transform.EulerAngles = rest + RotationAxis * Angle;
    }
}
