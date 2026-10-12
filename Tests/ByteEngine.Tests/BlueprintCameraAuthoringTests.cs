using System.Numerics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Editor;

namespace ByteEngine.Tests;

internal static class BlueprintCameraAuthoringTests
{
    public static void CheckProject(string folder)
    {
        using var project = EditorProjectContext.Open(Path.Combine(folder, "fps template.byteproject"), Console.WriteLine);
        var serializer = project.Scenes;
        var blueprint = new ByteEngine.Core.Blueprints.BlueprintSerializer().Load(Path.Combine(folder, "Assets/Blueprints/FPSController.byteblueprint"));
        var data = new ByteEngine.Core.Serialization.SerializationModels.SceneData { Name = "FPS Blueprint", GameObjects = [blueprint.Root, .. blueprint.Children] };
        foreach (var scene in new[] { serializer.Deserialize(data), serializer.Load(Path.Combine(folder, "Scenes/Main.bytescene")) })
        {
            var player = scene.GameObjects.Single(o => o.GetComponent<ByteEngine.Core.Gameplay.FirstPersonCamera3D>() != null);
            var rig = player.GetComponent<ByteEngine.Core.Gameplay.FirstPersonCamera3D>()!;
            var camera = scene.FindGameObject(rig.CameraObjectId)!;
            var model = camera.Children.Single(o => o.Name == "Model");
            var controller = player.GetComponent<ByteEngine.Core.Gameplay.PlayerController3D>()!;
            Vector3 eye = camera.Transform.WorldPosition, arms = model.Transform.WorldPosition;
            float near = camera.GetComponent<ByteEngine.Core.Graphics.Camera3D>()!.NearClip;
            controller.ControlYaw = 0; controller.ControlPitch = 0; rig.SnapToView();
            if (Vector3.Distance(camera.Transform.WorldPosition, eye) > .0001f || Vector3.Distance(model.Transform.WorldPosition, arms) > .0001f)
                throw new Exception("Project FPS eye/arms pose changed on runtime activation.");
            controller.ControlPitch = -60; rig.SnapToView();
            if (Vector3.Distance(camera.Transform.WorldPosition, eye) > .0001f || Vector3.Distance(model.Transform.WorldPosition, arms) < .01f ||
                camera.GetComponent<ByteEngine.Core.Graphics.Camera3D>()!.NearClip != near)
                throw new Exception("Project FPS pitch must rotate arms while preserving eye position and clipping.");
            Console.WriteLine("PASS Actual FPS project camera and arms: " + scene.Name);
        }
    }
    public static void Run(string root)
    {
        var camera = new EditorCamera3D { Position = new(2, 3, 4), Yaw = 37, Pitch = -12, FieldOfView = 72 };
        var state = BlueprintViewportState.Capture(camera);
        string path = Path.Combine(root, "views.json");
        Guid asset = Guid.NewGuid();
        new BlueprintViewportStateStore(path).Save(root, asset, camera);
        camera.Reset();
        if (!new BlueprintViewportStateStore(path).Restore(root, asset, camera) || BlueprintViewportState.Capture(camera) != state)
            throw new Exception("Blueprint editor view must persist across editor sessions.");
        if (new BlueprintViewportStateStore(path).Restore(root + "other", asset, camera))
            throw new Exception("Blueprint views must be scoped to their project.");
        File.WriteAllText(path, "broken preferences");
        if (new BlueprintViewportStateStore(path).Restore(root, asset, camera))
            throw new Exception("Corrupt navigation preferences must fall back safely.");
        var bounds = new BoundingBox3D(new(-2, -1, -3), new(2, 1, 3));
        foreach (float aspect in new[] { .4f, 1f, 2.5f })
        {
            camera.Frame(bounds, aspect);
            if (camera.Yaw != 37 || camera.Pitch != -12) throw new Exception("Framing must preserve navigation angles.");
            Matrix4x4 vp = camera.View * camera.Projection(aspect);
            foreach (float x in new[] { -2f, 2f })
            foreach (float y in new[] { -1f, 1f })
            foreach (float z in new[] { -3f, 3f })
            {
                Vector4 clip = Vector4.Transform(new Vector4(x, y, z, 1), vp);
                if (clip.W <= 0 || Math.Abs(clip.X / clip.W) > 1 || Math.Abs(clip.Y / clip.W) > 1)
                    throw new Exception("Blueprint framing must fit geometry in narrow and wide viewports.");
            }
        }
        Console.WriteLine("PASS Blueprint view persistence, project isolation, corrupt preference recovery and aspect-aware framing.");
    }
}
