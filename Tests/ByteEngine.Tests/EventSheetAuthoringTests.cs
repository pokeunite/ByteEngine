using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Tests;

internal static class EventSheetAuthoringTests
{
    public static void Run()
    {
        string path = Path.Combine(Path.GetTempPath(),
            "byteengine-event-drafts-" + Guid.NewGuid().ToString("N") + ".byteevents");
        try
        {
            var module = new EventModuleDefinition();
            Guid draftId = Guid.NewGuid();
            Guid nextActionId = Guid.NewGuid();
            module.EditorLooseActions.Add(new VisualInstruction
            {
                InstanceId = draftId,
                Id = "time.startTimer",
                NextActionId = nextActionId,
                EditorLayoutInitialized = true,
                EditorX = 123,
                EditorY = 456,
                Arguments =
                {
                    ["name"] = EventValue.String("Draft"),
                    ["duration"] = EventValue.Number(1)
                }
            });
            module.EditorLooseActions.Add(new VisualInstruction
            {
                InstanceId = nextActionId,
                Id = "time.stopTimer"
            });
            Guid conditionId = Guid.NewGuid();
            Guid gateId = Guid.NewGuid();
            module.EditorLooseConditions.Add(new VisualInstruction
            {
                InstanceId = conditionId,
                Id = "system.always"
            });
            module.EditorLooseConditions.Add(new VisualInstruction
            {
                InstanceId = gateId,
                Id = "logic.and",
                ConditionInputIds = { conditionId }
            });

            var serializer = new EventModuleSerializer();
            serializer.Save(module, path);
            EventModuleDefinition loaded = serializer.Load(path);
            Check(loaded.EditorLooseActions.Count == 2 &&
                  loaded.EditorLooseActions[0].InstanceId == draftId &&
                  loaded.EditorLooseActions[0].EditorX == 123 &&
                  loaded.EditorLooseActions[0].NextActionId == nextActionId &&
                  loaded.EditorLooseActions[0].Arguments["name"].Constant.String == "Draft",
                "unconnected action chain survives save/reload");
            Check(loaded.EditorLooseConditions.Count == 2 &&
                  loaded.EditorLooseConditions[1].ConditionInputIds.Contains(conditionId),
                "unconnected logic input survives save/reload");

            var scene = new Scene("Draft node test");
            var runtime = new EventModuleRuntime(VisualLogicRegistry.CreateDefault());
            runtime.Update(loaded, new VariableStore(), scene, scene.CreateGameObject("Owner"));
            Check(!runtime.IsTimerRunning("Draft"),
                "unconnected node is not executed by runtime");
            Console.WriteLine("Event Sheet authoring checks passed.");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("FAILED: " + message);
    }
}
