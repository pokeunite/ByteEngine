using System.Numerics;

using ByteEngine.Core.Characters;
using ByteEngine.Core.Classification;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.VisualLogic;

/// <summary>
/// Universal finite Start -> End line tracing.
///
/// The physics primitive remains a single world-space segment, matching the
/// useful core of Unreal's Line Trace API. ByteEngine adds lightweight End
/// resolvers (object, direction, cursor, world position) so common gameplay
/// traces do not require users to assemble vector math for every node.
/// </summary>
public sealed partial class VisualLogicRegistry
{
    /// <summary>
    /// VisualLogicRegistry previously relied on the compiler-generated
    /// parameterless constructor. Keeping this constructor parameterless
    /// preserves that behavior while making the universal trace nodes
    /// available to every registry instance, including CreateDefault().
    /// </summary>
    public VisualLogicRegistry()
    {
        RegisterUniversalLineTrace(this);
    }

    private static void RegisterUniversalLineTrace(
        VisualLogicRegistry registry)
    {
        registry.RegisterAction(
            new VisualActionDefinition
            {
                Id = "physics.lineTrace",
                Category = "Physics / Raycasts",
                DisplayName = "Line Trace",
                Execute = (instruction, context) =>
                {
                    LineTraceEvent(
                        instruction,
                        context);
                }
            });

        registry.RegisterCondition(
            new VisualConditionDefinition
            {
                Id = "physics.lineTraceHitsAnything",
                Category = "Physics / Raycasts",
                DisplayName = "Line Trace Hits Anything",
                Evaluate = LineTraceEvent
            });
    }

