using ByteEngine.Core.Gameplay;

namespace ByteEngine.Core.VisualLogic;

public sealed partial class VisualLogicRegistry
{
    private static void RegisterWaves(VisualLogicRegistry registry)
    {
        foreach (var (id, label, test) in new (string, string, Func<WaveSpawner3D, bool>)[]
        {
            ("waves.isIdle", "Wave Spawner Is Idle", w => w.State == WaveSpawnerState.Idle),
            ("waves.isRunning", "Waves Are Running", w => w.IsRunning),
            ("waves.isSpawning", "Wave Is Spawning", w => w.State == WaveSpawnerState.Spawning),
            ("waves.isWaitingForClear", "Wave Waiting For Clear", w => w.State == WaveSpawnerState.WaitingForClear),
            ("waves.isIntermission", "Wave Intermission", w => w.State == WaveSpawnerState.Intermission),
            ("waves.isCompleted", "All Waves Completed", w => w.State == WaveSpawnerState.Completed),
            ("waves.isFailed", "Waves Failed", w => w.State == WaveSpawnerState.Failed),
            ("waves.waveStarted", "Wave Started This Frame", w => w.WaveStartedThisFrame),
            ("waves.waveCleared", "Wave Cleared This Frame", w => w.WaveClearedThisFrame),
            ("waves.completedThisFrame", "All Waves Completed This Frame", w => w.AllWavesCompletedThisFrame),
            ("waves.failedThisFrame", "Waves Failed This Frame", w => w.FailedThisFrame),
            ("waves.noEnemiesRemain", "No Wave Enemies Remain", w => w.EnemiesRemaining == 0),
        })
            registry.RegisterCondition(new VisualConditionDefinition
            {
                Id = id, Category = "Gameplay / Waves", DisplayName = label,
                TargetComponent = nameof(WaveSpawner3D),
                Evaluate = (instruction, context) =>
                    ResolveObjectTarget(instruction, context, false)?.GetComponent<WaveSpawner3D>() is { } wave && test(wave)
            });
        foreach (var (id, label, run) in new (string, string, Action<WaveSpawner3D>)[]
        {
            ("waves.start", "Start Waves", w => w.StartWaves()),
            ("waves.stop", "Stop Waves", w => w.StopWaves()),
            ("waves.restart", "Restart Waves", w => w.RestartWaves()),
        })
            registry.RegisterAction(new VisualActionDefinition
            {
                Id = id, Category = "Gameplay / Waves", DisplayName = label,
                TargetComponent = nameof(WaveSpawner3D),
                Execute = (instruction, context) =>
                {
                    if (ResolveObjectTarget(instruction, context)?.GetComponent<WaveSpawner3D>() is { } wave) run(wave);
                }
            });
    }
}
