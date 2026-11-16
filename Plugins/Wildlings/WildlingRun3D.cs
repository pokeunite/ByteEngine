using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Authoring;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace Wildlings;

/// <summary>State of the current survivors run.</summary>
public enum WildlingRunState
{
    Idle,
    Running,
    Failed,
    BossDue,
    Cleared
}

/// <summary>
/// Survivors run director (GDD v0.3, build step 1-2).
///
/// Owns the run clock, the player's experience/level track and the fail state. Kills
/// are credited by subscribing to the Died event of every hostile health component in
/// the scene, so no spawner coupling is needed. Level-ups currently grant damage and
/// fire-rate directly because the card draft is a later slice; that placeholder is
/// flagged in the handoff.
/// </summary>
public sealed class WildlingRun3D : Component
{
    [SceneObjectPicker]
    public string TeamObjectName { get; set; } = "Wildling Manager";

    /// <summary>Creature the player drives; also the fail-state health source.</summary>
    [SceneObjectPicker]
    public string PlayerName { get; set; } = "Fire Lizard";

    /// <summary>Boss arrival, seconds into the run (GDD appendix: 720).</summary>
    public float BossSpawnSeconds { get; set; } = 720f;

    /// <summary>Hostile layer credited as a kill.</summary>
    public int HostileLayer { get; set; } = 3;

    public bool AutoStart { get; set; } = true;

    public float BaseExperiencePerLevel { get; set; } = 5f;

    public float ExperiencePerLevelGrowth { get; set; } = 4f;

    public float ExperiencePerKill { get; set; } = 1f;

    /// <summary>Experience carried by one dropped orb.</summary>
    public float ExperiencePerOrb { get; set; } = 1f;

    /// <summary>Orb visual radius, in metres.</summary>
    public float OrbRadius { get; set; } = .22f;

    /// <summary>Distance at which an orb starts homing in, in metres.</summary>
    public float OrbPickupRadius { get; set; } = 6f;

    /// <summary>How long an uncollected orb survives, in seconds.</summary>
    public float OrbLifetimeSeconds { get; set; } = 30f;

    public Vector4 OrbColor { get; set; } = new(.35f, .85f, 1f, 1f);

    /// <summary>Damage gained per level, as a fraction of base (0.12 = +12%).</summary>
    public float DamagePerLevel { get; set; } = .12f;

    /// <summary>Fire-interval multiplier per level (0.94 = 6% faster).</summary>
    public float CooldownPerLevel { get; set; } = .94f;

    public float MinimumCooldownMultiplier { get; set; } = .45f;

    /// <summary>Coins awarded per kill when the run ends.</summary>
    public int CoinPerKill { get; set; } = 5;

    /// <summary>Extra coins for clearing the region guardian.</summary>
    public int BossCoinBonus { get; set; } = 150;

    /// <summary>Species essence awarded for a clear.</summary>
    public int EssencePerClear { get; set; } = 3;

    /// <summary>
    /// When set, the finished run loads this project-relative scene (the sanctuary hub).
    /// Empty keeps the result on screen in the run scene.
    /// </summary>
    public string ReturnScenePath { get; set; } = string.Empty;

    /// <summary>Seconds the finished run stays on screen before <see cref="ReturnScenePath"/> loads.</summary>
    public float ReturnDelaySeconds { get; set; } = 3f;

    private bool _returnArmed;
    private float _returnRemaining;

    public WildlingRunState State { get; private set; } = WildlingRunState.Idle;

    public float Elapsed { get; private set; }

    public int Level { get; private set; } = 1;

    public float Experience { get; private set; }

    public int Kills { get; private set; }

    public float ExperienceToNext => MathF.Max(1f, BaseExperiencePerLevel + ExperiencePerLevelGrowth * (Level - 1));

    public float TimeRemaining => MathF.Max(0f, BossSpawnSeconds - Elapsed);

    public bool IsRunning => State is WildlingRunState.Running or WildlingRunState.BossDue;

    public bool BossSpawned { get; private set; }

    public bool BossDefeated { get; private set; }

    public string Clock
    {
        get
        {
            int total = (int)MathF.Max(0f, Elapsed);
            return $"{total / 60:00}:{total % 60:00}";
        }
    }

