using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Gameplay;

public enum ArenaGameState { Playing, Won, Lost }

public sealed class ArenaGameManager : Component
{
    private readonly Dictionary<Guid, Snapshot> _snapshots = new();
    private bool _announced;
    public Guid PlayerId { get; set; }
    public string PlayerName { get; set; } = "Player";
    public int RemainingEnemies { get; private set; }
    public ArenaGameState GameState { get; private set; } = ArenaGameState.Playing;
    public override int UpdateOrder => 2000;

    protected override void OnStart()
    {
        RuntimeScene? scene = AttachedGameObject?.Scene;
        if (scene == null) return;
        _snapshots.Clear();
        foreach (GameObject item in scene.GameObjects.Where(IsParticipant))
        {
            HealthComponent health = item.GetComponent<HealthComponent>()!;
            _snapshots[item.Id] = new Snapshot(item.Transform.LocalPosition, item.Transform.LocalRotation, health.MaxHealth, item.Active);
        }
        Evaluate(scene);
    }

    protected override void OnUpdate()
    {
        RuntimeScene? scene = AttachedGameObject?.Scene;
        if (scene == null) return;
        if (Input.IsKeyPressed(Key.R)) { Restart(scene); return; }
        Evaluate(scene);
    }

    public void Restart()
    {
        RuntimeScene? scene = AttachedGameObject?.Scene;
        if (scene != null) Restart(scene);
    }

    private void Restart(RuntimeScene scene)
    {
        foreach ((Guid id, Snapshot snapshot) in _snapshots)
        {
            GameObject? item = scene.FindGameObject(id);
            HealthComponent? health = item?.GetComponent<HealthComponent>();
            if (item == null || health == null) continue;
            item.Active = snapshot.Active;
            item.Transform.LocalPosition = snapshot.Position;
            item.Transform.LocalRotation = snapshot.Rotation;
            health.MaxHealth = snapshot.MaxHealth;
            health.CurrentHealth = snapshot.MaxHealth;
            item.GetComponent<CharacterController3D>()?.SetVelocity(System.Numerics.Vector3.Zero);
            item.GetComponent<SimpleEnemyAI3D>()?.ResetCombatState();
            item.GetComponent<ProjectileLauncher3D>()?.ResetCooldown();
        }
        foreach (GameObject projectile in scene.GameObjects.Where(item => item.GetComponent<Projectile3D>() != null).ToArray())
            scene.DestroyGameObject(projectile);
        GameState = ArenaGameState.Playing;
        _announced = false;
        Evaluate(scene);
        Console.WriteLine("[ByteArena] Arena restarted.");
    }

    private void Evaluate(RuntimeScene scene)
    {
        GameObject? player = PlayerId != Guid.Empty ? scene.FindGameObject(PlayerId) : null;
        if (player == null && !string.IsNullOrWhiteSpace(PlayerName)) player = scene.FindGameObject(PlayerName);
        foreach (GameObject enemy in scene.GameObjects.Where(item => item.GetComponent<SimpleEnemyAI3D>() != null))
        {
            if (enemy.GetComponent<HealthComponent>() is { IsDead: true }) enemy.Active = false;
        }
        RemainingEnemies = scene.GameObjects.Count(item => item.ActiveInHierarchy &&
            item.GetComponent<SimpleEnemyAI3D>() != null && item.GetComponent<HealthComponent>() is { IsDead: false });
        GameState = player?.GetComponent<HealthComponent>() is { IsDead: true } ? ArenaGameState.Lost :
            RemainingEnemies == 0 ? ArenaGameState.Won : ArenaGameState.Playing;
        if (GameState != ArenaGameState.Playing && !_announced)
        {
            Console.WriteLine(GameState == ArenaGameState.Won
                ? "[ByteArena] YOU WON! Press R to restart."
                : "[ByteArena] YOU LOST! Press R to restart.");
            _announced = true;
        }
    }

    private static bool IsParticipant(GameObject item) => item.GetComponent<HealthComponent>() != null &&
        (item.GetComponent<PlayerController3D>() != null || item.GetComponent<SimpleEnemyAI3D>() != null);
    private sealed record Snapshot(System.Numerics.Vector3 Position, System.Numerics.Quaternion Rotation, float MaxHealth, bool Active);
}
