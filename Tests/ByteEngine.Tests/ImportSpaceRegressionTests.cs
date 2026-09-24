using System.Numerics;
using Assimp;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Scene;
using ByteEngine.Editor;

namespace ByteEngine.Tests;

internal static class ImportSpaceRegressionTests
{
    public static void Run()
    {
        foreach (float height in new[] { 1.20f, 1.80f, 2.40f })
        {
            ImportedModel source = HeightModel(height);
            ImportedModel imported = ImportedModelSpace.Apply(
                source, ImportedModelSpace.GltfCorrection(1f));
            Assert(imported.Nodes.Count == source.Nodes.Count + 1, "glTF correction is one internal node");
            AssertNear(Height(imported), height, "glTF preserves authored metre height");
            AssertNear(Vector3.Transform(Vector3.UnitZ, imported.Nodes[0].LocalTransform),
                -Vector3.UnitZ, "glTF front maps to ByteEngine forward");
            AssertNear(Vector3.Transform(Vector3.UnitY, imported.Nodes[0].LocalTransform),
                Vector3.UnitY, "glTF up remains upright");
            Assert(ImportedModelSpace.Apply(source, Matrix4x4.Identity).Nodes.Count == source.Nodes.Count,
                "identity correction adds no wrapper");
        }

        TestFbxUnits(1.8f, 1f, 0.018f);
        TestFbxUnits(180f, 1f, 1.8f);
        TestFbxUnits(1800f, 0.1f, 1.8f);
        TestFbxUnits(1.8f, 100f, 1.8f);
        TestAlternateFront();
        TestSkinnedBindAndSocketSpace();
        TestReimportCorrectionRefresh();
        TestLegacyRebasePreservesVisiblePose();
        Console.WriteLine("Import-space units and axes regressions passed.");
    }

    private static void TestFbxUnits(float sourceHeight, float unitScaleFactor, float expectedHeight)
    {
        var scene = new Assimp.Scene();
        scene.Metadata.Add("UnitScaleFactor", new Metadata.Entry(MetaDataType.Double, (double)unitScaleFactor));
        SetAxis(scene, "UpAxis", 1, 1);
        SetAxis(scene, "FrontAxis", 2, 1);
        ImportedModel imported = ImportedModelSpace.Apply(
            HeightModel(sourceHeight), ImportedModelSpace.FbxCorrection(scene, 1f));
        AssertNear(Height(imported), expectedHeight, "FBX physical height follows UnitScaleFactor");
        AssertNear(Vector3.Transform(Vector3.UnitZ, imported.Nodes[0].LocalTransform),
            -Vector3.UnitZ * (unitScaleFactor / 100f), "FBX source front maps to ByteEngine forward");
    }

    private static void TestAlternateFront()
    {
        var scene = new Assimp.Scene();
        scene.Metadata.Add("UnitScaleFactor", new Metadata.Entry(MetaDataType.Double, 100d));
        SetAxis(scene, "UpAxis", 1, 1);
        SetAxis(scene, "FrontAxis", 2, -1);
        Matrix4x4 correction = ImportedModelSpace.FbxCorrection(scene, 1f);
        AssertNear(Vector3.Transform(-Vector3.UnitZ, correction), -Vector3.UnitZ,
            "negative source front maps to ByteEngine forward");
        AssertNear(Vector3.Transform(Vector3.UnitY, correction), Vector3.UnitY,
            "alternate FBX front preserves up");
    }

