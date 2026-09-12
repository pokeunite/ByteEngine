using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Serialization;

namespace ByteEngine.Tests;

internal static class Phase1GameplayTests
{
    public static void Run(string root, AssetDatabase database, AssetManager assets)
    {
        TestHealth();
        TestLifetime();
        TestRaycasts();
        TestProjectile();
        TestLauncher();
        TestEnemyAi();
        TestSerialization(root, database, assets);
    }

    private static void TestHealth()
    {
        var health = new HealthComponent { MaxHealth = 100f, CurrentHealth = 100f };
        int deaths = 0;
        health.Died += _ => deaths++;
        health.Damage(35f);
        Assert(Near(health.CurrentHealth, 65f), "Health damage");
        health.Heal(20f);
        Assert(Near(health.CurrentHealth, 85f), "Health healing");
        health.Heal(1000f);
        Assert(Near(health.CurrentHealth, 100f), "Health healing clamps to maximum");
        health.Invulnerable = true;
        health.Damage(50f);
        Assert(Near(health.CurrentHealth, 100f), "Invulnerable health ignores damage");
        health.Invulnerable = false;
        health.Kill();
        health.Kill();
        health.Damage(5f);
        Assert(health.IsDead && Near(health.HealthPercent, 0f) && deaths == 1, "Kill and death trigger once");
    }

    private static void TestLifetime()
    {
        var scene = new Scene("Lifetime");
        GameObject temporary = scene.CreateGameObject("Temporary");
        temporary.AddComponent(new LifetimeComponent { LifetimeSeconds = .1f });
        scene.LoadInternal();
        Tick(scene, .05);
        Assert(scene.FindGameObject(temporary.Id) != null, "Lifetime remains before expiry");
        Tick(scene, .06);
        Assert(scene.FindGameObject(temporary.Id) == null, "Lifetime destroys object after expiry");
    }

    private static void TestRaycasts()
    {
        var scene = new Scene("Queries");
        GameObject ignored = scene.CreateGameObject("Ignored");
        ignored.Transform.WorldPosition = new Vector3(0, 0, -2);
        ignored.AddComponent(new BoxCollider3D { Size = Vector3.One });
        GameObject box = scene.CreateGameObject("Box");
        box.Transform.WorldPosition = new Vector3(0, 0, -5);
        box.AddComponent(new BoxCollider3D { Size = Vector3.One });
        GameObject capsule = scene.CreateGameObject("Capsule");
        capsule.Transform.WorldPosition = new Vector3(3, 0, -5);
        capsule.AddComponent(new CapsuleCollider3D { Radius = .5f, Height = 2f });

        Assert(GameplayQuery3D.Raycast(scene, Vector3.Zero, -Vector3.UnitZ, out RaycastHit3D first, 20f) &&
            first.GameObject == ignored && Near(first.Distance, 1.5f), "BoxCollider3D raycast");
        Assert(GameplayQuery3D.Raycast(scene, Vector3.Zero, -Vector3.UnitZ, out RaycastHit3D second, 20f, ignored) &&
            second.GameObject == box && Near(second.Distance, 4.5f), "Raycast ignores one object");
        Assert(!GameplayQuery3D.Raycast(scene, Vector3.Zero, -Vector3.UnitZ, out _, 1f), "Raycast max distance");
        Assert(GameplayQuery3D.Raycast(scene, new Vector3(3, 0, 0), -Vector3.UnitZ, out RaycastHit3D capsuleHit, 20f) &&
            capsuleHit.GameObject == capsule && Near(capsuleHit.Distance, 4.5f), "CapsuleCollider3D raycast");
    }

