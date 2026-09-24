using System.Numerics;
using System.Runtime.CompilerServices;

using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Diagnostics;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Core.Scene;

public enum AttachmentTransformRule
{
    KeepRelative,
    KeepWorld,
    SnapToTarget
}

/// <summary>
/// Persistent skeletal socket attachments.
///
/// The important ownership rule is that an attached object is physically
/// parented to the GameObject that owns the SkeletalMeshRenderer. The object's
/// local transform is then driven from the renderer's current MODEL-SPACE bone
/// pose plus the socket and attachment offsets.
///
/// v4 keeps a virtual payload basis only for imported DESCENDANTS.
/// Model/SkeletalMeshRenderer roots are scene-placement containers. A model
/// instance missing the current asset's Import Space node receives that basis
/// virtually, so legacy scene instances match the Socket Preview. Descendant
/// imported-node transforms can still be preserved beneath that root.
///
/// Character/world movement and facing therefore propagate through the normal
/// Transform hierarchy. This avoids the old behaviour that repeatedly wrote a
/// weapon's world transform and could fight character/camera rotation.
/// </summary>
public static class SkeletalAttachmentService
{
    private static readonly ConditionalWeakTable<GameObject, Binding> Bindings =
        new();

    private static readonly ConditionalWeakTable<SkeletalMeshRenderer, RendererBindingSet> RendererBindings =
        new();

    public static bool AttachToSocket(
        GameObject child,
        GameObject parent,
        string socketName,
        AttachmentTransformRule locationRule = AttachmentTransformRule.SnapToTarget,
        AttachmentTransformRule rotationRule = AttachmentTransformRule.SnapToTarget,
        AttachmentTransformRule scaleRule = AttachmentTransformRule.KeepRelative) =>
        AttachToSocket(
            child,
            parent,
            socketName,
            locationRule,
            rotationRule,
            scaleRule,
            Vector3.Zero,
            Vector3.Zero,
            Vector3.One);

