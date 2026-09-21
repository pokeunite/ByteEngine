using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Editor.Panels;

internal sealed record AnimationSignalAuthoringResult(
    GameObject? Target,
    AnimationController? Controller,
    ModelAsset? Model,
    AssetReference ModelReference,
    string Message)
{
    public bool HasSelectors =>
        Target != null && Controller != null && Model != null;
}

internal static class AnimationSignalAuthoringResolver
{
    public static string CreateCacheKey(
        GameObject? target,
        AnimationController? controller)
    {
        if (target == null)
        {
            return "missing-target";
        }

        List<string> references = new();

        foreach (GameObject candidate in SelfAndDescendants(target))
        {
            AddReference(references, candidate.GetComponent<ModelHierarchyInstance>()?.Model);
            AddReference(references, candidate.GetComponent<SkeletalMeshRenderer>()?.Model);
        }

        return string.Join(
            "|",
            target.Id,
            ReferenceToken(controller?.AnimationProfile),
            string.Join(";", references));
    }

    public static AnimationSignalAuthoringResult Resolve(
        GameObject? target,
        EditorProjectContext project)
    {
        if (target == null)
        {
            return new(null, null, null, AssetReference.Empty,
                "Target cannot be resolved in the current authoring context.");
        }

        AnimationController? controller =
            target.GetComponent<AnimationController>();

        if (controller == null)
        {
            return new(target, null, null, AssetReference.Empty,
                "Target has no AnimationController.");
        }

        if (!controller.AnimationProfile.IsEmpty)
        {
            try
            {
                AnimationProfile profile =
                    project.Assets.LoadAnimationProfile(controller.AnimationProfile);
                AssetReference reference = profile.Rig.ReferenceModel;

                if (!reference.IsEmpty)
                {
                    ModelAsset model = project.Assets.LoadModel(reference);
                    return new(target, controller, model, reference, string.Empty);
                }
            }
            catch (Exception exception)
            {
                return new(target, controller, null, AssetReference.Empty,
                    $"Animation Profile reference model could not be loaded: {exception.Message}");
            }
        }

        if (ByteEngine.Editor.AnimationClipDiscovery.TryGetModel(
                controller,
                project,
                out ModelAsset? fallback,
                out AssetReference fallbackReference) &&
            fallback != null)
        {
            return new(target, controller, fallback, fallbackReference, string.Empty);
        }

        return new(target, controller, null, AssetReference.Empty,
            "The target AnimationController has no discoverable animation model.");
    }

    public static ImportedAnimation? FindClip(
        ModelAsset model,
        string storedValue) =>
        model.Animations.FirstOrDefault(animation =>
            string.Equals(animation.Key, storedValue, StringComparison.OrdinalIgnoreCase))
        ?? model.Animations.FirstOrDefault(animation =>
            string.Equals(animation.Name, storedValue, StringComparison.OrdinalIgnoreCase));

    public static bool ContainsEvent(
        ImportedAnimation animation,
        string name) =>
        string.IsNullOrWhiteSpace(name) ||
        animation.Events.Any(marker =>
            string.Equals(marker.Name, name, StringComparison.OrdinalIgnoreCase));

    public static bool ContainsWindow(
        ImportedAnimation animation,
        string name) =>
        string.IsNullOrWhiteSpace(name) ||
        animation.Windows.Any(window =>
            string.Equals(window.Name, name, StringComparison.OrdinalIgnoreCase));

    private static void AddReference(
        ICollection<string> references,
        AssetReference? reference)
    {
        if (reference != null && !reference.IsEmpty)
        {
            references.Add(ReferenceToken(reference));
        }
    }

    private static string ReferenceToken(AssetReference? reference) =>
        reference == null || reference.IsEmpty
            ? string.Empty
            : $"{reference.Guid:N}:{reference.CachedProjectPath}";

    private static IEnumerable<GameObject> SelfAndDescendants(GameObject root)
    {
        yield return root;

        foreach (GameObject child in root.Children)
        {
            foreach (GameObject descendant in SelfAndDescendants(child))
            {
                yield return descendant;
            }
        }
    }
}

