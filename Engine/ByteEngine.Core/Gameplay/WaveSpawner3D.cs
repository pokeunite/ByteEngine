using ByteEngine.Core.Assets;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Core.Gameplay;

public enum WaveSpawnMode { RoundRobin, Random }
public enum WaveSpawnerState { Idle, Spawning, WaitingForClear, Intermission, Completed, Failed }

public sealed class WaveSpawner3D : Component
{
    private readonly Dictionary<Guid, (GameObject Object, HealthComponent Health)> _alive = new();
    private readonly Dictionary<Guid, GameObject> _owned = new();
    private readonly Random _random = new();
    private HealthComponent? _targetHealth;
    private WaveSpawnerState _pausedState;
    private bool _holdInitialPulse;
    private bool _warnedMissingTarget;
    private bool _warnedSpawnFailure;
    private int _pointIndex, _spawned;
    private float _timer;
    public AssetReference EnemyBlueprint { get; set; } = AssetReference.Empty;
    public Guid SpawnPointTagId { get; set; }
    public WaveSpawnMode SpawnMode { get; set; }
    public bool AutoStart { get; set; } = true;
    public float SpawnInterval { get; set; } = .8f;
    public float WaveDelay { get; set; } = 3f;
    public int MaxWaves { get; set; } = 5;
    public int FirstWaveCount { get; set; } = 5;
    public int EnemiesPerWave { get; set; } = 3;
    public Guid FailureTargetId { get; set; }
    public string FailureTargetName { get; set; } = "";
    public bool StopOnTargetDeath { get; set; } = true;
    public WaveSpawnerState State { get; private set; }
    public int CurrentWave { get; private set; }
    public int WaveEnemyCount { get; private set; }
    public int EnemiesSpawnedThisWave => _spawned;
    public int EnemiesAlive => _alive.Count;
    public int EnemiesSpawned => _spawned;
    public int CurrentWaveEnemyCount => WaveEnemyCount;
    public int EnemiesRemaining => Math.Max(0, WaveEnemyCount - _spawned) + _alive.Count;
    public int TotalSpawned { get; private set; }
    /// <summary>Enemies whose Health emitted Died; excludes despawns.</summary>
    public int TotalKilled { get; private set; }
    public bool IsRunning => State is WaveSpawnerState.Spawning or WaveSpawnerState.WaitingForClear or WaveSpawnerState.Intermission;
    public bool IsComplete => State == WaveSpawnerState.Completed;
    public bool IsFailed => State == WaveSpawnerState.Failed;
    public bool IsIntermission => State == WaveSpawnerState.Intermission;
    public bool WaveStartedThisFrame { get; private set; }
    public bool WaveClearedThisFrame { get; private set; }
    public bool AllWavesCompletedThisFrame { get; private set; }
    public bool FailedThisFrame { get; private set; }

