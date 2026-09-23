using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Classification;
using ByteEngine.Core.InputSystem;

namespace ByteEngine.Editor;

internal static class BlueprintAuthoringService
{
    public static T AddComponent<T>(GameObject target, T component) where T : Component
    {
        AddDependencies(target, component.GetType());
        target = ResolveComponentTarget(target, component);
        PrepareVisualOverride(target, component);
        return target.AddComponent(component);
    }

    public static Component AddComponent(GameObject target, Component component)
    {
        AddDependencies(target, component.GetType());
        target = ResolveComponentTarget(target, component);
        PrepareVisualOverride(target, component);
        return target.AddComponent(component);
    }

    private static GameObject ResolveComponentTarget(GameObject target, Component component)
    {
        if (component is not VisualModelOverride || target.Name.Equals("Visual", StringComparison.OrdinalIgnoreCase))
            return target;

        GameObject? visual = target.Children.FirstOrDefault(
            child => child.Name.Equals("Visual", StringComparison.OrdinalIgnoreCase));
        return visual ?? throw new InvalidOperationException(
            "Visual Model Override requires a Character Blueprint Visual child.");
    }

    private static void PrepareVisualOverride(GameObject target, Component component)
    {
        if (component is not VisualModelOverride visualOverride)
            return;

        ModelHierarchyInstance? model = target.Children
            .Select(child => child.GetComponent<ModelHierarchyInstance>())
            .FirstOrDefault(instance => instance != null);
        if (model == null)
            return;

        visualOverride.ImportScale = model.AppliedImportScale;

        // Repair legacy Blueprints whose recorded import correction was lost.
        // A deliberately authored non-unit Visual scale is preserved.
        if (Vector3.DistanceSquared(target.Transform.LocalScale, Vector3.One) < 0.000001f &&
            MathF.Abs(visualOverride.ImportScale - 1.0f) > 0.0001f)
        {
            target.Transform.LocalScale = Vector3.One * visualOverride.ImportScale;
        }
    }

    private static void AddDependencies(GameObject target, Type componentType)
    {
        foreach (Type requirement in ComponentMetadataRegistry.Get(componentType).RequiresComponents ?? Array.Empty<Type>())
        {
            if (target.Components.Any(existing => existing.GetType() == requirement)) continue;
            if (requirement == typeof(CharacterController3D)) target.AddComponent(new CharacterController3D());
        }
    }