    private readonly HashSet<HealthComponent> _watched = new();
    private HealthComponent? _playerHealth;
    private bool _rewarded;
    private float _damageMultiplier = 1f;
    private float _cooldownMultiplier = 1f;


    protected override void OnStart()
    {
        if (AutoStart)
        {
            Begin();
        }
    }

    /// <summary>Starts (or restarts) the run clock and resets experience.</summary>
    public void Begin()
    {
        Elapsed = 0f;
        Kills = 0;
        Level = 1;
        Experience = 0f;
        _damageMultiplier = 1f;
        _cooldownMultiplier = 1f;
        State = WildlingRunState.Running;
        BossSpawned = false;
        BossDefeated = false;
        _rewarded = false;
        _watched.Clear();
        _playerHealth = null;
        _returnArmed = false;
        _returnRemaining = 0f;

        if (Player()?.GetComponent<WildlingCombat3D>() is { } combat)
        {
            combat.DamageMultiplier = 1f;
            combat.CooldownMultiplier = 1f;
        }

        if (Spawner() is { } spawner)
        {
            spawner.Spawning = true;
            spawner.ResetBoss();
        }
    }

    /// <summary>Loads <see cref="ReturnScenePath"/> once the result has been on screen long enough.</summary>
    private void ReturnToHub(float delta)
    {
        if (string.IsNullOrWhiteSpace(ReturnScenePath) || GameObject?.Scene is not { } scene)
        {
            return;
        }

        if (!_returnArmed)
        {
            _returnArmed = true;
            _returnRemaining = MathF.Max(0f, ReturnDelaySeconds);
        }

        _returnRemaining -= delta;

        if (_returnRemaining > 0f)
        {
            return;
        }

        _returnArmed = false;

        try
        {
            scene.RequestLoad(ReturnScenePath);
        }
        catch (ArgumentException)
        {
            Console.WriteLine($"Wildling Run: '{ReturnScenePath}' is not a project-relative .bytescene path.");
        }
    }

    protected override void OnUpdate()
    {
        if (State is WildlingRunState.Failed or WildlingRunState.Cleared)
        {
            ReturnToHub((float)Time.DeltaTime);
            return;
        }

        if (!IsRunning)
        {
            return;
        }

        Elapsed += (float)Time.DeltaTime;

        WatchHostiles();
        WatchPlayer();

        if (State == WildlingRunState.Running && Elapsed >= BossSpawnSeconds)
        {
            State = WildlingRunState.BossDue;

            if (Spawner()?.SpawnBoss() != null)
            {
                BossSpawned = true;
            }
        }
    }

    private void WatchPlayer()
    {
        if (_playerHealth != null)
        {
            return;
        }

        _playerHealth = Player()?.GetComponent<HealthComponent>();

        if (_playerHealth != null)
        {
            _playerHealth.Died += OnPlayerDied;
        }
    }

    private void WatchHostiles()
    {
        Scene? scene = GameObject?.Scene;
        if (scene == null)
        {
            return;
        }

        foreach (GameObject candidate in scene.GameObjects)
        {
            if (candidate.Layer != HostileLayer)
            {
                continue;
            }

            if (candidate.GetComponent<HealthComponent>() is not { } health || _watched.Contains(health))
            {
                continue;
            }

            _watched.Add(health);
            health.Died += OnHostileDied;
        }
    }

    private void OnHostileDied(HealthComponent health)
    {
        health.Died -= OnHostileDied;
        _watched.Remove(health);

        if (!IsRunning)
        {
            return;
        }

        Kills++;
        SpawnOrb(health.GameObject);
        RegisterBossDeath(health.GameObject);
    }

