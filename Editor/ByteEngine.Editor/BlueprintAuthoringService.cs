using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
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
        if (component is not VisualModelOverride || target.GetComponent<ModelHierarchyInstance>() != null ||
            target.Name.Equals("Model", StringComparison.OrdinalIgnoreCase))
            return target;

        GameObject? model = target.Children.FirstOrDefault(child =>
            child.GetComponent<ModelHierarchyInstance>() != null ||
            child.Name.Equals("Model", StringComparison.OrdinalIgnoreCase));
        return model ?? throw new InvalidOperationException(
            "Visual Model Override requires a Character Blueprint Model child.");
    }

    private static void PrepareVisualOverride(GameObject target, Component component)
    {
        if (component is VisualModelOverride visualOverride)
            visualOverride.ImportScale = 1.0f;
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

        bool promoteRawModel = player.GetComponent<ModelHierarchyInstance>() != null &&
            !player.Children.Any(child => child.Name.Equals("Visual", StringComparison.OrdinalIgnoreCase));
        GameObject modelRoot = NormalizeCharacterStructure(player);
        if (promoteRawModel && assets != null)
            GroundModelAtFeet(modelRoot, assets);

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
        Scene scene = player.Scene ?? throw new InvalidOperationException("The character must belong to a scene.");
        GameObject? visual = player.Children.FirstOrDefault(child => child.Name.Equals("Visual", StringComparison.OrdinalIgnoreCase));
        GameObject? model = visual?.Children.FirstOrDefault(child => child.GetComponent<ModelHierarchyInstance>() != null);
        model ??= player.Children.FirstOrDefault(child => child.GetComponent<ModelHierarchyInstance>() != null || child.Name.Equals("Model", StringComparison.OrdinalIgnoreCase));

        if (visual != null)
        {
            if (model == null)
                model = visual;
            else
            {
                if (visual.Components.Any(component => component is not VisualModelOverride))
                    throw new InvalidOperationException(
                        "Legacy Visual contains custom components; move them explicitly before flattening the model.");
                model.SetParent(player, true);
                foreach (GameObject child in visual.Children.ToArray())
                    child.SetParent(model, true);
                if (visual.GetComponent<VisualModelOverride>() != null && model.GetComponent<VisualModelOverride>() == null)
                    model.AddComponent(new VisualModelOverride());
                scene.DestroyGameObject(visual);
            }
        }

        model ??= EnsureModelRoot(player);
        if (!ReferenceEquals(model.Parent, player))
            model.SetParent(player, true);
        model.Name = "Model";

        ModelHierarchyInstance? rootModel = player.GetComponent<ModelHierarchyInstance>();
        if (rootModel != null)
        {
            if (model.GetComponent<ModelHierarchyInstance>() == null)
                model.AddComponent(new ModelHierarchyInstance { Model = rootModel.Model, AppliedImportScale = rootModel.AppliedImportScale });
            player.RemoveComponent(rootModel);
            foreach (GameObject child in player.Children.ToArray())
                if (!ReferenceEquals(child, model) && child.GetComponent<Camera3D>() == null)
                    child.SetParent(model, true);
        }

        if (Vector3.DistanceSquared(player.Transform.LocalScale, Vector3.One) > 0.000001f)
        {
            Vector3 worldPosition = model.Transform.WorldPosition;
            Vector3 worldScale = model.Transform.WorldScale;
            var cameras = player.Children
                .Where(child => child.GetComponent<Camera3D>() != null)
                .Select(child => (Object: child, Position: child.Transform.WorldPosition,
                    Rotation: child.Transform.WorldRotation, Scale: child.Transform.WorldScale))
                .ToArray();
            player.Transform.LocalScale = Vector3.One;
            model.Transform.WorldPosition = worldPosition;
            model.Transform.WorldScale = worldScale;
            foreach (var camera in cameras)
            {
                camera.Object.Transform.WorldPosition = camera.Position;
                camera.Object.Transform.WorldRotation = camera.Rotation;
                camera.Object.Transform.WorldScale = camera.Scale;
            }
        }

        if (model.GetComponent<VisualModelOverride>() is { } modelOverride)
            modelOverride.ImportScale = 1.0f;
        RemoveGameplayComponentsFromDescendants(player);
        MoveRuntimeOwnedCharacterFacingToVisual(player);
        return model;
    }

    // Compatibility entry point; corrections now live on Model rather than Visual.
    public static bool MoveRuntimeOwnedCharacterFacingToVisual(GameObject player)
    {
        PlayerController3D? controller = player.GetComponent<PlayerController3D>();
        if (controller == null || controller.CharacterRotation == CharacterRotationMode.Independent)
            return false;
        Vector3 rootEuler = player.Transform.EulerAngles;
        if (MathF.Abs(NormalizeAngle(rootEuler.Y)) < 0.001f)
            return false;
        GameObject model = EnsureModelRoot(player);
        Vector3 position = model.Transform.WorldPosition;
        Quaternion rotation = model.Transform.WorldRotation;
        var cameras = player.Children
            .Where(child => child.GetComponent<Camera3D>() != null)
            .Select(child => (Object: child, Position: child.Transform.WorldPosition,
                Rotation: child.Transform.WorldRotation))
            .ToArray();
        rootEuler.Y = 0.0f;
        player.Transform.EulerAngles = rootEuler;
        model.Transform.WorldPosition = position;
        model.Transform.WorldRotation = rotation;
        foreach (var camera in cameras)
        {
            camera.Object.Transform.WorldPosition = camera.Position;
            camera.Object.Transform.WorldRotation = camera.Rotation;
        }
        return true;
    }

    public static GameObject EnsureModelRoot(GameObject player)
    {
        GameObject? model = player.Children.FirstOrDefault(child =>
            child.Name.Equals("Model", StringComparison.OrdinalIgnoreCase) ||
            child.GetComponent<ModelHierarchyInstance>() != null);
        if (model != null) return model;
        Scene scene = player.Scene ?? throw new InvalidOperationException("The Blueprint root must belong to a scene.");
        model = scene.CreateGameObject("Model");
        model.SetParent(player, false);
        return model;
    }

    public static bool GroundModelAtFeet(GameObject modelObject, AssetManager assets)
    {
        ModelHierarchyInstance? instance = modelObject.GetComponent<ModelHierarchyInstance>();
        if (instance == null || instance.AutoGrounded || instance.Model.IsEmpty)
            return false;

        ModelAsset model = assets.LoadModel(instance.Model);
        if (!ModelImportScaleUtility.TryCalculateHierarchyBounds(
                model, out Vector3 minimum, out _))
            return false;
        if (!float.IsFinite(minimum.Y) || MathF.Abs(minimum.Y) > 10000f)
            return false;

        modelObject.Transform.LocalPosition += new Vector3(0f, -minimum.Y, 0f);
        instance.AutoGrounded = true;
        return true;
    }
    public static bool FitCharacterModelHeight(
        GameObject modelObject,
        AssetManager assets,
        float targetHeight = 1.8f)
    {
        if (modelObject.GetComponent<ModelHierarchyInstance>() is not { } instance ||
            instance.Model.IsEmpty ||
            !float.IsFinite(targetHeight) ||
            targetHeight <= 0f)
            return false;

        GameObject root = modelObject;
        while (root.Parent != null)
            root = root.Parent;
        if (root.GetComponent<CharacterController3D>() == null)
            return false;

        ModelAsset model = assets.LoadModel(instance.Model);
        if (!ModelImportScaleUtility.TryCalculateHierarchyBounds(
                model, out Vector3 minimum, out Vector3 maximum))
            return false;

        BoundingBox3D importedBounds = new(minimum, maximum);
        BoundingBox3D before = importedBounds.Transform(modelObject.Transform.WorldMatrix);
        float currentHeight = before.Size.Y;
        if (!before.IsValid || !float.IsFinite(currentHeight) ||
            currentHeight <= 0.00001f)
            return false;

        float multiplier = targetHeight / currentHeight;
        Vector3 fittedScale = modelObject.Transform.LocalScale * multiplier;
        if (!float.IsFinite(multiplier) || multiplier <= 0f ||
            !float.IsFinite(fittedScale.X) ||
            !float.IsFinite(fittedScale.Y) ||
            !float.IsFinite(fittedScale.Z) ||
            fittedScale.X <= 0f || fittedScale.Y <= 0f || fittedScale.Z <= 0f)
            return false;

        modelObject.Transform.LocalScale = fittedScale;
        BoundingBox3D after = importedBounds.Transform(modelObject.Transform.WorldMatrix);
        modelObject.Transform.WorldPosition += new Vector3(0f, before.Minimum.Y - after.Minimum.Y, 0f);
        CharacterCapsuleAutoFit.TryFit(root, assets, out _);
        return true;
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
