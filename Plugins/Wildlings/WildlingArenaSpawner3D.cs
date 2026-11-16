using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Authoring;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>
/// Survivors arena feed (GDD v0.3, step 1: "one minute of automatic Firebolt combat").
///
/// Spawns simple chase-and-touch hostiles around whichever creature the player controls,
/// up to <see cref="MaximumAlive"/>, on <see cref="IntervalSeconds"/>. Enemies are built
/// in code so the arena needs no authored prefabs: capsule body, primitive renderer,
/// health with destroy-on-death, and the engine's SimpleEnemyAI3D to chase and hit.
/// </summary>
public sealed class WildlingArenaSpawner3D : Component
{
    [SceneObjectPicker]
    public string TeamObjectName { get; set; } = "Wildling Manager";

    public bool Spawning { get; set; } = true;

    /// <summary>Name prefix used to count live enemies this spawner created.</summary>
    public string EnemyNamePrefix { get; set; } = "Cinderling";

    public float IntervalSeconds { get; set; } = 1.1f;

    public int MaximumAlive { get; set; } = 8;

    public float MinimumRadius { get; set; } = 8f;

    public float MaximumRadius { get; set; } = 13f;

    public float Health { get; set; } = 30f;

    public float MoveSpeed { get; set; } = 2.6f;

    public float Damage { get; set; } = 6f;

    public float AttackCooldownSeconds { get; set; } = 1.2f;

    public float AttackRange { get; set; } = 1.9f;

    public float DetectionRange { get; set; } = 40f;

    public int EnemyLayer { get; set; } = 3;

    public float BodyHeight { get; set; } = 1.2f;

    public float BodyRadius { get; set; } = .45f;

    public Vector4 BodyColor { get; set; } = new(.95f, .35f, .15f, 1f);

    /// <summary>
    /// Comma-separated scene objects carrying a <see cref="WildlingEnemyDefinition3D"/>.
    /// When set, spawns roll a weighted kind from these instead of the single fallback kind.
    /// </summary>
    public string DefinitionObjectNames { get; set; } = "Enemy Cinderling,Enemy Emberhusk,Enemy Ashwing";

    // ---- Region guardian (spawned once, when the run reaches the boss mark) ----

    public bool BossEnabled { get; set; } = true;

    public string BossNamePrefix { get; set; } = "Sunmeadow Guardian";

    public float BossHealth { get; set; } = 420f;

    public float BossMoveSpeed { get; set; } = 2.2f;

    public float BossDamage { get; set; } = 14f;

    public float BossAttackCooldownSeconds { get; set; } = 1.6f;

    public float BossAttackRange { get; set; } = 2.8f;

    public float BossScale { get; set; } = 2.4f;

    public float BossSpawnRadius { get; set; } = 11f;

    public float BossDetectionRange { get; set; } = 60f;

    /// <summary>Essence granted when the guardian is captured rather than killed.</summary>
    public int BossEssence { get; set; } = 5;

    public Vector4 BossColor { get; set; } = new(.82f, .18f, .95f, 1f);

    public bool BossSpawned { get; private set; }

    public void ResetBoss() => BossSpawned = false;

    private float _remaining;
    private int _serial;
    private float _angle;
    private readonly Random _roll = new();

    protected override void OnUpdate()
    {
        if (!Spawning)
        {
            return;
        }

        Scene? scene = GameObject?.Scene;
        GameObject? player = Player(scene);
        if (scene == null || player == null)
        {
            return;
        }

        _remaining -= (float)Time.DeltaTime;
        if (_remaining > 0f || CountAlive(scene) >= Math.Max(1, MaximumAlive))
        {
            return;
        }

        _remaining = MathF.Max(.2f, IntervalSeconds);

        if (PickDefinition(scene) is { } definition)
        {
            SpawnDefined(scene, player, definition);
        }
        else
        {
            Spawn(scene, player);
        }
    }

