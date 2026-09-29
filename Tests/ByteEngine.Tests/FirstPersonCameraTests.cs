using System.Numerics;
using System.Runtime.CompilerServices;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Tests;

internal static class FirstPersonCameraTests
{
    [ModuleInitializer]
    internal static void Run()
    {
        var scene = new Scene("FPS fixed eye regression");
        var player = scene.CreateGameObject("Player");
        var controller = player.AddComponent(new PlayerController3D());
        var boom = player.AddComponent(new CameraBoom3D
        {
            FirstPerson = true, PivotHeight = 1.65f, MinPitch = -89, MaxPitch = 89,
            ArmLength = 5, ShoulderOffset = 2, CameraLagEnabled = true, RotationLagEnabled = true
        });
        var cameraObject = scene.CreateGameObject("Camera");
        cameraObject.SetParent(player,false);
        cameraObject.AddComponent(new Camera3D());
        var weapon = scene.CreateGameObject("View Weapon");
        weapon.SetParent(cameraObject,false);
        var weaponRenderer = weapon.AddComponent(new MeshRenderer());
        var body = scene.CreateGameObject("Body");
        body.SetParent(player,false);
        var bodyRenderer = body.AddComponent(new MeshRenderer());
        boom.CameraObjectId = cameraObject.Id;
        foreach(float pitch in new[] {-89f, 0f, 89f})
        {
            controller.ControlYaw = 90; controller.ControlPitch = pitch;
            player.Transform.WorldPosition = new Vector3(2,pitch*.01f,3);
            boom.SnapToSocket();
            Vector3 expected = player.Transform.WorldPosition + Vector3.UnitY*1.65f;
            if(Vector3.Distance(expected,cameraObject.Transform.WorldPosition)>.0001f)
                throw new Exception("FPS look must not orbit or lag the eye position.");
            if(Vector3.Dot(cameraObject.Transform.Forward, CameraBoom3D.CalculateViewForward(90,pitch))<.999f)
                throw new Exception("FPS camera must follow control yaw and pitch directly.");
        }
        if(!bodyRenderer.Visible || !weaponRenderer.Visible)
            throw new Exception("Arms-only player models must remain visible by default.");
        boom.FirstPersonCameraOffset = new Vector3(0,.2f,.4f);
        boom.SnapToSocket();
        Vector3 offsetExpected = player.Transform.WorldPosition + Vector3.UnitY*1.85f -
            CameraBoom3D.CalculateViewForward(90,0)*.4f;
        if(Vector3.Distance(cameraObject.Transform.WorldPosition,offsetExpected)>.0001f)
            throw new Exception("FPS authoring offset must affect the camera.");
        boom.HideFirstPersonBody = true;
        boom.SnapToSocket();
        if(bodyRenderer.Visible || !weaponRenderer.Visible)
            throw new Exception("FPS must hide world body but preserve camera-mounted weapon.");
        var authoredScene = new Scene("Authored FPS");
        var root = authoredScene.CreateGameObject("Player");
        var rig = root.AddComponent(new CameraBoom3D { FirstPerson = true, UseControlRotation = false,
            MinPitch = -89, MaxPitch = 89 });
        var view = authoredScene.CreateGameObject("Camera"); view.SetParent(root,false);
        view.Transform.LocalPosition = new Vector3(.1f,1.4f,.3f);
        view.AddComponent(new Camera3D()); rig.CameraObjectId = view.Id;
        var arms = authoredScene.CreateGameObject("Arms"); arms.SetParent(view,false);
        arms.Transform.LocalPosition = new Vector3(0,-.2f,-.6f);
        rig.Pitch = 0; rig.SnapToSocket(); Vector3 rest = arms.Transform.WorldPosition;
        rig.Pitch = -60; rig.SnapToSocket();
        if(Vector3.Distance(view.Transform.WorldPosition,new Vector3(.1f,1.4f,.3f))>.0001f ||
            Vector3.Distance(rest,arms.Transform.WorldPosition)<.1f)
            throw new Exception("Authored camera position must survive Play and child arms must follow pitch.");
        var animation = root.AddComponent(new ByteEngine.Core.Animation.AnimationController());
        if(ByteEngine.Core.Animation.AnimationController.FindForObject(arms) != animation)
            throw new Exception("FPS model animation target must resolve its ancestor controller.");
        var generic = ByteEngine.Core.Animation.AnimationProfileSerializer.CreateDefault("Generic arms");
        if(generic.Rig.Type != ByteEngine.Core.Animation.AnimationRigType.Generic)
            throw new Exception("Generic animation profiles must not require a humanoid rig.");
        var setupScene = new Scene("FPS setup repeat");
        var setupPlayer = setupScene.CreateGameObject("Player");
        ByteEngine.Editor.BlueprintAuthoringService.SetupPlayablePlayer(setupPlayer, null,
            ByteEngine.Editor.PlayerViewPreset.FirstPerson);
        var setupCamera = setupPlayer.Children.Single(child => child.GetComponent<Camera3D>() != null);
        var setupModel = setupCamera.Children.Single(child => child.Name == "Model");
        setupCamera.Transform.LocalPosition = new Vector3(.2f, 1.5f, .1f);
        setupModel.Transform.LocalPosition = new Vector3(.1f, -.25f, -.5f);
        ByteEngine.Editor.BlueprintAuthoringService.SetupPlayablePlayer(setupPlayer, null,
            ByteEngine.Editor.PlayerViewPreset.FirstPerson);
        if (setupModel.Parent != setupCamera ||
            Vector3.Distance(setupCamera.Transform.LocalPosition, new Vector3(.2f,1.5f,.1f)) > .0001f ||
            Vector3.Distance(setupModel.Transform.LocalPosition, new Vector3(.1f,-.25f,-.5f)) > .0001f ||
            setupPlayer.Children.Any(child => child.Name == "Model"))
            throw new Exception("Reapplying FPS setup must preserve authored viewmesh hierarchy and transforms.");
        Console.WriteLine("First-person fixed eye, pitch/yaw and view-weapon regressions passed.");
    }
}
