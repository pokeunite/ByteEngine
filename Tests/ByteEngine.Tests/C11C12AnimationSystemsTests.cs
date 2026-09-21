using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Tests;

internal static class C11C12AnimationSystemsTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        ProfileRoundTripAndLegacyDefaults();
        DirectionAndPlaybackMath();
        DirectionalClipFallbacks();
        StateHysteresis();
        NamedActionCacheAndVisualLogic();
        C10MilestoneTests.Run();
    }

    private static void ProfileRoundTripAndLegacyDefaults()
    {
        string path = Path.Combine(Path.GetTempPath(), $"c11-c12-{Guid.NewGuid():N}.byteanim");
        try
        {
            var profile = new AnimationProfile
            {
                Locomotion = new AnimationLocomotionProfile
                {
                    DirectionalMovement = true, WalkBackward = "WalkBack", RunRight = "RunRight",
                    MoveThreshold = .12f, StateHysteresis = .2f, MatchPlaybackToSpeed = true
                },
                Actions = { new AnimationActionProfile { Name="Attack1", Clip="Slash", PlaybackSpeed=1.25f,
                    Priority=10, Interruptible=false, NextAction="Attack2", ComboWindow="CanCombo" } }
            };
            AnimationProfileSerializer.Save(path, profile);
            AnimationProfile loaded = AnimationProfileSerializer.Load(path);
            Assert(loaded.Version == AnimationProfile.CurrentVersion && loaded.Locomotion.WalkBackward == "WalkBack" &&
                loaded.Locomotion.RunRight == "RunRight" && loaded.Actions[0].PlaybackSpeed == 1.25f &&
                loaded.Actions[0].Priority == 10 && !loaded.Actions[0].Interruptible &&
                loaded.Actions[0].NextAction == "Attack2" && loaded.Actions[0].ComboWindow == "CanCombo", "C11/C12 profile round-trip");

            var legacy = new AnimationProfile { Version = 2 };
            legacy.Normalize();
            Assert(!legacy.Locomotion.DirectionalMovement && !legacy.Locomotion.MatchPlaybackToSpeed &&
                legacy.Locomotion.MoveThreshold == .05f && legacy.Actions.Count == 0, "C11/C12 legacy defaults");
        }
        finally { try { File.Delete(path); } catch { } }
    }

    private static void DirectionAndPlaybackMath()
    {
        Vector3 forward = -Vector3.UnitZ;
        Vector3 right = Vector3.UnitX;
        Assert(AnimationController.ResolveDirection(LocomotionDirection.None, -Vector3.UnitZ, forward, right, .1f) == LocomotionDirection.Forward, "C12 forward");
        Assert(AnimationController.ResolveDirection(LocomotionDirection.None, Vector3.UnitZ, forward, right, .1f) == LocomotionDirection.Backward, "C12 backward");
        Assert(AnimationController.ResolveDirection(LocomotionDirection.None, -Vector3.UnitX, forward, right, .1f) == LocomotionDirection.Left, "C12 left");
        Assert(AnimationController.ResolveDirection(LocomotionDirection.None, Vector3.UnitX, forward, right, .1f) == LocomotionDirection.Right, "C12 right");
        Assert(AnimationController.ResolveDirection(LocomotionDirection.Forward, new Vector3(.51f,0,-.5f), forward, right, .1f) == LocomotionDirection.Forward, "C12 direction hysteresis");
        Assert(AnimationController.ResolvePlaybackRate(LocomotionState.Run, 10, false, 2,5,.7f,1.4f) == 1, "C12 disabled matching");
        Assert(Near(AnimationController.ResolvePlaybackRate(LocomotionState.Walk, 1, true,2,5,.7f,1.4f),.7f), "C12 minimum clamp");
        Assert(Near(AnimationController.ResolvePlaybackRate(LocomotionState.Run, 10, true,2,5,.7f,1.4f),1.4f), "C12 maximum clamp");
    }

    private static void DirectionalClipFallbacks()
    {
        var controller = new AnimationController
        {
            Walk = "BaseWalk",
            Run = "BaseRun",
            DirectionalMovement = true
        };
        MethodInfo selector = typeof(AnimationController).GetMethod(
            "GetClipName", BindingFlags.Instance | BindingFlags.NonPublic)!;

        foreach (LocomotionDirection direction in new[]
                 {
                     LocomotionDirection.Forward,
                     LocomotionDirection.Backward,
                     LocomotionDirection.Left,
                     LocomotionDirection.Right
                 })
        {
            Assert((string)selector.Invoke(controller, new object[] { LocomotionState.Walk, direction })! == "BaseWalk",
                $"C12 base Walk fallback for {direction}");
            Assert((string)selector.Invoke(controller, new object[] { LocomotionState.Run, direction })! == "BaseRun",
                $"C12 base Run fallback for {direction}");
        }

        controller.WalkBackward = "WalkBackward";
        Assert((string)selector.Invoke(controller, new object[] { LocomotionState.Walk, LocomotionDirection.Backward })! == "WalkBackward",
            "C12 individual Walk backward override");
        Assert((string)selector.Invoke(controller, new object[] { LocomotionState.Walk, LocomotionDirection.Forward })! == "BaseWalk" &&
               (string)selector.Invoke(controller, new object[] { LocomotionState.Walk, LocomotionDirection.Left })! == "BaseWalk" &&
               (string)selector.Invoke(controller, new object[] { LocomotionState.Walk, LocomotionDirection.Right })! == "BaseWalk",
            "C12 partial Walk overrides preserve per-direction fallback");

        controller.RunRight = "RunRight";
        Assert((string)selector.Invoke(controller, new object[] { LocomotionState.Run, LocomotionDirection.Right })! == "RunRight",
            "C12 individual Run right override");
        Assert((string)selector.Invoke(controller, new object[] { LocomotionState.Run, LocomotionDirection.Forward })! == "BaseRun" &&
               (string)selector.Invoke(controller, new object[] { LocomotionState.Run, LocomotionDirection.Backward })! == "BaseRun" &&
               (string)selector.Invoke(controller, new object[] { LocomotionState.Run, LocomotionDirection.Left })! == "BaseRun",
            "C12 partial Run overrides preserve per-direction fallback");
    }
    private static void StateHysteresis()
    {
        var controller = new CharacterController3D();
        Set(controller, "<IsGrounded>k__BackingField", true);
        Set(controller, "<Velocity>k__BackingField", new Vector3(0,0,4.05f));
        Assert(AnimationController.ResolveLocomotionStateStable(controller, LocomotionState.Walk, true, .05f,4,.15f) == LocomotionState.Walk, "C12 walk/run hysteresis");
        Set(controller, "<Velocity>k__BackingField", new Vector3(0,0,4.2f));
        Assert(AnimationController.ResolveLocomotionStateStable(controller, LocomotionState.Walk, true, .05f,4,.15f) == LocomotionState.Run, "C12 run entry");
        Set(controller, "<Velocity>k__BackingField", Vector3.Zero);
        Assert(AnimationController.ResolveLocomotionStateStable(controller, LocomotionState.Walk, true, .05f,4,.15f) == LocomotionState.Idle, "C12 idle threshold");
    }

    private static void NamedActionCacheAndVisualLogic()
    {
        VisualLogicRegistry registry = VisualLogicRegistry.CreateDefault();
        foreach (string id in new[] { "animation.playNamedAction", "animation.queueNamedAction", "animation.queueCombo", "animation.cancelAction" })
            Assert(registry.TryGetAction(id, out _), $"C11 action registration {id}");
        foreach (string id in new[] { "animation.actionPlaying", "animation.currentActionIs" })
            Assert(registry.TryGetCondition(id, out _), $"C11 condition registration {id}");
    }

    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    private static bool Near(float a,float b) => Math.Abs(a-b)<.0001f;
    private static void Assert(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
}