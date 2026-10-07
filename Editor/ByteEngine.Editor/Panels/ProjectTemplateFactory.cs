using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;

namespace ByteEngine.Editor.Panels;

internal static class ProjectTemplateFactory
{
    public static Scene Create(ProjectTemplate template) => template switch
    {
        ProjectTemplate.Clean => new Scene("Main"),
        ProjectTemplate.Starter3D => CreateStarter3D(),
        ProjectTemplate.ByteArena => CreateByteArena(),
        ProjectTemplate.InteractiveSand => CreateInteractiveSand(),
        _ => throw new ArgumentOutOfRangeException(nameof(template))
    };

    private static Scene CreateInteractiveSand()
    {
        var scene = new Scene("Interactive Sand");
        scene.CreateGameObject("Interactive sand").AddComponent(new InteractiveSand3D());
        var camera=scene.CreateGameObject("Inspection camera").AddComponent(new Camera3D { ActiveGameCamera=true, FieldOfView=52, FarClip=100 });
        camera.Transform.WorldPosition=new(10,9,13);
        Matrix4x4.Invert(Matrix4x4.CreateLookAt(camera.Transform.WorldPosition,Vector3.Zero,Vector3.UnitY),out var world);
        camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(world);
        var ball=scene.CreateGameObject("Blue ball");ball.Transform.WorldPosition=new(0,.7f,0);ball.Transform.LocalScale=new(1.7f);
        ball.AddComponent(new MeshRenderer { UsePrimitive=true,Primitive=PrimitiveMeshType.Sphere,Material=new() { BaseColor=new(.07f,.34f,.7f,1),Roughness=.32f } });
        ball.AddComponent(new SandLabProbe3D());
        var sun=scene.CreateGameObject("Low sunlight").AddComponent(new DirectionalLight { Intensity=2.3f,Color=new(1,.91f,.76f),CastShadows=true,ShadowResolution=1024,ShadowDistance=35 });sun.Transform.EulerAngles=new(20,-65,0);
        scene.CreateGameObject("Sky").AddComponent(new SkyEnvironment { SkyMode=SkyMode3D.Procedural,Quality=GraphicsQuality.Fast,OverrideAmbient=true,AmbientIntensity=.28f,Exposure=1,Contrast=1.04f });
        var hud=scene.CreateGameObject("Demo controls");hud.AddComponent(new UiCanvas());hud.AddComponent(new UiText { Text="INTERACTIVE SAND\nWASD move | TAB tyre tracks | F6 clear\nI J K L orbit | U / O zoom",FontSize=18,Offset=new(24,22),Color=new(.95f,.92f,.83f,1) });
        return scene;
    }

    private static Scene CreateStarter3D()
    {
        var scene = new Scene("Main");

        GameObject camera = scene.CreateGameObject("Main Camera");
        camera.Transform.LocalPosition = new Vector3(0f, 2f, 6f);
        camera.Transform.EulerAngles = new Vector3(-18f, 0f, 0f);
        camera.AddComponent(new Camera3D());

        GameObject cube = scene.CreateGameObject("Cube");
        cube.AddComponent(new MeshRenderer
        {
            Primitive = PrimitiveMeshType.Cube,
            UsePrimitive = true,
            Material = new Material { BaseColor = new Vector4(.25f, .58f, 1f, 1f) }
        });

        GameObject ground = scene.CreateGameObject("Ground");
        ground.Transform.LocalPosition = new Vector3(0f, -1f, 0f);
        ground.Transform.LocalScale = new Vector3(10f, 1f, 10f);
        ground.AddComponent(new MeshRenderer
        {
            Primitive = PrimitiveMeshType.Plane,
            UsePrimitive = true,
            Material = new Material { BaseColor = new Vector4(.32f, .38f, .32f, 1f) }
        });
        ground.AddComponent(new BoxCollider3D { Size = new Vector3(1f, .05f, 1f) });
        ground.AddComponent(new GroundSurface());

        GameObject light = scene.CreateGameObject("Directional Light");
        light.Transform.EulerAngles = new Vector3(45f, -35f, 0f);
        light.AddComponent(new DirectionalLight { Intensity = 1.2f });

        return scene;
    }

