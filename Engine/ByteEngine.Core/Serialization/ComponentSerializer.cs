using System.Numerics;
using System.Text.Json.Nodes;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Blueprints;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization.SerializationModels;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Core.Serialization;

public sealed class ComponentSerializer
{
    private readonly Dictionary<Type, IComponentCodec> _byRuntimeType =
        new();

    private readonly Dictionary<string, IComponentCodec> _byTypeName =
        new(
            StringComparer.OrdinalIgnoreCase
        );

    private readonly ComponentSerializationContext _context;

    public ComponentSerializer(
        string projectRoot,
        AssetDatabase assetDatabase,
        AssetManager assets,
        Action<string>? warningSink = null)
    {
        _context =
            new ComponentSerializationContext(
                projectRoot,
                assetDatabase,
                assets,
                warningSink
            );

        Register(
            new Camera2DCodec()
        );

        Register(
            new SpriteRendererCodec()
        );

        Register(
            new Camera3DCodec()
        );

        Register(
            new DirectionalLightCodec()
        );

        Register(
            new MeshRendererCodec()
        );

        Register(
            new GroundSurfaceCodec()
        );

        Register(
            new BoxCollider3DCodec()
        );

        Register(
            new CharacterController3DCodec()
        );

        Register(
            new CapsuleCollider3DCodec()
        );

        Register(
            new AnimationControllerCodec()
        );

        Register(
            new BlueprintInstanceCodec()
        );

        Register(
            new ModelHierarchyInstanceCodec()
        );

        Register(
            new SkeletalMeshRendererCodec()
        );

        Register(
            new EventModuleComponentCodec()
        );

        Register(new HealthComponentCodec());
        Register(new LifetimeComponentCodec());
        Register(new Projectile3DCodec());
        Register(new ProjectileLauncher3DCodec());
        Register(new SimpleEnemyAI3DCodec());
        Register(new PlayerController3DCodec());
        Register(new PlayerShooter3DCodec());
        Register(new ThirdPersonCamera3DCodec());
        Register(new ArenaGameManagerCodec());
    }

    public void Register(
        IComponentCodec codec)
    {
        ArgumentNullException.ThrowIfNull(
            codec
        );

        _byRuntimeType[codec.ComponentType] =
            codec;

        _byTypeName[codec.TypeName] =
            codec;
    }

    public ComponentData? Serialize(
        Component component)
    {
        if (!_byRuntimeType.TryGetValue(
                component.GetType(),
                out IComponentCodec? codec))
        {
            _context.WarningSink?.Invoke(
                $"Component type '{component.GetType().FullName}' is not registered and was skipped."
            );

            return null;
        }

        ComponentData data =
            codec.Serialize(
                component,
                _context
            );

        data.Enabled =
            component.Enabled;

        return data;
    }

    public Component? Deserialize(
        ComponentData data)
    {
        if (!_byTypeName.TryGetValue(
                data.Type,
                out IComponentCodec? codec))
        {
            _context.WarningSink?.Invoke(
                $"Unknown component type '{data.Type}' was skipped."
            );

            return null;
        }

        try
        {
            Component component =
                codec.Deserialize(
                    data,
                    _context
                );

            component.Enabled =
                data.Enabled;

            return component;
        }
        catch (Exception exception)
        {
            _context.WarningSink?.Invoke(
                $"Could not deserialize component '{data.Type}': {exception.Message}. The component was skipped."
            );

            return null;
        }
    }

