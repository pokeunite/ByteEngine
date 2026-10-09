using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;

using ByteEngine.Core.Assets;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.Classification;

using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Serialization;

public sealed class SceneSerializer
{
    private const string TpsDCollisionSafetyMarginProperty =
        "collisionSafetyMargin";

    private const string TpsEIdleTurnStartAngleProperty =
        "idleTurnStartAngle";

    private const string TpsEIdleTurnFinishAngleProperty =
        "idleTurnFinishAngle";

    private const string TpsEIdleTurnSpeedProperty =
        "idleTurnSpeed";

    private const string ModelHiddenMeshKeysProperty =
        "hiddenMeshKeys";

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
                SchemaVersion = SceneData.CurrentSchemaVersion,
                FixedSimulation = scene.FixedSimulation,
                FixedStepSeconds = scene.SimulationClock.StepSeconds,
                MaximumCatchUpSteps = scene.SimulationClock.MaximumCatchUpSteps,
                Name = scene.Name,
                SceneId = scene.Id
            };

        foreach (var variable in scene.Variables)
            data.Variables.Add(new VariableData { Name = variable.Key, Value = variable.Value.Clone() });

        foreach (GameObject gameObject in scene.GameObjects)
            data.GameObjects.Add(SerializeObject(gameObject));

        return data;
    }

    public GameObjectData SerializeObject(GameObject gameObject)
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
                    ParentSocket = gameObject.ParentSocket,
                    AttachmentLocationRule = gameObject.AttachmentLocationRule,
                    AttachmentRotationRule = gameObject.AttachmentRotationRule,
                    AttachmentScaleRule = gameObject.AttachmentScaleRule,
                    AttachmentOffset = new TransformData
                    {
                        LocalPosition = new Vector3Data { X = gameObject.AttachmentPosition.X, Y = gameObject.AttachmentPosition.Y, Z = gameObject.AttachmentPosition.Z },
                        LocalRotation = new QuaternionData { X = gameObject.AttachmentRotation.X, Y = gameObject.AttachmentRotation.Y, Z = gameObject.AttachmentRotation.Z, W = gameObject.AttachmentRotation.W },
                        LocalScale = new Vector3Data { X = gameObject.AttachmentScale.X, Y = gameObject.AttachmentScale.Y, Z = gameObject.AttachmentScale.Z }
                    },
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
                    ApplyComponentSerializationCompatibility(
                        component,
                        componentData);

                    gameObjectData.Components.Add(
                        componentData
                    );
                }
            }

        return gameObjectData;
    }

    /// <summary>Applies an object delta in-place; unchanged components keep identity and plugin state.</summary>
    public void ApplyObjectDelta(RuntimeScene scene, IReadOnlyDictionary<Guid, GameObjectData?> objects)
    {
        foreach (var (id, data) in objects)
            if (data == null && scene.FindGameObject(id) is { } old)
            {
                foreach (var child in old.Children.ToArray()) child.SetParent(null, false);
                scene.DestroyGameObject(old);
            }
        foreach (var (id, data) in objects)
        {
            if (data == null) continue;
            var target = scene.FindGameObject(id);
            if (target == null) { target = new GameObject(id, data.Name); scene.AddGameObject(target); }
            target.Name = data.Name;
            target.Active = data.Active;
            target.Layer = data.Layer;
            target.SetTags(data.Tags);
            target.ParentSocket = data.ParentSocket;
            target.AttachmentLocationRule = data.AttachmentLocationRule;
            target.AttachmentRotationRule = data.AttachmentRotationRule;
            target.AttachmentScaleRule = data.AttachmentScaleRule;
            if (data.AttachmentOffset is { } offset)
            {
                if (offset.LocalPosition is { } p) target.AttachmentPosition = new(p.X,p.Y,p.Z);
                if (offset.LocalRotation is { } r) target.AttachmentRotation = new(r.X,r.Y,r.Z,r.W);
                if (offset.LocalScale is { } c) target.AttachmentScale = new(c.X,c.Y,c.Z);
            }
            if (data.Transform.LocalPosition is { } pos) target.Transform.LocalPosition = new(pos.X,pos.Y,pos.Z);
            if (data.Transform.LocalRotation is { } rot) target.Transform.LocalRotation = new(rot.X,rot.Y,rot.Z,rot.W);
            if (data.Transform.LocalScale is { } scale) target.Transform.LocalScale = new(scale.X,scale.Y,scale.Z);
            target.Variables.Clear();
            foreach (var variable in data.Variables) target.Variables.Set(variable.Name, variable.Value.Clone());
            var oldComponents = target.Components.ToArray();
            var unused=new HashSet<Component>(oldComponents);
            var ordered=new List<Component>();
            ComponentData? CaptureComponent(Component c){var result=_components.Serialize(c);if(result!=null)ApplyComponentSerializationCompatibility(c,result);return result;}
            string Encode(ComponentData? value)=>JsonSerializer.Serialize(value,JsonSerialization.Options);
            var previous=oldComponents.ToDictionary(c=>c,c=>CaptureComponent(c));
            // Reserve every exact match first. Restoring an earlier same-type component must not consume
            // an unchanged later component and destroy its live identity.
            var reserved=new HashSet<Component>();var exact=new Component?[data.Components.Count];
            for(int i=0;i<data.Components.Count;i++){string wanted=Encode(data.Components[i]);exact[i]=oldComponents.FirstOrDefault(c=>!reserved.Contains(c)&&Encode(previous[c])==wanted);if(exact[i]!=null)reserved.Add(exact[i]!);}
            for(int i=0;i<data.Components.Count;i++)
            {
                var value=data.Components[i];string encoded=Encode(value);
                Component? old=exact[i]??oldComponents.FirstOrDefault(c=>unused.Contains(c)&&!reserved.Contains(c)&&previous[c]?.Type==value.Type);
                if(old!=null)
                {
                    unused.Remove(old);
                    if(Encode(previous[old])==encoded){ordered.Add(old);continue;}
                    if(_components.Deserialize(value) is {} desired&&desired.GetType()==old.GetType())
                    {
                        foreach(var property in old.GetType().GetProperties(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))
                            if(property.CanRead&&property.CanWrite&&property.GetIndexParameters().Length==0&&(property.Name==nameof(Component.Enabled)||value.Properties.Any(p=>string.Equals(p.Key,property.Name,StringComparison.OrdinalIgnoreCase))))
                                property.SetValue(old,property.GetValue(desired));
                        ApplyComponentDeserializationCompatibility(old,value);
                        if(Encode(CaptureComponent(old))==encoded){ordered.Add(old);continue;}
                    }
                    target.RemoveComponent(old);
                }
                if(_components.Deserialize(value) is {} replacement){ApplyComponentDeserializationCompatibility(replacement,value);target.AddComponent(replacement);ordered.Add(replacement);}
            }
            foreach(var removed in oldComponents.Where(unused.Contains))
                if(previous[removed]==null)ordered.Add(removed);else target.RemoveComponent(removed);
            target.ReorderComponents(ordered);
        }
        foreach (var (id, data) in objects)
            if (data != null && scene.FindGameObject(id) is { } target)
            { target.SetParent(data.ParentId.HasValue ? scene.FindGameObject(data.ParentId.Value) : null, false); target.ParentSocket = data.ParentSocket; target.Transform.ResetInterpolation(); }
    }

    public RuntimeScene Deserialize(
        SceneData data)
    {
        if (data.SchemaVersion < 0 || data.SchemaVersion > SceneData.CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported scene schema {data.SchemaVersion}. Update ByteEngine before opening this scene.");
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

        scene.FixedSimulation = data.FixedSimulation;
        scene.SimulationClock.StepSeconds = double.IsFinite(data.FixedStepSeconds) ? Math.Clamp(data.FixedStepSeconds, .001, .1) : 1.0 / 60;
        scene.SimulationClock.MaximumCatchUpSteps = Math.Clamp(data.MaximumCatchUpSteps, 1, 64);
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
                    ApplyComponentDeserializationCompatibility(
                        component,
                        componentData);

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
                child.ParentSocket = gameObjectData.ParentSocket ?? string.Empty;
                child.AttachmentLocationRule = gameObjectData.AttachmentLocationRule;
                child.AttachmentRotationRule = gameObjectData.AttachmentRotationRule;
                child.AttachmentScaleRule = gameObjectData.AttachmentScaleRule;
                if (gameObjectData.AttachmentOffset is TransformData offset)
                {
                    Vector3Data p = offset.LocalPosition ?? new Vector3Data();
                    QuaternionData r = offset.LocalRotation ?? new QuaternionData();
                    Vector3Data s = offset.LocalScale ?? new Vector3Data { X = 1f, Y = 1f, Z = 1f };
                    child.AttachmentPosition = new Vector3(p.X, p.Y, p.Z);
                    child.AttachmentRotation = new Quaternion(r.X, r.Y, r.Z, r.W);
                    child.AttachmentScale = new Vector3(s.X, s.Y, s.Z);
                }
                if (child.IsAttached) SkeletalAttachmentService.Apply(child);
            }
        }

        return scene;
    }

    /// <summary>
    /// TPS-D introduced CollisionSafetyMargin after the original CameraBoom3D
    /// codec had already shipped. Writing it here serves two purposes until the
    /// component codec is versioned: the authored value persists, and its
    /// presence marks a scene as having passed through the TPS-D serializer.
    /// </summary>
    private static void ApplyComponentSerializationCompatibility(
        Component component,
        ComponentData componentData)
    {
        if (component is ModelHierarchyInstance modelInstance)
        {
            var hiddenMeshKeys =
                new JsonArray();

            foreach (string key in modelInstance.HiddenMeshKeys)
            {
                if (!string.IsNullOrWhiteSpace(key))
                {
                    hiddenMeshKeys.Add(
                        JsonValue.Create(
                            key));
                }
            }

            componentData.Properties[ModelHiddenMeshKeysProperty] =
                hiddenMeshKeys;
        }

        if (component is CameraBoom3D boom)
        {
            componentData.Properties[TpsDCollisionSafetyMarginProperty] =
                boom.CollisionSafetyMargin;
        }

        if (component is PlayerController3D player)
        {
            /*
             * TPS-E settings are persisted here while the legacy component
             * codec remains backwards compatible with older scene formats.
             */
            componentData.Properties[TpsEIdleTurnStartAngleProperty] =
                player.IdleTurnStartAngle;

            componentData.Properties[TpsEIdleTurnFinishAngleProperty] =
                player.IdleTurnFinishAngle;

            componentData.Properties[TpsEIdleTurnSpeedProperty] =
                player.IdleTurnSpeed;
        }
    }

    /// <summary>
    /// One-time TPS-D migration for scenes saved before collision became the
    /// standard spring-arm behavior. The old codec serialized its default false
    /// value, so there is no way to distinguish an untouched old default from an
    /// intentional opt-out. Scenes without the TPS-D marker are therefore moved
    /// to the new baseline once; after they are saved again the marker is present
    /// and an explicit EnableCameraCollision=false is respected normally.
    /// </summary>
    private static void ApplyComponentDeserializationCompatibility(
        Component component,
        ComponentData componentData)
    {
        if (component is ModelHierarchyInstance modelInstance &&
            componentData.Properties[ModelHiddenMeshKeysProperty]
                is JsonArray hiddenMeshKeys)
        {
            var keys =
                new List<string>();

            foreach (JsonNode? node in hiddenMeshKeys)
            {
                if (node is JsonValue value &&
                    value.TryGetValue(
                        out string? key) &&
                    !string.IsNullOrWhiteSpace(key))
                {
                    keys.Add(
                        key);
                }
            }

            modelInstance.SetHiddenMeshKeys(
                keys);
        }

        if (component is PlayerController3D player)
        {
            /*
             * The legacy PlayerController3D codec predates standard TPS-B/E and
             * still falls back to local movement + FaceCamera when properties
             * are missing. Correct only missing data here so explicitly authored
             * legacy aim/strafe scenes remain untouched.
             */
            if (componentData.Properties["useLocalOrientation"] ==
                null)
            {
                player.UseLocalOrientation =
                    false;
            }

            if (componentData.Properties["characterRotation"] ==
                null)
            {
                player.CharacterRotation =
                    CharacterRotationMode.FaceMovement;
            }

            player.IdleTurnStartAngle =
                componentData.Properties[TpsEIdleTurnStartAngleProperty]?
                    .GetValue<float>() ??
                60f;

            player.IdleTurnFinishAngle =
                componentData.Properties[TpsEIdleTurnFinishAngleProperty]?
                    .GetValue<float>() ??
                5f;

            player.IdleTurnSpeed =
                componentData.Properties[TpsEIdleTurnSpeedProperty]?
                    .GetValue<float>() ??
                300f;
        }

        if (component is not CameraBoom3D boom)
        {
            return;
        }

        bool hasTpsDMarker =
            componentData.Properties[TpsDCollisionSafetyMarginProperty] !=
            null;

        boom.CollisionSafetyMargin =
            componentData.Properties[TpsDCollisionSafetyMarginProperty]?
                .GetValue<float>() ??
            .05f;

        if (hasTpsDMarker)
        {
            return;
        }

        /*
         * Pre-TPS-D scenes commonly contain enableCameraCollision=false only
         * because that was the old default. Migrate those scenes to the new
         * collision-aware baseline on first load.
         */
        boom.EnableCameraCollision =
            true;

        /*
         * The old codec also used true as its fallback for both lag modes.
         * Only correct missing properties here; explicitly authored values are
         * preserved.
         */
        if (componentData.Properties["cameraLagEnabled"] ==
            null)
        {
            boom.CameraLagEnabled =
                false;
        }

        if (componentData.Properties["rotationLagEnabled"] ==
            null)
        {
            boom.RotationLagEnabled =
                false;
        }
    }

    public static void RemapObjectReferences(JsonNode? node, IReadOnlyDictionary<Guid, Guid> map)
    {
        if (node is JsonObject obj)
        {
            foreach (var pair in obj.ToArray())
            {
                bool objectReference = pair.Key.EndsWith("Object", StringComparison.OrdinalIgnoreCase) ||
                    pair.Key.Equals("objectId", StringComparison.OrdinalIgnoreCase) ||
                    pair.Key.EndsWith("ObjectId", StringComparison.OrdinalIgnoreCase) ||
                    pair.Key.Equals("targetId", StringComparison.OrdinalIgnoreCase);
                if (objectReference && pair.Value is JsonValue value && value.TryGetValue<string>(out var text) &&
                    Guid.TryParse(text, out var id) && map.TryGetValue(id, out var mapped)) obj[pair.Key] = mapped.ToString();
                else if (pair.Key == "objectMap" && pair.Value is JsonObject identities)
                {
                    foreach (var identity in identities.ToArray())
                        if (identity.Value is JsonValue v && v.TryGetValue<string>(out var target) &&
                            Guid.TryParse(target, out var old) && map.TryGetValue(old, out var placed)) identities[identity.Key] = placed.ToString();
                }
                else RemapObjectReferences(pair.Value, map);
            }
        }
        else if (node is JsonArray array) foreach (var item in array) RemapObjectReferences(item, map);
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
            foreach (var component in clone.Components)
            {
                RemapObjectReferences(component.Properties, idMap);
                if (component.Type == "BlueprintInstance" && preferredIds == null)
                    component.Properties["instanceId"] = Guid.NewGuid().ToString();
            }
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