    /// <summary>
    /// Resolve the user-friendly authoring inputs to one finite world-space
    /// segment, then store the first collider hit in LastRaycastHit.
    /// </summary>
    private static bool LineTraceEvent(
        VisualInstruction instruction,
        EventExecutionContext context)
    {
        context.RaycastPerformed = true;
        context.LastRaycastHit = null;

        string startMode =
            EventValueResolver.GetString(
                    instruction,
                    "startMode",
                    context,
                    "WorldPosition")
                .Trim();

        GameObject? startObject =
            null;

        Vector3 start;

        if (startMode.Equals(
                "Object",
                StringComparison.OrdinalIgnoreCase))
        {
            startObject =
                ResolveObjectArgument(
                    instruction,
                    "startObject",
                    context);

            if (startObject == null)
            {
                context.WarningSink?.Invoke(
                    "Line Trace could not resolve the Start Object.");

                return false;
            }

            // ByteEngine character/gameplay roots commonly use a feet pivot.
            // Starting a trace exactly on the ground surface makes the physics
            // query report the floor at distance zero, which looks like the
            // trace never ran and also produces a zero-length debug marker.
            //
            // Resolve Object starts the same way Object targets are resolved:
            // first enabled collider centre, otherwise the object's transform.
            // Empty muzzle objects therefore remain exact transform origins.
            start =
                ResolveTraceTargetPoint(
                    startObject);
        }
        else
        {
            // Backward compatible with the first universal Line Trace pass,
            // whose nodes only stored raw start/end Vector3 values.
            start =
                EventValueResolver.GetVector3(
                    instruction,
                    "start",
                    context,
                    Vector3.Zero);
        }

        if (!IsFiniteTraceVector(start))
        {
            context.WarningSink?.Invoke(
                "Line Trace requires a finite Start position.");

            return false;
        }

        int layer =
            (int)EventValueResolver.GetNumber(
                instruction,
                "layer",
                context,
                -1);

        LayerMask mask =
            layer is >= 0 and < 32
                ? LayerMask.FromLayers(layer)
                : LayerMask.All;

        bool ignoreStartHierarchy =
            EventValueResolver.GetBoolean(
                instruction,
                "ignoreSelf",
                context,
                true);

        bool includeTriggers =
            EventValueResolver.GetBoolean(
                instruction,
                "includeTriggers",
                context,
                false);

        bool drawDebug =
            EventValueResolver.GetBoolean(
                instruction,
                "drawDebug",
                context,
                false);

        float debugDuration =
            (float)EventValueResolver.GetNumber(
                instruction,
                "debugDuration",
                context,
                .25);

        GameObject? ignoreRoot =
            ignoreStartHierarchy
                ? ResolveTraceIgnoreRoot(
                    startObject ?? context.Self)
                : null;

        string endMode =
            EventValueResolver.GetString(
                    instruction,
                    "endMode",
                    context,
                    "WorldPosition")
                .Trim();

        Vector3 end;

        if (endMode.Equals(
                "Object",
                StringComparison.OrdinalIgnoreCase))
        {
            GameObject? target =
                ResolveObjectArgument(
                    instruction,
                    "endObject",
                    context);

            if (target == null)
            {
                context.WarningSink?.Invoke(
                    "Line Trace could not resolve the Target Object.");

                return false;
            }

            end =
                ResolveTraceTargetPoint(
                    target);
        }
        else if (endMode.Equals(
                     "Direction",
                     StringComparison.OrdinalIgnoreCase))
        {
            float distance =
                (float)EventValueResolver.GetNumber(
                    instruction,
                    "distance",
                    context,
                    100f);

            if (!float.IsFinite(distance) ||
                distance <= 0f)
            {
                context.WarningSink?.Invoke(
                    "Line Trace Distance must be greater than zero.");

                return false;
            }

            string directionMode =
                EventValueResolver.GetString(
                        instruction,
                        "traceDirectionMode",
                        context,
                        "Forward")
                    .Trim();

            Vector3 customDirection =
                EventValueResolver.GetVector3(
                    instruction,
                    "traceDirection",
                    context,
                    new Vector3(0f, 0f, -1f));

            GameObject basis =
                startObject ??
                context.Self;

            Vector3 direction =
                ResolveUniversalTraceDirection(
                    basis,
                    directionMode,
                    customDirection);

            if (!TryNormalizeTraceDirection(
                    direction,
                    out Vector3 normalizedDirection))
            {
                context.WarningSink?.Invoke(
                    "Line Trace Direction must be a finite non-zero vector.");

                return false;
            }

            end =
                start +
                normalizedDirection *
                distance;
        }
        else if (endMode.Equals(
                     "Cursor",
                     StringComparison.OrdinalIgnoreCase))
        {
            float maximumDistance =
                (float)EventValueResolver.GetNumber(
                    instruction,
                    "distance",
                    context,
                    100f);

            if (!float.IsFinite(maximumDistance) ||
                maximumDistance <= 0f)
            {
                context.WarningSink?.Invoke(
                    "Line Trace Maximum Distance must be greater than zero.");

                return false;
            }

            Camera3D? camera =
                context.Scene.ActiveCamera;

            if (camera == null)
            {
                context.WarningSink?.Invoke(
                    "Line Trace Mouse Cursor mode requires an active camera.");

                return false;
            }

            Input.NotifyGameViewPointerAim();

            float aspect =
                Input.GameViewSize.X /
                Math.Max(
                    Input.GameViewSize.Y,
                    1f);

            (Vector3 cameraOrigin, Vector3 cameraDirection) =
                camera.ScreenPointToRay(
                    Input.GameViewPointerNormalized,
                    aspect);

            float cameraSearchDistance =
                MathF.Min(
                    float.MaxValue,
                    maximumDistance +
                    Vector3.Distance(
                        cameraOrigin,
                        start));

            bool cameraFound =
                GameplayQuery3D.Raycast(
                    context.Scene,
                    cameraOrigin,
                    cameraDirection,
                    out RaycastHit3D cameraHit,
                    cameraSearchDistance,
                    ignoreRoot,
                    mask,
                    source: null,
                    bypassCollisionMatrix: true,
                    includeTriggers: includeTriggers);

            Vector3 desiredPoint =
                cameraFound
                    ? cameraHit.Point
                    : cameraOrigin +
                      cameraDirection *
                      cameraSearchDistance;

            Vector3 towardDesired =
                desiredPoint -
                start;

            float desiredDistance =
                towardDesired.Length();

            if (!float.IsFinite(desiredDistance) ||
                desiredDistance <= .00001f ||
                !TryNormalizeTraceDirection(
                    towardDesired,
                    out Vector3 normalizedDirection))
            {
                return false;
            }

            // This is the important rule the older cursor convenience node
            // made hard to reason about: Maximum Distance is measured from
            // the actual trace Start, not from the camera.
            end =
                start +
                normalizedDirection *
                MathF.Min(
                    maximumDistance,
                    desiredDistance);
        }
        else
        {
            end =
                EventValueResolver.GetVector3(
                    instruction,
                    "end",
                    context,
                    new Vector3(0f, 0f, -10f));
        }

        if (!IsFiniteTraceVector(end))
        {
            context.WarningSink?.Invoke(
                "Line Trace requires a finite End position.");

            return false;
        }

        Vector3 delta =
            end -
            start;

        float segmentDistance =
            delta.Length();

        if (!float.IsFinite(segmentDistance) ||
            segmentDistance <= .00001f)
        {
            context.WarningSink?.Invoke(
                "Line Trace Start and End resolve to the same position.");

            return false;
        }

        bool found =
            GameplayQuery3D.Raycast(
                context.Scene,
                start,
                delta,
                out RaycastHit3D hit,
                segmentDistance,
                ignoreRoot,
                mask,
                source: null,
                bypassCollisionMatrix: true,
                includeTriggers: includeTriggers);

        if (found)
        {
            context.LastRaycastHit =
                hit;
        }

        if (RuntimeDiagnostics.DebugWeaponRaycast)
        {
            string result =
                DescribeRayHit(
                    context.Scene,
                    found,
                    hit);

            RuntimeDiagnostics.RecordWeaponRaycast(
                $"LINE TRACE action={instruction.Id} self=\"{context.Self.Name}\" " +
                $"startMode={startMode} startObject=\"{startObject?.Name ?? "<world>"}\" start={DebugV3(start)} " +
                $"endMode={endMode} end={DebugV3(end)} distance={segmentDistance:0.000} " +
                $"ignoreRoot=\"{ignoreRoot?.Name ?? "<none>"}\" requestedLayer={layer} maskBits=0x{mask.Bits:X8} " +
                $"triggers={includeTriggers} result={result}");
        }

        if (drawDebug)
        {
            // A click/press trace may only execute for one frame. Keep the
            // visual around long enough to be plainly visible while testing.
            float visibleDuration =
                MathF.Max(
                    debugDuration,
                    .75f);

            DrawDebugRay(
                context.Scene,
                start,
                found
                    ? hit.Point
                    : end,
                found,
                visibleDuration,
                $"LineTrace_{context.Self.Id:N}_{instruction.InstanceId:N}");
        }

        return found;
    }

