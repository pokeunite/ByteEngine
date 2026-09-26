using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;
using RuntimeScene = ByteEngine.Core.Scene.Scene;

namespace ByteEngine.Core.Gameplay;

public sealed class ProjectileLauncher3D : Component
{
    private float _projectileSpeed = 30f;
    private float _damage = 10f;
    private float _fireCooldown = .2f;
    private float _cooldownRemaining;

    public AssetReference ProjectileBlueprint { get; set; } =
        AssetReference.Empty;

    public float ProjectileSpeed
    {
        get => _projectileSpeed;
        set => _projectileSpeed = Safe(value);
    }

    public float Damage
    {
        get => _damage;
        set => _damage = Safe(value);
    }

    public float FireCooldown
    {
        get => _fireCooldown;
        set => _fireCooldown = Safe(value);
    }

    public Vector3 MuzzleOffset { get; set; }

    public bool CanFire =>
        _cooldownRemaining <=
        0f;

    public void ResetCooldown() =>
        _cooldownRemaining =
            0f;

    public GameObject? Fire()
    {
        Vector3 direction =
            AttachedGameObject?.Transform.Forward ??
            Vector3.Zero;

        return FireInternal(
            direction,
            "ObjectForward");
    }

    public GameObject? Fire(
        Vector3 direction) =>
        FireInternal(
            direction,
            "ExplicitDirection");

    private GameObject? FireInternal(
        Vector3 direction,
        string route)
    {
        GameObject? shooter =
            AttachedGameObject;

        RuntimeScene? scene =
            shooter?.Scene;

        if (shooter == null ||
            scene == null)
        {
            RuntimeDiagnostics.RecordWeaponRaycast(
                $"PROJECTILE BLOCKED route={route} reason=NoShooterOrScene");
            return null;
        }

        if (!CanFire)
        {
            RuntimeDiagnostics.RecordWeaponRaycast(
                $"PROJECTILE BLOCKED route={route} shooter=\"{shooter.Name}\" reason=Cooldown remaining={_cooldownRemaining:0.000}s");
            return null;
        }

        if (!IsUsableDirection(direction))
        {
            RuntimeDiagnostics.RecordWeaponRaycast(
                $"PROJECTILE BLOCKED route={route} shooter=\"{shooter.Name}\" reason=InvalidDirection requested={V3(direction)}");
            return null;
        }

        Vector3 muzzle =
            Vector3.Transform(
                MuzzleOffset,
                shooter.Transform.WorldMatrix);

        Vector3 normalizedDirection =
            Vector3.Normalize(direction);

        GameObject? root =
            null;

        if (!ProjectileBlueprint.IsEmpty &&
            RuntimeSpawnService
                .IsBlueprintSpawnerConfigured)
        {
            root =
                RuntimeSpawnService
                    .SpawnBlueprint(
                        scene,
                        ProjectileBlueprint,
                        muzzle);
        }

        bool usedFallback =
            root == null;

        if (root == null)
        {
            root =
                scene.CreateGameObject(
                    "Projectile");

            root.Transform.WorldPosition =
                muzzle;

            /*
             * Keep fallback projectiles very obvious in a fast arena game.
             * This is intentionally larger than a physically realistic bullet
             * because readability matters more than scale for the demo.
             */
            root.Transform.LocalScale =
                new Vector3(
                    .24f);

            root.AddComponent(
                new MeshRenderer
                {
                    Primitive =
                        PrimitiveMeshType.Sphere,

                    Material =
                        new Material
                        {
                            BaseColor =
                                new Vector4(
                                    1f,
                                    .72f,
                                    .05f,
                                    1f),

                            Roughness =
                                .25f
                        }
                });

            root.AddComponent(
                new Projectile3D
                {
                    Radius =
                        .12f
                });

            root.AddComponent(
                new LifetimeComponent
                {
                    LifetimeSeconds =
                        5f
                });
        }

        Projectile3D projectile =
            FindProjectile(
                root) ??
            root.AddComponent(
                new Projectile3D());

        projectile.OwnerId =
            shooter.Id;

        projectile.Damage =
            Damage;

        projectile.Velocity =
            normalizedDirection *
            ProjectileSpeed;

        _cooldownRemaining =
            FireCooldown;

        Camera3D? camera =
            scene.ActiveCamera;

        Vector3 projectileForward =
            root.Transform.Forward;

        float visualToVelocityAngle =
            AngleDegrees(
                projectileForward,
                normalizedDirection);

        RuntimeDiagnostics.RecordWeaponRaycast(
            $"PROJECTILE FIRE route={route} " +
            $"shooter=\"{shooter.Name}\" pos={V3(shooter.Transform.WorldPosition)} euler={V3(shooter.Transform.EulerAngles)} " +
            $"shooterFwd={V3(shooter.Transform.Forward)} shooterRight={V3(shooter.Transform.Right)} shooterUp={V3(shooter.Transform.Up)} " +
            $"muzzleOffset={V3(MuzzleOffset)} muzzle={V3(muzzle)} requestedDir={V3(direction)} velocityDir={V3(normalizedDirection)} " +
            $"speed={ProjectileSpeed:0.000} velocity={V3(projectile.Velocity)} " +
            $"cameraPos={(camera != null ? V3(camera.Transform.WorldPosition) : "<none>")} cameraFwd={(camera != null ? V3(camera.Transform.Forward) : "<none>")} " +
            $"blueprint={(ProjectileBlueprint.IsEmpty ? "<empty>" : ProjectileBlueprint.ToString())} fallback={usedFallback} " +
            $"spawn=\"{root.Name}\" spawnFwd={V3(projectileForward)} spawnEuler={V3(root.Transform.EulerAngles)} visualVsVelocity={visualToVelocityAngle:0.00}deg");

        if (RuntimeDiagnostics.DebugWeaponRaycast)
        {
            DrawDebugDirection(
                scene,
                muzzle,
                normalizedDirection,
                4f,
                1f);
        }

        return root;
    }

