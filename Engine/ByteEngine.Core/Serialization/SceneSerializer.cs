using System.Numerics;
using System.Text.Json;

using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.Classification;

using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Serialization;

public sealed class SceneSerializer
{
    private readonly ComponentSerializer _components;
    private readonly ClassificationSettings? _classification;

    public SceneSerializer(
        ComponentSerializer components,
        ClassificationSettings? classification = null)
    {
        ArgumentNullException.ThrowIfNull(
            components);

        _components =
            components;

        /*
         * v0.9-c:
         *
         * Install the renderer/light codecs on every SceneSerializer.
         * Registering by runtime type and type name intentionally replaces
         * the legacy MeshRenderer codec while keeping ComponentSerializer
         * itself backwards compatible and small-risk.
         */
        RendererSerializationRegistrar.Register(
            _components);

        _classification =
            classification;
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

        foreach (var variable in scene.Variables)
            data.Variables.Add(new VariableData { Name = variable.Key, Value = variable.Value.Clone() });

        foreach (GameObject gameObject
                 in scene.GameObjects)
        {
            GameObjectData gameObjectData =
                new()
                {
                    Id = gameObject.Id,
                    Name = gameObject.Name,
                    Active = gameObject.Active,
                    Tags = gameObject.Tags.ToList(),
                    Layer = gameObject.Layer,
                    ParentId = gameObject.Parent?.Id,
                    Transform =
                        new TransformData
                        {
                            LocalPosition =
                                new Vector3Data
                                {
                                    X =
                                        gameObject.Transform.LocalPosition.X,
                                    Y =
                                        gameObject.Transform.LocalPosition.Y,
                                    Z = gameObject.Transform.LocalPosition.Z
                                },
                            LocalRotation =
                                new QuaternionData
                                {
                                    X = gameObject.Transform.LocalRotation.X,
                                    Y = gameObject.Transform.LocalRotation.Y,
                                    Z = gameObject.Transform.LocalRotation.Z,
                                    W = gameObject.Transform.LocalRotation.W
                                },
                            LocalScale =
                                new Vector3Data
                                {
                                    X = gameObject.Transform.LocalScale.X,
                                    Y = gameObject.Transform.LocalScale.Y,
                                    Z = gameObject.Transform.LocalScale.Z
                                }
                        },
                    Variables = gameObject.Variables.Select(variable => new VariableData
                    { Name = variable.Key, Value = variable.Value.Clone() }).ToList()
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
                    : data.Name,
                _classification
            );

        foreach (VariableData variable in data.Variables)
            scene.Variables.Set(variable.Name, variable.Value.Clone());

        var created = new Dictionary<Guid, GameObject>();

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
                        gameObjectData.Active,
                    Layer = gameObjectData.Layer
                };
            gameObject.SetTags(gameObjectData.Tags);
            if (gameObjectData.Layer is < 0 or >= 32)
                Console.Error.WriteLine($"Invalid layer {gameObjectData.Layer} on '{gameObject.Name}'; using Default.");

            foreach (VariableData variable in gameObjectData.Variables)
                gameObject.Variables.Set(variable.Name, variable.Value.Clone());

            bool legacyTransform = gameObjectData.Transform.LocalPosition == null;
            if (legacyTransform)
            {
                Vector2Data position = gameObjectData.Transform.Position ?? new Vector2Data();
                gameObject.Transform.LocalPosition = new Vector3(position.X, position.Y, 0f);
                gameObject.Transform.EulerAngles = new Vector3(0f, 0f, gameObjectData.Transform.Rotation ?? 0f);
                gameObject.Transform.LocalScale = Vector3.One;
            }
            else
            {
                Vector3Data position = gameObjectData.Transform.LocalPosition!;
                QuaternionData rotation = gameObjectData.Transform.LocalRotation ?? new QuaternionData();
                Vector3Data scale = gameObjectData.Transform.LocalScale ?? new Vector3Data { X = 1f, Y = 1f, Z = 1f };
                gameObject.Transform.LocalPosition = new Vector3(position.X, position.Y, position.Z);
                gameObject.Transform.LocalRotation = new Quaternion(rotation.X, rotation.Y, rotation.Z, rotation.W);
                gameObject.Transform.LocalScale = new Vector3(scale.X, scale.Y, scale.Z);
            }

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

            if (legacyTransform && gameObject.GetComponent<Graphics.SpriteRenderer>() is Graphics.SpriteRenderer legacySprite)
            {
                Vector2Data size = gameObjectData.Transform.Size ?? new Vector2Data { X = 64f, Y = 64f };
                legacySprite.Size = new Vector2(Math.Max(size.X, 1f), Math.Max(size.Y, 1f));
            }

            scene.AddGameObject(
                gameObject
            );

            created[gameObject.Id] = gameObject;
        }

        foreach (GameObjectData gameObjectData in data.GameObjects)
        {
            if (gameObjectData.ParentId.HasValue &&
                created.TryGetValue(gameObjectData.Id, out GameObject? child) &&
                created.TryGetValue(gameObjectData.ParentId.Value, out GameObject? parent))
            {
                child.SetParent(parent, false);
            }
        }

        return scene;
    }

    public IReadOnlyList<GameObject> InstantiateHierarchy(
        RuntimeScene target,
        IReadOnlyList<GameObjectData> sourceObjects)
    {
        return InstantiateHierarchy(target, sourceObjects, null, out _);
    }

    public IReadOnlyList<GameObject> InstantiateHierarchy(
        RuntimeScene target,
        IReadOnlyList<GameObjectData> sourceObjects,
        IReadOnlyDictionary<Guid, Guid>? preferredIds,
        out IReadOnlyDictionary<Guid, Guid> sourceToInstanceIds)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(sourceObjects);
        var idMap = sourceObjects.ToDictionary(
            item => item.Id,
            item => preferredIds != null && preferredIds.TryGetValue(item.Id, out Guid preferred) && preferred != Guid.Empty
                ? preferred
                : Guid.NewGuid());
        sourceToInstanceIds = idMap;
        var clones = new List<GameObjectData>();
        foreach (GameObjectData source in sourceObjects)
        {
            GameObjectData clone = JsonSerializer.Deserialize<GameObjectData>(
                JsonSerializer.Serialize(source, JsonSerialization.Options),
                JsonSerialization.Options) ?? throw new InvalidDataException("Could not clone Blueprint object data.");
            clone.Id = idMap[source.Id];
            clone.ParentId = source.ParentId.HasValue && idMap.TryGetValue(source.ParentId.Value, out Guid parentId)
                ? parentId
                : null;
            clones.Add(clone);
        }

        RuntimeScene temporary = Deserialize(new SceneData
        {
            Name = "Blueprint Instance",
            SceneId = Guid.NewGuid(),
            GameObjects = clones
        });
        GameObject[] roots = temporary.GameObjects.Where(item => item.Parent == null).ToArray();
        foreach (GameObject gameObject in temporary.GameObjects) target.AddGameObject(gameObject);
        return roots;
    }
}
