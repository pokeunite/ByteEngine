using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Tests;

internal static class EventModuleTimerTests
{
    public static void Run()
    {
        var registry = VisualLogicRegistry.CreateDefault();
        foreach (string id in new[] { "time.timerFinished", "time.timerRunning" })
            Check(registry.TryGetCondition(id, out var c) && c!.Category == "Time", id + " registered");
        foreach (string id in new[] { "time.startTimer", "time.stopTimer" })
            Check(registry.TryGetAction(id, out var a) && a!.Category == "Time", id + " registered");

        var scene = new Scene("Timer tests");
        GameObject self = scene.CreateGameObject("Owner");
        var globals = new VariableStore();
        var module = new EventModuleDefinition();
        var aRuntime = new EventModuleRuntime(registry);
        var bRuntime = new EventModuleRuntime(registry);
        void Step(EventModuleRuntime runtime, double dt)
        {
            Time.Update(dt);
            runtime.Update(module, globals, scene, self);
        }

        Check(!aRuntime.StartTimer("", 1) &&
            !aRuntime.StartTimer("Bad", double.NaN) &&
            !aRuntime.StartTimer("Bad", double.PositiveInfinity), "invalid timers rejected");
        Check(aRuntime.StartTimer(" Spawn ", .8) && aRuntime.IsTimerRunning("spawn") &&
            !aRuntime.IsTimerFinished("SPAWN"), "start and case-insensitive lookup");
        Check(aRuntime.StartTimer("Wave", 2), "second named timer");
        Check(bRuntime.StartTimer("Spawn", .2), "same name on separate runtime");
        Step(aRuntime, .3);
        Check(aRuntime.IsTimerRunning("SPAWN") && !aRuntime.IsTimerFinished("Spawn") &&
            aRuntime.IsTimerRunning("Wave"), "timer running before expiration");
        Check(bRuntime.IsTimerRunning("spawn") && !bRuntime.IsTimerFinished("spawn"),
            "runtime instances advance independently");
        Step(aRuntime, .5);
        Check(!aRuntime.IsTimerRunning("Spawn") && aRuntime.IsTimerFinished("Spawn") &&
            aRuntime.IsTimerRunning("Wave"), "crossing zero pulses exactly this update");
        Step(aRuntime, .01);
        Check(!aRuntime.IsTimerFinished("Spawn"), "finish pulse clears next update");
        Check(aRuntime.StartTimer("SPAWN", 1), "restart completed timer");
        Step(aRuntime, .6);
        Check(aRuntime.IsTimerRunning("spawn") && !aRuntime.IsTimerFinished("Spawn"),
            "restart uses new duration");
        aRuntime.StopTimer("spawn");
        Step(aRuntime, .5);
        Check(!aRuntime.IsTimerRunning("Spawn") && !aRuntime.IsTimerFinished("Spawn"),
            "stop prevents completion");
        Check(aRuntime.StartTimer("Zero", -1), "negative duration clamps to zero");
        Step(aRuntime, 0);
        Check(aRuntime.IsTimerFinished("Zero"), "zero duration completes next update");
        aRuntime.StopTimer("ZERO");
        Check(!aRuntime.IsTimerFinished("Zero"), "stop clears finish pulse");
        aRuntime.Reset();
        Check(!aRuntime.IsTimerRunning("Wave") && !aRuntime.IsTimerFinished("Zero"),
            "reset clears all timer state");
        Check(bRuntime.IsTimerRunning("Spawn"), "reset does not touch another runtime");

        int fired = 0;
        registry.RegisterAction(new VisualActionDefinition
        {
            Id = "test.count", Category = "Test", DisplayName = "Count",
            Execute = (_, _) => fired++
        });
        var start = new EventRuleDefinition();
        start.Conditions.Add(new VisualInstruction { Id = "system.always" });
        start.Actions.Add(Timer("time.startTimer", "Repeater", .4));
        var finish = new EventRuleDefinition();
        finish.Conditions.Add(Timer("time.timerFinished", "repeater"));
        finish.Actions.Add(new VisualInstruction { Id = "test.count" });
        finish.Actions.Add(Timer("time.startTimer", "REPEATER", .4));
        module.Rules.Add(start);
        module.Rules.Add(finish);
        Step(aRuntime, .1);
        Check(aRuntime.IsTimerRunning("Repeater") && fired == 0, "Event Sheet start action");
        start.Enabled = false;
        Step(aRuntime, .39);
        Check(fired == 0, "Event Sheet finish condition false early");
        Step(aRuntime, .01);
        Check(fired == 1 && aRuntime.IsTimerRunning("Repeater") &&
            aRuntime.IsTimerFinished("Repeater"),
            "finish condition fires and re-arms in same update");
        Step(aRuntime, .1);
        Check(fired == 1 && !aRuntime.IsTimerFinished("Repeater"), "no repeated finish pulse");
        Step(aRuntime, .3);
        Check(fired == 2, "timer cycles without Trigger Once");
        finish.Enabled = false;
        var stop = new EventRuleDefinition();
        stop.Conditions.Add(new VisualInstruction { Id = "system.always" });
        stop.Actions.Add(Timer("time.stopTimer", "repeater"));
        module.Rules.Add(stop);
        Step(aRuntime, .1);
        Check(!aRuntime.IsTimerRunning("Repeater") && !aRuntime.IsTimerFinished("Repeater"),
            "Event Sheet stop action clears timer");
        stop.Enabled = false;
        Step(aRuntime, .5);
        Check(fired == 2, "stopped timer cannot trigger later");
        aRuntime.Reset();
        Check(!aRuntime.IsTimerRunning("Repeater"), "reset clears rule-created timer");
        Console.WriteLine("Event Module timer checks passed.");
    }

    private static VisualInstruction Timer(string id, string name, double? duration = null)
    {
        var value = new VisualInstruction { Id = id };
        value.Arguments["name"] = EventValue.String(name);
        if (duration.HasValue) value.Arguments["duration"] = EventValue.Number(duration.Value);
        return value;
    }

    private static void Check(bool okay, string message)
    {
        if (!okay) throw new InvalidOperationException("FAILED: " + message);
    }
}