    public static bool AttachToSocket(
        GameObject child,
        GameObject parent,
        string socketName,
        AttachmentTransformRule locationRule,
        AttachmentTransformRule rotationRule,
        AttachmentTransformRule scaleRule,
        Vector3 positionOffset,
        Vector3 rotationOffsetDegrees,
        Vector3 scaleMultiplier)
    {
        ArgumentNullException.ThrowIfNull(child);
        ArgumentNullException.ThrowIfNull(parent);

        if (string.IsNullOrWhiteSpace(socketName) ||
            !SkeletalSocketResolver.TryResolveRenderer(
                parent,
                socketName,
                out SkeletalMeshRenderer? renderer) ||
            renderer?.ResolvedModel is not { } model ||
            model.FindSocket(socketName) is not { } socket ||
            !SkeletalSocketResolver.TryGetSocketWorldTransform(
                renderer,
                socket.Name,
                out SkeletalSocketTransform socketWorld))
        {
            return false;
        }

        GameObject rendererObject = renderer.GameObject;

        if (ReferenceEquals(child, rendererObject) ||
            rendererObject.IsDescendantOf(child))
        {
            return false;
        }

        GameObject? previousParent = child.Parent;
        string previousSocket = child.ParentSocket;
        AttachmentTransformRule previousLocationRule = child.AttachmentLocationRule;
        AttachmentTransformRule previousRotationRule = child.AttachmentRotationRule;
        AttachmentTransformRule previousScaleRule = child.AttachmentScaleRule;
        Vector3 previousAttachmentPosition = child.AttachmentPosition;
        Quaternion previousAttachmentRotation = child.AttachmentRotation;
        Vector3 previousAttachmentScale = child.AttachmentScale;
        Bindings.TryGetValue(child, out Binding? previousBinding);

        PayloadBasis payloadBasis =
            previousBinding?.PayloadBasis ??
            CapturePayloadBasis(
                child,
                locationRule,
                rotationRule,
                scaleRule);

        Vector3 worldPosition = child.Transform.WorldMatrix.Translation;
        Quaternion worldRotation = SkeletalSocketResolver.GetHierarchyWorldRotation(child);
        Vector3 worldScale = child.Transform.WorldScale;

        Vector3 previousRelativePosition =
            child.IsAttached
                ? child.AttachmentPosition
                : child.Transform.LocalPosition;

        Quaternion previousRelativeRotation =
            child.IsAttached
                ? child.AttachmentRotation
                : child.Transform.LocalRotation;

        Vector3 previousRelativeScale =
            child.IsAttached
                ? child.AttachmentScale
                : child.Transform.LocalScale;

        Vector3 basePosition =
            locationRule switch
            {
                AttachmentTransformRule.SnapToTarget => Vector3.Zero,
                AttachmentTransformRule.KeepWorld =>
                    SafeDivide(
                        Vector3.Transform(
                            worldPosition - socketWorld.Position,
                            Quaternion.Inverse(socketWorld.Rotation)),
                        socketWorld.Scale),
                _ => previousRelativePosition
            };

        Quaternion baseRotation =
            rotationRule switch
            {
                AttachmentTransformRule.SnapToTarget => Quaternion.Identity,
                AttachmentTransformRule.KeepWorld =>
                    NormalizeSafe(
                        Quaternion.Inverse(socketWorld.Rotation) *
                        worldRotation *
                        Quaternion.Inverse(payloadBasis.Rotation)),
                _ => previousRelativeRotation
            };

        Vector3 baseScale =
            scaleRule switch
            {
                AttachmentTransformRule.SnapToTarget => Vector3.One,
                AttachmentTransformRule.KeepWorld =>
                    SafeDivide(
                        worldScale,
                        socketWorld.Scale),
                _ => previousRelativeScale
            };

        Quaternion offsetRotation =
            Quaternion.CreateFromYawPitchRoll(
                DegreesToRadians(rotationOffsetDegrees.Y),
                DegreesToRadians(rotationOffsetDegrees.X),
                DegreesToRadians(rotationOffsetDegrees.Z));

        Vector3 attachmentPosition =
            basePosition +
            positionOffset;

        Quaternion attachmentRotation =
            NormalizeSafe(
                baseRotation *
                offsetRotation);

        Vector3 attachmentScale =
            SanitizeScale(
                baseScale *
                scaleMultiplier);

        if (!Finite(attachmentPosition) ||
            !Finite(attachmentRotation) ||
            !Finite(attachmentScale))
        {
            return false;
        }

        /*
         * Parent to the renderer object, not the logical character root.
         * The renderer object's Transform is the exact model-space parent used
         * by SkeletalMeshRenderer.OnRender(), so model import scale/facing and
         * character world rotation are inherited naturally.
         */
        if (!child.SetParent(
                rendererObject,
                worldPositionStays: true))
        {
            return false;
        }

        Invalidate(child);

        child.ParentSocket = socket.Name;
        child.AttachmentLocationRule = locationRule;
        child.AttachmentRotationRule = rotationRule;
        child.AttachmentScaleRule = scaleRule;
        child.AttachmentPosition = attachmentPosition;
        child.AttachmentRotation = attachmentRotation;
        child.AttachmentScale = attachmentScale;

        var binding =
            new Binding(
                parent,
                rendererObject,
                socket.Name,
                renderer,
                model.Guid,
                socket.Id,
                payloadBasis);

        RegisterBinding(
            child,
            binding);

        if (ApplyBinding(
                child,
                binding))
        {
            return true;
        }

        // Roll back cleanly if the live pose became unavailable mid-attach.
        Invalidate(child);

        child.ParentSocket = previousSocket;
        child.AttachmentLocationRule = previousLocationRule;
        child.AttachmentRotationRule = previousRotationRule;
        child.AttachmentScaleRule = previousScaleRule;
        child.AttachmentPosition = previousAttachmentPosition;
        child.AttachmentRotation = previousAttachmentRotation;
        child.AttachmentScale = previousAttachmentScale;

        child.SetParent(
            previousParent,
            worldPositionStays: false);

        child.Transform.WorldPosition = worldPosition;
        child.Transform.WorldRotation = worldRotation;
        child.Transform.WorldScale = worldScale;

        if (previousBinding != null)
        {
            RegisterBinding(
                child,
                previousBinding);
        }

        return false;
    }

