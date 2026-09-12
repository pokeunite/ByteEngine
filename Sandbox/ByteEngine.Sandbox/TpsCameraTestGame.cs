using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Sandbox;

public sealed class TpsCameraTestGame : ByteEngineApplication
{
    public TpsCameraTestGame() : base(1280, 720, "ByteEngine TPS Camera Test") { }

    protected override void OnEngineStart()
    {
        var scene = new Scene("TPS Camera Test");

        GameObject ground = scene.CreateGameObject("Ground");
        ground.Transform.LocalScale = new Vector3(20f, 1f, 20f);
        ground.AddComponent(new MeshRenderer
        {
            Primitive = PrimitiveMeshType.Plane,
            Material = new Material { BaseColor = new Vector4(.22f, .28f, .22f, 1f) }
        });
        ground.AddComponent(new BoxCollider3D { Size = new Vector3(1f, .1f, 1f) });
        ground.AddComponent(new GroundSurface());

        GameObject player = scene.CreateGameObject("Player");
        player.Transform.WorldPosition = new Vector3(0f, 1f, 0f);
        player.AddComponent(new MeshRenderer
        {
            Primitive = PrimitiveMeshType.Cube,
            Material = new Material { BaseColor = new Vector4(.15f, .55f, 1f, 1f) }
        });
        player.AddComponent(new CapsuleCollider3D { Radius = .5f, Height = 2f });
        player.AddComponent(new CharacterController3D { MoveSpeed = 5f });
        player.AddComponent(new PlayerController3D { UseLocalOrientation = false });
        CameraBoom3D boom = player.AddComponent(new CameraBoom3D
        {
            ArmLength = 5f,
            PivotHeight = 1.5f,
            Yaw = 0f,
            Pitch = 12f,
            MinPitch = -10f,
            MaxPitch = 50f,
            MouseSensitivityX = .12f,
            MouseSensitivityY = .1f,
            PositionSmoothness = 14f,
            RotationSmoothness = 18f,
            ShoulderOffset = 0f,
            EnableCameraCollision = true,
            CollisionRadius = .2f
        });

        GameObject camera = scene.CreateGameObject("Main Camera");
        camera.SetParent(player, false);
        Camera3D gameCamera = camera.AddComponent(new Camera3D { ActiveGameCamera = true });
        boom.CameraObjectId = camera.Id;

        GameObject light = scene.CreateGameObject("Directional Light");
        light.Transform.EulerAngles = new Vector3(50f, -35f, 0f);
        light.AddComponent(new DirectionalLight { Intensity = 1.2f, AmbientIntensity = .4f });

        Scenes.LoadScene(scene);
        scene.SetActiveCamera(gameCamera);
        CaptureGameInput();
        Console.WriteLine("TPS Camera Test: mouse=orbit, WASD=move, F8=dump, Esc=release, click=recapture.");
    }

    protected override void OnEngineUpdate()
    {
        if (!IsGameInputCaptured && Input.IsMouseButtonPressed(MouseButton.Left)) CaptureGameInput();
    }

    protected override void OnEngineShutdown() => ReleaseGameInput();
}
