using System.Numerics;
using ByteEngine.Core.Classification;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;

namespace ByteEngine.Core.VisualLogic;

public sealed partial class VisualLogicRegistry
{
    private static void RegisterPhysicsAndCombat(VisualLogicRegistry registry)
    {
        registry.RegisterCondition(new VisualConditionDefinition
        {
            Id = "physics.rayHitsAnything",
            Category = "Physics / Raycasts",
            DisplayName = "Ray Hits Anything",
            Evaluate = CastEventRay
        });
        registry.RegisterCondition(new VisualConditionDefinition
        {
            Id = "physics.lastRayHit",
            Category = "Physics / Raycasts",
            DisplayName = "Last Raycast Hit",
            Evaluate = (_, context) => context.RaycastPerformed && context.LastRaycastHit.HasValue
        });
        registry.RegisterCondition(new VisualConditionDefinition
        {
            Id = "physics.lastRayMissed",
            Category = "Physics / Raycasts",
            DisplayName = "Last Raycast Missed",
            Evaluate = (_, context) => context.RaycastPerformed && !context.LastRaycastHit.HasValue
        });
        registry.RegisterCondition(new VisualConditionDefinition
        {
            Id = "physics.lastRayHitObject",
            Category = "Physics / Raycasts",
            DisplayName = "Last Raycast Hit Object",
            Evaluate = (instruction, context) =>
                context.LastRaycastHit is { } hit &&
                ReferenceEquals(hit.GameObject, ResolveObjectTarget(instruction, context, false))
        });
        registry.RegisterAction(new VisualActionDefinition
        {
            Id = "physics.castRay",
            Category = "Physics / Raycasts",
            DisplayName = "Cast Ray",
            Execute = (instruction, context) => CastEventRay(instruction, context)
        });
        registry.RegisterAction(new VisualActionDefinition
        {
            Id = "physics.saveRayHit",
            Category = "Physics / Raycasts",
            DisplayName = "Save Raycast Result",
            Execute = (instruction, context) =>
            {
                string prefix = EventValueResolver.GetString(instruction, "prefix", context, "Ray").Trim();
                if (prefix.Length == 0 || prefix.Length > 64) return;
                RaycastHit3D? hit = context.LastRaycastHit;
                context.Self.Variables.Set(prefix + "Hit", VariableValue.FromBoolean(hit.HasValue));
                context.Self.Variables.Set(prefix + "ObjectId",
                    VariableValue.FromString(hit?.GameObject.Id.ToString() ?? string.Empty));
                context.Self.Variables.Set(prefix + "Point",
                    VariableValue.FromVector3(hit?.Point ?? Vector3.Zero));
                context.Self.Variables.Set(prefix + "Normal",
                    VariableValue.FromVector3(hit?.Normal ?? Vector3.Zero));
                context.Self.Variables.Set(prefix + "Distance",
                    VariableValue.FromNumber(hit?.Distance ?? 0f));
            }
        });
        registry.RegisterCondition(new VisualConditionDefinition
        {
            Id = "combat.canFire",
            Category = "Combat / Weapons",
            DisplayName = "Weapon Can Fire",
            TargetComponent = nameof(ProjectileLauncher3D),
            Evaluate = (instruction, context) =>
                ResolveObjectTarget(instruction, context, false)?
                    .GetComponent<ProjectileLauncher3D>()?.CanFire == true
        });
        registry.RegisterAction(new VisualActionDefinition
        {
            Id = "combat.fireWeapon",
            Category = "Combat / Weapons",
            DisplayName = "Fire Weapon",
            TargetComponent = nameof(ProjectileLauncher3D),
            Execute = (instruction, context) =>
                ResolveObjectTarget(instruction, context)?
                    .GetComponent<ProjectileLauncher3D>()?.Fire()
        });
        registry.RegisterAction(new VisualActionDefinition
        {
            Id = "combat.damageLastRayHit",
            Category = "Combat / Damage",
            DisplayName = "Damage Last Raycast Hit",
            Execute = (instruction, context) =>
            {
                if (context.LastRaycastHit is not { } hit) return;
                float amount = (float)EventValueResolver.GetNumber(instruction, "amount", context, 20);
                hit.GameObject.GetComponent<HealthComponent>()?.Damage(amount);
            }
        });
    }

