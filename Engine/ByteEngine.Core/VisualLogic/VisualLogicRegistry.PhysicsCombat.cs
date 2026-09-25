using System.Numerics;
using ByteEngine.Core.Classification;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
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
        return found;
    }
}
