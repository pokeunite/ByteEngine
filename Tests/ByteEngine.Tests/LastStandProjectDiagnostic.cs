using System.Diagnostics;
using ByteEngine.Core;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;

namespace ByteEngine.Tests;

internal static class LastStandProjectDiagnostic
{
    public static void Run(string projectFile)
    {
        using var project = new GameProjectRuntime(projectFile);
        var manager = new SceneManager();
        Scene scene = project.LoadStartupScene();
        GameObject player = scene.FindGameObject("p-TPS")
            ?? throw new InvalidOperationException("Missing Last Stand player.");
        GameObject waveOwner = scene.FindGameObject("WaveManager")
            ?? throw new InvalidOperationException("Missing Last Stand WaveManager.");
        WaveSpawner3D wave = waveOwner.GetComponent<WaveSpawner3D>()
            ?? throw new InvalidOperationException("Missing native WaveSpawner3D.");
        if (wave.FailureTargetId != player.Id)
            throw new InvalidOperationException("Wave failure is not linked to the player.");

        // Exercise the real project as a 20-character CPU stress case without
        // writing those temporary tuning values back to the user's scene.
        wave.FirstWaveCount = 20;
        wave.EnemiesPerWave = 0;
        wave.MaxWaves = 1;
        wave.SpawnInterval = .01f;
        manager.RestartSceneFactory = project.LoadStartupScene;
        manager.LoadScene(scene);
        for (int frame = 0; frame < 26; frame++) Tick(manager);
        int alive = wave.EnemiesAlive;
        Console.WriteLine($"Live animation components: controllers={scene.GameObjects.SelectMany(o => o.Components).OfType<AnimationController>().Count()}, skinned renderers={scene.GameObjects.SelectMany(o => o.Components).OfType<SkeletalMeshRenderer>().Count()}.");
        var sampleRenderer = scene.GameObjects.SelectMany(o => o.Components)
            .OfType<SkeletalMeshRenderer>().FirstOrDefault(r => r.Model.ProjectPath.Contains("AtlasZombie", StringComparison.OrdinalIgnoreCase));
        if (sampleRenderer != null)
        {
            var model = project.Assets.LoadModel(sampleRenderer.Model);
            Console.WriteLine($"Zombie model: meshes={model.Meshes.Count}, vertices={model.Meshes.Sum(m => m.Vertices.Length / 8)}, bones={model.Skeleton?.Bones.Count ?? 0}.");
            int oneInfluence = model.Meshes.Sum(m => m.JointWeights.Count(w =>
                (w.X > .000001f ? 1 : 0) + (w.Y > .000001f ? 1 : 0) +
                (w.Z > .000001f ? 1 : 0) + (w.W > .000001f ? 1 : 0) == 1));
            Console.WriteLine($"Zombie single-bone vertices: {oneInfluence}.");
        }
        double full = Measure(manager);
        var controllers = scene.GameObjects.SelectMany(o => o.Components).OfType<AnimationController>().ToArray();
        var renderers = scene.GameObjects.SelectMany(o => o.Components).OfType<SkeletalMeshRenderer>().ToArray();
        foreach (var component in controllers) component.Enabled = false;
        double withoutControllers = Measure(manager);
        foreach (var component in controllers) component.Enabled = true;
        foreach (var component in renderers) component.Enabled = false;
        double withoutSkinning = Measure(manager);
        foreach (var component in controllers) component.Enabled = false;
        double withoutAnimation = Measure(manager);
        foreach (var component in scene.GameObjects.SelectMany(o => o.Components)
                     .OfType<SimpleEnemyAI3D>())
            component.Enabled = false;
        double withoutAnimationOrAi = Measure(manager);
        Console.WriteLine($"Last Stand 20-character CPU update: alive={alive}, full={full:F2}, no-controller={withoutControllers:F2}, no-skin-renderer={withoutSkinning:F2}, no-animation={withoutAnimation:F2}, no-animation-or-AI={withoutAnimationOrAi:F2} ms/frame (rendering excluded).");

        player.GetComponent<HealthComponent>()?.Kill();
        Tick(manager);
        UiText? gameOver = scene.FindGameObject("GameOverTitle")?.GetComponent<UiText>();
        if (!wave.IsFailed || gameOver?.Visible != true)
            throw new InvalidOperationException("Native Game Over state did not show its HUD.");
        if (player.GetComponent<PlayerShooter3D>()?.Enabled != false)
            throw new InvalidOperationException("Shooting remained enabled after Game Over.");
        scene.RequestRestart();
        Tick(manager);
        if (ReferenceEquals(scene, manager.ActiveScene) ||
            manager.ActiveScene?.FindGameObject("GameOverTitle")?.GetComponent<UiText>()?.Visible != false)
            throw new InvalidOperationException("Restart did not restore the original HUD state.");

        Scene victoryScene = manager.ActiveScene!;
        WaveSpawner3D victoryWave = victoryScene.FindGameObject("WaveManager")!
            .GetComponent<WaveSpawner3D>()!;
        victoryWave.FirstWaveCount = 1;
        victoryWave.MaxWaves = 1;
        victoryWave.SpawnInterval = .01f;
        victoryWave.RestartWaves();
        for (int frame = 0; frame < 4; frame++) Tick(manager);
        GameObject enemy = victoryScene.GameObjects.First(o => o.GetComponent<SimpleEnemyAI3D>() != null);
        enemy.GetComponent<HealthComponent>()!.Kill();
        Tick(manager);
        if (victoryWave.TotalKilled != 1 || !victoryWave.IsComplete ||
            victoryScene.FindGameObject("VictoryTitle")?.GetComponent<UiText>()?.Visible != true ||
            victoryScene.FindGameObject("IntermissionText")?.GetComponent<UiText>()?.Visible != false ||
            victoryScene.FindGameObject("p-TPS")?.GetComponent<PlayerShooter3D>()?.Enabled != false ||
            victoryScene.FindGameObject("KillsValue")?.GetComponent<UiText>()?.Text != "1")
            throw new InvalidOperationException("Final wave did not update kills, hide Wave Clear, and show Victory.");
        manager.UnloadScene();
        Console.WriteLine("Last Stand saved-project Game Over, restart, kill counter, and Victory checks passed.");
    }

    private static void Tick(SceneManager manager)
    {
        Time.Update(1.0 / 60.0);
        manager.UpdateInternal();
    }

    private static double Measure(SceneManager manager)
    {
        SkeletalMeshRenderer.DiagnosticPoseUpdates = 0;
        SkeletalMeshRenderer.DiagnosticPoseTicks = 0;
        SkeletalMeshRenderer.DiagnosticSkinTicks = 0;
        var stopwatch = Stopwatch.StartNew();
        for (int frame = 0; frame < 60; frame++) Tick(manager);
        stopwatch.Stop();
        Console.WriteLine($"  pose updates/frame={SkeletalMeshRenderer.DiagnosticPoseUpdates / 60.0:F1}, pose={SkeletalMeshRenderer.DiagnosticPoseTicks * 1000.0 / Stopwatch.Frequency / 60.0:F2} ms, skin={SkeletalMeshRenderer.DiagnosticSkinTicks * 1000.0 / Stopwatch.Frequency / 60.0:F2} ms");
        return stopwatch.Elapsed.TotalMilliseconds / 60.0;
    }
}