    private static bool CastEventRay(VisualInstruction instruction, EventExecutionContext context)
    {
        context.RaycastPerformed = true;
        context.LastRaycastHit = null;
        GameObject? source = ResolveObjectArgument(instruction, "source", context);
        if (source == null) return false;

        Vector3 localDirection = EventValueResolver.GetVector3(
            instruction, "direction", context, new Vector3(0f, 0f, -1f));
        bool worldSpace = EventValueResolver.GetBoolean(instruction, "worldSpace", context, false);
        Vector3 worldDirection = worldSpace
            ? localDirection
            : Vector3.Transform(localDirection, source.Transform.WorldRotation);
        float distance = (float)EventValueResolver.GetNumber(instruction, "distance", context, 100f);
        bool drawDebug = EventValueResolver.GetBoolean(instruction, "drawDebug", context, false);
        float debugDuration = (float)EventValueResolver.GetNumber(instruction, "debugDuration", context, .25);

        if (!float.IsFinite(distance) || distance <= 0f ||
            !float.IsFinite(worldDirection.X) || !float.IsFinite(worldDirection.Y) ||
            !float.IsFinite(worldDirection.Z) || worldDirection.LengthSquared() < .000001f)
            return false;

        Vector3 localOrigin = EventValueResolver.GetVector3(
            instruction, "originOffset", context, Vector3.Zero);
        Vector3 origin = Vector3.Transform(localOrigin, source.Transform.WorldMatrix);
        int layer = (int)EventValueResolver.GetNumber(instruction, "layer", context, -1);
        LayerMask mask = layer is >= 0 and < 32 ? LayerMask.FromLayers(layer) : LayerMask.All;
        bool includeTriggers = EventValueResolver.GetBoolean(
            instruction, "includeTriggers", context, false);
        GameObject ignoredOwner = source;
        for (GameObject? ancestor = source.Parent; ancestor != null; ancestor = ancestor.Parent)
        {
            if (ancestor.GetComponent<CharacterController3D>() == null &&
                ancestor.GetComponent<ProjectileLauncher3D>() == null) continue;
            ignoredOwner = ancestor;
            break;
        }

        bool found = GameplayQuery3D.Raycast(context.Scene, origin, worldDirection,
            out RaycastHit3D hit, distance, ignoredOwner, mask, source,
            includeTriggers: includeTriggers);
        if (found) context.LastRaycastHit = hit;

        Vector3 normalizedWorldDirection =
            Vector3.Normalize(worldDirection);

        Camera3D? debugCamera =
            context.Scene.ActiveCamera;

        string hitText =
            found
                ? $"HIT object=\"{hit.GameObject.Name}\" point={DebugV3(hit.Point)} normal={DebugV3(hit.Normal)} hitDistance={hit.Distance:0.000}"
                : "MISS";

        RuntimeDiagnostics.RecordWeaponRaycast(
            $"EVENT RAY self=\"{context.Self.Name}\" source=\"{source.Name}\" parent=\"{source.Parent?.Name ?? "<none>"}\" " +
            $"sourcePos={DebugV3(source.Transform.WorldPosition)} sourceEuler={DebugV3(source.Transform.EulerAngles)} " +
            $"sourceFwd={DebugV3(source.Transform.Forward)} sourceRight={DebugV3(source.Transform.Right)} sourceUp={DebugV3(source.Transform.Up)} " +
            $"localOrigin={DebugV3(localOrigin)} origin={DebugV3(origin)} localDir={DebugV3(localDirection)} worldSpace={worldSpace} " +
            $"worldDir={DebugV3(normalizedWorldDirection)} maxDistance={distance:0.000} layer={layer} triggers={includeTriggers} " +
            $"ignoredOwner=\"{ignoredOwner.Name}\" ownerFwd={DebugV3(ignoredOwner.Transform.Forward)} " +
            $"cameraPos={(debugCamera != null ? DebugV3(debugCamera.Transform.WorldPosition) : "<none>")} " +
            $"cameraFwd={(debugCamera != null ? DebugV3(debugCamera.Transform.Forward) : "<none>")} result={hitText}");

        if (drawDebug)
        {
            Vector3 normalizedDirection = Vector3.Normalize(worldDirection);
            Vector3 end = found
                ? hit.Point
                : origin + normalizedDirection * distance;
            DrawDebugRay(context.Scene, origin, end, found, debugDuration);
        }

        return found;
    }

