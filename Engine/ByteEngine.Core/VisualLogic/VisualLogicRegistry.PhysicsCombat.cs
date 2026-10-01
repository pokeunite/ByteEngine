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
            Id = "physics.castRayToCursor",
            Category = "Physics / Raycasts",
            DisplayName = "Cast Ray To Cursor",
            Execute = (instruction, context) =>
                CastEventRayInternal(instruction, context, "TopDownCursor")
        });
        registry.RegisterAction(new VisualActionDefinition
        {
            Id = "physics.castRayInDirection",
            Category = "Physics / Raycasts",
            DisplayName = "Cast Ray In Direction",
            Execute = (instruction, context) =>
                CastEventRayInternal(instruction, context, "MuzzleDirection")
        });
        registry.RegisterAction(new VisualActionDefinition
        {
            Id = "physics.castRay",
            Category = "Physics / Raycasts",
            DisplayName = "Cast Ray (Advanced)",
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
                GameObject? healthOwner = hit.GameObject;
                while (healthOwner != null && healthOwner.GetComponent<HealthComponent>() == null)
                    healthOwner = healthOwner.Parent;
                if (healthOwner == null) return;
                Vector3 direction = context.LastRaycastDirection;
                if (direction.LengthSquared() < .000001f)
                    direction = hit.Point - context.Self.Transform.WorldPosition;
                healthOwner.GetComponent<SkeletalRagdoll3D>()?.ApplyImpact(direction,
                    Math.Clamp(amount / 25f, .1f, 3f));
                healthOwner.GetComponent<HealthComponent>()?.Damage(amount);
            }
        });
    }

    private static bool CastEventRay(
        VisualInstruction instruction,
        EventExecutionContext context) =>
        CastEventRayInternal(
            instruction,
            context,
            forcedAimMode: null);

    private static bool CastEventRayInternal(
        VisualInstruction instruction,
        EventExecutionContext context,
        string? forcedAimMode)
    {
        context.RaycastPerformed = true;
        context.LastRaycastHit = null;
        context.LastRaycastDirection = Vector3.Zero;
        GameObject? source = ResolveObjectArgument(instruction, "source", context);
        if (source == null) return false;

        GameObject muzzle =
            ResolveRayMuzzlePoint(
                instruction,
                context,
                source);

        Vector3 localDirection = EventValueResolver.GetVector3(
            instruction, "direction", context, new Vector3(0f, 0f, -1f));

        bool legacyWorldSpace =
            EventValueResolver.GetBoolean(
                instruction,
                "worldSpace",
                context,
                false);

        string directionMode =
            EventValueResolver.GetString(
                instruction,
                "directionMode",
                context,
                string.Empty)
            .Trim();

        string muzzlePath =
            EventValueResolver.GetString(
                instruction,
                "muzzlePath",
                context,
                string.Empty)
            .Trim();

        float distance = (float)EventValueResolver.GetNumber(instruction, "distance", context, 100f);
        bool drawDebug = EventValueResolver.GetBoolean(instruction, "drawDebug", context, false);
        float debugDuration = (float)EventValueResolver.GetNumber(instruction, "debugDuration", context, .25);

        if (!float.IsFinite(distance) || distance <= 0f)
            return false;

        Vector3 localOrigin = EventValueResolver.GetVector3(
            instruction, "originOffset", context, Vector3.Zero);

        Vector3 origin =
            Vector3.Transform(
                localOrigin,
                muzzle.Transform.WorldMatrix);
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

        string aimMode = forcedAimMode ?? EventValueResolver.GetString(
            instruction, "aimMode", context, "MuzzleDirection").Trim();
        bool cameraAim = aimMode.Equals("TopDownCursor", StringComparison.OrdinalIgnoreCase) ||
            aimMode.Equals("ThirdPersonCrosshair", StringComparison.OrdinalIgnoreCase) ||
            aimMode.Equals("FirstPersonCrosshair", StringComparison.OrdinalIgnoreCase);

        bool topDownCursorAim =
            aimMode.Equals("TopDownCursor", StringComparison.OrdinalIgnoreCase);
        if (topDownCursorAim)
            Input.NotifyGameViewPointerAim();

        Collider3D? implicitOriginCollider = null;
        string originMode =
            !string.IsNullOrWhiteSpace(muzzlePath)
                ? "ExplicitMuzzlePath"
                : localOrigin.LengthSquared() >= .000001f
                    ? "ExplicitOriginOffset"
                    : "RayOwnerTransform";

        // Character roots in ByteEngine use a feet/pivot origin. When Top Down /
        // camera aiming has no explicit muzzle point, firing from that pivot can
        // immediately report the floor at distance zero. Character colliders are
        // often placed on a child object, so search the complete ignored owner
        // hierarchy and transform the collider's center with its own transform.
        // Explicit muzzle paths and explicit local offsets remain authoritative.
        if (cameraAim &&
            string.IsNullOrWhiteSpace(muzzlePath) &&
            localOrigin.LengthSquared() < .000001f &&
            FindAimOriginCollider(ignoredOwner) is { } ownerCollider)
        {
            implicitOriginCollider = ownerCollider;
            originMode = "OwnerColliderCenter";
            origin = Vector3.Transform(
                ownerCollider.Center,
                ownerCollider.Transform.WorldMatrix);
        }

        Camera3D? camera = context.Scene.ActiveCamera;
        Vector2 screenPoint = topDownCursorAim
            ? Input.GameViewPointerNormalized : new Vector2(.5f);
        Vector3 cameraRayOrigin = Vector3.Zero;
        Vector3 cameraRayDirection = Vector3.Zero;
        Vector3 aimPoint = Vector3.Zero;
        RaycastHit3D cameraHit = default;
        bool cameraTargetHit = false;
        Vector3 worldDirection = ResolveRayDirection(
            muzzle, directionMode, localDirection, legacyWorldSpace);
        if (cameraAim && camera != null)
        {
            float aspect = Input.GameViewSize.X / Math.Max(Input.GameViewSize.Y, 1f);
            (cameraRayOrigin, cameraRayDirection) = camera.ScreenPointToRay(screenPoint, aspect);
            float cameraSearchDistance = MathF.Min(float.MaxValue,
                distance + Vector3.Distance(cameraRayOrigin, origin));
            cameraTargetHit = GameplayQuery3D.Raycast(context.Scene, cameraRayOrigin,
                cameraRayDirection, out cameraHit, cameraSearchDistance, ignoredOwner, mask,
                source: null, bypassCollisionMatrix: true,
                includeTriggers: includeTriggers);
            aimPoint = cameraTargetHit ? cameraHit.Point : cameraRayOrigin + cameraRayDirection * cameraSearchDistance;
            string aimStyle = forcedAimMode != null && topDownCursorAim
                ? "Exact3D"
                : EventValueResolver.GetString(instruction, "topDownAimStyle", context, "Exact3D");
            if (topDownCursorAim &&
                aimStyle.Equals("Planar", StringComparison.OrdinalIgnoreCase))
                aimPoint.Y = origin.Y;
            worldDirection = aimPoint - origin;
            if (drawDebug)
                DrawDebugRay(context.Scene, cameraRayOrigin,
                    cameraTargetHit ? cameraHit.Point : cameraRayOrigin + cameraRayDirection * cameraSearchDistance,
                    cameraTargetHit, debugDuration,
                    $"Camera_{source.Id:N}_{instruction.InstanceId:N}", new Vector4(.08f, .75f, 1f, 1f));
        }
        else if (cameraAim)
            context.WarningSink?.Invoke($"Cast Ray Aim Mode '{aimMode}' needs an active camera; using Muzzle Direction.");

        if (!float.IsFinite(worldDirection.X) || !float.IsFinite(worldDirection.Y) ||
            !float.IsFinite(worldDirection.Z) || worldDirection.LengthSquared() < .000001f)
            return false;

        // Cast Ray already exposes its own Collision Layer and trigger controls.
        // Do not silently filter the final weapon ray through the owner's project
        // collision matrix as well; otherwise the camera can select the collider
        // under the pointer and the muzzle ray can incorrectly skip that same
        // collider. The owner hierarchy is still ignored explicitly.
        bool found = GameplayQuery3D.Raycast(context.Scene, origin, worldDirection,
            out RaycastHit3D hit, distance, ignoredOwner, mask,
            source: null, bypassCollisionMatrix: true,
            includeTriggers: includeTriggers);
        if (found) context.LastRaycastHit = hit;

        Vector3 normalizedWorldDirection =
            Vector3.Normalize(worldDirection);
        context.LastRaycastDirection = normalizedWorldDirection;

        if (RuntimeDiagnostics.DebugWeaponRaycast)
        {
            Camera3D? debugCamera = camera;

            string cameraHitText =
                DescribeRayHit(
                    context.Scene,
                    cameraTargetHit,
                    cameraHit);

            string finalHitText =
                DescribeRayHit(
                    context.Scene,
                    found,
                    hit);

            string originOverlaps =
                DescribeOriginOverlaps(
                    context.Scene,
                    origin,
                    ignoredOwner,
                    mask,
                    includeTriggers);

            string resolvedDirectionMode =
                string.IsNullOrWhiteSpace(
                    directionMode)
                    ? legacyWorldSpace
                        ? "LegacyCustomWorld"
                        : "LegacyCustomLocal"
                    : directionMode;

            RuntimeDiagnostics.RecordWeaponRaycast(
                $"EVENT RAY action={instruction.Id} self=\"{context.Self.Name}\" source=\"{source.Name}\" sourceId={source.Id:N} parent=\"{source.Parent?.Name ?? "<none>"}\" " +
                $"sourceLayer={LayerName(context.Scene, source.Layer)} ignoredOwner=\"{ignoredOwner.Name}\" ignoredOwnerId={ignoredOwner.Id:N} " +
                $"muzzle=\"{muzzle.Name}\" muzzleParent=\"{muzzle.Parent?.Name ?? "<none>"}\" muzzlePath=\"{muzzlePath}\" " +
                $"originMode={originMode} originCollider={(implicitOriginCollider != null ? implicitOriginCollider.GameObject.Name + ":" + implicitOriginCollider.GetType().Name : "<none>")} " +
                $"sourcePos={DebugV3(source.Transform.WorldPosition)} sourceFwd={DebugV3(source.Transform.Forward)} " +
                $"muzzlePos={DebugV3(muzzle.Transform.WorldPosition)} muzzleEuler={DebugV3(muzzle.Transform.EulerAngles)} " +
                $"muzzleFwd={DebugV3(muzzle.Transform.Forward)} muzzleRight={DebugV3(muzzle.Transform.Right)} muzzleUp={DebugV3(muzzle.Transform.Up)} " +
                $"localOrigin={DebugV3(localOrigin)} origin={DebugV3(origin)} originOverlaps={originOverlaps} localDir={DebugV3(localDirection)} " +
                $"aimMode={aimMode} pointerOver={Input.IsPointerOverGameView} pointerFocused={Input.IsGameViewFocused} inputCaptured={Input.IsGameInputCaptured} " +
                $"screen=({screenPoint.X:0.000},{screenPoint.Y:0.000}) view=({Input.GameViewSize.X:0},{Input.GameViewSize.Y:0}) " +
                $"cameraRayOrigin={DebugV3(cameraRayOrigin)} cameraRayDir={DebugV3(cameraRayDirection)} cameraTarget={cameraHitText} aimPoint={DebugV3(aimPoint)} " +
                $"directionMode={resolvedDirectionMode} worldSpace={legacyWorldSpace} worldDir={DebugV3(normalizedWorldDirection)} " +
                $"maxDistance={distance:0.000} requestedLayer={layer} maskBits=0x{mask.Bits:X8} triggers={includeTriggers} " +
                $"ownerFwd={DebugV3(ignoredOwner.Transform.Forward)} cameraPos={(debugCamera != null ? DebugV3(debugCamera.Transform.WorldPosition) : "<none>")} " +
                $"cameraFwd={(debugCamera != null ? DebugV3(debugCamera.Transform.Forward) : "<none>")} finalResult={finalHitText}");
        }

        if (drawDebug)
        {
            Vector3 normalizedDirection = Vector3.Normalize(worldDirection);
            Vector3 end = found
                ? hit.Point
                : origin + normalizedDirection * distance;
            DrawDebugRay(context.Scene, origin, end, found, debugDuration,
                $"Muzzle_{source.Id:N}_{instruction.InstanceId:N}");
        }

        return found;
    }

    private static string DescribeRayHit(
        ByteEngine.Core.Scene.Scene scene,
        bool found,
        RaycastHit3D hit)
    {
        if (!found)
            return "MISS";

        GameObject? healthOwner = FindHealthOwner(hit.GameObject);
        return
            $"HIT(object=\"{hit.GameObject.Name}\",id={hit.GameObject.Id:N},layer={LayerName(scene, hit.GameObject.Layer)}," +
            $"collider={hit.Collider.GetType().Name},point={DebugV3(hit.Point)},normal={DebugV3(hit.Normal)},distance={hit.Distance:0.000}," +
            $"ground={IsGround(hit.GameObject)},healthOwner=\"{healthOwner?.Name ?? "<none>"}\")";
    }

    private static string DescribeOriginOverlaps(
        ByteEngine.Core.Scene.Scene scene,
        Vector3 origin,
        GameObject ignoredOwner,
        LayerMask mask,
        bool includeTriggers)
    {
        string[] overlaps = GameplayQuery3D.OverlapSphere(
                scene,
                origin,
                .01f,
                ignoredOwner,
                mask,
                source: null,
                bypassCollisionMatrix: true,
                includeTriggers: includeTriggers)
            .Take(8)
            .Select(item =>
                $"{item.GameObject.Name}:{item.Collider.GetType().Name}:{LayerName(scene, item.GameObject.Layer)}:ground={IsGround(item.GameObject)}")
            .ToArray();

        return overlaps.Length == 0
            ? "<none>"
            : "[" + string.Join(",", overlaps) + "]";
    }

    private static string LayerName(
        ByteEngine.Core.Scene.Scene scene,
        int layer) =>
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

    private static Collider3D? FindAimOriginCollider(GameObject root)
    {
        Collider3D? collider =
            root.Components
                .OfType<Collider3D>()
                .FirstOrDefault(item => item.Enabled && !item.IsTrigger);

        if (collider != null)
            return collider;

        foreach (GameObject child in root.Children)
        {
            collider = FindAimOriginCollider(child);
            if (collider != null)
                return collider;
        }

        return null;
    }

    private static GameObject ResolveRayMuzzlePoint(
        VisualInstruction instruction,
        EventExecutionContext context,
        GameObject source)
    {
        string path =
            EventValueResolver.GetString(
                instruction,
                "muzzlePath",
                context,
                string.Empty)
            .Trim();

        if (string.IsNullOrWhiteSpace(
                path))
        {
            return source;
        }

        GameObject current =
            source;

        foreach (string segment
                 in path.Split(
                     '/',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            GameObject? next =
                current.Children.FirstOrDefault(
                    child =>
                        string.Equals(
                            child.Name,
                            segment,
                            StringComparison.OrdinalIgnoreCase));

            if (next == null)
            {
                context.WarningSink?.Invoke(
                    $"Cast Ray Muzzle Point '{path}' was not found under '{source.Name}'. Using the Ray Owner instead.");

                return source;
            }

            current =
                next;
        }

        return current;
    }

    private static Vector3 ResolveRayDirection(
        GameObject muzzle,
        string directionMode,
        Vector3 storedDirection,
        bool legacyWorldSpace)
    {
        string normalizedMode =
            directionMode
                .Trim()
                .Replace(
                    " ",
                    string.Empty)
                .ToLowerInvariant();

        return normalizedMode switch
        {
            "forward" or
            "muzzleforward" =>
                muzzle.Transform.Forward,

            "back" or
            "muzzleback" =>
                -muzzle.Transform.Forward,

            "right" or
            "muzzleright" =>
                muzzle.Transform.Right,

            "left" or
            "muzzleleft" =>
                -muzzle.Transform.Right,

            "up" or
            "muzzleup" =>
                muzzle.Transform.Up,

            "down" or
            "muzzledown" =>
                -muzzle.Transform.Up,

            "customlocal" or
            "customlocaldirection" =>
                Vector3.Transform(
                    storedDirection,
                    muzzle.Transform.WorldRotation),

            "customworld" or
            "customworlddirection" =>
                storedDirection,

            _ =>
                legacyWorldSpace
                    ? storedDirection
                    : Vector3.Transform(
                        storedDirection,
                        muzzle.Transform.WorldRotation)
        };
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
        float duration,
        string markerKey,
        Vector4? color = null)
    {
        // The physics ray starts at the real origin. Only the visual is clipped:
        // drawing a cube through the camera near plane fills the whole screen.
        if (scene.ActiveCamera is { } camera)
        {
            Vector3 cameraPosition = camera.Transform.WorldPosition;
            Vector3 cameraForward = camera.Transform.Forward;
            float safeDepth = MathF.Max(camera.NearClip + .3f, .5f);
            float startDepth = Vector3.Dot(start - cameraPosition, cameraForward);
            float endDepth = Vector3.Dot(end - cameraPosition, cameraForward);
            if (startDepth < safeDepth)
            {
                float depthSpan = endDepth - startDepth;
                if (depthSpan <= .00001f || endDepth <= safeDepth) return;
                start = Vector3.Lerp(start, end,
                    Math.Clamp((safeDepth - startDepth) / depthSpan, 0f, 1f));
            }
        }

        Vector3 delta = end - start;
        float length = delta.Length();
        if (!float.IsFinite(length) || length <= .0001f) return;

        duration = float.IsFinite(duration)
            ? Math.Clamp(duration, .05f, 10f)
            : .25f;

        // One camera and one muzzle marker per Cast Ray instruction and owner.
        // Repeated fire refreshes them instead of stacking hundreds of cubes.
        string markerName = "__DebugRay_" + markerKey;
        GameObject debugRay = scene.GameObjects.FirstOrDefault(
            item => item.Name == markerName && item.ActiveInHierarchy) ??
            scene.CreateGameObject(markerName);

        debugRay.Transform.WorldPosition = (start + end) * .5f;
        debugRay.Transform.WorldRotation = RotationFromTo(
            Vector3.UnitZ,
            delta / length);
        debugRay.Transform.LocalScale = new Vector3(.008f, .008f, length);

        Vector4 rayColor = color ?? (hit
            ? new Vector4(1f, .08f, .04f, 1f)
            : new Vector4(.05f, 1f, .18f, 1f));
        if (debugRay.GetComponent<MeshRenderer>() is { } renderer)
            renderer.Material.BaseColor = rayColor;
        else
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
                    BaseColor = rayColor,
                    Roughness = .15f,
                    BlendMode = BlendMode3D.Additive,
                    DepthTest = false,
                    DepthWriteMode = DepthWriteMode3D.Disabled,
                    CullMode = CullMode3D.None
                }
            });

        if (debugRay.GetComponent<LifetimeComponent>() is { } lifetime)
            lifetime.Restart(duration);
        else
            debugRay.AddComponent(new LifetimeComponent { LifetimeSeconds = duration });
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