    public static CameraBoom3D SetupThirdPersonCharacter(GameObject player, AssetManager? assets = null)
    {
        ClassificationSettings? classification = player.Scene?.Classification ?? EditorProjectContext.Active?.Project.Classification;
        if (classification != null)
        {
            TagDefinition playerTag = classification.FindTag("Player") ?? classification.AddTag("Player")!;
            ObjectLayerDefinition? playerLayer = classification.FindLayer("Player");
            if (playerLayer == null)
            {
                int slot = Enumerable.Range(1, 31).First(index => classification.FindLayer(index) == null);
                classification.DefineLayer(slot, "Player");
                playerLayer = classification.FindLayer(slot);
            }
            player.AddTag(playerTag.Id);
            player.Layer = playerLayer!.Index;
            EditorProjectContext.Active?.SaveProject();
        }

        InputActions.Map.EnsureGameplayDefaults();

        bool createInitialCapsule =
            player.GetComponent<CapsuleCollider3D>() == null;

        NormalizeCharacterStructure(player);

        /*
         * The character root is treated as a feet/pivot origin by the movement
         * controller. A 2 m fallback capsule therefore must be centered at
         * Y = 1 m, not Y = 0. A zero-centered 2 m capsule starts one metre
         * below the root and can begin Play Mode penetrating the floor, causing
         * every horizontal sweep to report an immediate blocking contact.
         */
        CapsuleCollider3D capsule =
            EnsureSingleRootComponent(
                player,
                () => new CapsuleCollider3D
                {
                    Radius = .5f,
                    Height = 2f,
                    Center = new Vector3(0f, 1f, 0f)
                });

        EnsureSingleRootComponent(player, () => new CharacterController3D());
        EnsureSingleRootComponent(player, () => new AnimationController());

        PlayerController3D playerInput =
            EnsureSingleRootComponent(
                player,
                () => new PlayerController3D
                {
                    UseLocalOrientation = false,
                    CharacterRotation = CharacterRotationMode.FaceCamera,
                    TurnSpeed = 540f,
                    ControlPitch = 12f
                });

        playerInput.MoveAction = InputActions.Reference("Move");
        playerInput.LookAction = InputActions.Reference("Look");
        playerInput.JumpAction = InputActions.Reference("Jump");
        playerInput.SprintAction = InputActions.Reference("Sprint");

        /*
         * NormalizeCharacterStructure runs before PlayerController3D is
         * guaranteed to exist. Run the facing migration here as well so both
         * new and existing Character Blueprints get the same canonical layout.
         */
        MoveRuntimeOwnedCharacterFacingToVisual(
            player);

        CameraBoom3D boom =
            EnsureSingleRootComponent(
                player,
                () => new CameraBoom3D());

        GameObject? cameraObject = Descendants(player)
            .FirstOrDefault(child => child.GetComponent<Camera3D>() != null);

        Camera3D? previousActive = player.Scene?.ActiveCamera;

        if (cameraObject == null)
        {
            Scene scene =
                player.Scene ??
                throw new InvalidOperationException(
                    "The character must belong to a scene.");

            cameraObject = scene.CreateGameObject("Camera");
            cameraObject.SetParent(player, false);
            cameraObject.AddComponent(new Camera3D());
        }
        else if (!ReferenceEquals(cameraObject.Parent, player))
        {
            cameraObject.SetParent(player, false);
        }

        cameraObject.Name = "Camera";

        Camera3D camera =
            cameraObject.GetComponent<Camera3D>()!;

        boom.CameraObjectId = cameraObject.Id;

        if (previousActive == null ||
            ReferenceEquals(previousActive, camera))
        {
            player.Scene?.SetActiveCamera(camera);
        }

        /*
         * v0.11-C1 Fix 2:
         *
         * Older Character Blueprints can already contain the previous default
         * capsule (radius .5, height 2, center 0) with no auto-fit metadata.
         * Re-running Setup > Third Person Character must repair that legacy
         * placeholder instead of preserving the floor-penetrating collider.
         *
         * Custom/user-fitted capsules are left alone.
         */
        bool shouldAutoFit =
            createInitialCapsule ||
            IsLegacyUnfittedCharacterCapsule(capsule);

        if (assets != null &&
            shouldAutoFit)
        {
            if (!CharacterCapsuleAutoFit.TryFit(
                    player,
                    assets,
                    out _))
            {
                /*
                 * If model bounds are unavailable, keep the safe feet-origin
                 * fallback rather than falling back to the old zero-centered
                 * capsule.
                 */
                capsule.Center =
                    new Vector3(
                        capsule.Center.X,
                        Math.Max(
                            capsule.Height * .5f,
                            capsule.Radius),
                        capsule.Center.Z);
            }
        }
        else if (shouldAutoFit)
        {
            capsule.Center =
                new Vector3(
                    capsule.Center.X,
                    Math.Max(
                        capsule.Height * .5f,
                        capsule.Radius),
                    capsule.Center.Z);
        }

        return boom;
    }

    public static GameObject NormalizeCharacterStructure(GameObject player)
    {
        GameObject[] originalChildren = player.Children.ToArray();
        ModelHierarchyInstance? rootModel = player.GetComponent<ModelHierarchyInstance>();
        GameObject visual = EnsureVisualRoot(player);

        if (rootModel != null)
        {
            Vector3 rootScale = player.Transform.LocalScale;
            if (NearlyUniform(rootScale, rootModel.AppliedImportScale))
            {
                visual.Transform.LocalScale *= rootScale;
                player.Transform.LocalScale = Vector3.One;
            }

            GameObject importedModel =
                visual.Children.FirstOrDefault(
                    child => child.GetComponent<ModelHierarchyInstance>() != null)
                ?? player.Scene!.CreateGameObject(
                    player.Name.EndsWith(
                        "Model",
                        StringComparison.OrdinalIgnoreCase)
                        ? player.Name
                        : player.Name + "Model");

            importedModel.SetParent(visual, false);

            if (!importedModel.HasComponent<ModelHierarchyInstance>())
            {
                importedModel.AddComponent(
                    new ModelHierarchyInstance
                    {
                        Model = rootModel.Model,
                        AppliedImportScale = rootModel.AppliedImportScale
                    });
            }

            player.RemoveComponent(rootModel);

            foreach (GameObject child in originalChildren)
            {
                if (ReferenceEquals(child, visual) ||
                    child.GetComponent<Camera3D>() != null)
                {
                    continue;
                }

                child.SetParent(importedModel, false);
            }
        }

        RemoveGameplayComponentsFromDescendants(player);

        /*
         * The PlayerController3D owns the runtime character yaw in FaceCamera
         * and FaceMovement modes. Any authored yaw left on the Blueprint root
         * would therefore be overwritten as soon as Play starts.
         *
         * Character model facing corrections belong on the Visual child, not
         * on the gameplay/collision root.
         */
        MoveRuntimeOwnedCharacterFacingToVisual(
            player);

        return visual;
    }

