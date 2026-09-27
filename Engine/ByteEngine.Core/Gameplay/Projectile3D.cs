using System.Numerics;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
using RuntimeScene = ByteEngine.Core.Scene.Scene;
using ByteEngine.Core.Classification;

namespace ByteEngine.Core.Gameplay;

public sealed class Projectile3D : Component
{
    private float _damage = 10f;
    private float _radius = .05f;
    private bool _stopped;
    private bool _debugStartRecorded;

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

        if (RuntimeDiagnostics.DebugWeaponRaycast && !_debugStartRecorded)
        {
            _debugStartRecorded = true;
            GameObject? owner = scene.GameObjects.FirstOrDefault(item => item.Id == OwnerId);
            string[] overlaps = GameplayQuery3D.OverlapSphere(
                    scene,
                    origin,
                    Math.Max(Radius, .01f),
                    projectile,
                    CollisionMask,
                    source: projectile,
                    includeTriggers: false)
                .Take(8)
                .Select(item =>
                    $"{item.GameObject.Name}:{item.GameObject.Id:N}:{item.Collider.GetType().Name}:" +
                    $"{LayerName(scene, item.GameObject.Layer)}:ground={IsGround(item.GameObject)}")
                .ToArray();

            RuntimeDiagnostics.RecordWeaponRaycast(
                $"PROJECTILE START projectile=\"{projectile.Name}\" id={projectile.Id:N} ownerId={OwnerId:N} owner=\"{owner?.Name ?? "<missing>"}\" " +
                $"origin={V3(origin)} velocity={V3(Velocity)} direction={V3(direction)} radius={Radius:0.000} damage={Damage:0.000} " +
                $"projectileLayer={LayerName(scene, projectile.Layer)} maskBits=0x{CollisionMask.Bits:X8} " +
                $"spawnOverlaps={(overlaps.Length == 0 ? "<none>" : "[" + string.Join(",", overlaps) + "]")}");
        }

        if (GameplayQuery3D.SphereCast(scene, origin, direction, Radius, out RaycastHit3D hit, distance,
                projectile, OwnerId, CollisionMask, projectile))
        {
            GameObject? healthOwner = FindHealthOwner(hit.GameObject);
            bool matrixAllows = scene.Classification.CollisionMatrix.ShouldInteract(
                projectile.Layer,
                hit.GameObject.Layer);

            RuntimeDiagnostics.RecordWeaponRaycast(
                $"PROJECTILE HIT projectile=\"{projectile.Name}\" id={projectile.Id:N} ownerId={OwnerId:N} " +
                $"stepOrigin={V3(origin)} stepDistance={distance:0.000} direction={V3(direction)} radius={Radius:0.000} " +
                $"object=\"{hit.GameObject.Name}\" objectId={hit.GameObject.Id:N} layer={LayerName(scene, hit.GameObject.Layer)} " +
                $"collider={hit.Collider.GetType().Name} point={V3(hit.Point)} normal={V3(hit.Normal)} hitDistance={hit.Distance:0.000} " +
                $"ground={IsGround(hit.GameObject)} healthOwner=\"{healthOwner?.Name ?? "<none>"}\" matrixAllows={matrixAllows} " +
                $"destroyOnHit={DestroyOnHit}");

            projectile.Transform.WorldPosition = hit.Point;
            hit.GameObject.GetComponent<HealthComponent>()?.Damage(Damage);
            if (DestroyOnHit) scene.DestroyGameObject(projectile);
            else { Velocity = Vector3.Zero; _stopped = true; }
            return;
        }
        projectile.Transform.WorldPosition = origin + displacement;
    }

    private static string LayerName(RuntimeScene scene, int layer) =>
        $"{layer}:{scene.Classification.FindLayer(layer)?.Name ?? "<missing>"}";

    private static bool IsGround(GameObject gameObject)
    {
        for (GameObject? current = gameObject;
             current != null;
             current = current.Parent)
        {
            if (current.GetComponent<GroundSurface>() != null)
                return true;
        }

        return false;
    }

    private static GameObject? FindHealthOwner(GameObject gameObject)
    {
        for (GameObject? current = gameObject;
             current != null;
             current = current.Parent)
        {
            if (current.GetComponent<HealthComponent>() != null)
                return current;
        }

        return null;
    }

    private static string V3(Vector3 value) =>
        $"({value.X:0.000},{value.Y:0.000},{value.Z:0.000})";

    private static float Safe(float value) => float.IsFinite(value) ? Math.Max(0f, value) : 0f;
}
