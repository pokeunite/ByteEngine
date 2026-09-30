using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;

namespace ByteEngine.Tests;

internal static class LastStandFoundationTests
{
    public static void Run()
    {
        var scene = new Scene("Pickup test");
        GameObject player = scene.CreateGameObject("Player");
        HealthComponent health = player.AddComponent(new HealthComponent
        {
            MaxHealth = 100,
            CurrentHealth = 40
        });
        player.AddComponent(new CapsuleCollider3D { Height = 2, Radius = .4f });
        GameObject pickup = scene.CreateGameObject("Medkit");
        pickup.AddComponent(new BoxCollider3D { IsTrigger = true });
        pickup.AddComponent(new HealthPickup3D { Amount = 25, RequirePlayerController = false });
        scene.LoadInternal();
        Time.Update(1.0 / 60.0);
        scene.UpdateInternal();
        Check(health.CurrentHealth == 65 && scene.FindGameObject(pickup.Id) == null,
            "pickup heals exactly once and is destroyed");

        var manager = new SceneManager();
        Scene initial = new("Restart test");
        initial.CreateGameObject("Transient");
        manager.RestartSceneFactory = () => new Scene("Restart test");
        manager.LoadScene(initial);
        var registry = VisualLogicRegistry.CreateDefault();
        Check(registry.TryGetAction("scene.restart", out var action) && action != null,
            "restart action is registered");
        action!.Execute(new VisualInstruction { Id = "scene.restart" },
            new EventExecutionContext
            {
                Scene = initial,
                Self = initial.FindGameObject("Transient")!,
                Globals = new VariableStore()
            });
        Time.Update(1.0 / 60.0);
        manager.UpdateInternal();
        Check(!ReferenceEquals(manager.ActiveScene, initial) &&
            manager.ActiveScene?.FindGameObject("Transient") == null,
            "restart loads a fresh scene after the update");
        manager.UnloadScene();
        Console.WriteLine("Last Stand foundation tests passed.");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Last Stand: " + message);
    }
}
