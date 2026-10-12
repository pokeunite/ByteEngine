using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ByteEngine.Editor;

namespace ByteEngine.Tests;

internal static class FirstPersonComponentTests
{
    public static void Run(string folder)
    {
        TransformRotationTests.Run();
        using var project = EditorProjectContext.Create(Path.Combine(folder, "FPS.byteproject"), Console.WriteLine);
        var scene = new Scene("FPS dedicated component");
        var root = scene.CreateGameObject("Player");
        BlueprintAuthoringService.SetupPlayablePlayer(root, null, PlayerViewPreset.FirstPerson);
        var fps = root.GetComponent<FirstPersonCamera3D>()!;
        if (root.GetComponent<CameraBoom3D>() != null) throw new Exception("FPS setup must not leave a spring arm owner.");
        var camera = scene.FindGameObject(fps.CameraObjectId)!;
        var arms = camera.Children.Single(o => o.Name == "Model");
        camera.Transform.LocalPosition = new(.1f, 1.65f, -.2f);
        arms.Transform.LocalPosition = new(.2f, -.3f, -.6f);
        var controller = root.GetComponent<PlayerController3D>()!;
        controller.AcceptLookInput = false;
        root.GetComponent<ByteEngine.Core.Characters.CharacterController3D>()!.Gravity = 0;
        scene.LoadInternal();
        foreach (float dt in new[] { 1f/30, 1f/60, 1f/144 })
        foreach (float yaw in new[] { -179f, -90f, 0f, 90f, 179f })
        foreach (float pitch in new[] { -89f, 0f, 89f })
        {
            controller.ControlYaw = yaw; controller.ControlPitch = pitch;
            Time.Update(dt); scene.UpdateInternal();
            var expectedEye = Vector3.Transform(new Vector3(.1f,1.65f,-.2f), root.Transform.WorldMatrix);
            if (Vector3.Distance(camera.Transform.WorldPosition, expectedEye) > .0001f ||
                Vector3.Dot(camera.Transform.Forward, CameraBoom3D.CalculateViewForward(yaw,pitch)) < .9999f ||
                Vector3.Dot(root.Transform.Forward, CameraBoom3D.CalculateViewForward(yaw,0)) < .9999f)
                throw new Exception("FPS view and body must follow control without orbit, smoothing or frame-rate drift.");
            if (Vector3.Distance(arms.Transform.LocalPosition, new(.2f,-.3f,-.6f)) > .0001f)
                throw new Exception("FPS input changed authored arm offsets.");
            var renderedForward = Vector3.Normalize(Vector3.TransformNormal(-Vector3.UnitZ, camera.Transform.RenderMatrix));
            var renderedUp = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitY, camera.Transform.RenderMatrix));
            if (Vector3.Dot(renderedForward, camera.Transform.Forward) < .9999f ||
                Vector3.Dot(renderedUp, camera.Transform.Up) < .9999f)
                throw new Exception($"FPS renderer and gameplay rotation disagree at yaw={yaw}, pitch={pitch}; nested rotation order is wrong.");
            var renderedRight = Vector3.Normalize(Vector3.TransformNormal(Vector3.UnitX, camera.Transform.RenderMatrix));
            if (Math.Abs(renderedRight.Y) > .0001f || Vector3.Distance(arms.Transform.WorldPosition, arms.Transform.RenderMatrix.Translation) > .0001f)
                throw new Exception("FPS pitch introduced rendered roll or mismatched view-arm placement.");
        }
        var delta = fps.CalculateLookDelta(new(10,10));
        fps.InvertVerticalLook = true;
        if (Math.Abs(delta.X-1.2f) > .0001f || delta.Y >= 0 || fps.CalculateLookDelta(new(10,10)).Y <= 0)
            throw new Exception("FPS mouse sensitivity/inversion is incorrect.");
        var clone = project.Scenes.CloneForRuntime(scene);
        var cloneRoot = clone.FindGameObject("Player")!;
        var cloneFps = cloneRoot.GetComponent<FirstPersonCamera3D>()!;
        if (clone.FindGameObject(cloneFps.CameraObjectId)?.GetComponent<Camera3D>() == null)
            throw new Exception("FPS camera references failed save/runtime clone.");
        var blueprint = project.Scenes.Serialize(scene);
        var placed = project.Scenes.InstantiateHierarchy(clone, blueprint.GameObjects).Single(o => o.Parent == null);
        if (placed.GetComponent<FirstPersonCamera3D>()!.CameraObjectId == fps.CameraObjectId ||
            clone.FindGameObject(placed.GetComponent<FirstPersonCamera3D>()!.CameraObjectId)?.Parent != placed)
            throw new Exception("FPS Blueprint placement must remap its camera reference.");
        BlueprintAuthoringService.SetupPlayablePlayer(root,null,PlayerViewPreset.FirstPerson);
        if (root.GetComponent<FirstPersonCamera3D>() != fps) throw new Exception("Repeated FPS setup duplicates view ownership.");
        BlueprintAuthoringService.SetupPlayablePlayer(root,null,PlayerViewPreset.ThirdPerson);
        if (root.GetComponent<FirstPersonCamera3D>() != null || root.GetComponent<CameraBoom3D>() == null)
            throw new Exception("Switching back to TPS must replace FPS ownership.");
        var zeroScene = new Scene("Zero eye"); var zeroRoot = zeroScene.CreateGameObject("Player");
        var zeroCam = zeroScene.CreateGameObject("Camera"); zeroCam.SetParent(zeroRoot,false); zeroCam.AddComponent(new Camera3D());
        zeroRoot.AddComponent(new FirstPersonCamera3D { CameraObjectId = zeroCam.Id }).SnapToView();
        if (zeroCam.Transform.LocalPosition != Vector3.Zero) throw new Exception("Zero eye anchors must not silently use fallback height.");
        Console.WriteLine("PASS Dedicated FPS view: frame-rate independence, direct body yaw, pitch extremes, arm offsets, input, clone/remapping and TPS transition.");
    }
}
