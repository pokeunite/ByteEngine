using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Classification;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Core.Variables;

namespace ByteEngine.Tests;

internal static class WaveSpawnerTests
{
    public static void Run()
    {
        var points = new List<Vector3>();
        var enemies = new List<GameObject>();
        Guid registration = RuntimeSpawnService.ConfigureBlueprintSpawner((scene, _, position) =>
        {
            points.Add(position);
            GameObject enemy = scene.CreateGameObject("Enemy");
            enemy.AddComponent(new HealthComponent { DestroyOnDeath = true });
            enemies.Add(enemy);
            return enemy;
        });
        try
        {
            var classification = ClassificationSettings.CreateDefault();
            Guid tag = classification.AddTag("EnemySpawn")!.Id;
            var scene = new Scene("Waves", classification);
            GameObject a = scene.CreateGameObject("A"); a.AddTag(tag); a.Transform.WorldPosition = new Vector3(1, 0, 0);
            GameObject b = scene.CreateGameObject("B"); b.AddTag(tag); b.Transform.WorldPosition = new Vector3(2, 0, 0);
            GameObject player = scene.CreateGameObject("Player");
            HealthComponent playerHealth = player.AddComponent(new HealthComponent());
            GameObject owner = scene.CreateGameObject("Manager");
            WaveSpawner3D wave = owner.AddComponent(new WaveSpawner3D
            {
                EnemyBlueprint = new AssetReference("Assets/Enemy.byteblueprint"),
                SpawnPointTagId = tag, AutoStart = true, SpawnInterval = .5f, WaveDelay = 1f,
                FirstWaveCount = 2, EnemiesPerWave = 1, MaxWaves = 2,
                FailureTargetId = player.Id
            });
            scene.LoadInternal();
            Check(wave.State == WaveSpawnerState.Spawning && wave.CurrentWave == 1 && wave.WaveEnemyCount == 2, "auto start");
            Tick(scene, .25);
            Check(wave.EnemiesSpawned == 0 && wave.EnemiesRemaining == 2, "first spawn interval");
            Tick(scene, .25);
            Check(wave.EnemiesAlive == 1 && wave.EnemiesRemaining == 2, "first spawn and remaining count");
            Tick(scene, .5);
            Check(wave.EnemiesSpawned == 2 && wave.EnemiesAlive == 2, "second spawn");
            Check(points[0] == a.Transform.WorldPosition && points[1] == b.Transform.WorldPosition, "round robin");
            HealthComponent firstHealth = enemies[0].GetComponent<HealthComponent>()!;
            firstHealth.Kill();
            firstHealth.Kill();
            Check(wave.EnemiesAlive == 1 && wave.TotalKilled == 1,
                "death counted once with DestroyOnDeath");
            Tick(scene, .1);
            Check(wave.State == WaveSpawnerState.WaitingForClear, "not clear while alive");
            enemies[1].GetComponent<HealthComponent>()!.Kill();
            Tick(scene, .1);
            Check(wave.State == WaveSpawnerState.Intermission && wave.WaveClearedThisFrame &&
                wave.TotalKilled == 2, "wave clear and cumulative kills");
            Tick(scene, .5);
            Check(wave.CurrentWave == 1, "intermission waits");
            Tick(scene, .5);
            Check(wave.CurrentWave == 2 && wave.WaveEnemyCount == 3, "wave scaling");
            playerHealth.Kill();
            Check(wave.State == WaveSpawnerState.Failed && wave.FailedThisFrame, "failure target");
            wave.RestartWaves();
            Check(wave.CurrentWave == 0 && wave.TotalSpawned == 0 && wave.TotalKilled == 0 &&
                wave.State == WaveSpawnerState.Failed, "restart resets and detects already-dead failure target");
            wave.StopWaves();
            Check(wave.State == WaveSpawnerState.Failed, "stop preserves terminal failure");
            TestCompletionAndNodes();
            TestRandomAndUntrackable();
            TestSerialization();
        }
        finally { RuntimeSpawnService.ClearBlueprintSpawner(registration); }
        Console.WriteLine("WaveSpawner3D tests passed.");
    }
    private static void TestCompletionAndNodes()
    {
        var classification = ClassificationSettings.CreateDefault();
        Guid tag = classification.AddTag("EnemySpawn")!.Id;
        var scene = new Scene("One Wave", classification);
        scene.CreateGameObject("Spawn").AddTag(tag);
        GameObject owner = scene.CreateGameObject("Wave Manager");
        var wave = owner.AddComponent(new WaveSpawner3D
        {
            EnemyBlueprint = new AssetReference("Assets/Enemy.byteblueprint"),
            SpawnPointTagId = tag, AutoStart = false, MaxWaves = 1,
            FirstWaveCount = 1, SpawnInterval = .5f
        });
        scene.LoadInternal();
        Check(wave.State == WaveSpawnerState.Idle, "auto start false");
        var registry = VisualLogicRegistry.CreateDefault();
        var context = new EventExecutionContext { Scene = scene, Self = owner, Globals = new VariableStore() };
        bool Condition(string id)
        {
            Check(registry.TryGetCondition(id, out var definition) && definition != null &&
                definition.Category == "Gameplay / Waves" &&
                definition.TargetComponent == nameof(WaveSpawner3D), "registered condition " + id);
            return definition!.Evaluate(new VisualInstruction { Id = id }, context);
        }
        void Action(string id)
        {
            Check(registry.TryGetAction(id, out var definition) && definition != null &&
                definition.Category == "Gameplay / Waves" &&
                definition.TargetComponent == nameof(WaveSpawner3D), "registered action " + id);
            definition!.Execute(new VisualInstruction { Id = id }, context);
        }
        Check(Condition("waves.isIdle") && !Condition("waves.isRunning"), "idle condition");
        Action("waves.start");
        Check(Condition("waves.isRunning") && Condition("waves.waveStarted"), "start action and pulse");
        Tick(scene, .5);
        Check(wave.EnemiesAlive == 1 && wave.State == WaveSpawnerState.WaitingForClear, "single enemy spawned");
        GameObject enemy = scene.GameObjects.First(x => x.Name == "Enemy");
        enemy.GetComponent<HealthComponent>()!.Kill();
        Tick(scene, .01);
        Check(Condition("waves.waveCleared") && Condition("waves.completedThisFrame") &&
            Condition("waves.isCompleted"), "completion conditions");
        int total = wave.TotalSpawned;
        Tick(scene, 2);
        Check(!Condition("waves.waveCleared") && !Condition("waves.completedThisFrame") &&
            wave.TotalSpawned == total && wave.CurrentWave == 1, "pulses clear and no extra wave");
        Action("waves.restart");
        Check(wave.CurrentWave == 1 && wave.TotalSpawned == 0, "restart action");
        Action("waves.stop");
        Check(Condition("waves.isIdle"), "stop action");
        GameObject other = scene.CreateGameObject("Other Manager");
        WaveSpawner3D otherWave = other.AddComponent(new WaveSpawner3D { AutoStart = false });
        var selectedContext = new EventExecutionContext { Scene = scene, Self = other, Globals = new VariableStore() };
        var selected = new VisualInstruction { Id = "waves.start" };
        selected.Arguments["target"] = EventValue.String("id:" + owner.Id);
        registry.TryGetAction(selected.Id, out var selectedAction);
        selectedAction!.Execute(selected, selectedContext);
        Check(wave.IsRunning && otherWave.State == WaveSpawnerState.Idle, "selected object action target");
        var selectedCondition = new VisualInstruction { Id = "waves.isRunning" };
        selectedCondition.Arguments["target"] = EventValue.String("id:" + owner.Id);
        registry.TryGetCondition(selectedCondition.Id, out var selectedDefinition);
        Check(selectedDefinition!.Evaluate(selectedCondition, selectedContext), "selected object condition target");

        var missing = new Scene("Missing Points", classification);
        var bad = missing.CreateGameObject("Bad").AddComponent(new WaveSpawner3D
        {
            EnemyBlueprint = new AssetReference("Assets/Enemy.byteblueprint"),
            SpawnPointTagId = tag
        });
        missing.LoadInternal();
        Check(bad.State == WaveSpawnerState.Idle && bad.TotalSpawned == 0, "missing points safe");
    }
    private static void TestSerialization()
    {
        string root = Path.Combine(Path.GetTempPath(), "ByteEngine-wave-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        Directory.CreateDirectory(Path.Combine(root, "Scenes"));
        using var database = new AssetDatabase(root, new[] { "Assets", "Scenes" });
        using var assets = new AssetManager(database);
        var serializer = new SceneSerializer(new ComponentSerializer(root, database, assets));
        var scene = new Scene("Wave Persistence");
        Guid tag = Guid.NewGuid(), target = Guid.NewGuid(), blueprint = Guid.NewGuid();
        scene.CreateGameObject("Manager").AddComponent(new WaveSpawner3D
        {
            EnemyBlueprint = new AssetReference(blueprint, "Assets/Zombie.byteblueprint"),
            SpawnPointTagId = tag, SpawnMode = WaveSpawnMode.Random, AutoStart = false,
            SpawnInterval = .8f, WaveDelay = 3f, MaxWaves = 5, FirstWaveCount = 5,
            EnemiesPerWave = 3, FailureTargetId = target, StopOnTargetDeath = false
        });
        var copy = serializer.CloneForRuntime(scene).FindGameObject("Manager")?.GetComponent<WaveSpawner3D>();
        Check(copy != null && copy.EnemyBlueprint.Guid == blueprint &&
            copy.SpawnPointTagId == tag && copy.SpawnMode == WaveSpawnMode.Random &&
            !copy.AutoStart && copy.MaxWaves == 5 && copy.FirstWaveCount == 5 &&
            copy.EnemiesPerWave == 3 && copy.FailureTargetId == target &&
            !copy.StopOnTargetDeath, "component serialization");
    }
    private static void TestRandomAndUntrackable()
    {
        var classification = ClassificationSettings.CreateDefault();
        Guid tag = classification.AddTag("EnemySpawn")!.Id;
        var scene = new Scene("Random Waves", classification);
        GameObject valid = scene.CreateGameObject("Valid");
        valid.AddTag(tag);
        valid.Transform.WorldPosition = new Vector3(1, 0, 0);
        GameObject inactive = scene.CreateGameObject("Inactive");
        inactive.AddTag(tag);
        inactive.Transform.WorldPosition = new Vector3(99, 0, 0);
        inactive.Active = false;
        GameObject owner = scene.CreateGameObject("Manager");
        var wave = owner.AddComponent(new WaveSpawner3D
        {
            EnemyBlueprint = new AssetReference("Assets/Enemy.byteblueprint"),
            SpawnPointTagId = tag, SpawnMode = WaveSpawnMode.Random,
            FirstWaveCount = 3, MaxWaves = 1, SpawnInterval = .1f
        });
        var positions = new List<Vector3>();
        Guid registration = RuntimeSpawnService.ConfigureBlueprintSpawner((spawnScene, _, position) =>
        {
            positions.Add(position);
            return spawnScene.CreateGameObject("No Health");
        });
        try
        {
            scene.LoadInternal();
            Tick(scene, .1); Tick(scene, .1); Tick(scene, .1);
            Check(wave.State == WaveSpawnerState.Completed && wave.EnemiesAlive == 0 &&
                wave.TotalSpawned == 3 && positions.All(x => x == valid.Transform.WorldPosition),
                "random ignores inactive point; untrackable enemies do not deadlock");
            Tick(scene, .5);
            Check(wave.TotalSpawned == 3, "completed wave stops spawning");
        }
        finally { RuntimeSpawnService.ClearBlueprintSpawner(registration); }
    }
    private static void Tick(Scene scene, double seconds) { Time.Update(seconds); scene.UpdateInternal(); }
    private static void Check(bool value, string name)
    {
        if (!value) throw new InvalidOperationException("WaveSpawner3D: " + name);
    }
}
