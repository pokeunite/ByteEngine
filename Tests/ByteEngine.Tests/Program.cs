using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.Variables;
using ByteEngine.Editor;
using ByteEngine.Editor.Gizmos;
using ByteEngine.Editor.Panels;
using ByteEngine.Tests;

string root = Path.Combine(Path.GetTempPath(), "ByteEngine-v05-tests-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Path.Combine(root, "Assets")); Directory.CreateDirectory(Path.Combine(root, "Scenes"));
try
{
    using var database = new AssetDatabase(root, new[] { "Assets", "Scenes" }); using var assets = new AssetManager(database);
    var serializer = new SceneSerializer(new ComponentSerializer(root, database, assets));

    var scene = new Scene("3D Test"); scene.Variables.Set("Wave", VariableValue.FromNumber(2)); scene.Variables.Set("Wind", VariableValue.FromVector3(new(1, 2, 3)));
    GameObject cube = scene.CreateGameObject("Cube"); cube.Transform.LocalPosition = new(1, 2, 3); cube.Transform.EulerAngles = new(10, 20, 30); cube.Variables.Set("Health", VariableValue.FromNumber(100)); cube.AddComponent(new MeshRenderer { Primitive = PrimitiveMeshType.Sphere, Material = new Material { BaseColor = new(.2f, .4f, .8f, 1) } }); cube.AddComponent(new CharacterController3D { MoveSpeed = 8f, JumpForce = 11f, AirControl = .6f, SnapToGround = false }); cube.AddComponent(new CapsuleCollider3D { Radius = .7f, Height = 2.4f, Center = new Vector3(0, 1, 0), IsTrigger = true }); cube.AddComponent(new AnimationController { Idle = "Stand", Run = "Sprint", RunThreshold = 5.5f });
    GameObject camera = scene.CreateGameObject("Main Camera"); camera.Transform.LocalPosition = new(0, 2, 6); camera.AddComponent(new Camera3D { FieldOfView = 70 });
    GameObject ground = scene.CreateGameObject("Ground"); ground.Transform.LocalPosition = new(0, -1, 0); ground.AddComponent(new MeshRenderer { Primitive = PrimitiveMeshType.Plane }); ground.AddComponent(new GroundSurface { SurfaceType = "Concrete" });
    GameObject lightObject = scene.CreateGameObject("Directional Light"); lightObject.Transform.EulerAngles = new(45, -35, 0); lightObject.AddComponent(new DirectionalLight { Intensity = 1.2f, AmbientIntensity = .3f });
    Scene clone = serializer.CloneForRuntime(scene); Assert(clone.FindGameObject("Cube")?.GetComponent<MeshRenderer>()?.Primitive == PrimitiveMeshType.Sphere, "MeshRenderer round-trip"); Assert(clone.FindComponent<Camera3D>()?.FieldOfView == 70, "Camera3D round-trip"); Assert(clone.FindComponent<DirectionalLight>()?.AmbientIntensity == .3f, "DirectionalLight ambient round-trip"); CharacterController3D? clonedController = clone.FindGameObject("Cube")?.GetComponent<CharacterController3D>(); Assert(clonedController?.MoveSpeed == 8f && clonedController.JumpForce == 11f && clonedController.AirControl == .6f && !clonedController.SnapToGround, "CharacterController3D round-trip"); CapsuleCollider3D? clonedCapsule = clone.FindGameObject("Cube")?.GetComponent<CapsuleCollider3D>(); Assert(clonedCapsule?.Radius == .7f && clonedCapsule.Height == 2.4f && clonedCapsule.IsTrigger, "CapsuleCollider3D round-trip"); AnimationController? clonedAnimation = clone.FindGameObject("Cube")?.GetComponent<AnimationController>(); Assert(clonedAnimation?.Idle == "Stand" && clonedAnimation.Run == "Sprint" && clonedAnimation.RunThreshold == 5.5f, "AnimationController round-trip"); Assert(clone.FindGameObject("Ground")?.GetComponent<GroundSurface>()?.SurfaceType == "Concrete", "GroundSurface round-trip"); Assert(clone.Variables.TryGet("Wave", out var wave) && wave!.Number == 2, "Scene variable round-trip"); Assert(clone.Variables["Wind"].Vector3 == new Vector3(1, 2, 3), "Vector3 variable round-trip");
    string scenePath = Path.Combine(root, "Scenes", "RoundTrip.bytescene"); serializer.Save(scene, scenePath); Scene diskScene = serializer.Load(scenePath); Assert(diskScene.Variables["Wind"].Vector3 == new Vector3(1, 2, 3), "Vector3 disk persistence");
    string projectPath = Path.Combine(root, "Test.byteproject"); var projectData = new ProjectData { Name = "Test", GlobalVariables = { new VariableData { Name = "Spawn", Value = VariableValue.FromVector3(new(4, 5, 6)) } } }; var projectSerializer = new ProjectSerializer(); projectSerializer.Save(projectData, projectPath); Assert(projectSerializer.Load(projectPath).GlobalVariables[0].Value.Vector3 == new Vector3(4, 5, 6), "Global default disk persistence");
    clone.Variables["Wave"].Number = 9; clone.FindGameObject("Cube")!.Variables["Health"].Number = 1; Assert(scene.Variables["Wave"].Number == 2 && cube.Variables["Health"].Number == 100, "Play clone isolation");

    var legacy = new SceneData { Name = "Legacy", SceneId = Guid.NewGuid(), GameObjects = { new GameObjectData { Id = Guid.NewGuid(), Name = "Legacy Sprite", Transform = new TransformData { Position = new Vector2Data { X = 5, Y = 7 }, Rotation = 45, Size = new Vector2Data { X = 32, Y = 48 } }, Components = { new ComponentData { Type = "SpriteRenderer" } } } } };
    Scene migrated = serializer.Deserialize(legacy); GameObject legacyObject = migrated.GameObjects[0]; Assert(legacyObject.Transform.LocalPosition == new Vector3(5, 7, 0), "v0.4 position migration"); Assert(legacyObject.GetComponent<SpriteRenderer>()?.Size == new Vector2(32, 48), "v0.4 size migration");

    var child = scene.CreateGameObject("Child"); child.Transform.WorldPosition = new(2, 0, 0); child.SetParent(cube, true); Assert(Vector3.Distance(child.Transform.WorldPosition, new(2, 0, 0)) < .001f, "3D parenting preserves world position");
    var globals = new VariableStore(); globals.Set("Score", VariableValue.FromNumber(10)); var context = new VariableResolutionContext { Globals = globals, Scene = scene, Self = cube };
    Assert(VariableResolver.TryGet(new() { Scope = VariableScope.Global, MemberName = "Score" }, context, out object? score) && Convert.ToDouble(score) == 10, "Global resolver"); Assert(VariableResolver.TryGet(new() { Scope = VariableScope.Self, MemberName = "Health" }, context, out object? health) && Convert.ToDouble(health) == 100, "Self resolver"); Assert(VariableResolver.TryGet(new() { Scope = VariableScope.Component, ObjectId = camera.Id, ComponentType = "Camera3D", MemberName = "FieldOfView" }, context, out object? fov) && Convert.ToSingle(fov) == 70, "Component resolver");
    Assert(VariableResolver.TrySet(new() { Scope = VariableScope.Component, ComponentType = "Transform", MemberName = "LocalPosition.X" }, context, 42f) && cube.Transform.LocalPosition == new Vector3(42, 2, 3), "Nested Vector3 field write-back");
    Assert(VariableResolver.TrySet(new() { Scope = VariableScope.Component, ObjectId = lightObject.Id, ComponentType = "DirectionalLight", MemberName = "Color.Y" }, context, .4f) && Math.Abs(lightObject.GetComponent<DirectionalLight>()!.Color.Y - .4f) < .0001f, "Nested component struct write-back");

    var editorCamera = new EditorCamera3D(); Vector3 contextPosition = Gizmo3DController.ScreenToGroundPlane(new Vector2(400, 300), editorCamera, Vector2.Zero, new Vector2(800, 600)); Assert(Math.Abs(contextPosition.Y) < .0001f && float.IsFinite(contextPosition.X) && float.IsFinite(contextPosition.Z), "3D context placement intersects ground plane");

    string incoming = Path.Combine(root, "Incoming"); Directory.CreateDirectory(incoming); string fbx = Path.Combine(incoming, "character.fbx"); File.WriteAllText(fbx, "FBX test payload");
    string requestedProject = Path.Combine(root, "ImportProject.byteproject"); using EditorProjectContext editorProject = EditorProjectContext.Create(requestedProject, _ => { }); var editorLog = new EditorLog(); var externalImporter = new ExternalAssetImporter(editorProject, editorLog);
    IReadOnlyList<AssetRecord> firstImport = externalImporter.Import(new[] { fbx }); IReadOnlyList<AssetRecord> secondImport = externalImporter.Import(new[] { fbx });
    Assert(firstImport.Count == 1 && firstImport[0].Type == AssetType.Model3D && File.Exists(firstImport[0].FullPath) && File.Exists(firstImport[0].MetaPath), "External FBX import and registration");
    Assert(secondImport.Count == 1 && !string.Equals(firstImport[0].FullPath, secondImport[0].FullPath, StringComparison.OrdinalIgnoreCase), "External import collision naming");

    Scene cleanTemplate = ProjectTemplateFactory.Create(ProjectTemplate.Clean); Scene starterTemplate = ProjectTemplateFactory.Create(ProjectTemplate.Starter3D);
    Assert(cleanTemplate.Name == "Main" && cleanTemplate.GameObjectCount == 0, "Clean project template");
    Assert(starterTemplate.GameObjectCount == 4 && starterTemplate.FindComponent<Camera3D>() != null && starterTemplate.FindComponent<DirectionalLight>() != null && starterTemplate.FindGameObject("Ground")?.GetComponent<GroundSurface>() != null, "3D starter project template");
    Assert(starterTemplate.FindComponent<DirectionalLight>()!.Direction.Y < 0, "Starter directional light points toward the ground");

    string gltfPath = Path.Combine(root, "Assets", "Triangle.gltf"); WriteTriangleGltf(gltfPath); database.Scan(); Assert(database.TryGetAsset("Assets/Triangle.gltf", out AssetRecord? gltfRecord) && gltfRecord != null, "GLTF asset registration"); Guid stableModelGuid = gltfRecord!.Guid; ImportedModel imported = new GltfModelImporter().Import(gltfRecord, gltfRecord.Metadata.ModelImporter); Assert(imported.Nodes.Count == 2 && imported.Meshes.Count == 1 && imported.Meshes[0].Vertices.Length == 24 && imported.Materials.Count == 1, "GLTF hierarchy mesh and material parsing"); ModelAsset loadedModel = assets.LoadModel(new AssetReference(stableModelGuid, gltfRecord.ProjectPath)); Assert(loadedModel.Meshes.Count == 1, "ModelAsset load");
    string glbPath = Path.Combine(root, "Assets", "Triangle.glb"); SharpGLTF.Schema2.ModelRoot.Load(gltfPath).SaveGLB(glbPath); database.Scan(); Assert(database.TryGetAsset("Assets/Triangle.glb", out AssetRecord? glbRecord) && glbRecord != null, "GLB asset registration"); ImportedModel importedGlb = new GltfModelImporter().Import(glbRecord!, glbRecord!.Metadata.ModelImporter); Assert(importedGlb.Nodes.Count == 2 && importedGlb.Meshes.Count == 1, "Binary GLB hierarchy and mesh parsing");
    WriteTriangleGltf(gltfPath); database.Scan(); Assert(database.TryGetAsset(stableModelGuid, out AssetRecord? rescanned) && rescanned != null, "Model GUID stable after source change"); Assert(assets.ReimportModel(stableModelGuid).Guid == stableModelGuid, "Model reimport preserves GUID");

    string blueprintPath = Path.Combine(root, "Assets", "Player.byteblueprint"); var blueprint = new BlueprintDefinition { Name = "Player", Type = BlueprintType.Character, Root = new GameObjectData { Id = Guid.NewGuid(), Name = "Player" }, Variables = { new VariableData { Name = "Health", Value = VariableValue.FromNumber(100) } }, Sockets = { new SocketDefinition { Name = "RightHandSocket", Bone = "hand_r", Position = new Vector3(1, 2, 3), PreviewAssetGuid = stableModelGuid } }, EventModules = { Guid.NewGuid() } }; var blueprintSerializer = new BlueprintSerializer(); blueprintSerializer.Save(blueprint, blueprintPath); BlueprintDefinition loadedBlueprint = blueprintSerializer.Load(blueprintPath); Assert(loadedBlueprint.Type == BlueprintType.Character && loadedBlueprint.Variables[0].Value.Number == 100, "Blueprint variables serialization"); Assert(loadedBlueprint.Sockets[0].Bone == "hand_r" && loadedBlueprint.Sockets[0].PreviewAssetGuid == stableModelGuid, "Blueprint socket serialization"); Assert(loadedBlueprint.EventModules.Count == 1, "Blueprint logic module relationship");
    V07RegressionTests.Run(root, database, assets);
    Console.WriteLine("ByteEngine v0.7 tests passed: visual logic, controller assists, finite grounding, generated FBX import/scale analysis, model instances, GLTF/GLB models, persistence, and asset import.");
}
finally { try { Directory.Delete(root, true); } catch { } }

static void Assert(bool condition, string name) { if (!condition) throw new InvalidOperationException("FAILED: " + name); }

static void WriteTriangleGltf(string path)
{
    using var stream = new MemoryStream(); using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
    {
        foreach (float value in new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f, 0f }) writer.Write(value);
        foreach (float value in new[] { 0f, 0f, 1f, 0f, 0f, 1f, 0f, 0f, 1f }) writer.Write(value);
        foreach (float value in new[] { 0f, 0f, 1f, 0f, 0f, 1f }) writer.Write(value);
        writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)2);
    }
    string data = Convert.ToBase64String(stream.ToArray());
    string json = """
    {"asset":{"version":"2.0"},"buffers":[{"byteLength":102,"uri":"data:application/octet-stream;base64,__DATA__"}],"bufferViews":[{"buffer":0,"byteOffset":0,"byteLength":36,"target":34962},{"buffer":0,"byteOffset":36,"byteLength":36,"target":34962},{"buffer":0,"byteOffset":72,"byteLength":24,"target":34962},{"buffer":0,"byteOffset":96,"byteLength":6,"target":34963}],"accessors":[{"bufferView":0,"componentType":5126,"count":3,"type":"VEC3","min":[0,0,0],"max":[1,1,0]},{"bufferView":1,"componentType":5126,"count":3,"type":"VEC3"},{"bufferView":2,"componentType":5126,"count":3,"type":"VEC2"},{"bufferView":3,"componentType":5123,"count":3,"type":"SCALAR"}],"materials":[{"name":"TriangleMaterial","pbrMetallicRoughness":{"baseColorFactor":[0.2,0.6,1,1],"metallicFactor":0.25,"roughnessFactor":0.7}}],"meshes":[{"name":"Triangle","primitives":[{"attributes":{"POSITION":0,"NORMAL":1,"TEXCOORD_0":2},"indices":3,"material":0}]}],"nodes":[{"name":"Root","children":[1]},{"name":"TriangleNode","mesh":0}],"scenes":[{"nodes":[0]}],"scene":0}
    """.Replace("__DATA__", data);
    File.WriteAllText(path, json);
}
