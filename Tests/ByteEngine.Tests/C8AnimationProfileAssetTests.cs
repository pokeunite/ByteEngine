using System.Runtime.CompilerServices;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;

namespace ByteEngine.Tests;

/// <summary>
/// C8B regression coverage for .byteanim asset discovery, GUID references,
/// loading and identity-preserving hot reload.
/// </summary>
internal static class C8AnimationProfileAssetTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        VerifyAssetDiscoveryAndHotReload();
    }

    private static void VerifyAssetDiscoveryAndHotReload()
    {
        string root =
            Path.Combine(
                Path.GetTempPath(),
                "ByteEngine-c8b-profile-asset-tests-" +
                Guid.NewGuid().ToString("N"));

        string assetsDirectory =
            Path.Combine(
                root,
                "Assets");

        Directory.CreateDirectory(
            assetsDirectory);

        string profilePath =
            Path.Combine(
                assetsDirectory,
                "Player.byteanim");

        try
        {
            AnimationProfileSerializer.Save(
                profilePath,
                new AnimationProfile
                {
                    Name = "Player Animation",
                    Rig =
                        new AnimationRigProfile
                        {
                            Type =
                                AnimationRigType.Humanoid
                        }
                });

            using var database =
                new AssetDatabase(
                    root,
                    new[] { "Assets" });

            Assert(
                database.TryGetAsset(
                    "Assets/Player.byteanim",
                    out AssetRecord? record) &&
                record != null,
                "C8B: .byteanim profile was not discovered by AssetDatabase.");

            Assert(
                record!.Type ==
                    AssetType.AnimationProfile,
                "C8B: .byteanim asset was not classified as AnimationProfile.");

            Assert(
                record.Guid !=
                    Guid.Empty,
                "C8B: AnimationProfile did not receive a persistent asset GUID.");

            using var manager =
                new AssetManager(
                    database);

            var reference =
                new AssetReference(
                    record.Guid,
                    record.ProjectPath);

            AnimationProfile loaded =
                manager.LoadAnimationProfile(
                    reference);

            Assert(
                loaded.Name ==
                    "Player Animation" &&
                loaded.Rig.Type ==
                    AnimationRigType.Humanoid,
                "C8B: AssetManager did not load AnimationProfile content.");

            AnimationProfileSerializer.Save(
                profilePath,
                new AnimationProfile
                {
                    Name = "Player Animation Reloaded",
                    Rig =
                        new AnimationRigProfile
                        {
                            Type =
                                AnimationRigType.Generic
                        }
                });

            database.Scan();

            AnimationProfile loadedAgain =
                manager.LoadAnimationProfile(
                    reference);

            Assert(
                ReferenceEquals(
                    loaded,
                    loadedAgain),
                "C8B: AnimationProfile hot reload replaced object identity.");

            Assert(
                loaded.Name ==
                    "Player Animation Reloaded" &&
                loaded.Rig.Type ==
                    AnimationRigType.Generic,
                "C8B: AnimationProfile hot reload did not update the cached profile.");

            Assert(
                File.Exists(
                    profilePath +
                    ".meta"),
                "C8B: AnimationProfile metadata sidecar was not created.");
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
