using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
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
        NamedActionRequestRules();
        NamedActionChainRules();
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

    private static void NamedActionRequestRules()
    {
        Assert(AnimationController.ResolveNamedActionRequest(true, false, false, false, 100, 0, false) == NamedActionRequestDecision.KeepCurrent,
            "C11 same action without retrigger remains active");
        Assert(AnimationController.ResolveNamedActionRequest(true, true, false, false, 100, 0, false) == NamedActionRequestDecision.RestartSame,
            "C11 same action retriggers even when non-interruptible");
        Assert(AnimationController.ResolveNamedActionRequest(false, false, false, true, 20, 10, false) == NamedActionRequestDecision.Reject,
            "C11 lower-priority unrelated action is blocked");
        Assert(AnimationController.ResolveNamedActionRequest(false, false, false, true, 20, 20, false) == NamedActionRequestDecision.InterruptCurrent &&
               AnimationController.ResolveNamedActionRequest(false, false, false, true, 20, 30, false) == NamedActionRequestDecision.InterruptCurrent,
            "C11 equal/higher priority interrupts an interruptible action");
        Assert(AnimationController.ResolveNamedActionRequest(false, false, true, false, 100, 0, false) == NamedActionRequestDecision.InterruptCurrent,
            "C11 force interrupt overrides priority and interruptibility");
        Assert(AnimationController.ResolveNamedActionRequest(false, false, false, false, 20, 30, true) == NamedActionRequestDecision.QueueRequested,
            "C11 blocked queueIfBlocked stores the pending request");
    }
    private static void NamedActionChainRules()
    {
        var actor = new GameObject("Action Test");
        AnimationController controller = actor.AddComponent(new AnimationController { DriveLocomotion = false });
        var actions = (Dictionary<string, AnimationActionProfile>)typeof(AnimationController)
            .GetField("_namedActions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller)!;
        actions["Attack1"] = new AnimationActionProfile { Name="Attack1", Clip="A1", NextAction="Attack2", ComboWindow="CanCombo" };
        actions["Attack2"] = new AnimationActionProfile { Name="Attack2", Clip="A2", NextAction="Attack3" };
        actions["Attack3"] = new AnimationActionProfile { Name="Attack3", Clip="A3" };
        actions["Dodge"] = new AnimationActionProfile { Name="Dodge", Clip="Dodge", Priority=50, Interruptible=false };

        Assert(controller.IsActionInChain("Attack1", "Attack1"), "C11 trigger chain includes starting action");
        Assert(controller.IsActionInChain("Attack1", "Attack2"), "C11 trigger chain includes second action");
        Assert(controller.IsActionInChain("Attack1", "Attack3"), "C11 trigger chain includes third action");
        Assert(!controller.IsActionInChain("Attack1", "Dodge"), "C11 trigger rejects unrelated active action");

        actions["Attack3"].NextAction = "Attack1";
        Assert(!controller.IsActionInChain("Attack1", "Missing"), "C11 malformed combo cycle terminates safely");

        Set(controller, "_actionActive", true);
        Set(controller, "_currentAction", "Attack1");
        Set(controller, "_currentNamedAction", actions["Attack1"]);
        Assert(!controller.TriggerAction("Attack1") && controller.QueuedAction == string.Empty,
            "C11 trigger outside CanCombo does not queue");

        Guid comboWindowId = Guid.NewGuid();
        var comboWindow = new AnimationWindow { Id=comboWindowId, Name="CanCombo", StartTime=.1f, EndTime=.4f };
        Set(controller, "_animationWindowSnapshot", AnimationWindowCrossing.BuildSnapshot(new[] { comboWindow }, 1f));
        ((HashSet<Guid>)typeof(AnimationController).GetField("_activeAnimationWindows", BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(controller)!).Add(comboWindowId);
        Assert(controller.TriggerAction("Attack1") && controller.QueuedAction == "Attack2",
            "C11 trigger inside CanCombo queues Attack2");

        Set(controller, "_currentAction", "Attack2");
        Set(controller, "_currentNamedAction", actions["Attack2"]);
        Assert(controller.TriggerAction("Attack1") && controller.QueuedAction == "Attack3",
            "C11 trigger advances a three-action combo chain");

        Set(controller, "_currentAction", "Dodge");
        Set(controller, "_currentNamedAction", actions["Dodge"]);
        Assert(!controller.TriggerAction("Attack1") && controller.QueuedAction == "Attack3",
            "C11 trigger does not interrupt an unrelated action");

        Set(controller, "_currentAction", "Attack1");
        Set(controller, "_currentNamedAction", actions["Attack1"]);
        controller.QueueNamedAction("Attack2");
        controller.PlayNamedAction("Dodge", forceInterrupt: true);
        Assert(controller.QueuedAction == string.Empty,
            "C11 immediate force interrupt clears a stale queued action");
        Set(controller, "_actionActive", false);
        Set(controller, "_currentAction", string.Empty);
        Set(controller, "_currentNamedAction", null!);
        Assert(controller.QueueNamedAction("Attack2") && controller.QueuedAction == "Attack2", "C11 one pending named action");
        Assert(controller.QueueNamedAction("Attack3") && controller.QueuedAction == "Attack3", "C11 pending queue is bounded to one replacement");
        controller.CancelCurrentAction();
        Assert(controller.QueuedAction == string.Empty, "C11 cancel clears queued action");

        VisualLogicRegistry registry = VisualLogicRegistry.CreateDefault();
        Assert(registry.TryGetAction("animation.triggerAction", out VisualActionDefinition? trigger) && trigger != null &&
               trigger.DisplayName == "Trigger Action / Combo", "C11 Trigger Action / Combo registration");
    }
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(target,value);
    private static bool Near(float a,float b) => Math.Abs(a-b)<.0001f;
    private static void Assert(bool condition,string message) { if(!condition) throw new InvalidOperationException(message); }
}