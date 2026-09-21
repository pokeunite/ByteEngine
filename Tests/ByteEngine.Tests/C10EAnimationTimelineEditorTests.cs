
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Editor.Panels;

namespace ByteEngine.Tests;

internal static class C10EAnimationTimelineEditorTests
{
    internal static void Run()
    {
        EventTimesClampAndDirtyStateTracks();
        WindowRangesNormalize();
        WorkingCopyAppliesAndPersists();
        StableKeyResolvesAfterModelRefresh();
    }

    private static void EventTimesClampAndDirtyStateTracks()
    {
        ImportedAnimation animation = Clip("native:attack", "Attack", 1.0f);
        animation.Events.Add(new AnimationEventMarker
        {
            Id = Guid.NewGuid(), Name = "Late", Time = 5.0f
        });
        var document = new AnimationTimelineDocument(Guid.NewGuid(), animation);
        Assert(!document.IsDirty && Nearly(document.Events[0].Time, 1.0f),
            "C10-E loading clamps event time without dirtying the working copy");

        AnimationEventMarker added = document.AddEvent(-2.0f);
        Assert(document.IsDirty && Nearly(added.Time, 0.0f),
            "C10-E adding an event clamps time and marks dirty");
        Assert(document.DeleteEvent(added.Id) && document.IsDirty,
            "C10-E deleting by stable event ID marks dirty");
    }

    private static void WindowRangesNormalize()
    {
        ImportedAnimation animation = Clip("native:attack", "Attack", 0.8f);
        animation.Windows.Add(new AnimationWindow
        {
            Id = Guid.NewGuid(), Name = "Damage", StartTime = 0.7f, EndTime = 0.2f
        });
        var document = new AnimationTimelineDocument(Guid.NewGuid(), animation);
        Assert(Nearly(document.Windows[0].StartTime, 0.2f) &&
               Nearly(document.Windows[0].EndTime, 0.7f),
            "C10-E reversed window range normalizes on load");

        AnimationWindow window = document.AddWindow(2.0f);
        Assert(window.StartTime <= window.EndTime && window.EndTime <= 0.8f,
            "C10-E new window stays inside clip duration");
        window.StartTime = 4.0f;
        window.EndTime = -1.0f;
        document.MarkDirty();
        Assert(Nearly(window.StartTime, 0.0f) && Nearly(window.EndTime, 0.8f),
            "C10-E edited reversed/out-of-range window normalizes cleanly");
    }

    private static void WorkingCopyAppliesAndPersists()
    {
        string root = Path.Combine(Path.GetTempPath(), "ByteEngine-c10e-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        Guid modelGuid = Guid.NewGuid();
        try
        {
            ImportedAnimation animation = Clip("native:attack", "Attack", 1.0f);
            var document = new AnimationTimelineDocument(modelGuid, animation);
            document.AddEvent(0.25f).Payload = "whoosh";
            document.AddWindow(0.4f).Payload = "damage";
            document.ApplyTo(animation);
            ModelAnimationMetadataStore.Save(root, modelGuid, animation);
            document.MarkSaved();
            Assert(!document.IsDirty && animation.Events.Count == 1 && animation.Windows.Count == 1,
                "C10-E apply updates live ImportedAnimation and clears dirty after save");

            ModelAsset refreshed = Model(modelGuid, Clip("native:attack", "Attack", 1.0f));
            ModelAnimationMetadataStore.MergeInto(root, refreshed);
            Assert(refreshed.Animations[0].Events.Count == 1 &&
                   refreshed.Animations[0].Windows.Count == 1,
                "C10-E save reloads through existing model metadata store");
        }
        finally
        {
            try { Directory.Delete(root, true); } catch { }
        }
    }

    private static void StableKeyResolvesAfterModelRefresh()
    {
        Guid modelGuid = Guid.NewGuid();
        ModelAsset first = Model(modelGuid, Clip("stable:key", "Old Name", 1.0f));
        ImportedAnimation firstClip = first.Animations[0];
        ModelAsset refreshed = Model(modelGuid, Clip("stable:key", "Renamed Display", 1.2f));
        ImportedAnimation? resolved =
            AnimationTimelineDocument.ResolveAnimation(refreshed, firstClip.Key);
        Assert(resolved != null && !ReferenceEquals(firstClip, resolved) &&
               resolved.Name == "Renamed Display",
            "C10-E workspace re-resolves refreshed clips by stable key, not object/name");
    }

    private static ModelAsset Model(Guid guid, ImportedAnimation animation) =>
        new(
            new ImportedModel
            {
                Guid = guid,
                SourceAssetGuid = guid,
                Name = "Character",
                Animations = new List<ImportedAnimation> { animation }
            },
            new ModelImporterSettings());

    private static ImportedAnimation Clip(string key, string name, float duration) =>
        new() { Key = key, Name = name, Duration = duration };

    private static bool Nearly(float left, float right) =>
        MathF.Abs(left - right) <= 0.0001f;

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + message);
    }
}
