using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Tests;

/// <summary>
/// Focused v0.11-C7 animation regressions.
///
/// This runs automatically when the ByteEngine.Tests executable starts, before
/// the existing top-level regression runner. It intentionally avoids requiring
/// a particular imported character asset so the checks stay deterministic.
/// </summary>
internal static class C7AnimationRegressionTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyAuthoringDefaults();
        VerifyAnimationSerializationRoundTrip();
    }

    private static void VerifyAuthoringDefaults()
    {
        var controller =
            new AnimationController();

        Assert(
            controller.RootMotionMode == RootMotionMode.InPlace,
            "C7: AnimationController must default to InPlace root motion.");

        Assert(
            controller.RootMotionDelta == Vector3.Zero,
            "C7: RootMotionDelta must default to zero.");

        var socket =
            new BoneSocket3D();

        Assert(
            !socket.InheritBoneScale,
            "C6/C7 regression: BoneSocket3D must default InheritBoneScale to false.");

        Assert(
            Enum.IsDefined(RootMotionMode.InPlace) &&
            Enum.IsDefined(RootMotionMode.ApplyHorizontal),
            "C7: expected root-motion authoring modes are missing.");
    }

    private static void VerifyAnimationSerializationRoundTrip()
    {
        string root =
            Path.Combine(
                Path.GetTempPath(),
                "ByteEngine-c7-animation-tests-" +
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            Path.Combine(root, "Assets"));

        Directory.CreateDirectory(
            Path.Combine(root, "Scenes"));

        try
        {
            using var database =
                new AssetDatabase(
                    root,
                    new[] { "Assets", "Scenes" });

            using var assets =
                new AssetManager(database);

            var components =
                new ComponentSerializer(
                    root,
                    database,
                    assets);

            var serializer =
                new SceneSerializer(
                    components);

            /*
             * The base serializer owns AnimationController persistence. Register
             * the existing supplemental animation codecs so this fixture can
             * also verify BoneSocket3D without replacing that controller codec.
             */
            AnimationSerializationRegistrar.Register(
                components);

            var scene =
                new Scene("C7 Animation Regression");

            GameObject character =
                scene.CreateGameObject("Character");

            character.AddComponent(
                new AnimationController
                {
                    AnimationProfile =
                        new AssetReference(
                            Guid.Parse("a6f11f53-bd09-4dfa-92bd-67698fe8148e"),
                            "Assets/Player.byteanim"),
                    Idle = "Stand",
                    Walk = "Walk",
                    Run = "Sprint",
                    Jump = "Leap",
                    Fall = "Drop",
                    Land = "TouchDown",
                    RunThreshold = 6.25f,
                    DriveLocomotion = false,
                    TransitionDuration = 0.2f,
                    PlaybackSpeed = 1.15f,
                    RootMotionMode =
                        RootMotionMode.ApplyHorizontal
                });

            character.AddComponent(
                new BoneSocket3D
                {
                    BoneName = "hand.r",
                    PositionOffset =
                        new Vector3(0.1f, 0.2f, 0.3f),
                    RotationOffsetDegrees =
                        new Vector3(10.0f, 20.0f, 30.0f),
                    ScaleMultiplier =
                        new Vector3(1.0f, 1.1f, 0.9f),
                    InheritBoneScale = false
                });

            Scene clone =
                serializer.CloneForRuntime(scene);

            GameObject? clonedCharacter =
                clone.FindGameObject("Character");

            Assert(
                clonedCharacter != null,
                "C7: animation regression character failed scene round-trip.");

            AnimationController? clonedController =
                clonedCharacter!
                    .GetComponent<AnimationController>();

            Assert(
                clonedController != null,
                "C7: AnimationController failed scene round-trip.");

            Assert(
                clonedController!.AnimationProfile.Guid ==
                    Guid.Parse("a6f11f53-bd09-4dfa-92bd-67698fe8148e") &&
                clonedController.AnimationProfile.CachedProjectPath ==
                    "Assets/Player.byteanim" &&
                clonedController.Idle == "Stand" &&
                clonedController.Walk == "Walk" &&
                clonedController.Run == "Sprint" &&
                clonedController.Jump == "Leap" &&
                clonedController.Fall == "Drop" &&
                clonedController.Land == "TouchDown" &&
                MathF.Abs(clonedController.RunThreshold - 6.25f) < 0.0001f &&
                !clonedController.DriveLocomotion &&
                clonedController.RootMotionMode == RootMotionMode.ApplyHorizontal,
                "C7/C8: current AnimationController fields failed serialization round-trip.");

            Assert(
                MathF.Abs(
                    clonedController.TransitionDuration -
                    0.2f) < 0.0001f &&
                MathF.Abs(
                    clonedController.PlaybackSpeed -
                    1.15f) < 0.0001f,
                "C7: animation playback settings regressed during serialization.");

            BoneSocket3D? clonedSocket =
                clonedCharacter
                    .GetComponent<BoneSocket3D>();

            Assert(
                clonedSocket != null,
                "C6/C7 regression: BoneSocket3D failed scene round-trip.");

            Assert(
                clonedSocket!.BoneName == "hand.r" &&
                !clonedSocket.InheritBoneScale,
                "C6/C7 regression: BoneSocket3D authoring data changed during serialization.");

            Assert(
                Vector3.Distance(
                    clonedSocket.PositionOffset,
                    new Vector3(0.1f, 0.2f, 0.3f)) <
                    0.0001f,
                "C6/C7 regression: BoneSocket3D position offset failed round-trip.");

            Assert(
                Vector3.Distance(
                    clonedSocket.RotationOffsetDegrees,
                    new Vector3(10.0f, 20.0f, 30.0f)) <
                    0.0001f,
                "C6/C7 regression: BoneSocket3D rotation offset failed round-trip.");

            Assert(
                Vector3.Distance(
                    clonedSocket.ScaleMultiplier,
                    new Vector3(1.0f, 1.1f, 0.9f)) <
                    0.0001f,
                "C6/C7 regression: BoneSocket3D scale multiplier failed round-trip.");
            AnimationController? legacy =
                components.Deserialize(
                    new ComponentData
                    {
                        Type = "AnimationController",
                        Properties =
                            new JsonObject
                            {
                                ["idle"] = "LegacyIdle",
                                ["runThreshold"] = 3.5f
                            }
                    }) as AnimationController;

            Assert(
                legacy != null &&
                legacy.Idle == "LegacyIdle" &&
                legacy.AnimationProfile.IsEmpty &&
                legacy.DriveLocomotion &&
                MathF.Abs(legacy.TransitionDuration - 0.15f) < 0.0001f &&
                MathF.Abs(legacy.PlaybackSpeed - 1.0f) < 0.0001f &&
                legacy.RootMotionMode == RootMotionMode.InPlace,
                "C7/C8: legacy AnimationController data did not load modern defaults.");
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(
                        root,
                        recursive: true);
                }
            }
            catch
            {
                /*
                 * Test cleanup failure should never hide an animation regression.
                 */
            }
        }
    }

    private static void Assert(
        bool condition,
        string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