    protected override void OnStart()
    {
        if (Enabled && AutoStart) StartWaves();
        _holdInitialPulse = WaveStartedThisFrame;
    }
    protected override void OnStop() => Release();
    protected override void OnDestroy() => Release();
    protected override void OnUpdate()
    {
        if (_holdInitialPulse) _holdInitialPulse = false;
        else ClearPulses();
        if (!IsRunning) return;
        BindTarget();
        if (!IsRunning) return;
        foreach (var entry in _alive.ToArray())
            if (entry.Value.Health.IsDead || entry.Value.Object.Scene != AttachedGameObject?.Scene || !entry.Value.Object.ActiveInHierarchy)
                RemoveEnemy(entry.Key);
        foreach (var entry in _owned.ToArray())
            if (entry.Value.Scene != AttachedGameObject?.Scene) _owned.Remove(entry.Key);
        float dt = (float)Time.DeltaTime;
        if (!float.IsFinite(dt) || dt < 0) dt = 0;
        _timer -= dt;
        if (State == WaveSpawnerState.Intermission)
        {
            if (_timer <= 0) BeginWave();
            return;
        }
        if (State == WaveSpawnerState.Spawning && _timer <= 0)
        {
            if (!SpawnOne()) { _timer = SafeInterval; return; }
            _timer = SafeInterval;
            if (_spawned >= WaveEnemyCount) State = WaveSpawnerState.WaitingForClear;
        }
        if (State == WaveSpawnerState.WaitingForClear && _alive.Count == 0) ClearWave();
    }
    public void StartWaves()
    {
        if (!Enabled) return;
        if (IsRunning) return;
        if (State is WaveSpawnerState.Failed or WaveSpawnerState.Completed) { RestartWaves(); return; }
        if (_pausedState != WaveSpawnerState.Idle)
        {
            State = _pausedState;
            _pausedState = WaveSpawnerState.Idle;
            BindTarget();
            return;
        }
        var scene = AttachedGameObject?.Scene;
        if (scene == null || EnemyBlueprint.IsEmpty || SpawnPointTagId == Guid.Empty ||
            !RuntimeSpawnService.IsBlueprintSpawnerConfigured || MaxWaves < 1 || FirstWaveCount < 1 ||
            !Points(scene).Any())
        {
            Console.WriteLine("WaveSpawner3D: configure Blueprint, spawn-point tag, wave counts, and runtime spawn service.");
            return;
        }
        BindTarget();
        if (State != WaveSpawnerState.Failed) BeginWave();
    }
    public void StopWaves()
    {
        if (!IsRunning) return;
        _pausedState = State;
        State = WaveSpawnerState.Idle;
        UnbindTarget();
    }
    public void RestartWaves()
    {
        var scene = AttachedGameObject?.Scene;
        foreach (var entry in _alive.Values.ToArray())
        {
            entry.Health.Died -= EnemyDied;
        }
        _alive.Clear();
        foreach (var enemy in _owned.Values.ToArray())
            if (scene != null && enemy.Scene == scene) scene.DestroyGameObject(enemy);
        _owned.Clear();
        UnbindTarget();
        State = WaveSpawnerState.Idle;
        _pausedState = WaveSpawnerState.Idle;
        CurrentWave = WaveEnemyCount = TotalSpawned = TotalKilled = _spawned = _pointIndex = 0;
        _warnedSpawnFailure = false;
        ClearPulses();
        StartWaves();
    }
    private void BeginWave()
    {
        CurrentWave++;
        WaveEnemyCount = (int)Math.Min(10000L,
            Math.Max(1L, (long)FirstWaveCount) + Math.Max(0L, (long)EnemiesPerWave) * (CurrentWave - 1));
        _spawned = 0;
        _timer = SafeInterval;
        State = WaveSpawnerState.Spawning;
        WaveStartedThisFrame = true;
    }
    private bool SpawnOne()
    {
        var scene = AttachedGameObject?.Scene;
        if (scene == null) return false;
        var points = Points(scene).ToArray();
        if (points.Length == 0)
        {
            if (!_warnedSpawnFailure) Console.WriteLine("WaveSpawner3D: no active tagged spawn points; retrying.");
            _warnedSpawnFailure = true;
            return false;
        }
        int index = SpawnMode == WaveSpawnMode.Random ? _random.Next(points.Length) : _pointIndex++ % points.Length;
        GameObject? enemy = RuntimeSpawnService.SpawnBlueprint(scene, EnemyBlueprint, points[index].Transform.WorldPosition);
        if (enemy == null)
        {
            if (!_warnedSpawnFailure) Console.WriteLine("WaveSpawner3D: Blueprint spawn failed; retrying.");
            _warnedSpawnFailure = true;
            return false;
        }
        _warnedSpawnFailure = false;
        _spawned++;
        TotalSpawned++;
        _owned[enemy.Id] = enemy;
        var health = enemy.GetComponent<HealthComponent>();
        if (health == null) Console.WriteLine($"WaveSpawner3D: {enemy.Name} has no Health and will not block wave clear.");
        else if (!health.IsDead) { _alive.Add(enemy.Id, (enemy, health)); health.Died += EnemyDied; }
        return true;
    }
    private IEnumerable<GameObject> Points(ByteEngine.Core.Scene.Scene scene) =>
        scene.GameObjects.Where(x => x.ActiveInHierarchy && x.HasTag(SpawnPointTagId));
    private float SafeInterval => float.IsFinite(SpawnInterval) ? Math.Max(.01f, SpawnInterval) : .8f;
    private float SafeWaveDelay => float.IsFinite(WaveDelay) ? Math.Max(0f, WaveDelay) : 3f;
    private void EnemyDied(HealthComponent health)
    {
        foreach (var entry in _alive)
            if (ReferenceEquals(entry.Value.Health, health))
            {
                TotalKilled++;
                RemoveEnemy(entry.Key);
                break;
            }
    }
    private void RemoveEnemy(Guid id)
    {
        if (_alive.Remove(id, out var entry)) entry.Health.Died -= EnemyDied;
    }
    private void ClearWave()
    {
        WaveClearedThisFrame = true;
        if (CurrentWave >= MaxWaves)
        {
            State = WaveSpawnerState.Completed;
            AllWavesCompletedThisFrame = true;
            UnbindTarget();
        }
        else { State = WaveSpawnerState.Intermission; _timer = SafeWaveDelay; }
    }
    private void BindTarget()
    {
        if (!StopOnTargetDeath) { UnbindTarget(); return; }
        var scene = AttachedGameObject?.Scene;
        GameObject? target = FailureTargetId == Guid.Empty ? null : scene?.FindGameObject(FailureTargetId);
        if (target == null && FailureTargetName.Length > 0) target = scene?.FindGameObject(FailureTargetName);
        var health = target?.GetComponent<HealthComponent>();
        if (target == null && (FailureTargetId != Guid.Empty || FailureTargetName.Length > 0) && !_warnedMissingTarget)
        {
            Console.WriteLine("WaveSpawner3D: failure target not found; continuing without failure condition.");
            _warnedMissingTarget = true;
        }
        if (target != null && health == null && !_warnedMissingTarget)
        {
            Console.WriteLine("WaveSpawner3D: failure target has no Health; continuing without failure condition.");
            _warnedMissingTarget = true;
        }
        if (health != null) _warnedMissingTarget = false;
        if (ReferenceEquals(health, _targetHealth)) return;
        UnbindTarget();
        _targetHealth = health;
        if (health != null)
        {
            health.Died += TargetDied;
            if (health.IsDead)
            {
                State = WaveSpawnerState.Failed;
                FailedThisFrame = true;
                UnbindTarget();
            }
        }
    }
    private void TargetDied(HealthComponent _) { if (!IsRunning) return; State = WaveSpawnerState.Failed; FailedThisFrame = true; UnbindTarget(); }
    private void UnbindTarget() { if (_targetHealth != null) _targetHealth.Died -= TargetDied; _targetHealth = null; }
    private void Release()
    {
        UnbindTarget();
        foreach (var entry in _alive.Values) entry.Health.Died -= EnemyDied;
        _alive.Clear();
        _owned.Clear();
        State = WaveSpawnerState.Idle;
        _pausedState = WaveSpawnerState.Idle;
        CurrentWave = WaveEnemyCount = TotalSpawned = TotalKilled = _spawned = _pointIndex = 0;
        ClearPulses();
    }
    private void ClearPulses() => WaveStartedThisFrame = WaveClearedThisFrame = AllWavesCompletedThisFrame = FailedThisFrame = false;
}