    protected override void OnUpdate()
    {
        _cooldownRemaining =
            Math.Max(
                0f,
                _cooldownRemaining -
                (float)Time.DeltaTime);
    }

    private static Projectile3D? FindProjectile(
        GameObject root)
    {
        Projectile3D? projectile =
            root.GetComponent<Projectile3D>();

        if (projectile !=
            null)
        {
            return projectile;
        }

        foreach (GameObject child
                 in root.Children)
        {
            projectile =
                FindProjectile(
                    child);

            if (projectile !=
                null)
            {
                return projectile;
            }
        }

        return null;
    }

    private static void DrawDebugDirection(
        RuntimeScene scene,
        Vector3 start,
        Vector3 direction,
        float length,
        float duration)
    {
        if (!IsUsableDirection(direction) ||
            !float.IsFinite(length) ||
            length <= .0001f)
        {
            return;
        }

        Vector3 end =
            start +
            Vector3.Normalize(direction) *
            length;

        Vector3 delta =
            end -
            start;

        GameObject line =
            scene.CreateGameObject(
                "__DebugProjectileDirection");

        line.Transform.WorldPosition =
            (start + end) *
            .5f;

        line.Transform.WorldRotation =
            RotationFromTo(
                Vector3.UnitZ,
                Vector3.Normalize(delta));

        line.Transform.LocalScale =
            new Vector3(
                .02f,
                .02f,
                delta.Length());

        line.AddComponent(
            new MeshRenderer
            {
                Primitive =
                    PrimitiveMeshType.Cube,

                UsePrimitive =
                    true,

                FrustumCulling =
                    false,

                CastShadows =
                    false,

                ReceiveShadows =
                    false,

                Material =
                    new Material
                    {
                        // Cyan = projectile launch/velocity direction.
                        BaseColor =
                            new Vector4(
                                .05f,
                                .85f,
                                1f,
                                1f),

                        Roughness =
                            .15f,

                        BlendMode =
                            BlendMode3D.Additive,

                        DepthTest =
                            false,

                        DepthWriteMode =
                            DepthWriteMode3D.Disabled,

                        CullMode =
                            CullMode3D.None
                    }
            });

        line.AddComponent(
            new LifetimeComponent
            {
                LifetimeSeconds =
                    Math.Clamp(
                        duration,
                        .05f,
                        10f)
            });
    }

    private static Quaternion RotationFromTo(
        Vector3 from,
        Vector3 to)
    {
        from = Vector3.Normalize(from);
        to = Vector3.Normalize(to);

        float dot =
            Math.Clamp(
                Vector3.Dot(from, to),
                -1f,
                1f);

        if (dot > .999999f)
            return Quaternion.Identity;

        if (dot < -.999999f)
        {
            Vector3 axis =
                Vector3.Cross(
                    from,
                    Vector3.UnitX);

            if (axis.LengthSquared() < .000001f)
            {
                axis =
                    Vector3.Cross(
                        from,
                        Vector3.UnitY);
            }

            return Quaternion.CreateFromAxisAngle(
                Vector3.Normalize(axis),
                MathF.PI);
        }

        Vector3 rotationAxis =
            Vector3.Normalize(
                Vector3.Cross(
                    from,
                    to));

        return Quaternion.CreateFromAxisAngle(
            rotationAxis,
            MathF.Acos(dot));
    }

    private static float AngleDegrees(
        Vector3 a,
        Vector3 b)
    {
        if (!IsUsableDirection(a) ||
            !IsUsableDirection(b))
        {
            return 0f;
        }

        float dot =
            Math.Clamp(
                Vector3.Dot(
                    Vector3.Normalize(a),
                    Vector3.Normalize(b)),
                -1f,
                1f);

        return MathF.Acos(dot) *
               180f /
               MathF.PI;
    }

    private static string V3(
        Vector3 value) =>
        $"({value.X:0.000},{value.Y:0.000},{value.Z:0.000})";

    private static float Safe(
        float value) =>
        float.IsFinite(value)
            ? Math.Max(
                0f,
                value)
            : 0f;

    private static bool IsUsableDirection(Vector3 direction) =>
        float.IsFinite(direction.X) && float.IsFinite(direction.Y) && float.IsFinite(direction.Z) &&
        direction.LengthSquared() > .000001f;
}