    /// <summary>Weighted pick from the authored enemy kinds; null keeps the single fallback kind.</summary>
    private WildlingEnemyDefinition3D? PickDefinition(Scene scene)
    {
        if (string.IsNullOrWhiteSpace(DefinitionObjectNames))
        {
            return null;
        }

        List<WildlingEnemyDefinition3D> kinds = new();

        foreach (string raw in DefinitionObjectNames.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (scene.FindGameObject(raw)?.GetComponent<WildlingEnemyDefinition3D>() is { Weight: > 0 } definition)
            {
                kinds.Add(definition);
            }
        }

        if (kinds.Count == 0)
        {
            return null;
        }

        int total = 0;
        foreach (WildlingEnemyDefinition3D kind in kinds)
        {
            total += kind.Weight;
        }

        int choose = _roll.Next(total);

        foreach (WildlingEnemyDefinition3D kind in kinds)
        {
            choose -= kind.Weight;

            if (choose < 0)
            {
                return kind;
            }
        }

        return kinds[0];
    }

    private void SpawnDefined(Scene scene, GameObject player, WildlingEnemyDefinition3D definition)
    {
        _angle += 2.399963f;

        float inner = MathF.Max(.5f, MinimumRadius);
        float outer = MathF.Max(inner + .5f, MaximumRadius);
        float radius = inner + MathF.Abs(MathF.Sin(_angle)) * (outer - inner);

        Vector3 origin = player.Transform.WorldPosition;
        Vector3 position = new(
            origin.X + MathF.Cos(_angle) * radius,
            origin.Y,
            origin.Z + MathF.Sin(_angle) * radius);

        _serial++;

        CreateEnemy(
                scene,
                position,
                $"{definition.DisplayName} {_serial}",
                definition.Health,
                definition.MoveSpeed,
                definition.Damage,
                definition.AttackCooldownSeconds,
                definition.AttackRange,
                definition.DetectionRange,
                definition.Scale,
                definition.Color,
                player.Name)
            .AddComponent(new WildlingEnemy3D
            {
                SpeciesId = definition.SpeciesId,
                DisplayName = definition.DisplayName,
                EssencePerCapture = definition.EssencePerCapture
            });
    }

    private GameObject? Player(Scene? scene)
    {
        if (scene == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(TeamObjectName) &&
            scene.FindGameObject(TeamObjectName)?.GetComponent<TeamManager3D>() is { } team)
        {
            return team.ActiveMember ?? team.Trainer;
        }

        return null;
    }

    /// <summary>
    /// Spawns the region guardian once, in front of the player. Returns null when it is
    /// disabled or has already been spawned.
    /// </summary>
    public GameObject? SpawnBoss()
    {
        Scene? scene = GameObject?.Scene;
        GameObject? player = Player(scene);

        if (scene == null || player == null || !BossEnabled || BossSpawned)
        {
            return null;
        }

        BossSpawned = true;

        Vector3 origin = player.Transform.WorldPosition;
        Vector3 forward = player.Transform.Forward;
        forward.Y = 0f;

        Vector3 direction = forward.LengthSquared() > .0001f ? Vector3.Normalize(forward) : -Vector3.UnitZ;
        float radius = MathF.Max(4f, BossSpawnRadius);

        Vector3 position = new(
            origin.X + direction.X * radius,
            origin.Y,
            origin.Z + direction.Z * radius);

        GameObject guardian = CreateEnemy(
            scene,
            position,
            BossNamePrefix,
            BossHealth,
            BossMoveSpeed,
            BossDamage,
            BossAttackCooldownSeconds,
            BossAttackRange,
            BossDetectionRange,
            BossScale,
            BossColor,
            player.Name);

        guardian.AddComponent(new WildlingEnemy3D
        {
            SpeciesId = "sunmeadow_guardian",
            DisplayName = BossNamePrefix,
            EssencePerCapture = BossEssence,
            IsGuardian = true
        });

        return guardian;
    }