    public static bool Detach(
        GameObject child,
        bool keepWorldTransform = true)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (!child.IsAttached)
        {
            return false;
        }

        Vector3 worldPosition = child.Transform.WorldMatrix.Translation;
        Quaternion worldRotation = SkeletalSocketResolver.GetHierarchyWorldRotation(child);
        Vector3 worldScale = child.Transform.WorldScale;

        Invalidate(child);
        child.ParentSocket = string.Empty;

        if (!child.SetParent(
                null,
                worldPositionStays: false))
        {
            return false;
        }

        if (keepWorldTransform)
        {
            child.Transform.WorldPosition = worldPosition;
            child.Transform.WorldRotation = worldRotation;
            child.Transform.WorldScale = worldScale;
        }

        return true;
    }

    public static bool IsAttachedToSocket(
        GameObject child,
        GameObject? parent = null,
        string? socketName = null)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (!child.IsAttached)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(socketName) &&
            !string.Equals(
                child.ParentSocket,
                socketName,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (parent == null)
        {
            return true;
        }

        if (Bindings.TryGetValue(
                child,
                out Binding? binding))
        {
            return
                ReferenceEquals(
                    binding.RequestedParent,
                    parent) ||
                ReferenceEquals(
                    binding.PhysicalParent,
                    parent) ||
                binding.PhysicalParent.IsDescendantOf(
                    parent);
        }

        return
            ReferenceEquals(
                child.Parent,
                parent) ||
            child.Parent?.IsDescendantOf(parent) == true;
    }

    /// <summary>
    /// Compatibility/editor fallback. Runtime animation following is primarily
    /// driven directly by SkeletalMeshRenderer.UpdatePoseAndMeshes via
    /// UpdateForRenderer().
    ///
    /// Keeping this pass means serialized/legacy attachments can rehydrate even
    /// before a renderer produces its first animated pose, and editor previews
    /// remain live. Apply() now writes LOCAL model-space transforms only, so
    /// running this fallback cannot fight character world rotation.
    /// </summary>
    internal static void UpdateScene(
        Scene scene)
    {
        foreach (GameObject item
                 in scene.GameObjects.ToArray())
        {
            if (item.IsAttached)
            {
                Apply(item);
            }
        }
    }

    /// <summary>
    /// Called by SkeletalMeshRenderer immediately after it has evaluated the
    /// final blended/root-motion-corrected pose. This mirrors the Flax
    /// AnimatedModel -> BoneSocket update ownership: socket followers consume
    /// the same pose that is used to render the character.
    /// </summary>
    internal static void UpdateForRenderer(
        SkeletalMeshRenderer renderer)
    {
        if (!RendererBindings.TryGetValue(
                renderer,
                out RendererBindingSet? set))
        {
            return;
        }

        WeakReference<GameObject>[] followers;

        lock (set.SyncRoot)
        {
            followers =
                set.Children.ToArray();
        }

        bool prune = false;

        foreach (WeakReference<GameObject> weak
                 in followers)
        {
            if (!weak.TryGetTarget(
                    out GameObject? child))
            {
                prune = true;
                continue;
            }

            if (!Bindings.TryGetValue(
                    child,
                    out Binding? binding) ||
                !ReferenceEquals(
                    binding.Renderer,
                    renderer))
            {
                prune = true;
                continue;
            }

            ApplyBinding(
                child,
                binding);
        }

        if (prune)
        {
            PruneRendererSet(
                renderer,
                set);
        }
    }

    public static void Invalidate(
        GameObject child)
    {
        Bindings.Remove(child);
    }

    internal static bool HasCachedBinding(
        GameObject child) =>
        Bindings.TryGetValue(
            child,
            out _);

    internal static void WriteDiagnostics(
        GameObject child,
        RuntimeDiagnosticWriter writer)
    {
        writer.Section("Socket Attachment: " + child.Name);
        writer.Add("Socket", child.ParentSocket);
        writer.Add("Physical Parent", child.Parent?.Name);
        writer.Add("Child Local Rotation", child.Transform.LocalRotation);
        writer.Add("Child World Rotation", child.Transform.WorldRotation);
        writer.Add("Attachment Rotation", child.AttachmentRotation);

        if (!Bindings.TryGetValue(child, out Binding? binding))
        {
            writer.Add("Cached Binding", false);
            return;
        }

        writer.Add("Cached Binding", true);
        writer.Add("Renderer", binding.Renderer.GameObject.Name);
        writer.Add("Imported Payload Rotation", binding.PayloadBasis.Rotation);
        writer.Add("Imported Payload Scale", binding.PayloadBasis.Scale);
        writer.Add("Has Import Space Child", child.Children.Any(item =>
            item.Name.Equals("Import Space", StringComparison.Ordinal)));
        if (SkeletalSocketResolver.TryGetSocketWorldTransform(
                binding.Renderer, binding.SocketName,
                out SkeletalSocketTransform socketWorld))
        {
            writer.Add("Socket World Rotation", socketWorld.Rotation);
            writer.Add("Socket World Scale", socketWorld.Scale);
            writer.Add("Socket World Position", socketWorld.Position);
        }
    }

    internal static string BuildTraceSample(GameObject child)
    {
        if (!Bindings.TryGetValue(child, out Binding? binding))
            return $"object={child.Name} id={child.Id} socket={child.ParentSocket} binding=MISSING";

        SkeletalMeshRenderer renderer = binding.Renderer;
        SkeletalSocketDefinition? socket = renderer.ResolvedModel?.FindSocket(binding.SocketId);
        if (socket == null ||
            !renderer.TryGetBoneWorldMatrix(socket.BoneName, out Matrix4x4 boneWorld) ||
            !SkeletalSocketResolver.TryGetSocketWorldTransform(renderer, socket.Name,
                out SkeletalSocketTransform socketWorld))
            return $"object={child.Name} id={child.Id} socket={binding.SocketName} pose=MISSING";

        Quaternion attachmentRotation = NormalizeSafe(child.AttachmentRotation);
        Vector3 expectedPosition = socketWorld.Position +
            Vector3.Transform(
                (child.AttachmentPosition +
                 Vector3.Transform(binding.PayloadBasis.Position * child.AttachmentScale,
                     attachmentRotation)) * socketWorld.Scale,
                socketWorld.Rotation);
        Quaternion expectedRotation = NormalizeSafe(
            socketWorld.Rotation * attachmentRotation * binding.PayloadBasis.Rotation);
        Vector3 actualPosition = child.Transform.WorldMatrix.Translation;
        Quaternion actualRotation = SkeletalSocketResolver.GetHierarchyWorldRotation(child);
        float positionError = Vector3.Distance(expectedPosition, actualPosition);
        float rotationDot = Math.Clamp(MathF.Abs(Quaternion.Dot(
            expectedRotation, actualRotation)), 0f, 1f);
        float rotationErrorDegrees = 2f * MathF.Acos(rotationDot) * 180f / MathF.PI;

        GameObject? muzzle = FindDescendantByName(child, "Muzzle");
        GameObject? grip = FindDescendantByName(child, "PistolArmature");
        Vector3 barrel = muzzle != null && grip != null
            ? muzzle.Transform.WorldMatrix.Translation - grip.Transform.WorldMatrix.Translation
            : Vector3.Zero;
        float muzzleMatrixError = muzzle != null
            ? Vector3.Distance(muzzle.Transform.WorldMatrix.Translation, muzzle.Transform.WorldPosition)
            : float.NaN;
        float gripMatrixError = grip != null
            ? Vector3.Distance(grip.Transform.WorldMatrix.Translation, grip.Transform.WorldPosition)
            : float.NaN;
        string barrelDot = barrel.LengthSquared() > 1e-8f
            ? Vector3.Dot(Vector3.Normalize(barrel), renderer.Transform.Forward).ToString("0.000")
            : "n/a";

        static string V(Vector3 value) =>
            $"({value.X:0.000},{value.Y:0.000},{value.Z:0.000})";
        static string Q(Quaternion value) =>
            $"({value.X:0.000},{value.Y:0.000},{value.Z:0.000},{value.W:0.000})";

        return $"object={child.Name} id={child.Id} parent={binding.RequestedParent.Name} " +
            $"renderer={renderer.GameObject.Name} socket={socket.Name}/{socket.Id} bone={socket.BoneName} " +
            $"clip={renderer.CurrentAnimation} clipTime={renderer.PlaybackTime:0.000} " +
            $"handPos={V(boneWorld.Translation)} socketPos={V(socketWorld.Position)} " +
            $"gunPos={V(actualPosition)} expectedPos={V(expectedPosition)} " +
            $"socketScale={V(socketWorld.Scale)} gunScale={V(child.Transform.WorldScale)} " +
            $"posError={positionError:0.000000} rotErrorDeg={rotationErrorDegrees:0.000} " +
            $"socketRot={Q(socketWorld.Rotation)} gunRot={Q(actualRotation)} " +
            $"gunBarrel={V(barrel)} barrelForwardDot={barrelDot} " +
            $"muzzleMatrixError={muzzleMatrixError:0.000000} gripMatrixError={gripMatrixError:0.000000}";
    }

    private static GameObject? FindDescendantByName(GameObject root, string name)
    {
        foreach (GameObject child in root.Children)
        {
            if (string.Equals(child.Name, name, StringComparison.OrdinalIgnoreCase))
                return child;
            GameObject? nested = FindDescendantByName(child, name);
            if (nested != null)
                return nested;
        }

        return null;
    }

    public static bool Apply(
        GameObject child)
    {
        ArgumentNullException.ThrowIfNull(child);

        return
            TryResolveBinding(
                child,
                out Binding? binding) &&
            binding != null &&
            ApplyBinding(
                child,
                binding);
    }

    private static bool TryResolveBinding(
        GameObject child,
        out Binding? binding)
    {
        binding = null;

        if (child.Parent == null ||
            string.IsNullOrWhiteSpace(
                child.ParentSocket))
        {
            return false;
        }

        if (Bindings.TryGetValue(
                child,
                out binding))
        {
            if (ReferenceEquals(
                    child.Parent,
                    binding.PhysicalParent) &&
                ReferenceEquals(
                    binding.Renderer.GameObject,
                    binding.PhysicalParent) &&
                binding.Renderer.ResolvedModel is { } cachedModel &&
                cachedModel.Guid == binding.ModelGuid &&
                cachedModel.FindSocket(
                    binding.SocketId) != null)
            {
                return true;
            }

            Bindings.Remove(child);
            binding = null;
        }

        GameObject requestedParent =
            child.Parent;

        if (!SkeletalSocketResolver.TryResolveRenderer(
                requestedParent,
                child.ParentSocket,
                out SkeletalMeshRenderer? renderer) ||
            renderer?.ResolvedModel is not { } model ||
            model.FindSocket(
                child.ParentSocket) is not { } socket)
        {
            return false;
        }

        GameObject rendererObject =
            renderer.GameObject;

        if (!ReferenceEquals(
                child.Parent,
                rendererObject) &&
            !child.SetParent(
                rendererObject,
                worldPositionStays: true))
        {
            return false;
        }

        binding =
            new Binding(
                requestedParent,
                rendererObject,
                socket.Name,
                renderer,
                model.Guid,
                socket.Id,
                CapturePayloadBasis(
                    child,
                    child.AttachmentLocationRule,
                    child.AttachmentRotationRule,
                    child.AttachmentScaleRule));

        RegisterBinding(
            child,
            binding);

        return true;
    }

    private static bool ApplyBinding(
        GameObject child,
        Binding binding)
    {
        if (!ReferenceEquals(
                child.Parent,
                binding.PhysicalParent) ||
            !string.Equals(
                child.ParentSocket,
                binding.SocketName,
                StringComparison.OrdinalIgnoreCase) ||
            binding.Renderer.ResolvedModel is not { } model ||
            model.Guid != binding.ModelGuid)
        {
            return false;
        }

        SkeletalSocketDefinition? socket =
            model.FindSocket(
                binding.SocketId) ??
            model.FindSocket(
                child.ParentSocket);

        if (socket == null ||
            !SkeletalSocketResolver.TryGetSocketModelTransform(
                binding.Renderer,
                socket,
                out SkeletalSocketTransform socketModel))
        {
            // Keep the last valid transform instead of snapping elsewhere.
            return false;
        }

        if (!string.Equals(
                child.ParentSocket,
                socket.Name,
                StringComparison.Ordinal))
        {
            child.ParentSocket =
                socket.Name;
        }

        /*
         * The socket is a virtual parent of the attachment, which is itself a
         * virtual parent of the imported payload. Compose their TRS values the
         * same way Transform composes an actual GameObject hierarchy. A matrix
         * product with non-uniform socket scale and a rotated attachment has
         * shear; decomposing it back to TRS changes the weapon's rotation.
         */
        PayloadBasis payload = binding.PayloadBasis;
        Quaternion attachmentRotation = NormalizeSafe(child.AttachmentRotation);
        Quaternion socketRotation = NormalizeSafe(socketModel.Rotation);
        Vector3 attachmentPosition =
            child.AttachmentPosition +
            Vector3.Transform(
                payload.Position * child.AttachmentScale,
                attachmentRotation);
        Vector3 localPosition =
            socketModel.Position +
            Vector3.Transform(
                attachmentPosition * socketModel.Scale,
                socketRotation);
        Quaternion localRotation =
            NormalizeSafe(
                socketRotation *
                attachmentRotation *
                payload.Rotation);
        Vector3 localScale =
            SanitizeScale(
                payload.Scale *
                child.AttachmentScale *
                socketModel.Scale);

        if (!Finite(localPosition) ||
            !Finite(localRotation) ||
            !Finite(localScale))
        {
            return false;
        }

        /*
         * Never write world transforms here. The child is already parented to
         * the SkeletalMeshRenderer's GameObject, so character movement, facing,
         * Blueprint placement and visual/import correction flow through normal
         * hierarchy composition exactly once.
         */
        child.Transform.LocalPosition = localPosition;
        child.Transform.LocalRotation = localRotation;
        child.Transform.LocalScale = localScale;

        return true;
    }

    /// <summary>
    /// Captures the transform that belongs to the ATTACHED ASSET itself rather
    /// than to the socket binding. This is the virtual child below the socket.
    ///
    /// Why this exists:
    /// Event Sheets can reference either an imported model ROOT or an imported
    /// descendant. Those cases must not be treated the same:
    ///
    /// - model root: its Transform is scene placement, so SnapToTarget uses an
    ///   identity payload basis (the same structure used by Socket Preview);
    /// - imported descendant: preserve the authored transform from model root to
    ///   that descendant so selecting a mesh/node directly does not erase its
    ///   genuine imported orientation/offset.
    ///
    /// Import scale is deliberately excluded to avoid applying it twice.
    /// </summary>
    private static PayloadBasis CapturePayloadBasis(
        GameObject child,
        AttachmentTransformRule locationRule,
        AttachmentTransformRule rotationRule,
        AttachmentTransformRule scaleRule)
    {
        GameObject? modelRoot =
            FindModelRoot(child);

        if (modelRoot == null)
        {
            return PayloadBasis.Identity;
        }

        Vector3 position =
            Vector3.Zero;

        Quaternion rotation =
            Quaternion.Identity;

        Vector3 scale =
            Vector3.One;

        if (ReferenceEquals(
                modelRoot,
                child))
        {
            /*
             * The model root is a scene-placement container. Its pre-attach
             * rotation is not an asset correction and must not be retained by
             * SnapToTarget. An older scene instance may lack the "Import Space"
             * child now present in the imported asset. The socket preview
             * renders that child; without it, the live weapon can point away
             * from the preview. Apply that missing basis virtually.
             */
            PayloadBasis importBasis =
                MissingImportBasis(modelRoot);
            position = importBasis.Position;
            rotation = importBasis.Rotation;
            scale = importBasis.Scale;
        }
        else
        {
            /*
             * If an imported descendant is attached directly, preserve the
             * authored offset/orientation from its model root so the selected
             * node keeps the same basis it had inside the imported asset.
             *
             * Scale is intentionally NOT copied into PayloadBasis. Scale rules
             * stay authoritative and, most importantly, we do not re-apply the
             * model import scale underneath an already-scaled character model.
             */
            if (Matrix4x4.Invert(
                    modelRoot.Transform.WorldMatrix,
                    out Matrix4x4 inverseRoot))
            {
                Matrix4x4 relative =
                    child.Transform.WorldMatrix *
                    inverseRoot;

                if (Matrix4x4.Decompose(
                        relative,
                        out _,
                        out Quaternion relativeRotation,
                        out Vector3 relativePosition))
                {
                    if (locationRule ==
                        AttachmentTransformRule.SnapToTarget)
                    {
                        position =
                            relativePosition;
                    }

                    if (rotationRule ==
                        AttachmentTransformRule.SnapToTarget)
                    {
                        rotation =
                            NormalizeSafe(
                                relativeRotation);
                    }
                }
            }
        }

        if (!Finite(position) ||
            !Finite(rotation) ||
            !Finite(scale))
        {
            return PayloadBasis.Identity;
        }

        return
            new PayloadBasis(
                position,
                NormalizeSafe(rotation),
                SanitizeScale(scale));
    }

    private static PayloadBasis MissingImportBasis(GameObject modelRoot)
    {
        ModelHierarchyInstance? instance =
            modelRoot.GetComponent<ModelHierarchyInstance>();
        if (instance == null ||
            instance.Model.IsEmpty ||
            modelRoot.Children.Any(child =>
                child.Name.Equals("Import Space", StringComparison.Ordinal)) ||
            !AnimationRuntimeAssets.TryGet(out AssetManager? assets) ||
            assets == null)
        {
            return PayloadBasis.Identity;
        }

        try
        {
            ModelAsset model = assets.LoadModel(instance.Model);
            ImportedNode? correction = model.Nodes.FirstOrDefault(node =>
                node.Key == ImportedModelSpace.CorrectionNodeKey);
            if (correction != null &&
                Matrix4x4.Decompose(correction.LocalTransform,
                    out Vector3 scale,
                    out Quaternion rotation,
                    out Vector3 position) &&
                Finite(position) &&
                Finite(rotation) &&
                Finite(scale))
            {
                return new PayloadBasis(position, NormalizeSafe(rotation),
                    SanitizeScale(scale));
            }
        }
        catch
        {
            // An unavailable asset cannot supply the missing import basis,
            // but should not prevent an otherwise valid socket attachment.
        }

        return PayloadBasis.Identity;
    }

    private static GameObject? FindModelRoot(
        GameObject child)
    {
        // The selected object itself can be the model instance root.
        if (IsModelRoot(child))
        {
            return child;
        }

        // Otherwise locate the nearest imported/skeletal model ancestor.
        for (GameObject? current =
                 child.Parent;
             current != null;
             current = current.Parent)
        {
            if (IsModelRoot(current))
            {
                return current;
            }
        }

        return null;
    }

    private static bool IsModelRoot(
        GameObject item) =>
        item.GetComponent<ModelHierarchyInstance>() != null ||
        item.GetComponent<SkeletalMeshRenderer>() != null ||
        item.GetComponent<VisualModelOverride>() != null;

    private static void RegisterBinding(
        GameObject child,
        Binding binding)
    {
        Bindings.Remove(child);
        Bindings.Add(
            child,
            binding);

        RendererBindingSet set =
            RendererBindings.GetValue(
                binding.Renderer,
                static _ =>
                    new RendererBindingSet());

        lock (set.SyncRoot)
        {
            for (int index =
                     set.Children.Count - 1;
                 index >= 0;
                 index--)
            {
                if (!set.Children[index].TryGetTarget(
                        out GameObject? existing))
                {
                    set.Children.RemoveAt(index);
                    continue;
                }

                if (ReferenceEquals(
                        existing,
                        child))
                {
                    set.Children.RemoveAt(index);
                }
            }

            set.Children.Add(
                new WeakReference<GameObject>(
                    child));
        }
    }

    private static void PruneRendererSet(
        SkeletalMeshRenderer renderer,
        RendererBindingSet set)
    {
        lock (set.SyncRoot)
        {
            for (int index =
                     set.Children.Count - 1;
                 index >= 0;
                 index--)
            {
                if (!set.Children[index].TryGetTarget(
                        out GameObject? child) ||
                    !Bindings.TryGetValue(
                        child,
                        out Binding? binding) ||
                    !ReferenceEquals(
                        binding.Renderer,
                        renderer))
                {
                    set.Children.RemoveAt(index);
                }
            }
        }
    }

    private static float DegreesToRadians(
        float value) =>
        value *
        MathF.PI /
        180.0f;

    private static Quaternion NormalizeSafe(
        Quaternion value) =>
        value.LengthSquared() <
            0.000000000001f
            ? Quaternion.Identity
            : Quaternion.Normalize(value);

    private static Vector3 SafeDivide(
        Vector3 numerator,
        Vector3 denominator) =>
        new(
            numerator.X /
            SafeDivisor(denominator.X),
            numerator.Y /
            SafeDivisor(denominator.Y),
            numerator.Z /
            SafeDivisor(denominator.Z));

    private static float SafeDivisor(
        float value) =>
        MathF.Abs(value) <
            0.0001f
            ? MathF.CopySign(
                0.0001f,
                value == 0.0f
                    ? 1.0f
                    : value)
            : value;

    private static Vector3 SanitizeScale(
        Vector3 value) =>
        new(
            SafeDivisor(value.X),
            SafeDivisor(value.Y),
            SafeDivisor(value.Z));

    private static bool Finite(
        Vector3 value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z);

    private static bool Finite(
        Quaternion value) =>
        float.IsFinite(value.X) &&
        float.IsFinite(value.Y) &&
        float.IsFinite(value.Z) &&
        float.IsFinite(value.W);

    private sealed class RendererBindingSet
    {
        public object SyncRoot { get; } =
            new();

        public List<WeakReference<GameObject>> Children { get; } =
            new();
    }

    private readonly record struct PayloadBasis(
        Vector3 Position,
        Quaternion Rotation,
        Vector3 Scale)
    {
        public static PayloadBasis Identity { get; } =
            new(
                Vector3.Zero,
                Quaternion.Identity,
                Vector3.One);

        public Matrix4x4 Matrix =>
            Matrix4x4.CreateScale(Scale) *
            Matrix4x4.CreateFromQuaternion(Rotation) *
            Matrix4x4.CreateTranslation(Position);
    }

    private sealed record Binding(
        GameObject RequestedParent,
        GameObject PhysicalParent,
        string SocketName,
        SkeletalMeshRenderer Renderer,
        Guid ModelGuid,
        Guid SocketId,
        PayloadBasis PayloadBasis);
}
