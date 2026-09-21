using System.Reflection;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Editor.Panels;

namespace ByteEngine.Tests;

internal static class C10GAnimationMilestoneClosureTests
{
    public static void Run()
    {
        NativeAndBakedParity();
    }

    private static void NativeAndBakedParity()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "ByteEngine-C10-G-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);

        try
        {
            Guid modelGuid = Guid.NewGuid();
            ModelAsset target = CreateTargetModel(modelGuid);
            ImportedAnimation native = target.Animations.Single();
            ModelOwnedAnimationBakeResult bake =
                ModelOwnedAnimationStore.Bake(
                    root,
                    target,
                    Clip("temporary", "Temporary", 1.2f),
                    "Retargeted Attack",
                    Guid.NewGuid(),
                    "Assets/Source.fbx",
                    "source:attack",
                    "Attack",
                    false);
            ImportedAnimation baked = bake.Animation;

            Metadata expectedNative =
                AddMetadata(
                    native,
                    "Footstep",
                    "surface:stone",
                    0.23f,
                    "Damage",
                    "damage:10",
                    0.32f,
                    0.56f);
            Metadata expectedBaked =
                AddMetadata(
                    baked,
                    "WeaponDraw",
                    "audio:draw",
                    0.18f,
                    "CanCombo",
                    "combo:open",
                    0.62f,
                    0.84f);

            ModelAnimationMetadataStore.Save(
                root,
                modelGuid,
                native);
            ModelAnimationMetadataStore.Save(
                root,
                modelGuid,
                baked);

            ModelAsset reloaded = CreateTargetModel(modelGuid);
            Assert(
                ModelOwnedAnimationStore.MergeInto(root, reloaded) == 1,
                "baked clip did not merge into target model");
            Assert(
                ModelAnimationMetadataStore.MergeInto(root, reloaded) == 2,
                "native and baked metadata did not merge together");

            ImportedAnimation nativeReloaded =
                reloaded.Animations.Single(
                    item => item.Key == native.Key);
            ImportedAnimation bakedReloaded =
                reloaded.Animations.Single(
                    item => item.Key == baked.Key);

            AssertMetadata(
                nativeReloaded,
                expectedNative,
                "native");
            AssertMetadata(
                bakedReloaded,
                expectedBaked,
                "baked");

            AssertAuthoringParity(
                reloaded,
                nativeReloaded,
                bakedReloaded);
            AssertVisualLogicParity(
                modelGuid,
                nativeReloaded,
                bakedReloaded);
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch
            {
            }
        }
    }

    private static void AssertAuthoringParity(
        ModelAsset model,
        ImportedAnimation native,
        ImportedAnimation baked)
    {
        ImportedAnimation? foundNative =
            AnimationSignalAuthoringResolver.FindClip(
                model,
                native.Key);
        ImportedAnimation? foundBaked =
            AnimationSignalAuthoringResolver.FindClip(
                model,
                baked.Key);

        Assert(
            ReferenceEquals(foundNative, native) &&
            ReferenceEquals(foundBaked, baked),
            "authoring resolver did not discover native and baked clips equally");
        Assert(
            AnimationSignalAuthoringResolver.ContainsEvent(
                native,
                native.Events.Single().Name) &&
            AnimationSignalAuthoringResolver.ContainsEvent(
                baked,
                baked.Events.Single().Name),
            "authoring resolver event parity failed");
        Assert(
            AnimationSignalAuthoringResolver.ContainsWindow(
                native,
                native.Windows.Single().Name) &&
            AnimationSignalAuthoringResolver.ContainsWindow(
                baked,
                baked.Windows.Single().Name),
            "authoring resolver window parity failed");
    }

    private static void AssertVisualLogicParity(
        Guid modelGuid,
        ImportedAnimation native,
        ImportedAnimation baked)
    {
        Scene scene = new("C10-G");
        GameObject actor = scene.CreateGameObject("Actor");
        AnimationController controller =
            actor.AddComponent(new AnimationController());
        VisualLogicRegistry registry =
            VisualLogicRegistry.CreateDefault();

        Assert(
            registry.TryGetCondition(
                "animation.eventFired",
                out VisualConditionDefinition? eventFired),
            "Animation Event Fired is not registered");
        Assert(
            registry.TryGetCondition(
                "animation.windowEntered",
                out VisualConditionDefinition? windowEntered),
            "Animation Window Entered is not registered");
        Assert(
            registry.TryGetCondition(
                "animation.windowExited",
                out VisualConditionDefinition? windowExited),
            "Animation Window Exited is not registered");
        Assert(
            registry.TryGetCondition(
                "animation.windowActive",
                out VisualConditionDefinition? windowActive),
            "Animation Window Active is not registered");

        foreach (ImportedAnimation animation in new[] { native, baked })
        {
            AnimationEventMarker marker =
                animation.Events.Single();
            VisualInstruction eventInstruction =
                Signal(
                    "animation.eventFired",
                    "event",
                    marker.Name,
                    animation.Key,
                    marker.Payload);
            EventExecutionContext eventContext =
                Context(
                    scene,
                    actor,
                    controller,
                    AnimationSignalKind.EventFired,
                    new AnimationEventOccurrence(
                        modelGuid,
                        animation.Key,
                        animation.Name,
                        marker.Id,
                        marker.Name,
                        marker.Payload,
                        marker.Time,
                        0),
                    null);

            Assert(
                eventFired!.Evaluate(
                    eventInstruction,
                    eventContext),
                $"{animation.Name} stable-key event filtering failed");

            AnimationWindow window =
                animation.Windows.Single();
            VisualInstruction windowInstruction =
                Signal(
                    "animation.windowEntered",
                    "window",
                    window.Name,
                    animation.Key,
                    window.Payload);
            AnimationWindowOccurrence occurrence =
                new(
                    modelGuid,
                    animation.Key,
                    animation.Name,
                    window.Id,
                    window.Name,
                    window.Payload,
                    window.StartTime,
                    window.EndTime,
                    0);

            Assert(
                windowEntered!.Evaluate(
                    windowInstruction,
                    Context(
                        scene,
                        actor,
                        controller,
                        AnimationSignalKind.WindowEntered,
                        null,
                        occurrence)),
                $"{animation.Name} Window Entered parity failed");
            Assert(
                windowExited!.Evaluate(
                    windowInstruction,
                    Context(
                        scene,
                        actor,
                        controller,
                        AnimationSignalKind.WindowExited,
                        null,
                        occurrence)),
                $"{animation.Name} Window Exited parity failed");

            SetActiveWindow(
                controller,
                window,
                animation.Duration);

            Assert(
                windowActive!.Evaluate(
                    Signal(
                        "animation.windowActive",
                        "window",
                        window.Name,
                        string.Empty,
                        string.Empty),
                    Context(
                        scene,
                        actor,
                        controller,
                        AnimationSignalKind.None,
                        null,
                        null)),
                $"{animation.Name} Window Active parity failed");
        }
    }

    private static void SetActiveWindow(
        AnimationController controller,
        AnimationWindow window,
        float duration)
    {
        typeof(AnimationController)
            .GetField(
                "_animationWindowSnapshot",
                BindingFlags.Instance |
                BindingFlags.NonPublic)!
            .SetValue(
                controller,
                AnimationWindowCrossing.BuildSnapshot(
                    new[] { window },
                    duration));

        HashSet<Guid> active =
            (HashSet<Guid>)typeof(AnimationController)
                .GetField(
                    "_activeAnimationWindows",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic)!
                .GetValue(controller)!;

        active.Clear();
        active.Add(window.Id);
    }

    private static EventExecutionContext Context(
        Scene scene,
        GameObject actor,
        AnimationController controller,
        AnimationSignalKind kind,
        AnimationEventOccurrence? animationEvent,
        AnimationWindowOccurrence? animationWindow) =>
        new()
        {
            Globals = new VariableStore(),
            Scene = scene,
            Self = actor,
            AnimationSignalKind = kind,
            AnimationSignalSource = controller,
            AnimationEvent = animationEvent,
            AnimationWindow = animationWindow
        };

    private static VisualInstruction Signal(
        string id,
        string nameArgument,
        string name,
        string clip,
        string payload) =>
        new()
        {
            Id = id,
            Arguments =
            {
                ["target"] = EventValue.String("Self"),
                [nameArgument] = EventValue.String(name),
                ["clip"] = EventValue.String(clip),
                ["payload"] = EventValue.String(payload)
            }
        };

    private static ModelAsset CreateTargetModel(Guid modelGuid) =>
        new(
            new ImportedModel
            {
                Guid = modelGuid,
                SourceAssetGuid = modelGuid,
                Name = "Target",
                Animations =
                    new List<ImportedAnimation>
                    {
                        Clip(
                            "native:locomotion",
                            "Rig|Sprint_Loop",
                            1.0f)
                    }
            },
            new ModelImporterSettings());

    private static ImportedAnimation Clip(
        string key,
        string name,
        float duration) =>
        new()
        {
            Key = key,
            Name = name,
            Duration = duration
        };

    private static Metadata AddMetadata(
        ImportedAnimation animation,
        string eventName,
        string eventPayload,
        float eventTime,
        string windowName,
        string windowPayload,
        float windowStart,
        float windowEnd)
    {
        AnimationEventMarker marker =
            new()
            {
                Id = Guid.NewGuid(),
                Name = eventName,
                Payload = eventPayload,
                Time = eventTime
            };
        AnimationWindow window =
            new()
            {
                Id = Guid.NewGuid(),
                Name = windowName,
                Payload = windowPayload,
                StartTime = windowStart,
                EndTime = windowEnd
            };

        animation.Events.Add(marker);
        animation.Windows.Add(window);

        return new Metadata(
            marker.Id,
            marker.Name,
            marker.Payload,
            marker.Time,
            window.Id,
            window.Name,
            window.Payload,
            window.StartTime,
            window.EndTime);
    }

    private static void AssertMetadata(
        ImportedAnimation animation,
        Metadata expected,
        string kind)
    {
        AnimationEventMarker marker =
            animation.Events.Single();
        AnimationWindow window =
            animation.Windows.Single();

        Assert(
            marker.Id == expected.EventId &&
            marker.Name == expected.EventName &&
            marker.Payload == expected.EventPayload &&
            Nearly(marker.Time, expected.EventTime),
            $"{kind} event metadata or stable ID changed");
        Assert(
            window.Id == expected.WindowId &&
            window.Name == expected.WindowName &&
            window.Payload == expected.WindowPayload &&
            Nearly(window.StartTime, expected.WindowStart) &&
            Nearly(window.EndTime, expected.WindowEnd),
            $"{kind} window metadata or stable ID changed");
    }

    private static bool Nearly(float left, float right) =>
        Math.Abs(left - right) <= 0.0001f;

    private static void Assert(bool condition, string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                "FAILED C10-G: " + name);
        }
    }

    private readonly record struct Metadata(
        Guid EventId,
        string EventName,
        string EventPayload,
        float EventTime,
        Guid WindowId,
        string WindowName,
        string WindowPayload,
        float WindowStart,
        float WindowEnd);
}