    private static void TestSkinnedBindAndSocketSpace()
    {
        var scene = new Assimp.Scene();
        scene.Metadata.Add("UnitScaleFactor", new Metadata.Entry(MetaDataType.Double, 1d));
        SetAxis(scene, "UpAxis", 1, 1);
        SetAxis(scene, "FrontAxis", 2, 1);
        Matrix4x4 correction = ImportedModelSpace.FbxCorrection(scene, 1f);

        var source = new ImportedModel
        {
            Nodes = new List<ImportedNode>
            {
                new() { Key = "mesh", Name = "Mesh" },
                new() { Key = "hand", Name = "Hand", LocalTransform = Matrix4x4.CreateTranslation(0, 100, 0) }
            },
            Skeleton = new SkeletonAsset
            {
                Bones = new List<ByteEngine.Core.Assets.Importers.Bone>
                {
                    new() { Name = "Hand", BindPose = Matrix4x4.CreateTranslation(0, -100, 0) }
                }
            },
            Animations = new List<ImportedAnimation>
            {
                new() { Name = "MoveHand", Channels = new List<ImportedAnimationChannel>
                {
                    new() { NodeName = "Hand", Translation = new ImportedVectorTrack
                    {
                        Keys = new List<ImportedVectorKey>
                        {
                            new(0f, new Vector3(0, 100, 0), Vector3.Zero, Vector3.Zero),
                            new(1f, new Vector3(0, 110, 0), Vector3.Zero, Vector3.Zero)
                        }
                    }}
                }}
            }
        };
        ImportedModel imported = ImportedModelSpace.Apply(source, correction);
        Assert(imported.Animations[0].Channels[0].Translation!.Keys.Count == 2,
            "Source animation tracks are not rewritten independently of the model");

        Matrix4x4 meshGlobal = correction;
        Matrix4x4.Invert(meshGlobal, out Matrix4x4 inverseMesh);
        Matrix4x4 bindJoint = Matrix4x4.CreateTranslation(0, 100, 0) * correction;
        Matrix4x4 skinAtBind = source.Skeleton!.Bones[0].BindPose * bindJoint * inverseMesh;
        AssertNear(Vector3.Transform(Vector3.Zero, skinAtBind), Vector3.Zero,
            "Shared correction cancels between inverse bind, joint and mesh");
        Matrix4x4 movedJoint = Matrix4x4.CreateTranslation(0, 110, 0) * correction;
        AssertNear(movedJoint.Translation - bindJoint.Translation, new Vector3(0, 0.1f, 0),
            "Animated source translation becomes metres in model space");
        AssertNear(Vector3.Transform(new Vector3(0, 10, 0), movedJoint),
            new Vector3(0, 1.2f, 0),
            "Socket-local translation follows the corrected animated bone");
    }
    private static void TestReimportCorrectionRefresh()
    {
        var scene = new ByteEngine.Core.Scene.Scene("Import Refresh");
        GameObject modelRoot = scene.CreateGameObject("Model");
        GameObject correctionNode = scene.CreateGameObject("Import Space");
        correctionNode.SetParent(modelRoot, false);
        ImportedModel first = ImportedModelSpace.Apply(
            HeightModel(1.8f), ImportedModelSpace.GltfCorrection(1f));
        ImportedModel updated = ImportedModelSpace.Apply(
            HeightModel(1.8f), ImportedModelSpace.GltfCorrection(2f));
        Assert(EditorSceneCommands.RefreshImportSpace(modelRoot, new ModelAsset(first)),
            "Initial import correction reaches the saved instance");
        AssertNear(correctionNode.Transform.LocalScale.X, 1f,
            "Initial correction has unit user scale");
        Assert(!EditorSceneCommands.RefreshImportSpace(modelRoot, new ModelAsset(first)),
            "Reimport with identical settings is a no-op");
        Assert(EditorSceneCommands.RefreshImportSpace(modelRoot, new ModelAsset(updated)),
            "Changed import settings update the existing internal node");
        AssertNear(correctionNode.Transform.LocalScale.X, 2f,
            "Import scale changes once, not multiplicatively");
        Assert(!EditorSceneCommands.RefreshImportSpace(modelRoot, new ModelAsset(updated)),
            "Repeated normalization remains idempotent");
        AssertNear(modelRoot.Transform.LocalScale.X, 1f,
            "User-facing Model transform remains unit scale");
    }
    private static void TestLegacyRebasePreservesVisiblePose()
    {
        var scene = new ByteEngine.Core.Scene.Scene("Legacy Import Rebase");
        GameObject parent = scene.CreateGameObject("Character");
        GameObject modelRoot = scene.CreateGameObject("Model");
        modelRoot.SetParent(parent, false);
        modelRoot.Transform.LocalPosition = new Vector3(0.3f, 0.4f, 0.5f);
        modelRoot.Transform.EulerAngles = new Vector3(0, 180, 0);
        modelRoot.Transform.LocalScale = Vector3.One * 0.015f;
        var marker = modelRoot.AddComponent(new ModelHierarchyInstance
        {
            AppliedImportScale = 0.01f
        });
        GameObject meshNode = scene.CreateGameObject("Mesh");
        meshNode.SetParent(modelRoot, false);
        meshNode.Transform.LocalPosition = new Vector3(12, 80, 3);
        GameObject socketChild = scene.CreateGameObject("Weapon");
        socketChild.SetParent(modelRoot, false);
        socketChild.ParentSocket = "Weapon_R";
        socketChild.Transform.LocalPosition = new Vector3(2, 3, 4);
        Matrix4x4 socketBefore = socketChild.Transform.WorldMatrix;
        Matrix4x4 before = meshNode.Transform.WorldMatrix;
        var fbx = new Assimp.Scene();
        fbx.Metadata.Add("UnitScaleFactor", new Metadata.Entry(MetaDataType.Double, 1d));
        SetAxis(fbx, "UpAxis", 1, 1);
        SetAxis(fbx, "FrontAxis", 2, 1);
        ModelAsset imported = new(ImportedModelSpace.Apply(
            HeightModel(180), ImportedModelSpace.FbxCorrection(fbx, 1f)));

        Assert(EditorSceneCommands.RefreshImportSpace(modelRoot, imported),
            "Legacy hierarchy gains one internal correction");
        Assert(modelRoot.Children.Count(child => child.Name == "Import Space") == 1 &&
            marker.AppliedImportScale == 1f,
            "Legacy marker records new single-owner correction");
        foreach (Vector3 point in new[] { Vector3.Zero, new Vector3(1, 2, 3) })
            AssertNear(Vector3.Transform(point, meshNode.Transform.WorldMatrix),
                Vector3.Transform(point, before), "Legacy visible mesh pose survives rebase");
        Assert(ReferenceEquals(socketChild.Parent, modelRoot) &&
            socketChild.ParentSocket == "Weapon_R",
            "Socket attachment stays directly parented to Model");
        AssertNear(Vector3.Transform(Vector3.Zero, socketChild.Transform.WorldMatrix),
            Vector3.Transform(Vector3.Zero, socketBefore),
            "Socket attachment world pose survives migration");
        Assert(!EditorSceneCommands.RefreshImportSpace(modelRoot, imported),
            "Legacy import-space rebase runs exactly once");
    }
    private static void SetAxis(Assimp.Scene scene, string key, int component, int sign)
    {
        scene.Metadata.Add(key, new Metadata.Entry(MetaDataType.Int32, component));
        scene.Metadata.Add(key + "Sign", new Metadata.Entry(MetaDataType.Int32, sign));
    }