    private static Scene CreateByteArena()
    {
        var scene = new Scene("ByteArena");

        GameObject ground = scene.CreateGameObject("Ground");
        ground.Transform.LocalScale = new Vector3(24f, 1f, 24f);
        ground.AddComponent(new MeshRenderer
        {
            Primitive = PrimitiveMeshType.Plane,
            UsePrimitive = true,
            Material = new Material { BaseColor = new Vector4(.18f, .24f, .2f, 1f) }
        });
        ground.AddComponent(new BoxCollider3D
        {
            Size = new Vector3(1f, .1f, 1f),
            Center = new Vector3(0f, -.05f, 0f)
        });
        ground.AddComponent(new GroundSurface { SurfaceType = "Arena" });

        GameObject player = scene.CreateGameObject("Player");
        player.Transform.LocalPosition = new Vector3(0f, 1f, 5f);
        player.AddComponent(new MeshRenderer
        {
            Primitive = PrimitiveMeshType.Cube,
            UsePrimitive = true,
            Material = new Material
            {
                BaseColor = new Vector4(.15f, .55f, 1f, 1f)
            }
        });
        player.AddComponent(new CapsuleCollider3D
        {
            Radius = .5f,
            Height = 2f
        });
        player.AddComponent(new CharacterController3D
        {
            MoveSpeed = 5f,
            JumpForce = 7f
        });
        player.AddComponent(new HealthComponent
        {
            MaxHealth = 100f,
            CurrentHealth = 100f
        });
        player.AddComponent(new ProjectileLauncher3D
        {
            ProjectileSpeed = 18f,
            Damage = 25f,
            FireCooldown = .25f,
            MuzzleOffset = new Vector3(0f, .1f, -.9f)
        });
        player.AddComponent(new PlayerController3D
        {
            UseLocalOrientation = false
        });
        player.AddComponent(new PlayerShooter3D());

        GameObject camera = scene.CreateGameObject("Main Camera");
        camera.Transform.LocalPosition = new Vector3(0f, 5f, 11.5f);
        camera.AddComponent(new Camera3D
        {
            FieldOfView = 58f
        });
        camera.AddComponent(new ThirdPersonCamera3D
        {
            TargetId = player.Id,
            TargetName = player.Name,
            Distance = 6.5f,
            Height = 1.2f,
            LookAtHeight = .75f,
            FollowSmoothing = 10f,
            Pitch = 20f,
            MinPitch = -10f,
            MaxPitch = 55f,
            MouseSensitivity = .15f,
            ShoulderOffset = .5f
        });

        Vector3[] enemyPositions =
        {
            new(-4f, 1f, -7f),
            new(0f, 1f, -11f),
            new(4f, 1f, -7f)
        };

        for (int index = 0;
             index < enemyPositions.Length;
             index++)
        {
            GameObject enemy = scene.CreateGameObject($"Enemy {index + 1}");
            enemy.Transform.LocalPosition = enemyPositions[index];
            enemy.AddComponent(new MeshRenderer
            {
                Primitive = PrimitiveMeshType.Cube,
                UsePrimitive = true,
                Material = new Material
                {
                    BaseColor = new Vector4(.9f, .18f, .12f, 1f)
                }
            });
            enemy.AddComponent(new BoxCollider3D { Size = Vector3.One });
            enemy.AddComponent(new HealthComponent
            {
                MaxHealth = 50f,
                CurrentHealth = 50f
            });
            enemy.AddComponent(new SimpleEnemyAI3D
            {
                TargetId = player.Id,
                TargetName = player.Name,
                MoveSpeed = 2.5f,
                DetectionRange = 30f,
                AttackRange = 1.5f,
                StopDistance = 1.25f,
                Damage = 10f,
                AttackCooldown = 1f
            });
        }

        GameObject light = scene.CreateGameObject("Directional Light");
        light.Transform.EulerAngles = new Vector3(50f, -35f, 0f);
        light.AddComponent(new DirectionalLight
        {
            Intensity = 1.25f,
            AmbientIntensity = .4f
        });

        GameObject manager = scene.CreateGameObject("Game Manager");
        manager.AddComponent(new ArenaGameManager
        {
            PlayerId = player.Id,
            PlayerName = player.Name
        });

        return scene;
    }
}
