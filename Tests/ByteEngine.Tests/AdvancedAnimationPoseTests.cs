using System.Numerics;
using System.Runtime.CompilerServices;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Diagnostics;

namespace ByteEngine.Tests;

internal static class AdvancedAnimationPoseTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        BlendSpaces();
        PoseAndMasks();
        AimAndIk();
        Synchronization();
        Graph();
        Profiles();
        FootIkTrace();
        FootContact();
        ContactTransitions();
        Console.WriteLine("Advanced animation pose regressions passed.");
    }

    private static void FootIkTrace()
    {
        RuntimeDiagnostics.ClearFootIkTrace();
        RuntimeDiagnostics.DebugFootIk = false;
        RuntimeDiagnostics.RecordFootIk("disabled");
        Check(RuntimeDiagnostics.GetFootIkTrace().Length == 0, "foot IK trace opt-in");
        RuntimeDiagnostics.DebugFootIk = true;
        for (int i = 0; i < 605; i++) RuntimeDiagnostics.RecordFootIk($"sample={i}");
        string trace = RuntimeDiagnostics.GetFootIkTrace();
        Check(!trace.Contains("sample=0") && trace.Contains("sample=604"),
            "foot IK trace keeps latest samples");
        Check(trace.Split(Environment.NewLine).Length == 600, "foot IK trace bounded");
        RuntimeDiagnostics.DebugFootIk = false;
        RuntimeDiagnostics.ClearFootIkTrace();
    }

    private static void FootContact()
    {
        Check(Near(AnimationPoseMath.FootMotionWeight(0f), 1f),
            "stationary foot grounding remains active");
        Check(AnimationPoseMath.FootMotionWeight(.8f) is > 0f and < 1f,
            "foot grounding fades smoothly with movement");
        Check(Near(AnimationPoseMath.FootMotionWeight(3f), 0f),
            "running legs are not forced onto ground contacts");
        Check(AnimationPoseMath.FootContactWeight(.10f, 0f, .85f) > .1f,
            "near-ground stance foot keeps IK");
        Check(Near(AnimationPoseMath.FootContactWeight(.25f, .15f, .85f), 0f),
            "raised swing foot releases IK");
        Check(Near(AnimationPoseMath.FootContactWeight(0f, 0f, .85f), 1f),
            "planted foot receives full IK");
        Check(AnimationPoseMath.FootContactWeight(.014f, .228f, .85f) > .9f,
            "lower foot remains planted beside a .23m step");
        Check(!AnimationPoseMath.IsUsableFootGroundHit(0f, Vector3.UnitY,
            -.85f, -.475f, .75f), "inside-collider ray rejected");
        Check(!AnimationPoseMath.IsUsableFootGroundHit(.2f, Vector3.UnitX,
            -.85f, -.975f, .75f), "side normal rejected");
        Check(!AnimationPoseMath.IsUsableFootGroundHit(.2f, Vector3.UnitY,
            -.85f, -.25f, .75f), "excessively high step rejected");
        Check(AnimationPoseMath.IsUsableFootGroundHit(.375f, Vector3.UnitY,
            -.85f, -.475f, .75f), "Cube-height surface accepted");
        Check(AnimationPoseMath.IsUsableFootGroundHit(.5f, Vector3.UnitY,
            -.85f, -.975f, .75f), "walkable ground accepted");
        Check(Near(AnimationPoseMath.FootSoleOffset(new Vector3(0f, .10f, 0f),
            new Vector3(0f, -.06f, 0f), Vector3.UnitY, 0f), .175f),
            "ankle target includes toe-to-sole clearance");
        Check(Near(Quaternion.Dot(AnimationPoseMath.FromTo(Vector3.UnitY, Vector3.UnitY),
            Quaternion.Identity), 1f), "flat-ground foot tilt preserves animated orientation");
    }

    private static void ContactTransitions()
    {
        float blendAlpha = AnimationPoseMath.ExponentialAlpha(20f, .016f);
        Check(blendAlpha > 0f && blendAlpha < 1f, "blend input transitions over multiple frames");
        var foot = new FootContactState();
        AnimationPoseMath.AdvanceFootContact(ref foot, true, Vector3.Zero, Vector3.UnitY,
            1f, new Vector3(0f, .75f, 0f), .85f, .016f);
        Check(foot.Locked && Near(foot.Weight, 1f), "initial planted foot locks");
        AnimationPoseMath.AdvanceFootContact(ref foot, true, new Vector3(.1f, 0f, 0f),
            Vector3.UnitY, 1f, new Vector3(.1f, .75f, 0f), .85f, .016f);
        Check(foot.Locked && Near(foot.Target.X, 0f), "planted foot stays in world space");
        AnimationPoseMath.AdvanceFootContact(ref foot, true, new Vector3(.1f, 0f, 0f),
            Vector3.UnitY, 0f, new Vector3(.1f, .75f, 0f), .85f, .016f);
        Check(!foot.Locked && foot.Weight > 0f && foot.Weight < 1f,
            "foot releases smoothly on swing");
        for (int i = 0; i < 30; i++)
            AnimationPoseMath.AdvanceFootContact(ref foot, true, new Vector3(.1f, 0f, 0f),
                Vector3.UnitY, 0f, new Vector3(.1f, .75f, 0f), .85f, .016f);
        AnimationPoseMath.AdvanceFootContact(ref foot, true, new Vector3(.2f, 0f, 0f),
            Vector3.UnitY, 1f, new Vector3(.2f, .75f, 0f), .85f, .016f);
        Check(foot.Locked && Near(foot.Anchor.X, .2f) && foot.Weight < 1f,
            "new stance replants without snapping weight");
        AnimationPoseMath.AdvanceFootContact(ref foot, true, new Vector3(.3f, 0f, 0f),
            Vector3.UnitY, 1f, new Vector3(.2f, .75f, 0f), .85f, .016f, true);
        Check(foot.Locked && Near(foot.Anchor.X, .3f), "new ground surface releases old anchor");
        AnimationPoseMath.AdvanceFootContact(ref foot, true, new Vector3(.65f, 0f, 0f),
            Vector3.UnitY, 1f, new Vector3(.65f, .75f, 0f), .85f, .016f);
        Check(!foot.Locked && foot.AwaitLift, "overextended plant releases instead of stretching leg");
        AnimationPoseMath.AdvanceFootContact(ref foot, true, new Vector3(.65f, 0f, 0f),
            Vector3.UnitY, 1f, new Vector3(.65f, .75f, 0f), .85f, .016f);
        Check(!foot.Locked, "released plant does not re-lock before swing");
        AnimationPoseMath.AdvanceFootContact(ref foot, true, new Vector3(.65f, 0f, 0f),
            Vector3.UnitY, 0f, new Vector3(.65f, .75f, 0f), .85f, .016f);
        for (int i = 0; i < 30; i++)
            AnimationPoseMath.AdvanceFootContact(ref foot, false, Vector3.Zero, Vector3.UnitY,
                0f, Vector3.Zero, .85f, .016f);
        Check(!foot.HasTarget && foot.Weight < .005f, "lost ground fades and clears foot target");
        Vector3 pole = AnimationPoseMath.SmoothDirection(Vector3.UnitZ, -Vector3.UnitZ, .016f, 14f);
        Check(Finite(pole) && Vector3.Dot(pole, Vector3.UnitZ) > 0f,
            "opposed knee hints turn gradually without a pole flip");
    }

    private static void BlendSpaces()
    {
        var one = new AnimationBlendSpace { Samples =
        {
            new() { Clip = "Idle", X = 0f },
            new() { Clip = "Walk", X = 2f },
            new() { Clip = "Run", X = 5f }
        }};
        Span<float> weights = stackalloc float[3];
        AnimationBlendWeights.Evaluate(one, 2f, 0f, weights);
        Check(Near(weights[1], 1f), "1D exact sample");
        AnimationBlendWeights.Evaluate(one, 3.5f, 0f, weights);
        Check(Near(weights[1], .5f) && Near(weights[2], .5f), "1D midpoint");
        AnimationBlendWeights.Evaluate(one, -10f, 0f, weights);
        Check(Near(weights[0], 1f), "1D lower edge");
        AnimationBlendWeights.Evaluate(one, 100f, 0f, weights);
        Check(Near(weights[2], 1f), "1D upper edge");

        var two = new AnimationBlendSpace { TwoDimensional = true, Samples =
        {
            new() { Clip = "Left", X = -1f, Y = 0f },
            new() { Clip = "Right", X = 1f, Y = 0f },
            new() { Clip = "Forward", X = 0f, Y = 1f },
            new() { Clip = "Backward", X = 0f, Y = -1f }
        }};
        Span<float> four = stackalloc float[4];
        AnimationBlendWeights.Evaluate(two, 1f, 0f, four);
        Check(Near(four[1], 1f), "2D exact sample");
        AnimationBlendWeights.Evaluate(two, 0f, 0f, four);
        Check(Near(four.ToArray().Sum(), 1f) &&
            four.ToArray().Count(value => value > 1e-5f) <= 3, "2D local triangle weights");
        AnimationBlendWeights.Evaluate(two, .3f, .8f, four);
        Check(Near(four.ToArray().Sum(), 1f) && four.ToArray().All(value => value >= 0f), "2D normalized");

        var grounded = new AnimationBlendSpace { TwoDimensional = true, Samples =
        {
            new() { Clip = "Idle", X = 0f, Y = 0f },
            new() { Clip = "Walk", X = 0f, Y = 2f },
            new() { Clip = "Back", X = 0f, Y = -2f },
            new() { Clip = "Left", X = -2f, Y = 0f },
            new() { Clip = "Right", X = 2f, Y = 0f },
            new() { Clip = "Run", X = 0f, Y = 5f }
        }};
        Span<float> local = stackalloc float[6];
        AnimationBlendWeights.Evaluate(grounded, 0f, 3.5f, local);
        Check(Near(local[1], .5f) && Near(local[5], .5f) &&
            local[0] == 0f && local[2] == 0f && local[3] == 0f && local[4] == 0f,
            "forward walk/run blend excludes idle, backward and strafe clips");
        AnimationBlendWeights.Evaluate(grounded, 0f, .5f, local);
        Check(Near(local[0], .75f) && Near(local[1], .25f) &&
            local[2] == 0f && local[3] == 0f && local[4] == 0f,
            "idle/walk interpolation stays on local graph edge");
        float cycleScale = AnimationBlendWeights.PlaybackScale(1.37f,
            new float[] { .5f, .5f }, new float[] { .83f, .53f });
        Check(Near(cycleScale, 1.37f / .68f),
            "Blend Space clock follows weighted walk/run cycle instead of Idle duration");
    }

    private static void PoseAndMasks()
    {
        var basis = new AnimationPose(2);
        var sample = new AnimationPose(2);
        var reference = new AnimationPose(2);
        AnimationBoneTransform identity = new(Vector3.Zero, Quaternion.Identity, Vector3.One);
        basis[0] = basis[1] = reference[0] = reference[1] = identity;
        sample[0] = sample[1] = new AnimationBoneTransform(new Vector3(2, 0, 0),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI), new Vector3(2));
        basis.Blend(sample, .5f, new float[] { 1f, 0f });
        Check(Near(basis[0].Position.X, 1f) && Near(basis[0].Scale.X, 1.5f),
            "masked pose translation and scale");
        Check(Near(basis[1].Position.X, 0f) && Near(basis[1].Scale.X, 1f),
            "excluded mask bone unchanged");
        Check(Near(MathF.Abs(basis[0].Rotation.Y), .7071068f, .01f), "pose rotation interpolation");
        basis.Additive(sample, reference, 0f);
        Check(Near(basis[0].Position.X, 1f), "additive zero weight");
        basis.Additive(sample, reference, 1f, new float[] { 1f, 0f });
        Check(Near(basis[0].Position.X, 3f) && Near(basis[0].Scale.X, 3f),
            "additive local delta");
        Check(Near(basis[1].Position.X, 0f), "masked additive exclusion");
    }

    private static void AimAndIk()
    {
        Check(Near(Quaternion.Dot(AnimationPoseMath.ClampAim(200f, 200f, 45f, 30f, 0f),
            Quaternion.Identity), 1f), "zero-weight aim");
        Quaternion limited = AnimationPoseMath.ClampAim(200f, 200f, 45f, 30f, 1f);
        Quaternion expected = Quaternion.CreateFromYawPitchRoll(MathF.PI / 4f, MathF.PI / 6f, 0f);
        Check(Near(MathF.Abs(Quaternion.Dot(limited, expected)), 1f), "aim clamps");
        Check(Finite(limited), "aim quaternion finite");
        Vector3 root = Vector3.Zero, middle = Vector3.UnitY, end = new(0, 2, 0);
        Check(AnimationPoseMath.SolveTwoBone(root, middle, end, new(1, 1, 0), Vector3.UnitZ,
            out Vector3 solvedMid, out Vector3 solvedEnd) &&
            Vector3.Distance(solvedEnd, new(1, 1, 0)) < .001f && Finite(solvedMid),
            "reachable two-bone IK");
        Check(AnimationPoseMath.SolveTwoBone(root, middle, end, new(0, 10, 0), Vector3.Zero,
            out solvedMid, out solvedEnd) && Finite(solvedMid) && Finite(solvedEnd) &&
            Vector3.Distance(root, solvedEnd) <= 2f, "unreachable and degenerate pole stable");
        Check(!AnimationPoseMath.SolveTwoBone(root, root, end, end, Vector3.UnitX,
            out _, out _), "zero-length IK chain rejected");
        Vector3 knee = new(.15f, 1f, -.25f);
        Check(AnimationPoseMath.SolveTwoBone(new(0f, 2f, 0f), knee, Vector3.Zero,
            new(0f, .05f, 0f), knee, out solvedMid, out solvedEnd) &&
            solvedMid.Z < 0f && Finite(solvedMid), "foot IK preserves animated knee bend side");
        Vector3 stepHip = new(-.044f, .892f, -.092f);
        Vector3 stepKnee = new(-.001f, .524f, -.306f);
        Vector3 stepAnkle = new(-.072f, .114f, -.371f);
        Vector3 stepTarget = new(-.072f, .473f, -.371f);
        Vector3 stepToe = stepAnkle + new Vector3(0f, -.10f, .16f);
        Vector3 stablePole = AnimationPoseMath.FootKneePole(
            stepHip, stepKnee, stepAnkle, stepToe, stepTarget);
        Check(AnimationPoseMath.SolveTwoBone(stepHip, stepKnee, stepAnkle,
            stepTarget, stablePole, out solvedMid, out solvedEnd) &&
            MathF.Abs(solvedMid.X - stepHip.X) < .12f &&
            solvedMid.Z > stepHip.Z + .10f,
            "step knee bends toward toes rather than sideways");
    }

    private static void Synchronization()
    {
        var source = new List<AnimationSyncMarker>
        {
            new() { Name = "LeftFoot", NormalizedTime = 0f },
            new() { Name = "RightFoot", NormalizedTime = .5f }
        };
        var target = new List<AnimationSyncMarker>
        {
            new() { Name = "LeftFoot", NormalizedTime = .1f },
            new() { Name = "RightFoot", NormalizedTime = .6f }
        };
        Check(Near(AnimationSyncMath.MapPhase(.25f, source, target), .35f), "named gait marker phase");
        Check(Near(AnimationSyncMath.MapPhase(.25f, null, target), .25f), "normalized sync fallback");
    }

    private static void Graph()
    {
        var graph = new AnimationStateGraph { Enabled = true, EntryState = "Idle",
            Parameters =
            {
                new() { Name = "Speed", Kind = AnimationParameterKind.Float },
                new() { Name = "Grounded", Kind = AnimationParameterKind.Bool, BoolDefault = true },
                new() { Name = "Land", Kind = AnimationParameterKind.Trigger }
            },
            States =
            {
                new() { Name = "Idle", Source = "Idle" },
                new() { Name = "Move", SourceKind = AnimationPoseSourceKind.BlendSpace2D, Source = "Movement" },
                new() { Name = "Fall", Source = "Fall" },
                new() { Name = "Landing", Source = "Land" }
            },
            Transitions =
            {
                new() { From = "Idle", To = "Move", Duration = .2f,
                    Conditions = { new() { Parameter = "Speed", Comparison = AnimationComparison.Greater, FloatValue = .1f } } },
                new() { From = "Move", To = "Fall",
                    Conditions = { new() { Parameter = "Grounded", BoolValue = false } } },
                new() { From = "Fall", To = "Landing",
                    Conditions = { new() { Parameter = "Land" } } }
            }
        };
        var runtime = new AnimationStateRuntime(graph);
        Check(runtime.CurrentState == "Idle" && !runtime.Step(.1f, 0f), "graph entry/float false");
        Check(runtime.SetFloat("Speed", 1f) && runtime.Step(.1f, 0f) &&
            runtime.CurrentState == "Move" && Near(runtime.TransitionDuration, .2f), "float transition");
        runtime.Step(.1f, 0f);
        Check(Near(runtime.BlendAlpha, .5f), "transition blending");
        Check(runtime.SetBool("Grounded", false) && runtime.Step(.1f, 0f) &&
            runtime.CurrentState == "Fall", "bool transition");
        Check(runtime.Trigger("Land") && runtime.Step(.1f, 0f) &&
            runtime.CurrentState == "Landing" && !runtime.Step(.1f, 0f),
            "trigger consumption");
    }

    private static void Profiles()
    {
        string path = Path.Combine(Path.GetTempPath(), "byte-advanced-" + Guid.NewGuid().ToString("N") + ".byteanim");
        try
        {
            var profile = new AnimationProfile();
            profile.BlendSpaces.Add(new AnimationBlendSpace { Name = "Movement", TwoDimensional = true,
                MinX = -7f, MaxX = 7f, MinY = -6f, MaxY = 6f,
                Samples = { new AnimationBlendSample { Clip = "Run", X = 1f } } });
            profile.Locomotion.BlendSpace = "Movement";
            profile.Layers.Add(new AnimationLayerProfile { Name = "Rifle", Clip = "Aim",
                Mask = new AnimationBoneMask { Kind = AnimationBoneMaskKind.UpperBody, RootBone = "Spine" } });
            profile.SyncGroups.Add(new AnimationSyncGroup { Name = "Gait",
                Clips = { "Walk", "Run" }, Markers =
                {
                    ["Walk"] = new List<AnimationSyncMarker> { new() { Name = "LeftFoot", NormalizedTime = 0f } }
                }});
            profile.StateGraph.Enabled = true;
            profile.StateGraph.States.Add(new AnimationGraphState { Name = "Move", Source = "Movement",
                SourceKind = AnimationPoseSourceKind.BlendSpace2D, EditorPosition = new Vector2(135f, 205f) });
            profile.Procedural.IkChains.Add(new AnimationIkChainProfile { Name = "Hand",
                RootBone = "UpperArm", MidBone = "LowerArm", EndBone = "Hand" });
            AnimationProfileSerializer.Save(path, profile);
            AnimationProfile loaded = AnimationProfileSerializer.Load(path);
            Check(loaded.Version == AnimationProfile.CurrentVersion &&
                loaded.BlendSpaces[0].Samples[0].Clip == "Run" &&
                Near(loaded.BlendSpaces[0].MinX, -7f) && Near(loaded.BlendSpaces[0].MaxY, 6f) &&
                loaded.Locomotion.BlendSpace == "Movement" &&
                loaded.Layers[0].Mask.RootBone == "Spine" &&
                loaded.SyncGroups[0].Markers["Walk"][0].Name == "LeftFoot" &&
                loaded.StateGraph.States[0].Source == "Movement" &&
                loaded.StateGraph.States[0].EditorPosition == new Vector2(135f, 205f) &&
                loaded.Procedural.IkChains[0].EndBone == "Hand" &&
                loaded.Procedural.IkChains[0].PoleOffset == Vector3.UnitZ,
                "new profile sections round-trip");
            File.WriteAllText(path, "{\"Version\":2,\"Name\":\"Legacy\"}");
            AnimationProfile old = AnimationProfileSerializer.Load(path);
            Check(old.Version == AnimationProfile.CurrentVersion && old.BlendSpaces.Count == 0 &&
                old.Layers.Count == 0 && old.StateGraph.States.Count == 0, "legacy profile loads");
        }
        finally { try { File.Delete(path); } catch { } }
    }

    private static bool Near(float a, float b, float tolerance = .001f) => MathF.Abs(a - b) < tolerance;
    private static bool Finite(Vector3 value) => float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
    private static bool Finite(Quaternion value) => float.IsFinite(value.X) && float.IsFinite(value.Y) &&
        float.IsFinite(value.Z) && float.IsFinite(value.W);
    private static void Check(bool valid, string name)
    {
        if (!valid) throw new InvalidOperationException("Advanced animation: " + name);
    }
}
