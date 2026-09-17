using System.Runtime.CompilerServices;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;

namespace ByteEngine.Tests;

internal static class C8AnimationControllerProfileTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyProfileAppliesToController();
        VerifyControllerProfileReferenceRoundTrip();
    }

    private static void VerifyProfileAppliesToController()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "ByteEngine-c8c-profile-controller-" + Guid.NewGuid().ToString("N"));

        string assetsDir = Path.Combine(root, "Assets");
        Directory.CreateDirectory(assetsDir);

        string profilePath = Path.Combine(assetsDir, "Player.byteanim");

        try
        {
            AnimationProfileSerializer.Save(
                profilePath,
                new AnimationProfile
                {
                    Locomotion =
                        new AnimationLocomotionProfile
                        {
                            Idle = "Stand",
                            Walk = "Jog",
                            Run = "Sprint",
                            Jump = "Leap",
                            Fall = "Air",
                            Land = "TouchDown",
                            RunThreshold = 6.0f,
                            TransitionDuration = 0.25f,
                            PlaybackSpeed = 1.2f,
                            RootMotionMode = RootMotionMode.ApplyHorizontal
                        }
                });

            using var database =
                new AssetDatabase(root, new[] { "Assets" });
            using var assets =
                new AssetManager(database);

            Assert(
                database.TryGetAsset(
                    "Assets/Player.byteanim",
                    out AssetRecord? record) &&
                record != null,
                "C8C: profile asset was not discovered.");

            Guid registration =
                AnimationRuntimeAssets.Configure(assets);

            try
            {
                var controller =
                    new AnimationController
                    {
                        AnimationProfile =
                            new AssetReference(
                                record!.Guid,
                                record.ProjectPath)
                    };

                Assert(
                    controller.ApplyAnimationProfile(),
                    "C8C: controller could not apply its Animation Profile.");

                Assert(
                    controller.Idle == "Stand" &&
                    controller.Walk == "Jog" &&
                    controller.Run == "Sprint" &&
                    controller.RootMotionMode == RootMotionMode.ApplyHorizontal,
                    "C8C: profile locomotion settings were not applied.");
            }
            finally
            {
                AnimationRuntimeAssets.Clear(registration);
            }
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
            catch
            {
            }
        }
    }

    private static void VerifyControllerProfileReferenceRoundTrip()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "ByteEngine-c8c-profile-serialization-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        Directory.CreateDirectory(Path.Combine(root, "Scenes"));

        string profilePath = Path.Combine(root, "Assets", "Player.byteanim");

        try
        {
            AnimationProfileSerializer.Save(
                profilePath,
                new AnimationProfile { Name = "Player" });

            using var database =
                new AssetDatabase(root, new[] { "Assets", "Scenes" });
            using var assets =
                new AssetManager(database);

            Assert(
                database.TryGetAsset(
                    "Assets/Player.byteanim",
                    out AssetRecord? record) &&
                record != null,
                "C8C: serialization test profile was not discovered.");

            var serializer =
                new SceneSerializer(
                    new ComponentSerializer(
                        root,
                        database,
                        assets));

            var scene = new Scene("C8 Animation Profile Test");
            var character = new GameObject("Character");
            scene.AddGameObject(character);

            character.AddComponent(
                new AnimationController
                {
                    AnimationProfile =
                        new AssetReference(
                            record!.Guid,
                            record.ProjectPath)
                });

            Scene clone =
                serializer.CloneForRuntime(scene);

            GameObject? clonedCharacter =
                clone.FindGameObject("Character");

            AnimationController? clonedController =
                clonedCharacter?
                    .GetComponent<AnimationController>();

            Assert(
                clonedController != null,
                "C8C: AnimationController did not survive scene clone.");

            Assert(
                clonedController!.AnimationProfile.Guid == record.Guid &&
                clonedController.AnimationProfile.CachedProjectPath ==
                    record.ProjectPath,
                "C8C: Animation Profile reference failed scene round-trip.");
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
            catch
            {
            }
        }
    }

    private static void Assert(
        bool condition,
        string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
