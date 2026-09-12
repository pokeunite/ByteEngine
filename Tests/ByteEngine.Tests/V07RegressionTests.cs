using System.Numerics;

using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Editor;

namespace ByteEngine.Tests;

internal static class V07RegressionTests
{
    public static void Run(
        string root,
        AssetDatabase database,
        AssetManager assets)
    {
        TestVisualLogicExecution();
        TestCharacterGroundingAndJumpAssists();
        TestHierarchyScaleAnalysis(root);
        TestGeneratedFbxImport(root);
        TestModelInstancePersistence(root, database, assets);
    }

    private static void TestVisualLogicExecution()
    {
        var globals =
            new VariableStore();

        globals.Set(
            "Score",
            VariableValue.FromNumber(1));

        globals.Set(
            "BranchResult",
            VariableValue.FromNumber(0));

        var scene =
            new Scene("Visual Logic Test");

        GameObject self =
            scene.CreateGameObject("Self");

        var addScore =
            new VisualInstruction
            {
                Id = "variable.add",
                Arguments =
                    new Dictionary<string, EventValue>
                    {
                        ["target"] =
                            Reference(
                                VariableScope.Global,
                                "Score"),

                        ["amount"] =
                            EventValue.Number(2)
                    }
            };

        var triggerOnce =
            new VisualInstruction
            {
                Id = "system.triggerOnce"
            };

        var triggerRule =
            new EventRuleDefinition
            {
                Conditions =
                    new List<VisualInstruction>
                    {
                        triggerOnce
                    },

                Actions =
                    new List<VisualInstruction>
                    {
                        addScore
                    }
            };

        var module =
            new EventModuleDefinition
            {
                Name = "Runtime Regression",
                Rules =
                    new List<EventRuleDefinition>
                    {
                        triggerRule
                    }
            };

        var runtime =
            new EventModuleRuntime();

        runtime.Update(
            module,
            globals,
            scene,
            self);

        runtime.Update(
            module,
            globals,
            scene,
            self);

        Assert(
            globals["Score"].Number == 3,
            "Trigger Once executes its action only once while latched");

        var branch =
            new VisualInstruction
            {
                Id = "flow.branch",
                Arguments =
                    new Dictionary<string, EventValue>
                    {
                        ["condition"] =
                            EventValue.Boolean(true)
                    }
            };

        VisualInstruction trueAction =
            SetGlobalNumber(
                "BranchResult",
                7);

        VisualInstruction falseAction =
            SetGlobalNumber(
                "BranchResult",
                -7);

        branch.TrueActionId =
            trueAction.InstanceId;

        branch.FalseActionId =
            falseAction.InstanceId;

        module.Rules =
            new List<EventRuleDefinition>
            {
                new()
                {
                    HasExplicitExecutionFlow = true,
                    FirstActionId = branch.InstanceId,
                    Actions =
                        new List<VisualInstruction>
                        {
                            branch,
                            trueAction,
                            falseAction
                        }
                }
            };

        runtime.Reset();
        runtime.Update(
            module,
            globals,
            scene,
            self);

        Assert(
            globals["BranchResult"].Number == 7,
            "Execution branch follows the true output");

        var firstCondition =
            new VisualInstruction
            {
                Id = "system.always"
            };

        var secondCondition =
            new VisualInstruction
            {
                Id = "system.always"
            };

        var andGate =
            new VisualInstruction
            {
                Id = "logic.and",
                ConditionInputIds =
                    new List<Guid>
                    {
                        firstCondition.InstanceId,
                        secondCondition.InstanceId
                    }
            };

        VisualInstruction explicitAction =
            SetGlobalNumber(
                "BranchResult",
                11);

        module.Rules =
            new List<EventRuleDefinition>
            {
                new()
                {
                    HasExplicitConditionFlow = true,
                    ConnectedConditionIds =
                        new List<Guid>
                        {
                            andGate.InstanceId
                        },
                    Conditions =
                        new List<VisualInstruction>
                        {
                            firstCondition,
                            secondCondition,
                            andGate
                        },
                    Actions =
                        new List<VisualInstruction>
                        {
                            explicitAction
                        }
                }
            };

        runtime.Reset();
        runtime.Update(
            module,
            globals,
            scene,
            self);

        Assert(
            globals["BranchResult"].Number == 11,
            "Explicit AND condition graph reaches its action");
    }

