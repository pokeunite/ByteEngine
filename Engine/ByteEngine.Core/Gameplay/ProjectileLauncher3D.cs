using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;
using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Gameplay;

public sealed class ProjectileLauncher3D : Component
{
    private float _projectileSpeed = 30f, _damage = 10f, _fireCooldown = .2f, _cooldownRemaining;

    public AssetReference ProjectileBlueprint { get; set; } = AssetReference.Empty;
    public float ProjectileSpeed { get => _projectileSpeed; set => _projectileSpeed = Safe(value); }
    public float Damage { get => _damage; set => _damage = Safe(value); }
    public float FireCooldown { get => _fireCooldown; set => _fireCooldown = Safe(value); }
    public Vector3 MuzzleOffset { get; set; }
    public bool CanFire => _cooldownRemaining <= 0f;

    public void ResetCooldown() => _cooldownRemaining = 0f;

    public GameObject? Fire()
    {
        GameObject? shooter = AttachedGameObject;
        RuntimeScene? scene = shooter?.Scene;
        if (shooter == null || scene == null || !CanFire) return null;
        Vector3 muzzle = Vector3.Transform(MuzzleOffset, shooter.Transform.WorldMatrix);
        GameObject? root = null;
        if (!ProjectileBlueprint.IsEmpty && RuntimeSpawnService.IsBlueprintSpawnerConfigured)
            root = RuntimeSpawnService.SpawnBlueprint(scene, ProjectileBlueprint, muzzle);
        if (root == null)
        {
            root = scene.CreateGameObject("Projectile");
            root.Transform.WorldPosition = muzzle;
            root.Transform.LocalScale = new Vector3(.15f);
            root.AddComponent(new MeshRenderer
            {
                Primitive = PrimitiveMeshType.Sphere,
                Material = new Material { BaseColor = new Vector4(1f, .75f, .08f, 1f) }
            });
            root.AddComponent(new Projectile3D());
            root.AddComponent(new LifetimeComponent());
        }
        Projectile3D projectile = FindProjectile(root) ?? root.AddComponent(new Projectile3D());
        projectile.OwnerId = shooter.Id;
        projectile.Damage = Damage;
        projectile.Velocity = shooter.Transform.Forward * ProjectileSpeed;
        _cooldownRemaining = FireCooldown;
        return root;
    }

    protected override void OnUpdate() => _cooldownRemaining = Math.Max(0f, _cooldownRemaining - (float)Time.DeltaTime);

    private static Projectile3D? FindProjectile(GameObject root)
    {
        Projectile3D? projectile = root.GetComponent<Projectile3D>();
        if (projectile != null) return projectile;
        foreach (GameObject child in root.Children) if ((projectile = FindProjectile(child)) != null) return projectile;
        return null;
    }

    private static float Safe(float value) => float.IsFinite(value) ? Math.Max(0f, value) : 0f;
}