    private static GameObject CreateEnemy(
        Scene scene,
        Vector3 position,
        string name,
        float health,
        float moveSpeed,
        float damage,
        float attackCooldown,
        float attackRange,
        float detectionRange,
        float scale,
        Vector4 color,
        string targetName)
    {
        GameObject enemy = scene.CreateGameObject(name);
        enemy.Layer = 3;
        enemy.Transform.WorldPosition = position;
        enemy.Transform.WorldScale = new Vector3(scale * .8f, scale, scale * .8f);

        enemy.AddComponent(new CapsuleCollider3D
        {
            Radius = MathF.Max(.2f, scale * .38f),
            Height = MathF.Max(.4f, scale),
            Center = new Vector3(0f, MathF.Max(.2f, scale) * .5f, 0f)
        });

        enemy.AddComponent(new MeshRenderer
        {
            UsePrimitive = true,
            Primitive = PrimitiveMeshType.Sphere,
            Material = new Material
            {
                BaseColor = color,
                Roughness = .8f
            }
        });

        enemy.AddComponent(new HealthComponent
        {
            MaxHealth = MathF.Max(1f, health),
            CurrentHealth = MathF.Max(1f, health),
            DestroyOnDeath = true
        });

        enemy.AddComponent(new SimpleEnemyAI3D
        {
            TargetName = targetName,
            MoveSpeed = moveSpeed,
            DetectionRange = detectionRange,
            AttackRange = attackRange,
            Damage = damage,
            AttackCooldown = attackCooldown,
            StopDistance = .1f
        });

        return enemy;
    }

    private int CountAlive(Scene scene)
    {
        int count = 0;

        foreach (GameObject candidate in scene.GameObjects)
        {
            if (candidate.Layer != EnemyLayer || !candidate.ActiveInHierarchy)
            {
                continue;
            }

            if (!candidate.Name.StartsWith(EnemyNamePrefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (candidate.GetComponent<HealthComponent>() is { IsDead: true })
            {
                continue;
            }

            count++;
        }

        return count;
    }

    private void Spawn(Scene scene, GameObject player)
    {
        // Golden-angle spread keeps spawns from stacking on one side of the arena.
        _angle += 2.399963f;

        float inner = MathF.Max(.5f, MinimumRadius);
        float outer = MathF.Max(inner + .5f, MaximumRadius);
        float radius = inner + MathF.Abs(MathF.Sin(_angle)) * (outer - inner);

        Vector3 origin = player.Transform.WorldPosition;
        Vector3 position = new(
            origin.X + MathF.Cos(_angle) * radius,
            origin.Y,
            origin.Z + MathF.Sin(_angle) * radius);

        _serial++;
        GameObject enemy = scene.CreateGameObject($"{EnemyNamePrefix} {_serial}");
        enemy.Layer = EnemyLayer;
        enemy.Transform.WorldPosition = position;
        enemy.Transform.WorldScale = new Vector3(BodyHeight * .8f, BodyHeight, BodyHeight * .8f);

        enemy.AddComponent(new CapsuleCollider3D
        {
            Radius = BodyRadius,
            Height = BodyHeight,
            Center = new Vector3(0f, BodyHeight * .5f, 0f)
        });

        enemy.AddComponent(new MeshRenderer
        {
            UsePrimitive = true,
            Primitive = PrimitiveMeshType.Sphere,
            Material = new Material
            {
                BaseColor = BodyColor,
                Roughness = .8f
            }
        });

        enemy.AddComponent(new HealthComponent
        {
            MaxHealth = Health,
            CurrentHealth = Health,
            DestroyOnDeath = true
        });

        enemy.AddComponent(new SimpleEnemyAI3D
        {
            TargetName = player.Name,
            MoveSpeed = MoveSpeed,
            DetectionRange = DetectionRange,
            AttackRange = AttackRange,
            Damage = Damage,
            AttackCooldown = AttackCooldownSeconds,
            StopDistance = .1f
        });
    }
}
