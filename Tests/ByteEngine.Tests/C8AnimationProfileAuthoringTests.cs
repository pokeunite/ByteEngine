using System.Runtime.CompilerServices;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;

namespace ByteEngine.Tests;

internal static class C8AnimationProfileAuthoringTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyEditableSectionsPersist();
    }

    private static void VerifyEditableSectionsPersist()
    {
        string root =
            Path.Combine(
                Path.GetTempPath(),
                "ByteEngine-c8d-profile-authoring-" +
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        string path =
            Path.Combine(
                root,
                "Authoring.byteanim");

        try
        {
            var profile =
                new AnimationProfile
                {
                    Name = "Authoring",
                    Rig =
                        new AnimationRigProfile
                        {
                            Type =
                                AnimationRigType.Humanoid
                        },
                    Locomotion =
                        new AnimationLocomotionProfile
                        {
                            Idle = "Idle_A",
                            Walk = "Walk_A",
                            Run = "Run_A",
                            RunThreshold = 5.0f
                        },
                    Actions =
                    {
                        new AnimationActionProfile
                        {
                            Name = "Attack",
                            Clip = "Attack_A",
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
                profile);

            AnimationProfile clone =
                AnimationProfileSerializer.Load(
                    path);

            Assert(
                clone.Rig.Type ==
                    AnimationRigType.Humanoid,
                "C8D: Rig section failed persistence.");

            Assert(
                clone.Locomotion.Run ==
                    "Run_A" &&
                MathF.Abs(
                    clone.Locomotion.RunThreshold -
                    5.0f) <
                    0.0001f,
                "C8D: Locomotion section failed persistence.");

            Assert(
                clone.Actions.Count ==
                    1 &&
                clone.Actions[0].Name ==
                    "Attack" &&
                clone.Actions[0].Clip ==
                    "Attack_A",
                "C8D: Actions section failed persistence.");

            Assert(
                clone.Procedural.AimEnabled &&
                clone.Procedural.FootIkEnabled,
                "C8D: Procedural section failed persistence.");
        }
        finally
        {
            try
            {
                if (Directory.Exists(root))
                {
                    Directory.Delete(
                        root,
                        true);
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
