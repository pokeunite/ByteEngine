using System.Runtime.CompilerServices;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;

namespace ByteEngine.Tests;

internal static class C95OwnedRetargetAnimationTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyBakePersistsAndMergesWithoutSourceAsset();
        VerifyConflictsProtectImportedAnimations();
        VerifyBakedReplacementRequiresExplicitApproval();
        VerifyRetargetedAnimationCanBeRemovedWithoutTouchingNativeClips();
        VerifyCorruptLibraryDoesNotBreakModelMergeOrGetOverwritten();
    }

    private static void VerifyBakePersistsAndMergesWithoutSourceAsset()
    {
        string root =
            NewRoot(
                "persist");

        try
        {
            Guid targetGuid =
                Guid.NewGuid();

            Guid sourceGuid =
                Guid.NewGuid();

            ModelAsset target =
                CreateTarget(
                    targetGuid);

            ImportedAnimation temporary =
                Clip(
                    "temporary-retarget",
                    "Heavy Punch",
                    0.82f);

            ModelOwnedAnimationBakeResult result =
                ModelOwnedAnimationStore.Bake(
                    root,
                    target,
                    temporary,
                    "Heavy Punch",
                    sourceGuid,
                    "Assets/Incoming/HeavyPunch.fbx",
                    "source:heavy-punch",
                    "Heavy Punch",
                    false);

            Assert(
                !result.Replaced,
                "C9.5: first bake was incorrectly reported as a replacement.");

            Assert(
                File.Exists(
                    result.LibraryPath),
                "C9.5: model-owned animation library was not persisted.");

            Assert(
                target.Animations.Any(
                    animation =>
                        string.Equals(
                            animation.Name,
                            "Heavy Punch",
                            StringComparison.Ordinal)),
                "C9.5: successful bake did not immediately appear on the target ModelAsset.");

            /*
             * Simulate a fresh editor/runtime load after the loose source FBX
             * has been removed. The baked animation must still be self-contained.
             */
            ModelAsset reloaded =
                CreateTarget(
                    targetGuid);

            int merged =
                ModelOwnedAnimationStore.MergeInto(
                    root,
                    reloaded);

            Assert(
                merged ==
                    1,
                "C9.5: persisted baked animation was not merged on fresh model load.");

            Assert(
                reloaded.Animations.Any(
                    animation =>
                        string.Equals(
                            animation.Name,
                            "Heavy Punch",
                            StringComparison.Ordinal) &&
                        Math.Abs(
                            animation.Duration -
                            0.82f) <
                        0.0001f),
                "C9.5: fresh target model did not recover the baked animation data.");

            ModelOwnedAnimationProvenance? provenance =
                ModelOwnedAnimationStore.GetProvenance(
                    root,
                    reloaded,
                    "Heavy Punch");

            Assert(
                provenance?.SourceModelGuid ==
                    sourceGuid,
                "C9.5: retarget source provenance was not retained.");
        }
        finally
        {
            Cleanup(
                root);
        }
    }

    private static void VerifyConflictsProtectImportedAnimations()
    {
        string root =
            NewRoot(
                "native-conflict");

        try
        {
            ModelAsset target =
                CreateTarget(
                    Guid.NewGuid());

            ModelOwnedAnimationConflictKind conflict =
                ModelOwnedAnimationStore.GetConflict(
                    root,
                    target,
                    "Idle");

            Assert(
                conflict ==
                    ModelOwnedAnimationConflictKind.ImportedAnimation,
                "C9.5: an embedded/native animation name was not protected as an imported conflict.");

            bool threw =
                false;

            try
            {
                ModelOwnedAnimationStore.Bake(
                    root,
                    target,
                    Clip(
                        "temp",
                        "Idle",
                        1.0f),
                    "Idle",
                    Guid.NewGuid(),
                    "Assets/IdleSource.fbx",
                    "source:idle",
                    "Idle",
                    true);
            }
            catch (InvalidOperationException)
            {
                threw =
                    true;
            }

            Assert(
                threw,
                "C9.5: bake silently overwrote an animation embedded in the target model.");
        }
        finally
        {
            Cleanup(
                root);
        }
    }

    private static void VerifyBakedReplacementRequiresExplicitApproval()
    {
        string root =
            NewRoot(
                "replace");

        try
        {
            ModelAsset target =
                CreateTarget(
                    Guid.NewGuid());

            Guid sourceGuid =
                Guid.NewGuid();

            ModelOwnedAnimationStore.Bake(
                root,
                target,
                Clip(
                    "temp:a",
                    "Sword Swing",
                    0.7f),
                "Sword Swing",
                sourceGuid,
                "Assets/Sword.fbx",
                "source:sword",
                "Sword Swing",
                false);

            Assert(
                ModelOwnedAnimationStore.GetConflict(
                    root,
                    target,
                    "Sword Swing") ==
                ModelOwnedAnimationConflictKind.BakedAnimation,
                "C9.5: baked animation name was not identified as a replaceable baked conflict.");

            bool blockedWithoutApproval =
                false;

            try
            {
                ModelOwnedAnimationStore.Bake(
                    root,
                    target,
                    Clip(
                        "temp:b",
                        "Sword Swing",
                        1.1f),
                    "Sword Swing",
                    sourceGuid,
                    "Assets/Sword.fbx",
                    "source:sword:new",
                    "Sword Swing",
                    false);
            }
            catch (InvalidOperationException)
            {
                blockedWithoutApproval =
                    true;
            }

            Assert(
                blockedWithoutApproval,
                "C9.5: existing baked animation was overwritten without explicit approval.");

            ModelOwnedAnimationBakeResult replaced =
                ModelOwnedAnimationStore.Bake(
                    root,
                    target,
                    Clip(
                        "temp:c",
                        "Sword Swing",
                        1.1f),
                    "Sword Swing",
                    sourceGuid,
                    "Assets/Sword.fbx",
                    "source:sword:new",
                    "Sword Swing",
                    true);

            Assert(
                replaced.Replaced,
                "C9.5: explicit baked replacement was not reported as a replacement.");

            ModelAsset reloaded =
                CreateTarget(
                    target.Guid);

            ModelOwnedAnimationStore.MergeInto(
                root,
                reloaded);

            ImportedAnimation? clip =
                reloaded.Animations.FirstOrDefault(
                    animation =>
                        string.Equals(
                            animation.Name,
                            "Sword Swing",
                            StringComparison.Ordinal));

            Assert(
                clip !=
                    null &&
                Math.Abs(
                    clip.Duration -
                    1.1f) <
                0.0001f,
                "C9.5: approved baked replacement was not persisted.");

            Assert(
                reloaded.Animations.Count(
                    animation =>
                        string.Equals(
                            animation.Name,
                            "Sword Swing",
                            StringComparison.OrdinalIgnoreCase)) ==
                    1,
                "C9.5: replacing a baked animation created duplicate target-owned clips.");
        }
        finally
        {
            Cleanup(
                root);
        }
    }


    private static void VerifyRetargetedAnimationCanBeRemovedWithoutTouchingNativeClips()
    {
        string root =
            NewRoot(
                "remove");

        try
        {
            ModelAsset target =
                CreateTarget(
                    Guid.NewGuid());

            ModelOwnedAnimationStore.Bake(
                root,
                target,
                Clip(
                    "temp:remove",
                    "Heavy Punch",
                    0.8f),
                "Heavy Punch",
                Guid.NewGuid(),
                "Assets/HeavyPunch.fbx",
                "source:heavy",
                "Heavy Punch",
                false);

            Assert(
                ModelOwnedAnimationStore.GetBakedAnimationNames(
                    root,
                    target.Guid)
                    .Contains(
                        "Heavy Punch",
                        StringComparer.OrdinalIgnoreCase),
                "C9.5 UX: baked animation did not appear in the removable owned-animation list.");

            bool removed =
                ModelOwnedAnimationStore.RemoveBakedAnimation(
                    root,
                    target,
                    "Heavy Punch");

            Assert(
                removed,
                "C9.5 UX: retargeted animation removal returned false.");

            Assert(
                !target.Animations.Any(
                    animation =>
                        string.Equals(
                            animation.Name,
                            "Heavy Punch",
                            StringComparison.OrdinalIgnoreCase)),
                "C9.5 UX: removed retargeted animation remained in the live ModelAsset.");

            Assert(
                target.Animations.Any(
                    animation =>
                        string.Equals(
                            animation.Name,
                            "Idle",
                            StringComparison.OrdinalIgnoreCase)),
                "C9.5 UX: removing a baked animation damaged a native imported animation.");

            ModelAsset reloaded =
                CreateTarget(
                    target.Guid);

            int merged =
                ModelOwnedAnimationStore.MergeInto(
                    root,
                    reloaded);

            Assert(
                merged ==
                    0,
                "C9.5 UX: removed animation returned after a fresh model merge.");
        }
        finally
        {
            Cleanup(
                root);
        }
    }

    private static void VerifyCorruptLibraryDoesNotBreakModelMergeOrGetOverwritten()
    {
        string root =
            NewRoot(
                "corrupt");

        try
        {
            Guid targetGuid =
                Guid.NewGuid();

            string path =
                ModelOwnedAnimationStore.GetLibraryPath(
                    root,
                    targetGuid);

            Directory.CreateDirectory(
                Path.GetDirectoryName(
                    path)!);

            File.WriteAllText(
                path,
                "{ definitely-not-valid-json");

            ModelAsset target =
                CreateTarget(
                    targetGuid);

            int merged =
                ModelOwnedAnimationStore.MergeInto(
                    root,
                    target);

            Assert(
                merged ==
                    0,
                "C9.5: corrupt generated animation data should be ignored during base model load.");

            bool blockedBake =
                false;

            try
            {
                ModelOwnedAnimationStore.Bake(
                    root,
                    target,
                    Clip(
                        "temp:corrupt",
                        "Punch",
                        0.5f),
                    "Punch",
                    Guid.NewGuid(),
                    "Assets/Punch.fbx",
                    "source:punch",
                    "Punch",
                    false);
            }
            catch (InvalidDataException)
            {
                blockedBake =
                    true;
            }

            Assert(
                blockedBake,
                "C9.5: Bake overwrote a corrupt existing animation library instead of protecting it.");
        }
        finally
        {
            Cleanup(
                root);
        }
    }

    private static ModelAsset CreateTarget(
        Guid targetGuid)
    {
        var imported =
            new ImportedModel
            {
                Guid =
                    targetGuid,

                SourceAssetGuid =
                    targetGuid,

                Name =
                    "Kakashi",

                Animations =
                    new List<ImportedAnimation>
                    {
                        Clip(
                            "native:idle",
                            "Idle",
                            1.2f),

                        Clip(
                            "native:walk",
                            "Walk",
                            0.9f)
                    }
            };

        return
            new ModelAsset(
                imported,
                new ModelImporterSettings());
    }

    private static ImportedAnimation Clip(
        string key,
        string name,
        float duration)
    {
        return
            new ImportedAnimation
            {
                Key =
                    key,

                Name =
                    name,

                Duration =
                    duration,

                Channels =
                    new List<ImportedAnimationChannel>
                    {
                        new()
                        {
                            NodeName =
                                "Hips",

                            Translation =
                                new ImportedVectorTrack
                                {
                                    Keys =
                                        new List<ImportedVectorKey>
                                        {
                                            new(
                                                0.0f,
                                                new System.Numerics.Vector3(
                                                    0.0f,
                                                    1.0f,
                                                    0.0f),
                                                System.Numerics.Vector3.Zero,
                                                System.Numerics.Vector3.Zero)
                                        }
                                }
                        }
                    }
            };
    }

    private static string NewRoot(
        string suffix)
    {
        string root =
            Path.Combine(
                Path.GetTempPath(),
                $"ByteEngine-c95-{suffix}-{Guid.NewGuid():N}");

        Directory.CreateDirectory(
            root);

        return root;
    }

    private static void Cleanup(
        string root)
    {
        try
        {
            Directory.Delete(
                root,
                true);
        }
        catch
        {
            // Temp cleanup is best effort only.
        }
    }

    private static void Assert(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                message);
        }
    }
}
