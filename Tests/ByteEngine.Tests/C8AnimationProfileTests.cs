using System.Numerics;
using System.Runtime.CompilerServices;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;

namespace ByteEngine.Tests;

/// <summary>
/// C8 unified-animation-profile format regressions.
/// </summary>
internal static class C8AnimationProfileTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyDefaults();
        VerifyProfileRoundTrip();
    }

    private static void VerifyDefaults()
    {
        AnimationProfile profile =
            AnimationProfileSerializer.CreateDefault();

        Assert(
            profile.Version ==
                AnimationProfile.CurrentVersion,
            "C8: AnimationProfile version default is invalid.");

        Assert(
            profile.Rig.Type ==
                AnimationRigType.Generic,
            "C8: new profiles must default to Generic until explicitly configured as Humanoid.");

        Assert(
            profile.Locomotion.RootMotionMode ==
                RootMotionMode.InPlace,
            "C8: unified profile must preserve the safe InPlace root-motion default.");

        Assert(
            profile.Locomotion.Idle == "Idle" &&
            profile.Locomotion.Walk == "Walk" &&
            profile.Locomotion.Run == "Run",
            "C8: default locomotion clip names regressed.");
    }

    private static void VerifyProfileRoundTrip()
    {
        string root =
            Path.Combine(
                Path.GetTempPath(),
                "ByteEngine-c8-profile-tests-" +
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        string path =
            Path.Combine(
                root,
                "Player.byteanim");

        try
        {
            var source =
                new AnimationProfile
                {
                    Name = "Player Animation",
                    Rig =
                        new AnimationRigProfile
                        {
                            Type =
                                AnimationRigType.Humanoid,
                            ReferenceModel =
                                new AssetReference(
                                    Guid.NewGuid(),
                                    "Assets/Characters/Hero.fbx")
                        },
                    Locomotion =
                        new AnimationLocomotionProfile
                        {
                            Idle = "Stand",
                            Walk = "WalkForward",
                            Run = "Sprint",
                            Jump = "Jump",
                            Fall = "Fall",
                            Land = "Land",
                            RunThreshold = 5.5f,
                            TransitionDuration = 0.2f,
                            PlaybackSpeed = 1.1f,
                            RootMotionMode =
                                RootMotionMode.ApplyHorizontal
                        },
                    Actions =
                    {
                        new AnimationActionProfile
                        {
                            Name = "Attack",
                            Clip = "SwordAttack",
                            BlendIn = 0.08f,
                            BlendOut = 0.12f
                        }
                    },
                    Procedural =
                        new AnimationProceduralProfile
                        {
                            AimEnabled = true,
                            FootIkEnabled = true
                        }
                };

            AnimationProfileSerializer.Save(
                path,
                source);

            AnimationProfile clone =
                AnimationProfileSerializer.Load(
                    path);

            Assert(
                clone.Name ==
                    "Player Animation",
                "C8: profile name failed round-trip.");

            Assert(
                clone.Rig.Type ==
                    AnimationRigType.Humanoid,
                "C8: rig type failed round-trip.");

            Assert(
                clone.Rig.ReferenceModel.CachedProjectPath ==
                    "Assets/Characters/Hero.fbx",
                "C8: reference model failed round-trip.");

            Assert(
                clone.Locomotion.Run ==
                    "Sprint" &&
                MathF.Abs(
                    clone.Locomotion.RunThreshold -
                    5.5f) <
                    0.0001f &&
                clone.Locomotion.RootMotionMode ==
                    RootMotionMode.ApplyHorizontal,
                "C8: locomotion settings failed round-trip.");

            Assert(
                clone.Actions.Count ==
                    1 &&
                clone.Actions[0].Name ==
                    "Attack" &&
                clone.Actions[0].Clip ==
                    "SwordAttack",
                "C8: action definition failed round-trip.");

            Assert(
                clone.Procedural.AimEnabled &&
                clone.Procedural.FootIkEnabled,
                "C8: procedural profile flags failed round-trip.");
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
            }
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
