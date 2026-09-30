using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Classification;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.Serialization.SerializationModels;

namespace ByteEngine.Mcp.Authoring;

internal static class PlayablePreset
{
    public static (GameObjectData Root, List<GameObjectData> Children) Build(
        string name, string view, AssetRecord modelAsset, AssetManager assets,
        ClassificationSettings classification,
        SceneSerializer serializer)
    {
        string preset = view.ToLowerInvariant().Replace("-", "").Replace("_", "");
        if (preset is not ("tps" or "fps" or "topdown" or "isometric"))
            throw new McpFault("INVALID_REQUEST", "View must be tps, fps, topdown, or isometric.");

        AssetReference reference = new(modelAsset.Guid, modelAsset.ProjectPath);
        ModelAsset imported = assets.LoadModel(reference);
        if (imported.Meshes.Count == 0)
            throw new McpFault("INVALID_ASSET_TYPE", "Choose a model with renderable meshes.");

        var scene = new Scene("MCP Player Authoring", classification);
        GameObject player = scene.CreateGameObject(name);
        if (classification.FindTag("Player") is { } playerTag) player.AddTag(playerTag.Id);
        if (classification.FindLayer("Player") is { } playerLayer) player.Layer = playerLayer.Index;
        player.AddComponent(new CapsuleCollider3D { Radius = .5f, Height = 2f, Center = new Vector3(0, 1, 0) });
        player.AddComponent(new CharacterController3D());
        player.AddComponent(new AnimationController());
        player.AddComponent(new HealthComponent { MaxHealth = 100, CurrentHealth = 100 });
        player.AddComponent(new ProjectileLauncher3D {
            Damage = 20, ProjectileSpeed = 45, FireCooldown = .18f,
            MuzzleOffset = new Vector3(0, 1.35f, -.35f)
        });
        var controller = player.AddComponent(new PlayerController3D {
            UseLocalOrientation = false, CharacterRotation = CharacterRotationMode.FaceCamera,
            TurnSpeed = 540
        });
        var shooter = player.AddComponent(new PlayerShooter3D());
        var boom = player.AddComponent(new CameraBoom3D {
            CameraLagEnabled = false, RotationLagEnabled = false
        });

        GameObject camera = scene.CreateGameObject("Camera");
        camera.SetParent(player, false);
        var cameraComponent = camera.AddComponent(new Camera3D());
        boom.CameraObjectId = camera.Id;

        GameObject modelRoot = scene.CreateGameObject("Model");
        modelRoot.SetParent(preset == "fps" ? camera : player, false);
        modelRoot.AddComponent(new ModelHierarchyInstance { Model = reference, AppliedImportScale = 1 });
        var nodes = imported.Nodes.ToDictionary(node => node.Key,
            node => scene.CreateGameObject(node.Name), StringComparer.Ordinal);
        foreach (var node in imported.Nodes)
        {
            GameObject item = nodes[node.Key];
            GameObject parent = node.ParentKey != null && nodes.TryGetValue(node.ParentKey, out GameObject? found)
                ? found : modelRoot;
            item.SetParent(parent, false);
            if (Matrix4x4.Decompose(node.LocalTransform, out Vector3 scale,
                    out Quaternion rotation, out Vector3 position))
            {
                item.Transform.LocalPosition = position;
                item.Transform.LocalRotation = rotation;
                item.Transform.LocalScale = scale;
            }
            foreach (string meshKey in node.MeshKeys)
            {
                var mesh = imported.Meshes.First(m => m.Key == meshKey);
                GameObject meshObject = node.MeshKeys.Count == 1 ? item : scene.CreateGameObject(mesh.Name);
                if (meshObject != item) meshObject.SetParent(item, false);
                var renderer = new MeshRenderer {
                    MeshReference = new ModelMeshReference(reference, meshKey),
                    Mesh = assets.GetModelMesh(reference, meshKey)
                };
                if (mesh.MaterialKey != null)
                {
                    renderer.MaterialReference = new ModelMaterialReference(reference, mesh.MaterialKey);
                    renderer.Material = assets.GetModelMaterial(reference, mesh.MaterialKey);
                }
                meshObject.AddComponent(renderer);
            }
        }

        shooter.AimAtPointer = preset is "topdown" or "isometric";
        controller.AcceptLookInput = preset is "tps" or "fps";
        boom.FirstPerson = preset == "fps";
        switch (preset)
        {
            case "fps":
                boom.UseControlRotation = true; boom.ArmLength = 0; boom.PivotHeight = 1.65f;
                boom.ShoulderOffset = 0; boom.MinPitch = -89; boom.MaxPitch = 89;
                boom.EnableCameraCollision = false;
                cameraComponent.NearClip = .01f;
                camera.Transform.LocalPosition = new Vector3(0, 1.65f, 0);
                break;
            case "topdown":
                controller.CharacterRotation = CharacterRotationMode.Independent;
                boom.UseControlRotation = false; boom.Pitch = 75; boom.MinPitch = 75; boom.MaxPitch = 75;
                boom.ArmLength = 11; boom.PivotHeight = 1; boom.ShoulderOffset = 0;
                break;
            case "isometric":
                controller.CharacterRotation = CharacterRotationMode.Independent;
                boom.UseControlRotation = false; boom.Yaw = 45; boom.Pitch = 40;
                boom.MinPitch = 40; boom.MaxPitch = 40; boom.ArmLength = 10;
                boom.PivotHeight = 1; boom.ShoulderOffset = 0;
                break;
            default:
                boom.UseControlRotation = true; boom.MinPitch = -40; boom.MaxPitch = 65;
                boom.ArmLength = 4.75f; boom.PivotHeight = 1.6f; boom.ShoulderOffset = .45f;
                break;
        }
        SceneData data = serializer.Serialize(scene);
        return (data.GameObjects.Single(x => x.Id == player.Id),
            data.GameObjects.Where(x => x.Id != player.Id).ToList());
    }
}