    private static ImportedModel HeightModel(float height) => new()
    {
        Name = "Height Probe",
        Nodes = new List<ImportedNode>
        {
            new() { Key = "mesh", Name = "Mesh", MeshKeys = new List<string> { "mesh" } }
        },
        Meshes = new List<ImportedMesh>
        {
            new()
            {
                Key = "mesh",
                Vertices = new float[]
                {
                    0, 0, 0, 0, 1, 0, 0, 0,
                    0, height, 0, 0, 1, 0, 0, 1,
                    0.1f, height, 0, 0, 1, 0, 1, 1
                },
                Indices = new uint[] { 0, 1, 2 }
            }
        }
    };

    private static float Height(ImportedModel imported)
    {
        Matrix4x4 correction = imported.Nodes[0].Key == ImportedModelSpace.CorrectionNodeKey
            ? imported.Nodes[0].LocalTransform : Matrix4x4.Identity;
        float sourceHeight = imported.Meshes[0].Vertices[9];
        return Vector3.Transform(new Vector3(0, sourceHeight, 0), correction).Y -
            Vector3.Transform(Vector3.Zero, correction).Y;
    }

    private static void AssertNear(float actual, float expected, string message)
    {
        if (MathF.Abs(actual - expected) > 0.0005f)
            throw new InvalidOperationException(message + $": {actual} != {expected}");
    }

    private static void AssertNear(Vector3 actual, Vector3 expected, string message)
    {
        if (Vector3.Distance(actual, expected) > 0.0005f)
            throw new InvalidOperationException(message + $": {actual} != {expected}");
    }

    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
