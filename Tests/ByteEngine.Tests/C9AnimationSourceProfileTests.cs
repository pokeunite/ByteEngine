using System.Runtime.CompilerServices;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;

namespace ByteEngine.Tests;

internal static class C9AnimationSourceProfileTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyAnimationSourceRoundTrip();
        VerifyOldProfileDefaultsToReferenceModelWorkflow();
    }

    private static void VerifyAnimationSourceRoundTrip()
    {
        string root =
            Path.Combine(
                Path.GetTempPath(),
                "ByteEngine-c9-animation-source-" +
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            root);

        string path =
            Path.Combine(
                root,
                "Player.byteanim");

        try
        {
            Guid targetGuid =
                Guid.NewGuid();

            Guid sourceGuid =
                Guid.NewGuid();

            var profile =
                new AnimationProfile
                {
                    Name =
                        "Player",

                    Rig =
                        new AnimationRigProfile
                        {
                            Type =
                                AnimationRigType.Humanoid,

                            ReferenceModel =
                                new AssetReference(
                                    targetGuid,
                                    "Assets/Characters/Player.fbx"),

                            AnimationSourceModel =
                                new AssetReference(
                                    sourceGuid,
                                    "Assets/Animations/HumanoidLibrary.fbx")
                        }
                };

            AnimationProfileSerializer.Save(
                path,
                profile);

            AnimationProfile clone =
                AnimationProfileSerializer.Load(
                    path);

            Assert(
                clone.Version ==
                    AnimationProfile.CurrentVersion,
                "C9F: profile schema version was not upgraded.");

            Assert(
                clone.Rig.ReferenceModel.Guid ==
                    targetGuid,
                "C9F: Reference Model did not survive .byteanim round-trip.");

            Assert(
                clone.Rig.AnimationSourceModel.Guid ==
                    sourceGuid,
                "C9F: Animation Source Model did not survive .byteanim round-trip.");

            Assert(
                string.Equals(
                    clone.Rig.AnimationSourceModel.CachedProjectPath,
                    "Assets/Animations/HumanoidLibrary.fbx",
                    StringComparison.Ordinal),
                "C9F: Animation Source Model path did not survive .byteanim round-trip.");
        }
        finally
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
    }

    private static void VerifyOldProfileDefaultsToReferenceModelWorkflow()
    {
        string root =
            Path.Combine(
                Path.GetTempPath(),
                "ByteEngine-c9-animation-source-legacy-" +
                Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(
            root);

        string path =
            Path.Combine(
                root,
                "Legacy.byteanim");

        try
        {
            File.WriteAllText(
                path,
                """
                {
                  "version": 1,
                  "name": "Legacy",
                  "rig": {
                    "type": "Humanoid",
                    "referenceModel": {
                      "guid": "00000000-0000-0000-0000-000000000000",
                      "cachedProjectPath": "Assets/Characters/Legacy.fbx"
                    }
                  }
                }
                """);

            AnimationProfile profile =
                AnimationProfileSerializer.Load(
                    path);

            Assert(
                profile.Rig.AnimationSourceModel.IsEmpty,
                "C9F: old profiles without Animation Source Model must preserve the native Reference Model workflow.");

            Assert(
                profile.Version ==
                    AnimationProfile.CurrentVersion,
                "C9F: legacy profile was not normalized to the current schema version.");
        }
        finally
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
