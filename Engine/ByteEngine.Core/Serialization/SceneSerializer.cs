using System.Numerics;
using System.Text.Json;

using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;

using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Serialization;

public sealed class SceneSerializer
{
    private readonly ComponentSerializer _components;

    public SceneSerializer(
        ComponentSerializer components)
    {
        _components =
            components;
    }

    public void Save(
        RuntimeScene scene,
        string filePath)
    {
        ArgumentNullException.ThrowIfNull(
            scene
        );

        SceneData data =
            Serialize(scene);

        JsonSerialization.WriteAtomic(
            filePath,
            data
        );
    }

    public RuntimeScene Load(
        string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"ByteEngine scene file was not found: {filePath}",
                filePath
            );
        }

        try
        {
            string json =
                File.ReadAllText(
                    filePath
                );

            SceneData data =
                JsonSerializer.Deserialize<SceneData>(
                    json,
                    JsonSerialization.Options
                ) ??
                throw new InvalidDataException(
                    "The scene file did not contain scene data."
                );

            return Deserialize(data);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Invalid ByteEngine scene JSON in '{filePath}': {exception.Message}",
                exception
            );
        }
    }

    public RuntimeScene CloneForRuntime(
        RuntimeScene editorScene)
    {
        SceneData data =
            Serialize(
                editorScene
            );

        data.Name =
            $"{editorScene.Name} (Runtime)";

        return Deserialize(data);
    }

    public SceneData Serialize(
        RuntimeScene scene)
    {
        SceneData data =
            new()
            {
                Name = scene.Name,
                SceneId = scene.Id
            };

        foreach (GameObject gameObject
                 in scene.GameObjects)
        {
            GameObjectData gameObjectData =
                new()
                {
                    Id = gameObject.Id,
                    Name = gameObject.Name,
                    Active = gameObject.Active,
                    Transform =
                        new TransformData
                        {
                            Position =
                                new Vector2Data
                                {
                                    X =
                                        gameObject.Transform.Position.X,
                                    Y =
                                        gameObject.Transform.Position.Y
                                },
                            Rotation =
                                gameObject.Transform.Rotation,
                            Size =
                                new Vector2Data
                                {
                                    X =
                                        gameObject.Transform.Size.X,
                                    Y =
                                        gameObject.Transform.Size.Y
                                }
                        }
                };

            foreach (Component component
                     in gameObject.Components)
            {
                ComponentData? componentData =
                    _components.Serialize(
                        component
                    );

                if (componentData != null)
                {
                    gameObjectData.Components.Add(
                        componentData
                    );
                }
            }

            data.GameObjects.Add(
                gameObjectData
            );
        }

        return data;
    }

    public RuntimeScene Deserialize(
        SceneData data)
    {
        if (data.SceneId ==
            Guid.Empty)
        {
            throw new InvalidDataException(
                "Scene ID cannot be empty."
            );
        }

        RuntimeScene scene =
            new(
                data.SceneId,
                string.IsNullOrWhiteSpace(
                    data.Name)
                    ? "Untitled Scene"
                    : data.Name
            );

        foreach (GameObjectData gameObjectData
                 in data.GameObjects)
        {
            Guid id =
                gameObjectData.Id ==
                Guid.Empty
                    ? Guid.NewGuid()
                    : gameObjectData.Id;

            GameObject gameObject =
                new(
                    id,
                    string.IsNullOrWhiteSpace(
                        gameObjectData.Name)
                        ? "GameObject"
                        : gameObjectData.Name
                )
                {
                    Active =
                        gameObjectData.Active
                };

            gameObject.Transform.Position =
                new Vector2(
                    gameObjectData.Transform.Position.X,
                    gameObjectData.Transform.Position.Y
                );

            gameObject.Transform.Rotation =
                gameObjectData.Transform.Rotation;

            gameObject.Transform.Size =
                new Vector2(
                    Math.Max(
                        gameObjectData.Transform.Size.X,
                        1.0f
                    ),
                    Math.Max(
                        gameObjectData.Transform.Size.Y,
                        1.0f
                    )
                );

            foreach (ComponentData componentData
                     in gameObjectData.Components)
            {
                Component? component =
                    _components.Deserialize(
                        componentData
                    );

                if (component != null)
                {
                    gameObject.AddComponent(
                        component
                    );
                }
            }

            scene.AddGameObject(
                gameObject
            );
        }

        return scene;
    }
}
