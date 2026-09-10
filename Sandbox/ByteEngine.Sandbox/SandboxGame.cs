using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;

using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace ByteEngine.Sandbox;

public sealed class SandboxGame
    : ByteEngineApplication
{
    private Texture2D? _playerTexture;

    public SandboxGame()
        : base(
            1280,
            720,
            $"ByteEngine Sandbox v{ByteEngineInfo.Version}"
        )
    {
    }

    protected override void OnEngineStart()
    {
        string texturePath =
            Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "player.png"
            );

        _playerTexture =
            new Texture2D(
                texturePath,
                TextureFilter.Nearest
            );

        Scene mainScene =
            new(
                "Main Scene"
            );

        // PLAYER

        GameObject player =
            mainScene.CreateGameObject(
                "Player"
            );

        player.Transform.Position =
            new Vector2(
                0.0f,
                0.0f
            );

        player.AddComponent(
            new SpriteRenderer(
                _playerTexture
            ) { Size = new Vector2(128.0f, 128.0f) }
        );

        player.AddComponent(
            new PlayerController
            {
                Speed =
                    300.0f
            }
        );

        // CAMERA

        GameObject camera =
            mainScene.CreateGameObject(
                "Main Camera"
            );

        camera.Transform.Position =
            player.Transform.Position;

        camera.AddComponent(
            new Camera2D
            {
                Zoom =
                    1.0f
            }
        );

        camera.AddComponent(
            new CameraFollowComponent
            {
                Target =
                    player.Transform
            }
        );

        // ENEMY 1

        GameObject enemy1 =
            mainScene.CreateGameObject(
                "Enemy 1"
            );

        enemy1.Transform.Position =
            new Vector2(
                500.0f,
                0.0f
            );

        enemy1.AddComponent(
            new SpriteRenderer(
                _playerTexture
            )
            {
                Size = new Vector2(96.0f, 96.0f),
                Tint =
                    new Vector4(
                        1.0f,
                        0.25f,
                        0.25f,
                        1.0f
                    )
            }
        );

        // ENEMY 2

        GameObject enemy2 =
            mainScene.CreateGameObject(
                "Enemy 2"
            );

        enemy2.Transform.Position =
            new Vector2(
                -500.0f,
                -250.0f
            );

        enemy2.AddComponent(
            new SpriteRenderer(
                _playerTexture
            )
            {
                Size = new Vector2(96.0f, 96.0f),
                Tint =
                    new Vector4(
                        1.0f,
                        0.6f,
                        0.2f,
                        1.0f
                    )
            }
        );

        // PROP 1

        GameObject prop1 =
            mainScene.CreateGameObject(
                "Prop 1"
            );

        prop1.Transform.Position =
            new Vector2(
                0.0f,
                500.0f
            );

        prop1.AddComponent(
            new SpriteRenderer(
                _playerTexture
            )
            {
                Size = new Vector2(80.0f, 80.0f),
                Tint =
                    new Vector4(
                        0.25f,
                        1.0f,
                        0.35f,
                        1.0f
                    )
            }
        );

        // PROP 2

        GameObject prop2 =
            mainScene.CreateGameObject(
                "Prop 2"
            );

        prop2.Transform.Position =
            new Vector2(
                900.0f,
                500.0f
            );

        prop2.AddComponent(
            new SpriteRenderer(
                _playerTexture
            )
            {
                Size = new Vector2(80.0f, 80.0f),
                Tint =
                    new Vector4(
                        0.4f,
                        0.4f,
                        1.0f,
                        1.0f
                    )
            }
        );

        Scenes.LoadScene(
            mainScene
        );

        Console.WriteLine(
            $"Scene contains {mainScene.GameObjectCount} GameObjects."
        );

        Console.WriteLine(
            "WASD / Arrow Keys = Move"
        );

        Console.WriteLine(
            "ESC = Exit"
        );
    }

    protected override void OnEngineShutdown()
    {
        _playerTexture?.Dispose();

        Console.WriteLine(
            "Sandbox resources destroyed."
        );
    }
}
