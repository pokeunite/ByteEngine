using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Scene;
using ByteEngine.Editor;
using ByteEngine.Editor.Panels;

namespace ByteEngine.Tests;

internal static class C10FAnimationSignalAuthoringResolverTests
{
    public static void Run()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "ByteEngine-C10F-authoring-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(root);

        try
        {
            string projectPath = Path.Combine(root, "Resolver.byteproject");

            using EditorProjectContext project =
                EditorProjectContext.Create(projectPath, _ => { });

            string modelPath =
                Path.Combine(project.ProjectRoot, "Assets", "Actor.gltf");

            File.WriteAllText(
                modelPath,
                """
                {
                  "asset": { "version": "2.0" },
                  "nodes": [{ "name": "Root" }],
                  "scenes": [{ "nodes": [0] }],
                  "scene": 0
                }
                """);

            project.AssetDatabase.Scan();

            Assert(
                project.AssetDatabase.TryGetAsset(
                    "Assets/Actor.gltf",
                    out AssetRecord? modelRecord) &&
                modelRecord != null,
                "profile test model registered");

            AssetReference modelReference =
                new(modelRecord!.Guid, modelRecord.ProjectPath);
            ModelAsset model =
                project.Assets.LoadModel(modelReference);
            ImportedAnimation native =
                model.RegisterRuntimeAnimation(
                    new ImportedAnimation
                    {
                        Key = "native:idle",
                        Name = "Idle",
                        Duration = 1.0f
                    });

            native.Events.Add(
                new AnimationEventMarker
                {
                    Name = "Footstep",
                    Time = 0.23f
                });
            native.Windows.Add(
                new AnimationWindow
                {
                    Name = "CanCombo",
                    StartTime = 0.6f,
                    EndTime = 0.82f
                });

            model.RegisterRuntimeAnimation(
                new ImportedAnimation
                {
                    Key = "baked:attack",
                    Name = "Rig|Attack",
                    Duration = 1.0f
                });

            string profilePath =
                Path.Combine(project.ProjectRoot, "Assets", "Actor.byteanim");
            AnimationProfileSerializer.Save(
                profilePath,
                new AnimationProfile
                {
                    Name = "Actor",
                    Rig =
                    {
                        ReferenceModel = modelReference
                    }
                });

            project.AssetDatabase.Scan();

            Assert(
                project.AssetDatabase.TryGetAsset(
                    "Assets/Actor.byteanim",
                    out AssetRecord? profileRecord) &&
                profileRecord != null,
                "profile registered");

            GameObject target = new("Actor");
            AnimationController controller =
                target.AddComponent(
                    new AnimationController
                    {
                        AnimationProfile =
                            new AssetReference(
                                profileRecord!.Guid,
                                profileRecord.ProjectPath)
                    });

            AnimationSignalAuthoringResult resolved =
                AnimationSignalAuthoringResolver.Resolve(target, project);

            Assert(resolved.Target == target, "target preserved");
            Assert(resolved.Controller == controller, "controller resolved");
            Assert(resolved.ModelReference.Guid == modelRecord.Guid,
                "profile reference model selected");
            Assert(resolved.Model?.Animations.Any(
                clip => clip.Name == "Idle") == true,
                "native clip exposed");
            Assert(resolved.Model?.Animations.Any(
                clip => clip.Name == "Rig|Attack") == true,
                "baked target-owned clip exposed");

            ImportedAnimation selected =
                AnimationSignalAuthoringResolver.FindClip(
                    resolved.Model!,
                    native.Key)!;

            Assert(
                AnimationSignalAuthoringResolver.ContainsEvent(
                    selected,
                    "Footstep"),
                "selected clip events exposed");
            Assert(
                AnimationSignalAuthoringResolver.ContainsWindow(
                    selected,
                    "CanCombo"),
                "selected clip windows exposed");

            Assert(
                !AnimationSignalAuthoringResolver.ContainsEvent(
                    selected,
                    "Footstep_Old") &&
                !AnimationSignalAuthoringResolver.ContainsWindow(
                    selected,
                    "Damage_Old"),
                "stale filters detected without mutation");
            Assert(
                native.Events.Single().Name == "Footstep" &&
                native.Windows.Single().Name == "CanCombo",
                "stale checks preserve metadata");

            AnimationSignalAuthoringResult missingTarget =
                AnimationSignalAuthoringResolver.Resolve(null, project);
            Assert(
                !missingTarget.HasSelectors &&
                missingTarget.Message.Contains(
                    "cannot be resolved",
                    StringComparison.OrdinalIgnoreCase),
                "missing target gracefully falls back");

            AnimationSignalAuthoringResult missingController =
                AnimationSignalAuthoringResolver.Resolve(
                    new GameObject("No Controller"),
                    project);
            Assert(
                !missingController.HasSelectors &&
                missingController.Message.Contains(
                    "no AnimationController",
                    StringComparison.Ordinal),
                "missing controller gracefully falls back");
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

    private static void Assert(bool condition, string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                "FAILED: C10-F authoring " + name);
        }
    }
}