    /// <summary>
    /// Lightweight Event Sheet ray visualizer.
    ///
    /// The physics query remains completely separate from rendering. When the
    /// author explicitly enables Draw Debug Ray, a short-lived, non-colliding
    /// thin cube is rendered along the ray. This mirrors the editor/debug-draw
    /// workflow used by larger engines while requiring no change to the physics
    /// backend. Because it has no collider it cannot affect later raycasts.
    /// </summary>
    private static void DrawDebugRay(
        ByteEngine.Core.Scene.Scene scene,
        Vector3 start,
        Vector3 end,
        bool hit,
        float duration)
    {
        Vector3 delta = end - start;
        float length = delta.Length();
        if (!float.IsFinite(length) || length <= .0001f) return;

        duration = float.IsFinite(duration)
            ? Math.Clamp(duration, .05f, 10f)
            : .25f;

        GameObject debugRay = scene.CreateGameObject(
            hit ? "__DebugRay_Hit" : "__DebugRay_Miss");

        debugRay.Transform.WorldPosition = (start + end) * .5f;
        debugRay.Transform.WorldRotation = RotationFromTo(
            Vector3.UnitZ,
            delta / length);
        debugRay.Transform.LocalScale = new Vector3(.018f, .018f, length);

        debugRay.AddComponent(new MeshRenderer
        {
            Primitive = PrimitiveMeshType.Cube,
            UsePrimitive = true,
            FrustumCulling = false,
            CastShadows = false,
            ReceiveShadows = false,
            AutomaticRenderQueue = true,
            Material = new Material
            {
                // Red = a blocking hit, green = the ray reached full distance.
                BaseColor = hit
                    ? new Vector4(1f, .08f, .04f, 1f)
                    : new Vector4(.05f, 1f, .18f, 1f),
                Roughness = .15f,
                BlendMode = BlendMode3D.Additive,
                DepthTest = false,
                DepthWriteMode = DepthWriteMode3D.Disabled,
                CullMode = CullMode3D.None
            }
        });

        debugRay.AddComponent(new LifetimeComponent
        {
            LifetimeSeconds = duration
        });
    }

    private static string DebugV3(
        Vector3 value) =>
        $"({value.X:0.000},{value.Y:0.000},{value.Z:0.000})";

    private static Quaternion RotationFromTo(Vector3 from, Vector3 to)
    {
        from = Vector3.Normalize(from);
        to = Vector3.Normalize(to);
        float dot = Math.Clamp(Vector3.Dot(from, to), -1f, 1f);

        if (dot > .999999f)
            return Quaternion.Identity;

        if (dot < -.999999f)
        {
            Vector3 axis = Vector3.Cross(from, Vector3.UnitX);
            if (axis.LengthSquared() < .000001f)
                axis = Vector3.Cross(from, Vector3.UnitY);
            return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.PI);
        }

        Vector3 rotationAxis = Vector3.Normalize(Vector3.Cross(from, to));
        return Quaternion.CreateFromAxisAngle(rotationAxis, MathF.Acos(dot));
    }
}