    private static void TestProjectile()
    {
        var scene = new Scene("Projectile");
        GameObject owner = scene.CreateGameObject("Owner");
        owner.AddComponent(new BoxCollider3D { Size = new Vector3(2, 2, 2) });
        HealthComponent ownerHealth = owner.AddComponent(new HealthComponent());
        GameObject target = scene.CreateGameObject("Target");
        target.Transform.WorldPosition = new Vector3(0, 0, -8);
        target.AddComponent(new BoxCollider3D { Size = Vector3.One });
        HealthComponent targetHealth = target.AddComponent(new HealthComponent());
        GameObject projectileObject = scene.CreateGameObject("Fast Projectile");
        projectileObject.AddComponent(new Projectile3D
        {
            Velocity = new Vector3(0, 0, -100),
            Damage = 25f,
            Radius = .05f,
            DestroyOnHit = true,
            OwnerId = owner.Id
        });
        scene.LoadInternal();
        Tick(scene, .1);
        Assert(Near(ownerHealth.CurrentHealth, 100f), "Projectile ignores owner");
        Assert(Near(targetHealth.CurrentHealth, 75f), "Projectile damages HealthComponent");
        Assert(scene.FindGameObject(projectileObject.Id) == null, "Projectile swept movement hits and destroys");
    }

    private static void TestLauncher()
    {
        var scene = new Scene("Launcher");
        GameObject shooter = scene.CreateGameObject("Shooter");
        var launcher = shooter.AddComponent(new ProjectileLauncher3D
        {
            ProjectileSpeed = 40f,
            Damage = 12f,
            FireCooldown = .5f,
            MuzzleOffset = new Vector3(0, 0, -1)
        });
        scene.LoadInternal();
        GameObject? first = launcher.Fire();
        GameObject? blocked = launcher.Fire();
        Assert(first?.GetComponent<Projectile3D>() is { } projectile && projectile.OwnerId == shooter.Id &&
            Near(projectile.Damage, 12f) && projectile.Velocity.Length() > 39f, "Launcher configures projectile ownership and velocity");
        Assert(blocked == null && !launcher.CanFire, "ProjectileLauncher cooldown blocks fire");
        Tick(scene, .49);
        Assert(launcher.Fire() == null, "ProjectileLauncher cooldown remains deterministic");
        Tick(scene, .02);
        Assert(launcher.CanFire && launcher.Fire() != null, "ProjectileLauncher fires after cooldown");
    }

    private static void TestEnemyAi()
    {
        var scene = new Scene("Enemy AI");
        GameObject player = scene.CreateGameObject("Player");
        player.Transform.WorldPosition = new Vector3(0, 0, -1);
        HealthComponent playerHealth = player.AddComponent(new HealthComponent());
        GameObject enemy = scene.CreateGameObject("Enemy");
        enemy.AddComponent(new HealthComponent());
        enemy.AddComponent(new SimpleEnemyAI3D
        {
            TargetId = player.Id,
            MoveSpeed = 0f,
            DetectionRange = 10f,
            AttackRange = 2f,
            StopDistance = 1f,
            Damage = 10f,
            AttackCooldown = .5f
        });
        scene.LoadInternal();
        Tick(scene, .01);
        Assert(Near(playerHealth.CurrentHealth, 90f), "SimpleEnemyAI attacks in range");
        Tick(scene, .25);
        Assert(Near(playerHealth.CurrentHealth, 90f), "SimpleEnemyAI respects attack cooldown");
        Tick(scene, .26);
        Assert(Near(playerHealth.CurrentHealth, 80f), "SimpleEnemyAI attacks after cooldown");
        enemy.GetComponent<HealthComponent>()!.Kill();
        Tick(scene, 1.0);
        Assert(Near(playerHealth.CurrentHealth, 80f), "Dead SimpleEnemyAI stops attacking");
    }