    /// <summary>Killing the guardian clears the run.</summary>
    private void RegisterBossDeath(GameObject? dead)
    {
        if (BossDefeated || dead == null || Spawner() is not { } spawner)
        {
            return;
        }

        string prefix = spawner.BossNamePrefix;

        if (string.IsNullOrWhiteSpace(prefix) ||
            !dead.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        BossDefeated = true;
        State = WildlingRunState.Cleared;
        spawner.Spawning = false;
        FinishRun(true);
    }

    /// <summary>
    /// Banks the run once: coins and essence go to the profile, and the same guard stops
    /// a death-after-clear from paying out twice.
    /// </summary>
    public int FinishRun(bool cleared)
    {
        if (_rewarded)
        {
            return 0;
        }

        _rewarded = true;

        int coins = Kills * Math.Max(0, CoinPerKill) + (cleared ? Math.Max(0, BossCoinBonus) : 0);
        Profile()?.RecordRun(cleared, Kills, Elapsed, coins, SpeciesIdForPlayer(), cleared ? EssencePerClear : 0);
        return coins;
    }

    /// <summary>Species id rewarded for this run; comes from the creature's stats when present.</summary>
    private string SpeciesIdForPlayer()
    {
        GameObject? player = Player();
        return player?.GetComponent<WildlingStats3D>()?.Definition.Id
               ?? WildlingSpeciesDatabase.FireLizardId;
    }

    private WildlingProfile3D? Profile() => Manager()?.GetComponent<WildlingProfile3D>();

    private GameObject? Manager() =>
        string.IsNullOrWhiteSpace(TeamObjectName)
            ? null
            : GameObject?.Scene?.FindGameObject(TeamObjectName);

    /// <summary>Grants experience; called when an orb reaches the player.</summary>
    public void AddExperience(float amount)
    {
        if (!IsRunning || amount <= 0f)
        {
            return;
        }

        Experience += amount;

        while (Experience >= ExperienceToNext)
        {
            Experience -= ExperienceToNext;
            LevelUp();
        }
    }

    private void SpawnOrb(GameObject? source)
    {
        Scene? scene = GameObject?.Scene;
        if (scene == null || source == null)
        {
            return;
        }

        Vector3 position = source.Transform.WorldPosition;

        GameObject orb = scene.CreateGameObject("XP orb");
        orb.Transform.WorldPosition = position;
        orb.Transform.WorldScale = Vector3.One * MathF.Max(.05f, OrbRadius);

        orb.AddComponent(new MeshRenderer
        {
            UsePrimitive = true,
            Primitive = PrimitiveMeshType.Sphere,
            CastShadows = false,
            ReceiveShadows = false,
            Material = new Material
            {
                BaseColor = OrbColor,
                EmissionEnabled = true,
                EmissionColor = new Vector3(OrbColor.X, OrbColor.Y, OrbColor.Z),
                EmissionIntensity = 1.6f,
                Roughness = .35f
            }
        });

        orb.AddComponent(new WildlingExperienceOrb3D
        {
            Amount = MathF.Max(1f, ExperiencePerOrb),
            PickupRadius = MathF.Max(.5f, OrbPickupRadius),
            LifetimeSeconds = MathF.Max(2f, OrbLifetimeSeconds),
            PlayerName = PlayerName,
            RunObjectName = TeamObjectName
        });
    }

    private void LevelUp()
    {
        Level++;
        _damageMultiplier += MathF.Max(0f, DamagePerLevel);
        _cooldownMultiplier = MathF.Max(
            Math.Clamp(MinimumCooldownMultiplier, .05f, 1f),
            _cooldownMultiplier * Math.Clamp(CooldownPerLevel, .1f, 1f));

        if (Player()?.GetComponent<WildlingCombat3D>() is { } combat)
        {
            combat.DamageMultiplier = _damageMultiplier;
            combat.CooldownMultiplier = _cooldownMultiplier;
        }

        LeveledUp?.Invoke(Level);
    }

    /// <summary>Raised when a level is gained; the draft listens for it.</summary>
    public event Action<int>? LeveledUp;

    private void OnPlayerDied(HealthComponent health)
    {
        health.Died -= OnPlayerDied;
        _playerHealth = null;
        State = WildlingRunState.Failed;
        FinishRun(false);

        if (Spawner() is { } spawner)
        {
            spawner.Spawning = false;
        }
    }

    private GameObject? Player()
    {
        Scene? scene = GameObject?.Scene;
        if (scene == null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(PlayerName) && scene.FindGameObject(PlayerName) is { } named)
        {
            return named;
        }

        return string.IsNullOrWhiteSpace(TeamObjectName)
            ? null
            : scene.FindGameObject(TeamObjectName)?.GetComponent<TeamManager3D>()?.ActiveMember;
    }

    private WildlingArenaSpawner3D? Spawner() =>
        string.IsNullOrWhiteSpace(TeamObjectName)
            ? null
            : GameObject?.Scene?.FindGameObject(TeamObjectName)?.GetComponent<WildlingArenaSpawner3D>();
}