    private static void TestCharacterGroundingAndJumpAssists()
    {
        var scene =
            new Scene("Controller Test");

        GameObject ground =
            scene.CreateGameObject("Finite Ground");

        ground.AddComponent(
            new BoxCollider3D
            {
                Size =
                    new Vector3(
                        2.0f,
                        0.2f,
                        2.0f)
            });

        ground.AddComponent(
            new GroundSurface());

        GameObject character =
            scene.CreateGameObject("Character");

        character.Transform.WorldPosition =
            new Vector3(
                0.0f,
                1.1f,
                0.0f);

        character.AddComponent(
            new CapsuleCollider3D());

        CharacterController3D controller =
            character.AddComponent(
                new CharacterController3D
                {
                    CoyoteTime = 0.15f,
                    JumpBuffer = 0.15f
                });

        scene.LoadInternal();
        UpdateScene(
            scene,
            1.0 / 60.0);

        Assert(
            controller.IsGrounded,
            "Character grounds inside finite collider bounds");

        character.Transform.WorldPosition =
            new Vector3(
                3.0f,
                1.1f,
                0.0f);

        controller.SetVelocity(
            Vector3.Zero);

        UpdateScene(
            scene,
            1.0 / 60.0);

        Assert(
            !controller.IsGrounded,
            "Character does not ground outside finite collider bounds");

        controller.Jump();
        UpdateScene(
            scene,
            1.0 / 60.0);

        Assert(
            controller.VerticalVelocity > 0.0f,
            "Coyote time permits a jump just after leaving ground");

        scene.UnloadInternal();

        var bufferedScene =
            new Scene("Buffered Jump Test");

        GameObject bufferedGround =
            bufferedScene.CreateGameObject("Ground");

        bufferedGround.AddComponent(
            new BoxCollider3D
            {
                Size =
                    new Vector3(
                        2.0f,
                        0.2f,
                        2.0f)
            });

        bufferedGround.AddComponent(
            new GroundSurface());

        GameObject bufferedCharacter =
            bufferedScene.CreateGameObject("Character");

        bufferedCharacter.Transform.WorldPosition =
            new Vector3(
                0.0f,
                1.35f,
                0.0f);

        bufferedCharacter.AddComponent(
            new CapsuleCollider3D());

        CharacterController3D bufferedController =
            bufferedCharacter.AddComponent(
                new CharacterController3D
                {
                    Gravity = 0.0f,
                    GroundDistance = 0.06f,
                    JumpBuffer = 0.2f
                });

        bufferedController.SetVelocity(
            new Vector3(
                0.0f,
                -2.0f,
                0.0f));

        bufferedController.Jump();
        bufferedScene.LoadInternal();
        UpdateScene(
            bufferedScene,
            0.1);

        Assert(
            bufferedController.VerticalVelocity > 0.0f,
            "Buffered jump fires when the character lands");

        bufferedScene.UnloadInternal();
    }

    private static void TestHierarchyScaleAnalysis(
        string root)
    {
        Guid guid =
            Guid.NewGuid();

        const string meshKey =
            "mesh";

        var imported =
            new ImportedModel
            {
                Guid = guid,
                SourceAssetGuid = guid,
                Meshes =
                    new List<ImportedMesh>
                    {
                        new()
                        {
                            Key = meshKey,
                            Vertices =
                                new float[]
                                {
                                    0, 0, 0, 0, 1, 0, 0, 0,
                                    1, 0, 0, 0, 1, 0, 1, 0,
                                    0, 1, 0, 0, 1, 0, 0, 1
                                },
                            Indices =
                                new uint[]
                                {
                                    0, 1, 2
                                }
                        }
                    },
                Nodes =
                    new List<ImportedNode>
                    {
                        new()
                        {
                            Key = "node",
                            LocalTransform =
                                Matrix4x4.CreateScale(0.01f),
                            MeshKeys =
                                new List<string>
                                {
                                    meshKey
                                }
                        }
                    }
            };

        var metadata =
            new AssetMetadata
            {
                Guid = guid,
                Type = AssetType.Model3D,
                ModelImporter =
                    new ModelImporterSettings
                    {
                        ImportScale = 0.01f
                    }
            };

        var asset =
            new AssetRecord(
                guid,
                AssetType.Model3D,
                "Assets/Test.fbx",
                Path.Combine(
                    root,
                    "Assets",
                    "Test.fbx"),
                Path.Combine(
                    root,
                    "Assets",
                    "Test.fbx.meta"),
                metadata);

        Assert(
            ModelImporter.ForPath(
                asset.FullPath) is FbxModelImporter,
            "FBX assets dispatch to the Assimp importer");

        ModelScaleAnalysis analysis =
            ModelImportScaleUtility.Analyze(
                asset,
                new ModelAsset(imported));

        Assert(
            analysis.Normalized &&
            MathF.Abs(
                analysis.FinalLargestDimension -
                1.0f) <
            0.0001f,
            "FBX hierarchy scale normalization uses transformed bounds");
    }

