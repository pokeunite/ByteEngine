using System.Numerics;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using RuntimeScene = ByteEngine.Core.Scene.Scene;
using ByteEngine.Core.Classification;

namespace ByteEngine.Core.Gameplay;

public sealed class Projectile3D : Component
{
    private float _damage = 10f;
    private float _radius = .05f;
    private bool _stopped;

    public Vector3 Velocity { get; set; }
    public float Damage { get => _damage; set => _damage = Safe(value); }
    public float Radius { get => _radius; set => _radius = Safe(value); }
    public bool DestroyOnHit { get; set; } = true;
    public Guid OwnerId { get; set; }
    public LayerMask CollisionMask { get; set; } = LayerMask.All;

    protected override void OnUpdate()
    {
        if (_stopped || Velocity.LengthSquared() <= .0000001f) return;
        GameObject? projectile = AttachedGameObject;
        RuntimeScene? scene = projectile?.Scene;
        if (projectile == null || scene == null) return;
        Vector3 displacement = Velocity * (float)Time.DeltaTime;
        float distance = displacement.Length();
        if (distance <= .000001f) return;
        Vector3 origin = projectile.Transform.WorldPosition;
        Vector3 direction = displacement / distance;
        if (GameplayQuery3D.SphereCast(scene, origin, direction, Radius, out RaycastHit3D hit, distance,
                projectile, OwnerId, CollisionMask, projectile))
        {
            projectile.Transform.WorldPosition = hit.Point;
            hit.GameObject.GetComponent<HealthComponent>()?.Damage(Damage);
            if (DestroyOnHit) scene.DestroyGameObject(projectile);
            else { Velocity = Vector3.Zero; _stopped = true; }
            return;
        }
        projectile.Transform.WorldPosition = origin + displacement;
    }

    private static float Safe(float value) => float.IsFinite(value) ? Math.Max(0f, value) : 0f;
}