    public static bool MoveRuntimeOwnedCharacterFacingToVisual(
        GameObject player)
    {
        ArgumentNullException.ThrowIfNull(
            player);

        PlayerController3D? playerInput =
            player.GetComponent<PlayerController3D>();

        if (playerInput ==
                null ||
            playerInput.CharacterRotation ==
                CharacterRotationMode.Independent)
        {
            return false;
        }

        Vector3 rootEuler =
            player.Transform.EulerAngles;

        float authoredYaw =
            NormalizeAngle(
                rootEuler.Y);

        if (MathF.Abs(
                authoredYaw) <
            0.001f)
        {
            return false;
        }

        GameObject visual =
            EnsureVisualRoot(
                player);

        /*
         * Preserve the Visual's exact world pose while removing Y rotation
         * from the gameplay root. Restoring the Visual world pose causes the
         * authored facing correction to become a Visual-local offset instead.
         *
         * Example:
         *   Before: Player Y = -180, Visual Y = 0
         *   After : Player Y =    0, Visual carries the equivalent -180 offset
         *
         * The runtime PlayerController can now rotate Player freely without
         * destroying the model's imported-facing correction.
         */
        Vector3 visualWorldPosition =
            visual.Transform.WorldPosition;

        Quaternion visualWorldRotation =
            visual.Transform.WorldRotation;

        rootEuler.Y =
            0.0f;

        player.Transform.EulerAngles =
            rootEuler;

        visual.Transform.WorldPosition =
            visualWorldPosition;

        visual.Transform.WorldRotation =
            visualWorldRotation;

        return true;
    }

    public static GameObject EnsureVisualRoot(GameObject player)
    {
        GameObject? visual =
            player.Children.FirstOrDefault(
                child =>
                    child.Name.Equals(
                        "Visual",
                        StringComparison.OrdinalIgnoreCase));

        if (visual != null)
        {
            return visual;
        }

        Scene scene =
            player.Scene ??
            throw new InvalidOperationException(
                "The Blueprint root must belong to a preview scene.");

        visual = scene.CreateGameObject("Visual");
        visual.SetParent(player, false);
        return visual;
    }

    private static bool IsLegacyUnfittedCharacterCapsule(
        CapsuleCollider3D capsule)
    {
        bool defaultDimensions =
            MathF.Abs(capsule.Radius - .5f) < .0001f &&
            MathF.Abs(capsule.Height - 2f) < .0001f;

        bool zeroCenter =
            capsule.Center.LengthSquared() < .000001f;

        bool noRecordedFit =
            string.IsNullOrWhiteSpace(capsule.AutoFitSource) &&
            capsule.VisualBounds.LengthSquared() < .000001f;

        return
            defaultDimensions &&
            zeroCenter &&
            noRecordedFit;
    }

    private static IEnumerable<GameObject> Descendants(GameObject root)
    {
        foreach (GameObject child in root.Children)
        {
            yield return child;

            foreach (GameObject descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static T EnsureSingleRootComponent<T>(
        GameObject root,
        Func<T> create)
        where T : Component
    {
        T? component =
            root.Components
                .OfType<T>()
                .FirstOrDefault();

        foreach (T duplicate
                 in root.Components
                     .OfType<T>()
                     .Skip(1)
                     .ToArray())
        {
            root.RemoveComponent(duplicate);
        }

        return
            component ??
            root.AddComponent(create());
    }

    private static void RemoveGameplayComponentsFromDescendants(GameObject root)
    {
        Type[] gameplayTypes =
        {
            typeof(CapsuleCollider3D),
            typeof(CharacterController3D),
            typeof(PlayerController3D),
            typeof(AnimationController),
            typeof(CameraBoom3D)
        };

        foreach (GameObject child in Descendants(root))
        {
            foreach (Component component
                     in child.Components
                         .Where(
                             item =>
                                 gameplayTypes.Contains(item.GetType()))
                         .ToArray())
            {
                child.RemoveComponent(component);
            }
        }
    }

    private static float NormalizeAngle(
        float value)
    {
        if (!float.IsFinite(
                value))
        {
            return 0.0f;
        }

        value %=
            360.0f;

        if (value >
            180.0f)
        {
            value -=
                360.0f;
        }

        if (value <=
            -180.0f)
        {
            value +=
                360.0f;
        }

        return value;
    }

    private static bool NearlyUniform(
        Vector3 scale,
        float expected) =>
        MathF.Abs(scale.X - expected) < .0001f &&
        MathF.Abs(scale.Y - expected) < .0001f &&
        MathF.Abs(scale.Z - expected) < .0001f;
}