    private static void TestModelInstancePersistence(
        string root,
        AssetDatabase database,
        AssetManager assets)
    {
        Guid modelGuid =
            Guid.NewGuid();

        var scene =
            new Scene("Model Instance Persistence");

        GameObject instanceRoot =
            scene.CreateGameObject("Model");

        instanceRoot.AddComponent(
            new ModelHierarchyInstance
            {
                Model =
                    new AssetReference(
                        modelGuid,
                        "Assets/Model.fbx"),
                AppliedImportScale = 0.5f
            });

        var serializer =
            new SceneSerializer(
                new ComponentSerializer(
                    root,
                    database,
                    assets));

        Scene clone =
            serializer.CloneForRuntime(
                scene);

        ModelHierarchyInstance? restored =
            clone.FindGameObject("Model")?
                .GetComponent<ModelHierarchyInstance>();

        Assert(
            restored?.Model.Guid == modelGuid &&
            MathF.Abs(
                restored.AppliedImportScale -
                0.5f) <
            0.0001f,
            "Imported model hierarchy marker persists per instance");
    }

    private static void TestGeneratedFbxImport(
        string root)
    {
        using var context =
            new Assimp.AssimpContext();

        Assimp.ExportFormatDescription? format =
            context.GetSupportedExportFormats()
                .FirstOrDefault(
                    candidate =>
                        candidate.FileExtension.Equals(
                            "fbx",
                            StringComparison.OrdinalIgnoreCase));

        if (format ==
            null)
        {
            throw new InvalidOperationException(
                "FAILED: bundled Assimp library does not expose an FBX exporter for regression fixtures");
        }

        var source =
            new Assimp.Scene("Generated FBX");

        source.RootNode =
            new Assimp.Node("Root");

        var meshNode =
            new Assimp.Node(
                "Triangle",
                source.RootNode);

        meshNode.MeshIndices.Add(0);
        source.RootNode.Children.Add(
            meshNode);

        var mesh =
            new Assimp.Mesh(
                "Triangle",
                Assimp.PrimitiveType.Triangle);

        mesh.Vertices.Add(
            new Vector3(0, 0, 0));
        mesh.Vertices.Add(
            new Vector3(1, 0, 0));
        mesh.Vertices.Add(
            new Vector3(0, 1, 0));
        mesh.Faces.Add(
            new Assimp.Face(
                new[]
                {
                    0, 1, 2
                }));
        mesh.MaterialIndex =
            0;

        source.Meshes.Add(
            mesh);

        source.Materials.Add(
            new Assimp.Material
            {
                Name = "Triangle Material"
            });

        string path =
            Path.Combine(
                root,
                "Assets",
                "GeneratedTriangle.fbx");

        context.ExportFile(
            source,
            path,
            format.FormatId);

        Guid guid =
            Guid.NewGuid();

        var metadata =
            new AssetMetadata
            {
                Guid = guid,
                Type = AssetType.Model3D
            };

        var record =
            new AssetRecord(
                guid,
                AssetType.Model3D,
                "Assets/GeneratedTriangle.fbx",
                path,
                path + ".meta",
                metadata);

        ImportedModel imported =
            new FbxModelImporter()
                .Import(
                    record,
                    metadata.ModelImporter);

        Assert(
            imported.Meshes.Count == 1 &&
            imported.Meshes[0].Indices.Length == 3 &&
            imported.Nodes.Any(
                node =>
                    node.MeshKeys.Count == 1),
            "Generated FBX parses through the production importer");
    }

    private static EventValue Reference(
        VariableScope scope,
        string memberName)
    {
        return EventValue.FromReference(
            new VariableReference
            {
                Scope = scope,
                MemberName = memberName
            });
    }

    private static VisualInstruction SetGlobalNumber(
        string name,
        double value)
    {
        return new VisualInstruction
        {
            Id = "variable.set",
            Arguments =
                new Dictionary<string, EventValue>
                {
                    ["target"] =
                        Reference(
                            VariableScope.Global,
                            name),

                    ["value"] =
                        EventValue.Number(value)
                }
        };
    }

    private static void UpdateScene(
        Scene scene,
        double deltaTime)
    {
        Time.Update(
            deltaTime);

        scene.UpdateInternal();
    }

    private static void Assert(
        bool condition,
        string name)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                "FAILED: " +
                name);
        }
    }
}