    private static void TestSerialization(string root, AssetDatabase database, AssetManager assets)
    {
        var serializer = new SceneSerializer(new ComponentSerializer(root, database, assets));
        var scene = new Scene("Gameplay Persistence");
        GameObject owner = scene.CreateGameObject("Gameplay Object");
        Guid targetId = Guid.NewGuid(), blueprintId = Guid.NewGuid();
        owner.AddComponent(new HealthComponent { MaxHealth = 250f, CurrentHealth = 125f, Invulnerable = true, DestroyOnDeath = true });
        owner.AddComponent(new LifetimeComponent { LifetimeSeconds = 8.5f });
        owner.AddComponent(new Projectile3D { Velocity = new Vector3(1, 2, 3), Damage = 17f, Radius = .3f, DestroyOnHit = false, OwnerId = owner.Id });
        owner.AddComponent(new ProjectileLauncher3D
        {
            ProjectileBlueprint = new AssetReference(blueprintId, "Assets/Projectile.byteblueprint"),
            ProjectileSpeed = 55f,
            Damage = 22f,
            FireCooldown = .4f,
            MuzzleOffset = new Vector3(1, 2, -3)
        });
        owner.AddComponent(new SimpleEnemyAI3D
        {
            TargetId = targetId,
            TargetName = "Player",
            MoveSpeed = 4f,
            DetectionRange = 30f,
            AttackRange = 2.5f,
            Damage = 13f,
            AttackCooldown = .75f,
            StopDistance = 1.25f
        });
        Scene clone = serializer.CloneForRuntime(scene);
        GameObject copy = clone.FindGameObject("Gameplay Object")!;
        HealthComponent health = copy.GetComponent<HealthComponent>()!;
        LifetimeComponent lifetime = copy.GetComponent<LifetimeComponent>()!;
        Projectile3D projectile = copy.GetComponent<Projectile3D>()!;
        ProjectileLauncher3D launcher = copy.GetComponent<ProjectileLauncher3D>()!;
        SimpleEnemyAI3D ai = copy.GetComponent<SimpleEnemyAI3D>()!;
        Assert(Near(health.MaxHealth, 250f) && Near(health.CurrentHealth, 125f) && health.Invulnerable && health.DestroyOnDeath, "HealthComponent serialization");
        Assert(Near(lifetime.LifetimeSeconds, 8.5f), "LifetimeComponent serialization");
        Assert(projectile.Velocity == new Vector3(1, 2, 3) && Near(projectile.Damage, 17f) && Near(projectile.Radius, .3f) && !projectile.DestroyOnHit && projectile.OwnerId == owner.Id, "Projectile3D serialization");
        Assert(launcher.ProjectileBlueprint.Guid == blueprintId && Near(launcher.ProjectileSpeed, 55f) && Near(launcher.Damage, 22f) && Near(launcher.FireCooldown, .4f) && launcher.MuzzleOffset == new Vector3(1, 2, -3), "ProjectileLauncher3D serialization");
        Assert(ai.TargetId == targetId && ai.TargetName == "Player" && Near(ai.MoveSpeed, 4f) && Near(ai.DetectionRange, 30f) && Near(ai.AttackRange, 2.5f) && Near(ai.Damage, 13f) && Near(ai.AttackCooldown, .75f) && Near(ai.StopDistance, 1.25f), "SimpleEnemyAI3D serialization");

        string scenePath = Path.Combine(root, "Scenes", "Phase1Gameplay.bytescene");
        serializer.Save(scene, scenePath);
        GameObject diskCopy = serializer.Load(scenePath).FindGameObject("Gameplay Object")!;
        Assert(diskCopy.GetComponent<HealthComponent>() != null &&
            diskCopy.GetComponent<LifetimeComponent>() != null &&
            diskCopy.GetComponent<Projectile3D>() != null &&
            diskCopy.GetComponent<ProjectileLauncher3D>() != null &&
            diskCopy.GetComponent<SimpleEnemyAI3D>() != null,
            "All Phase 1 components survive scene save/load");
    }

    private static void Tick(Scene scene, double deltaTime) { Time.Update(deltaTime); scene.UpdateInternal(); }
    private static bool Near(float value, float expected) => Math.Abs(value - expected) < .001f;
    private static void Assert(bool condition, string name) { if (!condition) throw new InvalidOperationException("FAILED: " + name); }
}