    private static Vector3 ResolveTraceTargetPoint(
        GameObject target)
    {
        Collider3D? triggerFallback =
            null;

        foreach (GameObject item in EnumerateTraceHierarchy(target))
        {
            foreach (Collider3D collider in item.Components.OfType<Collider3D>())
            {
                if (!collider.Enabled)
                {
                    continue;
                }

                if (!collider.IsTrigger)
                {
                    return Vector3.Transform(
                        collider.Center,
                        collider.Transform.WorldMatrix);
                }

                triggerFallback ??=
                    collider;
            }
        }

        if (triggerFallback != null)
        {
            return Vector3.Transform(
                triggerFallback.Center,
                triggerFallback.Transform.WorldMatrix);
        }

        return target.Transform.WorldPosition;
    }

    private static IEnumerable<GameObject> EnumerateTraceHierarchy(
        GameObject root)
    {
        yield return root;

        foreach (GameObject child in root.Children)
        {
            foreach (GameObject descendant in EnumerateTraceHierarchy(child))
            {
                yield return descendant;
            }
        }
    }

    private static GameObject ResolveTraceIgnoreRoot(
        GameObject item)
    {
        // Never blindly climb to the absolute scene root. If a project groups
        // gameplay objects under one shared parent, doing that would cause the
        // ray query to ignore every collider in that whole group.
        //
        // For a muzzle/weapon child, prefer the nearest gameplay owner so the
        // trace does not hit its own character. For arbitrary objects, ignore
        // only that object's own hierarchy.
        for (GameObject? current = item;
             current != null;
             current = current.Parent)
        {
            if (current.GetComponent<CharacterController3D>() != null ||
                current.GetComponent<ProjectileLauncher3D>() != null)
            {
                return current;
            }
        }

        return item;
    }

    private static Vector3 ResolveUniversalTraceDirection(
        GameObject basis,
        string directionMode,
        Vector3 customDirection)
    {
        string normalized =
            directionMode
                .Replace(" ", string.Empty)
                .Trim()
                .ToLowerInvariant();

        return normalized switch
        {
            "forward" =>
                basis.Transform.Forward,

            "back" =>
                -basis.Transform.Forward,

            "right" =>
                basis.Transform.Right,

            "left" =>
                -basis.Transform.Right,

            "up" =>
                basis.Transform.Up,

            "down" =>
                -basis.Transform.Up,

            "customworld" =>
                customDirection,

            "customlocal" =>
                Vector3.Transform(
                    customDirection,
                    basis.Transform.WorldRotation),

            _ =>
                basis.Transform.Forward
        };
    }

    private static bool TryNormalizeTraceDirection(
        Vector3 direction,
        out Vector3 normalized)
    {
        normalized =
            Vector3.Zero;

        if (!IsFiniteTraceVector(direction) ||
            direction.LengthSquared() <= .000001f)
        {
            return false;
        }

        normalized =
            Vector3.Normalize(direction);

        return IsFiniteTraceVector(normalized);
    }

    private static bool IsFiniteTraceVector(
        Vector3 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z);
}