    private sealed class Camera2DCodec
        : IComponentCodec
    {
        public string TypeName =>
            "Camera2D";

        public Type ComponentType =>
            typeof(Camera2D);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            Camera2D camera =
                (Camera2D)component;

            return new ComponentData
            {
                Type =
                    TypeName,

                Properties =
                    new JsonObject
                    {
                        ["zoom"] =
                            camera.Zoom
                    }
            };
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            float zoom =
                data.Properties["zoom"]?
                    .GetValue<float>() ??
                1.0f;

            return new Camera2D
            {
                Zoom =
                    zoom
            };
        }
    }

    private sealed class SpriteRendererCodec
        : IComponentCodec
    {
        public string TypeName =>
            "SpriteRenderer";

        public Type ComponentType =>
            typeof(SpriteRenderer);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            SpriteRenderer sprite =
                (SpriteRenderer)component;

            AssetReference? reference =
                sprite.TextureReference ??
                CreateReferenceFromTexture(
                    sprite.Texture,
                    context
                );

            JsonObject properties =
                new()
                {
                    ["texture"] =
                        CreateTextureNode(
                            reference
                        ),

                    ["tint"] =
                        new JsonArray(
                            sprite.Tint.X,
                            sprite.Tint.Y,
                            sprite.Tint.Z,
                            sprite.Tint.W
                        ),

                    ["visible"] =
                        sprite.Visible,

                    ["orderInLayer"] =
                        sprite.OrderInLayer,

                    ["size"] =
                        new JsonArray(
                            sprite.Size.X,
                            sprite.Size.Y
                        )
                };

            return new ComponentData
            {
                Type =
                    TypeName,

                Properties =
                    properties
            };
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            AssetReference reference =
                ReadTextureReference(
                    data.Properties["texture"],
                    context
                );

            Texture2D? texture =
                reference.IsEmpty
                    ? null
                    : context.Assets.LoadTexture(
                        reference
                    );

            Vector4 tint =
                ReadTint(
                    data.Properties["tint"]
                );

            bool visible =
                data.Properties["visible"]?
                    .GetValue<bool>() ??
                true;

            int orderInLayer =
                data.Properties["orderInLayer"]?
                    .GetValue<int>() ??
                0;

            Vector2 size =
                ReadVector2(
                    data.Properties["size"],
                    new Vector2(
                        64.0f,
                        64.0f
                    )
                );

            return new SpriteRenderer(
                texture,
                reference
            )
            {
                Tint =
                    tint,

                Visible =
                    visible,

                OrderInLayer =
                    orderInLayer,

                Size =
                    size
            };
        }

        private static JsonNode CreateTextureNode(
            AssetReference? reference)
        {
            var texture =
                new JsonObject();

            if (reference == null ||
                reference.IsEmpty)
            {
                return texture;
            }

            if (reference.Guid !=
                Guid.Empty)
            {
                texture["guid"] =
                    reference.Guid.ToString();
            }

            if (reference.CachedProjectPath !=
                null)
            {
                texture["path"] =
                    reference.CachedProjectPath;
            }

            return texture;
        }

        private static AssetReference ReadTextureReference(
            JsonNode? node,
            ComponentSerializationContext context)
        {
            if (node is JsonValue legacy &&
                legacy.TryGetValue(
                    out string? path) &&
                !string.IsNullOrWhiteSpace(
                    path))
            {
                AssetReference migrated =
                    context.AssetDatabase.ResolveReference(
                        path
                    );

                context.WarningSink?.Invoke(
                    migrated.Guid != Guid.Empty
                        ? $"Migrated legacy texture path '{path}' to asset GUID {migrated.Guid}."
                        : $"Legacy texture path '{path}' could not be resolved; the path reference was preserved."
                );

                return migrated;
            }

            if (node is JsonObject value)
            {
                string? guidText =
                    value["guid"]?
                        .GetValue<string>();

                string? cachedPath =
                    value["path"]?
                        .GetValue<string>();

                if (Guid.TryParse(
                        guidText,
                        out Guid guid))
                {
                    return new AssetReference(
                        guid,
                        cachedPath
                    );
                }

                if (!string.IsNullOrWhiteSpace(
                        cachedPath))
                {
                    return context.AssetDatabase.ResolveReference(
                        cachedPath
                    );
                }
            }

            return AssetReference.Empty;
        }

        private static AssetReference? CreateReferenceFromTexture(
            Texture2D? texture,
            ComponentSerializationContext context)
        {
            if (texture == null)
            {
                return null;
            }

            if (texture.FilePath == null)
            {
                context.WarningSink?.Invoke(
                    "A SpriteRenderer using a generated texture could not be assigned a persistent asset path."
                );

                return null;
            }

            string relative =
                Path.GetRelativePath(
                    context.ProjectRoot,
                    texture.FilePath
                );

            if (relative.StartsWith(
                    "..",
                    StringComparison.Ordinal))
            {
                context.WarningSink?.Invoke(
                    $"Texture '{texture.FilePath}' is outside the project and cannot be serialized as an asset reference."
                );

                return null;
            }

            if (context.AssetDatabase.TryGetAsset(
                    relative,
                    out AssetRecord? asset) &&
                asset != null)
            {
                return new AssetReference(
                    asset.Guid,
                    asset.ProjectPath
                );
            }

            context.WarningSink?.Invoke(
                $"Texture '{relative}' is not registered in the asset database."
            );

            return new AssetReference(
                relative
            );
        }

        private static Vector4 ReadTint(
            JsonNode? node)
        {
            if (node is not JsonArray array ||
                array.Count < 4)
            {
                return Vector4.One;
            }

            return new Vector4(
                array[0]?
                    .GetValue<float>() ??
                1.0f,

                array[1]?
                    .GetValue<float>() ??
                1.0f,

                array[2]?
                    .GetValue<float>() ??
                1.0f,

                array[3]?
                    .GetValue<float>() ??
                1.0f
            );
        }

        private static Vector2 ReadVector2(
            JsonNode? node,
            Vector2 fallback)
        {
            if (node is not JsonArray array ||
                array.Count < 2)
            {
                return fallback;
            }

            return new Vector2(
                array[0]?
                    .GetValue<float>() ??
                fallback.X,

                array[1]?
                    .GetValue<float>() ??
                fallback.Y
            );
        }
    }

    private sealed class Camera3DCodec
        : IComponentCodec
    {
        public string TypeName =>
            "Camera3D";

        public Type ComponentType =>
            typeof(Camera3D);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            var camera =
                (Camera3D)component;

            return Data(
                TypeName,
                new JsonObject
                {
                    ["fieldOfView"] =
                        camera.FieldOfView,

                    ["nearClip"] =
                        camera.NearClip,

                    ["farClip"] =
                        camera.FarClip
                }
            );
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            return new Camera3D
            {
                FieldOfView =
                    Float(
                        data,
                        "fieldOfView",
                        60.0f
                    ),

                NearClip =
                    Float(
                        data,
                        "nearClip",
                        0.1f
                    ),

                FarClip =
                    Float(
                        data,
                        "farClip",
                        1000.0f
                    )
            };
        }
    }

    private sealed class DirectionalLightCodec
        : IComponentCodec
    {
        public string TypeName =>
            "DirectionalLight";

        public Type ComponentType =>
            typeof(DirectionalLight);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            var light =
                (DirectionalLight)component;

            return Data(
                TypeName,
                new JsonObject
                {
                    ["color"] =
                        Array(
                            light.Color
                        ),

                    ["intensity"] =
                        light.Intensity,

                    ["ambientIntensity"] =
                        light.AmbientIntensity
                }
            );
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            return new DirectionalLight
            {
                Color =
                    Vector3(
                        data.Properties["color"],
                        System.Numerics.Vector3.One
                    ),

                Intensity =
                    Float(
                        data,
                        "intensity",
                        1.0f
                    ),

                AmbientIntensity =
                    Float(
                        data,
                        "ambientIntensity",
                        0.25f
                    )
            };
        }
    }

    private sealed class MeshRendererCodec
        : IComponentCodec
    {
        public string TypeName =>
            "MeshRenderer";

        public Type ComponentType =>
            typeof(MeshRenderer);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            var renderer =
                (MeshRenderer)component;

            var properties =
                new JsonObject
                {
                    ["primitive"] =
                        renderer.Primitive.ToString(),

                    ["visible"] =
                        renderer.Visible,

                    ["metallic"] =
                        renderer.Material.Metallic,

                    ["roughness"] =
                        renderer.Material.Roughness,

                    ["baseColor"] =
                        new JsonArray(
                            renderer.Material.BaseColor.X,
                            renderer.Material.BaseColor.Y,
                            renderer.Material.BaseColor.Z,
                            renderer.Material.BaseColor.W
                        )
                };

            if (renderer.MeshReference != null)
            {
                properties["modelGuid"] =
                    renderer.MeshReference
                        .Model
                        .Guid
                        .ToString();

                properties["modelPath"] =
                    renderer.MeshReference
                        .Model
                        .CachedProjectPath;

                properties["meshKey"] =
                    renderer.MeshReference
                        .SubAssetKey;
            }

            if (renderer.MaterialReference != null)
            {
                properties["materialKey"] =
                    renderer.MaterialReference
                        .SubAssetKey;
            }

            return Data(
                TypeName,
                properties
            );
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            Enum.TryParse(
                data.Properties["primitive"]?
                    .GetValue<string>(),
                true,
                out PrimitiveMeshType primitive
            );

            JsonArray? colorData =
                data.Properties["baseColor"]
                    as JsonArray;

            Vector4 color =
                colorData?.Count >= 4
                    ? new Vector4(
                        colorData[0]!
                            .GetValue<float>(),

                        colorData[1]!
                            .GetValue<float>(),

                        colorData[2]!
                            .GetValue<float>(),

                        colorData[3]!
                            .GetValue<float>()
                    )
                    : Vector4.One;

            var material =
                new Material
                {
                    BaseColor =
                        color,

                    Metallic =
                        Float(
                            data,
                            "metallic",
                            0.0f
                        ),

                    Roughness =
                        Float(
                            data,
                            "roughness",
                            1.0f
                        )
                };

            var renderer =
                new MeshRenderer
                {
                    Primitive =
                        primitive,

                    Visible =
                        data.Properties["visible"]?
                            .GetValue<bool>() ??
                        true,

                    Material =
                        material
                };

            string? meshKey =
                data.Properties["meshKey"]?
                    .GetValue<string>();

            if (!string.IsNullOrWhiteSpace(
                    meshKey))
            {
                Guid.TryParse(
                    data.Properties["modelGuid"]?
                        .GetValue<string>(),
                    out Guid modelGuid
                );

                string? modelPath =
                    data.Properties["modelPath"]?
                        .GetValue<string>();

                var modelReference =
                    new AssetReference(
                        modelGuid,
                        modelPath
                    );

                renderer.MeshReference =
                    new ModelMeshReference(
                        modelReference,
                        meshKey
                    );

                string? materialKey =
                    data.Properties["materialKey"]?
                        .GetValue<string>();

                if (!string.IsNullOrWhiteSpace(
                        materialKey))
                {
                    renderer.MaterialReference =
                        new ModelMaterialReference(
                            modelReference,
                            materialKey
                        );
                }

                try
                {
                    renderer.Mesh =
                        context.Assets.GetModelMesh(
                            modelReference,
                            meshKey
                        );

                    if (renderer.MaterialReference !=
                        null)
                    {
                        renderer.Material =
                            context.Assets.GetModelMaterial(
                                modelReference,
                                renderer.MaterialReference
                                    .SubAssetKey
                            );
                    }
                }
                catch (Exception exception)
                {
                    context.WarningSink?.Invoke(
                        $"Could not resolve imported mesh '{meshKey}': {exception.Message}"
                    );
                }
            }

            return renderer;
        }
    }

    private sealed class GroundSurfaceCodec
        : IComponentCodec
    {
        public string TypeName =>
            "GroundSurface";

        public Type ComponentType =>
            typeof(GroundSurface);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            var surface =
                (GroundSurface)component;

            return Data(
                TypeName,
                new JsonObject
                {
                    ["walkable"] =
                        surface.Walkable,

                    ["surfaceType"] =
                        surface.SurfaceType,

                    ["friction"] =
                        surface.Friction
                }
            );
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            return new GroundSurface
            {
                Walkable =
                    data.Properties["walkable"]?
                        .GetValue<bool>() ??
                    true,

                SurfaceType =
                    data.Properties["surfaceType"]?
                        .GetValue<string>() ??
                    "Default",

                Friction =
                    Float(
                        data,
                        "friction",
                        1.0f
                    )
            };
        }
    }

    private sealed class PlayerController3DCodec : IComponentCodec
    {
        public string TypeName => "PlayerController3D";
        public Type ComponentType => typeof(PlayerController3D);
        public ComponentData Serialize(Component component, ComponentSerializationContext context) =>
            Data(TypeName, new JsonObject { ["useLocalOrientation"] = ((PlayerController3D)component).UseLocalOrientation });
        public Component Deserialize(ComponentData data, ComponentSerializationContext context) => new PlayerController3D
        {
            UseLocalOrientation = data.Properties["useLocalOrientation"]?.GetValue<bool>() ?? true
        };
    }

    private sealed class PlayerShooter3DCodec : IComponentCodec
    {
        public string TypeName => "PlayerShooter3D";
        public Type ComponentType => typeof(PlayerShooter3D);
        public ComponentData Serialize(Component component, ComponentSerializationContext context) =>
            Data(TypeName, new JsonObject { ["automatic"] = ((PlayerShooter3D)component).Automatic });
        public Component Deserialize(ComponentData data, ComponentSerializationContext context) => new PlayerShooter3D
        {
            Automatic = data.Properties["automatic"]?.GetValue<bool>() ?? true
        };
    }

    private sealed class ThirdPersonCamera3DCodec : IComponentCodec
    {
        public string TypeName => "ThirdPersonCamera3D";
        public Type ComponentType => typeof(ThirdPersonCamera3D);
        public ComponentData Serialize(Component component, ComponentSerializationContext context)
        {
            var camera = (ThirdPersonCamera3D)component;
            return Data(TypeName, new JsonObject
            {
                ["targetId"] = camera.TargetId.ToString(),
                ["targetName"] = camera.TargetName,
                ["distance"] = camera.Distance,
                ["height"] = camera.Height,
                ["lookAtHeight"] = camera.LookAtHeight,
                ["followSmoothing"] = camera.FollowSmoothing,
                ["yaw"] = camera.Yaw,
                ["pitch"] = camera.Pitch,
                ["minPitch"] = camera.MinPitch,
                ["maxPitch"] = camera.MaxPitch,
                ["mouseSensitivity"] = camera.MouseSensitivity,
                ["shoulderOffset"] = camera.ShoulderOffset
            });
        }
        public Component Deserialize(ComponentData data, ComponentSerializationContext context)
        {
            Guid.TryParse(Text(data, "targetId", string.Empty), out Guid targetId);
            return new ThirdPersonCamera3D
            {
                TargetId = targetId,
                TargetName = Text(data, "targetName", string.Empty),
                Distance = Float(data, "distance", 7f),
                Height = Float(data, "height", 4f),
                LookAtHeight = Float(data, "lookAtHeight", 1f),
                FollowSmoothing = Float(data, "followSmoothing", 10f),
                MinPitch = Float(data, "minPitch", -10f),
                MaxPitch = Float(data, "maxPitch", 55f),
                Yaw = Float(data, "yaw", 0f),
                Pitch = Float(data, "pitch", 20f),
                MouseSensitivity = Float(data, "mouseSensitivity", .15f),
                ShoulderOffset = Float(data, "shoulderOffset", .5f)
            };
        }
    }

    private sealed class ArenaGameManagerCodec : IComponentCodec
    {
        public string TypeName => "ArenaGameManager";
        public Type ComponentType => typeof(ArenaGameManager);
        public ComponentData Serialize(Component component, ComponentSerializationContext context)
        {
            var manager = (ArenaGameManager)component;
            return Data(TypeName, new JsonObject { ["playerId"] = manager.PlayerId.ToString(), ["playerName"] = manager.PlayerName });
        }
        public Component Deserialize(ComponentData data, ComponentSerializationContext context)
        {
            Guid.TryParse(Text(data, "playerId", string.Empty), out Guid playerId);
            return new ArenaGameManager { PlayerId = playerId, PlayerName = Text(data, "playerName", "Player") };
        }
    }

    private sealed class HealthComponentCodec : IComponentCodec
    {
        public string TypeName => "HealthComponent";
        public Type ComponentType => typeof(HealthComponent);
        public ComponentData Serialize(Component component, ComponentSerializationContext context)
        {
            var health = (HealthComponent)component;
            return Data(TypeName, new JsonObject
            {
                ["maxHealth"] = health.MaxHealth,
                ["currentHealth"] = health.CurrentHealth,
                ["invulnerable"] = health.Invulnerable,
                ["destroyOnDeath"] = health.DestroyOnDeath
            });
        }
        public Component Deserialize(ComponentData data, ComponentSerializationContext context) => new HealthComponent
        {
            MaxHealth = Float(data, "maxHealth", 100f),
            CurrentHealth = Float(data, "currentHealth", 100f),
            Invulnerable = data.Properties["invulnerable"]?.GetValue<bool>() ?? false,
            DestroyOnDeath = data.Properties["destroyOnDeath"]?.GetValue<bool>() ?? false
        };
    }

    private sealed class LifetimeComponentCodec : IComponentCodec
    {
        public string TypeName => "LifetimeComponent";
        public Type ComponentType => typeof(LifetimeComponent);
        public ComponentData Serialize(Component component, ComponentSerializationContext context) =>
            Data(TypeName, new JsonObject { ["lifetimeSeconds"] = ((LifetimeComponent)component).LifetimeSeconds });
        public Component Deserialize(ComponentData data, ComponentSerializationContext context) =>
            new LifetimeComponent { LifetimeSeconds = Float(data, "lifetimeSeconds", 5f) };
    }

    private sealed class Projectile3DCodec : IComponentCodec
    {
        public string TypeName => "Projectile3D";
        public Type ComponentType => typeof(Projectile3D);
        public ComponentData Serialize(Component component, ComponentSerializationContext context)
        {
            var projectile = (Projectile3D)component;
            return Data(TypeName, new JsonObject
            {
                ["velocity"] = Array(projectile.Velocity),
                ["damage"] = projectile.Damage,
                ["radius"] = projectile.Radius,
                ["destroyOnHit"] = projectile.DestroyOnHit,
                ["ownerId"] = projectile.OwnerId.ToString()
            });
        }
        public Component Deserialize(ComponentData data, ComponentSerializationContext context)
        {
            Guid.TryParse(Text(data, "ownerId", string.Empty), out Guid ownerId);
            return new Projectile3D
            {
                Velocity = Vector3(data.Properties["velocity"], System.Numerics.Vector3.Zero),
                Damage = Float(data, "damage", 10f),
                Radius = Float(data, "radius", .05f),
                DestroyOnHit = data.Properties["destroyOnHit"]?.GetValue<bool>() ?? true,
                OwnerId = ownerId
            };
        }
    }

    private sealed class ProjectileLauncher3DCodec : IComponentCodec
    {
        public string TypeName => "ProjectileLauncher3D";
        public Type ComponentType => typeof(ProjectileLauncher3D);
        public ComponentData Serialize(Component component, ComponentSerializationContext context)
        {
            var launcher = (ProjectileLauncher3D)component;
            return Data(TypeName, new JsonObject
            {
                ["projectileBlueprintGuid"] = launcher.ProjectileBlueprint.Guid.ToString(),
                ["projectileBlueprintPath"] = launcher.ProjectileBlueprint.CachedProjectPath,
                ["projectileSpeed"] = launcher.ProjectileSpeed,
                ["damage"] = launcher.Damage,
                ["fireCooldown"] = launcher.FireCooldown,
                ["muzzleOffset"] = Array(launcher.MuzzleOffset)
            });
        }
        public Component Deserialize(ComponentData data, ComponentSerializationContext context)
        {
            Guid.TryParse(Text(data, "projectileBlueprintGuid", string.Empty), out Guid guid);
            string path = Text(data, "projectileBlueprintPath", string.Empty);
            AssetReference reference = guid != Guid.Empty ? new AssetReference(guid, path) :
                string.IsNullOrWhiteSpace(path) ? AssetReference.Empty : new AssetReference(path);
            return new ProjectileLauncher3D
            {
                ProjectileBlueprint = reference,
                ProjectileSpeed = Float(data, "projectileSpeed", 30f),
                Damage = Float(data, "damage", 10f),
                FireCooldown = Float(data, "fireCooldown", .2f),
                MuzzleOffset = Vector3(data.Properties["muzzleOffset"], System.Numerics.Vector3.Zero)
            };
        }
    }

    private sealed class SimpleEnemyAI3DCodec : IComponentCodec
    {
        public string TypeName => "SimpleEnemyAI3D";
        public Type ComponentType => typeof(SimpleEnemyAI3D);
        public ComponentData Serialize(Component component, ComponentSerializationContext context)
        {
            var ai = (SimpleEnemyAI3D)component;
            return Data(TypeName, new JsonObject
            {
                ["targetId"] = ai.TargetId.ToString(),
                ["targetName"] = ai.TargetName,
                ["moveSpeed"] = ai.MoveSpeed,
                ["detectionRange"] = ai.DetectionRange,
                ["attackRange"] = ai.AttackRange,
                ["damage"] = ai.Damage,
                ["attackCooldown"] = ai.AttackCooldown,
                ["stopDistance"] = ai.StopDistance
            });
        }
        public Component Deserialize(ComponentData data, ComponentSerializationContext context)
        {
            Guid.TryParse(Text(data, "targetId", string.Empty), out Guid targetId);
            return new SimpleEnemyAI3D
            {
                TargetId = targetId,
                TargetName = Text(data, "targetName", string.Empty),
                MoveSpeed = Float(data, "moveSpeed", 3f),
                DetectionRange = Float(data, "detectionRange", 20f),
                AttackRange = Float(data, "attackRange", 1.5f),
                Damage = Float(data, "damage", 10f),
                AttackCooldown = Float(data, "attackCooldown", 1f),
                StopDistance = Float(data, "stopDistance", 1f)
            };
        }
    }

    private sealed class BoxCollider3DCodec
        : IComponentCodec
    {
        public string TypeName =>
            "BoxCollider3D";

        public Type ComponentType =>
            typeof(BoxCollider3D);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            var collider =
                (BoxCollider3D)component;

            return Data(
                TypeName,
                new JsonObject
                {
                    ["size"] =
                        Array(
                            collider.Size
                        ),

                    ["center"] =
                        Array(
                            collider.Center
                        ),

                    ["isTrigger"] =
                        collider.IsTrigger
                }
            );
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            return new BoxCollider3D
            {
                Size =
                    Vector3(
                        data.Properties["size"],
                        System.Numerics.Vector3.One
                    ),

                Center =
                    Vector3(
                        data.Properties["center"],
                        System.Numerics.Vector3.Zero
                    ),

                IsTrigger =
                    data.Properties["isTrigger"]?
                        .GetValue<bool>() ??
                    false
            };
        }
    }

    private sealed class CharacterController3DCodec
        : IComponentCodec
    {
        public string TypeName =>
            "CharacterController3D";

        public Type ComponentType =>
            typeof(CharacterController3D);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            var controller =
                (CharacterController3D)component;

            return Data(
                TypeName,
                new JsonObject
                {
                    ["moveSpeed"] =
                        controller.MoveSpeed,

                    ["acceleration"] =
                        controller.Acceleration,

                    ["deceleration"] =
                        controller.Deceleration,

                    ["airControl"] =
                        controller.AirControl,

                    ["jumpForce"] =
                        controller.JumpForce,

                    ["gravity"] =
                        controller.Gravity,

                    ["groundDistance"] =
                        controller.GroundDistance,

                    ["maxSlope"] =
                        controller.MaxSlope,

                    ["stepHeight"] =
                        controller.StepHeight,

                    ["coyoteTime"] =
                        controller.CoyoteTime,

                    ["jumpBuffer"] =
                        controller.JumpBuffer,

                    ["snapToGround"] =
                        controller.SnapToGround
                }
            );
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            return new CharacterController3D
            {
                MoveSpeed =
                    Float(
                        data,
                        "moveSpeed",
                        5.0f
                    ),

                Acceleration =
                    Float(
                        data,
                        "acceleration",
                        30.0f
                    ),

                Deceleration =
                    Float(
                        data,
                        "deceleration",
                        35.0f
                    ),

                AirControl =
                    Float(
                        data,
                        "airControl",
                        0.35f
                    ),

                JumpForce =
                    Float(
                        data,
                        "jumpForce",
                        7.0f
                    ),

                Gravity =
                    Float(
                        data,
                        "gravity",
                        20.0f
                    ),

                GroundDistance =
                    Float(
                        data,
                        "groundDistance",
                        0.15f
                    ),

                MaxSlope =
                    Float(
                        data,
                        "maxSlope",
                        50.0f
                    ),

                StepHeight =
                    Float(
                        data,
                        "stepHeight",
                        0.3f
                    ),

                CoyoteTime =
                    Float(
                        data,
                        "coyoteTime",
                        0.1f
                    ),

                JumpBuffer =
                    Float(
                        data,
                        "jumpBuffer",
                        0.1f
                    ),

                SnapToGround =
                    data.Properties["snapToGround"]?
                        .GetValue<bool>() ??
                    true
            };
        }
    }

    private sealed class CapsuleCollider3DCodec
        : IComponentCodec
    {
        public string TypeName =>
            "CapsuleCollider3D";

        public Type ComponentType =>
            typeof(CapsuleCollider3D);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            var collider =
                (CapsuleCollider3D)component;

            return Data(
                TypeName,
                new JsonObject
                {
                    ["radius"] =
                        collider.Radius,

                    ["height"] =
                        collider.Height,

                    ["center"] =
                        Array(
                            collider.Center
                        ),

                    ["isTrigger"] =
                        collider.IsTrigger
                }
            );
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            return new CapsuleCollider3D
            {
                Radius =
                    Float(
                        data,
                        "radius",
                        0.5f
                    ),

                Height =
                    Float(
                        data,
                        "height",
                        2.0f
                    ),

                Center =
                    Vector3(
                        data.Properties["center"],
                        System.Numerics.Vector3.Zero
                    ),

                IsTrigger =
                    data.Properties["isTrigger"]?
                        .GetValue<bool>() ??
                    false
            };
        }
    }

    private sealed class AnimationControllerCodec
        : IComponentCodec
    {
        public string TypeName =>
            "AnimationController";

        public Type ComponentType =>
            typeof(AnimationController);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            var controller =
                (AnimationController)component;

            return Data(
                TypeName,
                new JsonObject
                {
                    ["idle"] =
                        controller.Idle,

                    ["walk"] =
                        controller.Walk,

                    ["run"] =
                        controller.Run,

                    ["jump"] =
                        controller.Jump,

                    ["fall"] =
                        controller.Fall,

                    ["land"] =
                        controller.Land,

                    ["runThreshold"] =
                        controller.RunThreshold
                }
            );
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            return new AnimationController
            {
                Idle =
                    Text(
                        data,
                        "idle",
                        "Idle"
                    ),

                Walk =
                    Text(
                        data,
                        "walk",
                        "Walk"
                    ),

                Run =
                    Text(
                        data,
                        "run",
                        "Run"
                    ),

                Jump =
                    Text(
                        data,
                        "jump",
                        "Jump"
                    ),

                Fall =
                    Text(
                        data,
                        "fall",
                        "Fall"
                    ),

                Land =
                    Text(
                        data,
                        "land",
                        "Land"
                    ),

                RunThreshold =
                    Float(
                        data,
                        "runThreshold",
                        4.0f
                    )
            };
        }
    }

    private sealed class BlueprintInstanceCodec
        : IComponentCodec
    {
        public string TypeName =>
            "BlueprintInstance";

        public Type ComponentType =>
            typeof(BlueprintInstance);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            var instance =
                (BlueprintInstance)component;

            return Data(
                TypeName,
                new JsonObject
                {
                    ["blueprintGuid"] =
                        instance.Blueprint
                            .Guid
                            .ToString(),

                    ["blueprintPath"] =
                        instance.Blueprint
                            .CachedProjectPath,

                    ["instanceId"] =
                        instance.InstanceId
                            .ToString()
                }
            );
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            Guid.TryParse(
                data.Properties["blueprintGuid"]?
                    .GetValue<string>(),
                out Guid blueprintGuid
            );

            Guid.TryParse(
                data.Properties["instanceId"]?
                    .GetValue<string>(),
                out Guid instanceId
            );

            return new BlueprintInstance
            {
                Blueprint =
                    new AssetReference(
                        blueprintGuid,
                        data.Properties["blueprintPath"]?
                            .GetValue<string>()
                    ),

                InstanceId =
                    instanceId ==
                    Guid.Empty
                        ? Guid.NewGuid()
                        : instanceId
            };
        }
    }

    private sealed class SkeletalMeshRendererCodec
        : IComponentCodec
    {
        public string TypeName =>
            "SkeletalMeshRenderer";

        public Type ComponentType =>
            typeof(SkeletalMeshRenderer);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            var renderer =
                (SkeletalMeshRenderer)component;

            return Data(
                TypeName,
                new JsonObject
                {
                    ["modelGuid"] =
                        renderer.Model
                            .Guid
                            .ToString(),

                    ["modelPath"] =
                        renderer.Model
                            .CachedProjectPath,

                    ["skeletonKey"] =
                        renderer.SkeletonKey,

                    ["materialKeys"] =
                        new JsonArray(
                            renderer.MaterialKeys
                                .Select(
                                    key =>
                                        (JsonNode?)JsonValue.Create(
                                            key
                                        )
                                )
                                .ToArray()
                        ),

                    ["visible"] =
                        renderer.Visible
                }
            );
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            Guid.TryParse(
                data.Properties["modelGuid"]?
                    .GetValue<string>(),
                out Guid modelGuid
            );

            List<string> materials =
                data.Properties["materialKeys"]
                    is JsonArray array
                    ? array
                        .Select(
                            node =>
                                node?
                                    .GetValue<string>()
                        )
                        .Where(
                            value =>
                                value != null
                        )
                        .Cast<string>()
                        .ToList()
                    : new List<string>();

            return new SkeletalMeshRenderer
            {
                Model =
                    new AssetReference(
                        modelGuid,
                        data.Properties["modelPath"]?
                            .GetValue<string>()
                    ),

                SkeletonKey =
                    data.Properties["skeletonKey"]?
                        .GetValue<string>(),

                MaterialKeys =
                    materials,

                Visible =
                    data.Properties["visible"]?
                        .GetValue<bool>() ??
                    true
            };
        }
    }

    private sealed class ModelHierarchyInstanceCodec
        : IComponentCodec
    {
        public string TypeName =>
            "ModelHierarchyInstance";

        public Type ComponentType =>
            typeof(ModelHierarchyInstance);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            var instance =
                (ModelHierarchyInstance)component;

            return Data(
                TypeName,
                new JsonObject
                {
                    ["modelGuid"] =
                        instance.Model.Guid.ToString(),

                    ["modelPath"] =
                        instance.Model.CachedProjectPath,

                    ["appliedImportScale"] =
                        instance.AppliedImportScale
                });
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            Guid.TryParse(
                data.Properties["modelGuid"]?
                    .GetValue<string>(),
                out Guid modelGuid);

            return new ModelHierarchyInstance
            {
                Model =
                    new AssetReference(
                        modelGuid,
                        data.Properties["modelPath"]?
                            .GetValue<string>()),

                AppliedImportScale =
                    Float(
                        data,
                        "appliedImportScale",
                        1.0f)
            };
        }
    }

    private sealed class EventModuleComponentCodec
        : IComponentCodec
    {
        public string TypeName =>
            "EventModuleComponent";

        public Type ComponentType =>
            typeof(EventModuleComponent);

        public ComponentData Serialize(
            Component component,
            ComponentSerializationContext context)
        {
            var eventModules =
                (EventModuleComponent)component;

            var modules =
                new JsonArray();

            foreach (AssetReference reference
                     in eventModules.Modules)
            {
                var module =
                    new JsonObject();

                if (reference.Guid !=
                    Guid.Empty)
                {
                    module["guid"] =
                        reference.Guid.ToString();
                }

                if (!string.IsNullOrWhiteSpace(
                        reference.CachedProjectPath))
                {
                    module["path"] =
                        reference.CachedProjectPath;
                }

                modules.Add(
                    module
                );
            }

            return Data(
                TypeName,
                new JsonObject
                {
                    ["modules"] =
                        modules
                }
            );
        }

        public Component Deserialize(
            ComponentData data,
            ComponentSerializationContext context)
        {
            var eventModules =
                new EventModuleComponent();

            eventModules.SetWarningSink(
                context.WarningSink
            );

            if (data.Properties["modules"]
                is not JsonArray modules)
            {
                return eventModules;
            }

            var serializer =
                new EventModuleSerializer();

            foreach (JsonNode? node
                     in modules)
            {
                if (node is not JsonObject value)
                {
                    continue;
                }

                string? guidText =
                    value["guid"]?
                        .GetValue<string>();

                string? cachedPath =
                    value["path"]?
                        .GetValue<string>();

                Guid.TryParse(
                    guidText,
                    out Guid guid
                );

                AssetReference reference;

                if (guid !=
                    Guid.Empty)
                {
                    reference =
                        new AssetReference(
                            guid,
                            cachedPath
                        );
                }
                else if (!string.IsNullOrWhiteSpace(
                             cachedPath))
                {
                    reference =
                        context.AssetDatabase
                            .ResolveReference(
                                cachedPath
                            );
                }
                else
                {
                    continue;
                }

                /*
                 * Keep the reference even when the asset
                 * cannot currently be resolved.
                 *
                 * This prevents missing assets from silently
                 * destroying the serialized relationship.
                 */
                eventModules.AddModuleReference(
                    reference
                );

                AssetRecord? asset =
                    context.AssetDatabase.Resolve(
                        reference
                    );

                if (asset == null)
                {
                    context.WarningSink?.Invoke(
                        $"Event Module '{reference}' could not be resolved."
                    );

                    continue;
                }

                if (asset.Type !=
                    AssetType.EventModule)
                {
                    context.WarningSink?.Invoke(
                        $"Asset '{asset.ProjectPath}' is not an Event Module."
                    );

                    continue;
                }

                try
                {
                    EventModuleDefinition definition =
                        serializer.Load(
                            asset.FullPath
                        );

                    eventModules.AddResolvedModule(
                        reference,
                        definition
                    );
                }
                catch (Exception exception)
                {
                    context.WarningSink?.Invoke(
                        $"Could not load Event Module '{asset.ProjectPath}': {exception.Message}"
                    );
                }
            }

            return eventModules;
        }
    }

    private static ComponentData Data(
        string type,
        JsonObject properties)
    {
        return new ComponentData
        {
            Type =
                type,

            Properties =
                properties
        };
    }

    private static float Float(
        ComponentData data,
        string name,
        float fallback)
    {
        return data.Properties[name]?
                   .GetValue<float>() ??
               fallback;
    }

    private static string Text(
        ComponentData data,
        string name,
        string fallback)
    {
        return data.Properties[name]?
                   .GetValue<string>() ??
               fallback;
    }

    private static JsonArray Array(
        System.Numerics.Vector3 value)
    {
        return new JsonArray(
            value.X,
            value.Y,
            value.Z
        );
    }

    private static System.Numerics.Vector3 Vector3(
        JsonNode? node,
        System.Numerics.Vector3 fallback)
    {
        if (node is not JsonArray array ||
            array.Count < 3)
        {
            return fallback;
        }

        return new System.Numerics.Vector3(
            array[0]?
                .GetValue<float>() ??
            fallback.X,

            array[1]?
                .GetValue<float>() ??
            fallback.Y,

            array[2]?
                .GetValue<float>() ??
            fallback.Z
        );
    }
}
