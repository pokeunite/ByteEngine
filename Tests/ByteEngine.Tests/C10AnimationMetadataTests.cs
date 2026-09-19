using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Tests;

internal static class C10AnimationMetadataTests
{
    public static void Run()
    {
        NativeMetadataSaveReloadAndReimport();
        BakedMetadataSaveReloadAndReplacement();
        BakedDeletionCleansMetadata();
        MalformedMetadataIsNeverOverwritten();
    }

    private static void NativeMetadataSaveReloadAndReimport()
    {
        string root = NewRoot("native");
        Guid modelGuid = Guid.NewGuid();
        try
        {
            ModelAsset original = CreateModel(modelGuid);
            ImportedAnimation clip = original.Animations[0];
            AddMetadata(clip);
            ModelAnimationMetadataStore.Save(root, modelGuid, clip);

            ModelAsset reloaded = CreateModel(modelGuid);
            Assert(ModelAnimationMetadataStore.MergeInto(root, reloaded) == 1,
                "C10-D native metadata did not reload");
            AssertMetadata(reloaded.Animations[0], "C10-D native metadata reload");

            ModelAsset reimported = CreateModel(modelGuid);
            Assert(ModelAnimationMetadataStore.MergeInto(root, reimported) == 1,
                "C10-D stable native key did not survive reimport");
            AssertMetadata(reimported.Animations[0], "C10-D native reimport metadata");
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static void BakedMetadataSaveReloadAndReplacement()
    {
        string root = NewRoot("baked");
        Guid modelGuid = Guid.NewGuid();
        try
        {
            ModelAsset target = CreateModel(modelGuid);
            ModelOwnedAnimationBakeResult first = Bake(root, target, false);
            AddMetadata(first.Animation);
            ModelAnimationMetadataStore.Save(root, modelGuid, first.Animation);

            ModelAsset reloaded = CreateModel(modelGuid);
            ModelOwnedAnimationStore.MergeInto(root, reloaded);
            ModelAnimationMetadataStore.MergeInto(root, reloaded);
            ImportedAnimation saved = reloaded.Animations.Single(
                item => item.Key == first.Animation.Key);
            AssertMetadata(saved, "C10-D baked metadata reload");

            ModelOwnedAnimationBakeResult replaced = Bake(root, target, true);
            Assert(replaced.Animation.Key == first.Animation.Key,
                "C10-D baked replacement changed stable key");
            AssertMetadata(replaced.Animation, "C10-D baked replacement metadata");
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static void BakedDeletionCleansMetadata()
    {
        string root = NewRoot("delete");
        Guid modelGuid = Guid.NewGuid();
        try
        {
            ModelAsset target = CreateModel(modelGuid);
            ImportedAnimation baked = Bake(root, target, false).Animation;
            AddMetadata(baked);
            ModelAnimationMetadataStore.Save(root, modelGuid, baked);
            string metadataPath = ModelAnimationMetadataStore.GetMetadataPath(root, modelGuid);
            Assert(File.Exists(metadataPath), "C10-D metadata file was not created");
            Assert(ModelOwnedAnimationStore.RemoveBakedAnimation(root, target, baked.Name),
                "C10-D baked animation delete failed");
            Assert(!File.Exists(metadataPath),
                "C10-D baked animation delete left orphaned metadata");
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static void MalformedMetadataIsNeverOverwritten()
    {
        string root = NewRoot("corrupt");
        Guid modelGuid = Guid.NewGuid();
        try
        {
            string path = ModelAnimationMetadataStore.GetMetadataPath(root, modelGuid);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            const string corrupt = "{ definitely not valid metadata";
            File.WriteAllText(path, corrupt);

            ModelAsset model = CreateModel(modelGuid);
            AddMetadata(model.Animations[0]);
            bool blocked = false;
            try
            {
                ModelAnimationMetadataStore.Save(root, modelGuid, model.Animations[0]);
            }
            catch (InvalidDataException)
            {
                blocked = true;
            }

            Assert(blocked, "C10-D corrupt metadata did not block save");
            Assert(File.ReadAllText(path) == corrupt,
                "C10-D corrupt metadata was silently overwritten");

            ModelAsset safeLoad = CreateModel(modelGuid);
            Assert(ModelAnimationMetadataStore.MergeInto(root, safeLoad) == 0,
                "C10-D corrupt metadata should fail safe during model load");
            Assert(safeLoad.Animations[0].Events.Count == 0 &&
                   safeLoad.Animations[0].Windows.Count == 0,
                "C10-D corrupt metadata mutated imported animation defaults");
        }
        finally
        {
            Cleanup(root);
        }
    }

    private static ModelOwnedAnimationBakeResult Bake(
        string root,
        ModelAsset target,
        bool replace) =>
        ModelOwnedAnimationStore.Bake(
            root,
            target,
            Clip("temporary", "Temporary", 0.8f),
            "Heavy Punch",
            Guid.NewGuid(),
            "Assets/Source.fbx",
            "source:punch",
            "Punch",
            replace);

    private static ModelAsset CreateModel(Guid modelGuid) =>
        new(
            new ImportedModel
            {
                Guid = modelGuid,
                SourceAssetGuid = modelGuid,
                Name = "Character",
                Animations = new List<ImportedAnimation>
                {
                    Clip("native:idle", "Idle", 1.0f)
                }
            },
            new ModelImporterSettings());

    private static ImportedAnimation Clip(string key, string name, float duration) =>
        new() { Key = key, Name = name, Duration = duration };

    private static void AddMetadata(ImportedAnimation animation)
    {
        animation.Events.Add(
            new AnimationEventMarker
            {
                Id = Guid.NewGuid(),
                Name = "Whoosh",
                Time = 0.12f,
                Payload = "audio:sword"
            });
        animation.Windows.Add(
            new AnimationWindow
            {
                Id = Guid.NewGuid(),
                Name = "Damage",
                StartTime = 0.28f,
                EndTime = 0.52f,
                Payload = "damage:25"
            });
    }

    private static void AssertMetadata(ImportedAnimation animation, string label)
    {
        Assert(animation.Events.Count == 1 &&
               animation.Events[0].Name == "Whoosh" &&
               animation.Events[0].Payload == "audio:sword" &&
               animation.Windows.Count == 1 &&
               animation.Windows[0].Name == "Damage" &&
               animation.Windows[0].Payload == "damage:25",
            label);
    }

    private static string NewRoot(string suffix)
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            $"ByteEngine-c10-{suffix}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

    private static void Cleanup(string root)
    {
        try { Directory.Delete(root, true); } catch { }
    }

    private static void Assert(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("FAILED: " + name);
    }
}
