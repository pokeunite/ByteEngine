using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Editor;

internal static class AnimationClipDiscovery
{
    private static readonly (string Property, string[] Keywords)[] LocomotionMappings =
    {
        (nameof(AnimationController.Idle), new[] { "idle", "stand" }),
        (nameof(AnimationController.Walk), new[] { "walk" }),
        (nameof(AnimationController.Run), new[] { "run", "sprint", "jog" }),
        (nameof(AnimationController.Jump), new[] { "jump" }),
        (nameof(AnimationController.Fall), new[] { "fall", "falling", "air" }),
        (nameof(AnimationController.Land), new[] { "land", "landing" })
    };

    public static bool TryGetModel(
        AnimationController controller,
        EditorProjectContext project,
        out ModelAsset? model,
        out AssetReference modelReference)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(project);

        foreach (GameObject candidate in SelfAndDescendants(controller.GameObject))
        {
            if (candidate.GetComponent<ModelHierarchyInstance>() is not { } instance ||
                instance.Model.IsEmpty)
            {
                continue;
            }

            if (TryLoadAnimatedModel(
                    project,
                    instance.Model,
                    out model))
            {
                modelReference = instance.Model;
                return true;
            }
        }

        foreach (GameObject candidate in SelfAndDescendants(controller.GameObject))
        {
            if (candidate.GetComponent<SkeletalMeshRenderer>() is not { } renderer ||
                renderer.Model.IsEmpty)
            {
                continue;
            }

            if (TryLoadAnimatedModel(
                    project,
                    renderer.Model,
                    out model))
            {
                modelReference = renderer.Model;
                return true;
            }
        }

        model = null;
        modelReference = AssetReference.Empty;
        return false;
    }

    public static IReadOnlyList<string> GetClipNames(
        AnimationController controller,
        EditorProjectContext project)
    {
        return TryGetModel(
                controller,
                project,
                out ModelAsset? model,
                out _) &&
            model != null
            ? model.Animations
                .Select(animation => animation.Name)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()
            : Array.Empty<string>();
    }

    public static bool AutoAssign(
        AnimationController controller,
        EditorProjectContext project)
    {
        if (!TryGetModel(
                controller,
                project,
                out ModelAsset? model,
                out _) ||
            model == null ||
            model.Animations.Count == 0)
        {
            return false;
        }

        string[] names = model.Animations
            .Select(animation => animation.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToArray();

        bool changed = false;

        foreach ((string propertyName, string[] keywords) in LocomotionMappings)
        {
            string? match = FindBestMatch(names, keywords);

            if (string.IsNullOrWhiteSpace(match))
            {
                continue;
            }

            string current = GetValue(controller, propertyName);

            if (string.Equals(
                    current,
                    match,
                    StringComparison.Ordinal))
            {
                continue;
            }

            SetValue(controller, propertyName, match);
            changed = true;
        }

        return changed;
    }

    public static bool IsLocomotionClipProperty(string propertyName) =>
        LocomotionMappings.Any(item =>
            string.Equals(
                item.Property,
                propertyName,
                StringComparison.Ordinal));

    private static bool TryLoadAnimatedModel(
        EditorProjectContext project,
        AssetReference reference,
        out ModelAsset? model)
    {
        try
        {
            model = project.Assets.LoadModel(reference);

            return model.Animations.Count > 0;
        }
        catch
        {
            model = null;
            return false;
        }
    }

    private static string? FindBestMatch(
        IReadOnlyList<string> names,
        IReadOnlyList<string> keywords)
    {
        foreach (string keyword in keywords)
        {
            string? exact = names.FirstOrDefault(name =>
                string.Equals(
                    name,
                    keyword,
                    StringComparison.OrdinalIgnoreCase));

            if (exact != null)
            {
                return exact;
            }
        }

        foreach (string keyword in keywords)
        {
            string? suffix = names.FirstOrDefault(name =>
                EndsWithToken(name, keyword));

            if (suffix != null)
            {
                return suffix;
            }
        }

        foreach (string keyword in keywords)
        {
            string? contains = names.FirstOrDefault(name =>
                name.Contains(
                    keyword,
                    StringComparison.OrdinalIgnoreCase));

            if (contains != null)
            {
                return contains;
            }
        }

        return null;
    }

    private static bool EndsWithToken(
        string value,
        string token)
    {
        if (!value.EndsWith(
                token,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        int prefixLength = value.Length - token.Length;

        if (prefixLength <= 0)
        {
            return true;
        }

        char separator = value[prefixLength - 1];

        return separator is
            '_' or
            '-' or
            '|' or
            ':' or
            '/' or
            '\\' or
            ' ';
    }

    private static string GetValue(
        AnimationController controller,
        string propertyName) =>
        propertyName switch
        {
            nameof(AnimationController.Idle) => controller.Idle,
            nameof(AnimationController.Walk) => controller.Walk,
            nameof(AnimationController.Run) => controller.Run,
            nameof(AnimationController.Jump) => controller.Jump,
            nameof(AnimationController.Fall) => controller.Fall,
            nameof(AnimationController.Land) => controller.Land,
            _ => string.Empty
        };

    private static void SetValue(
        AnimationController controller,
        string propertyName,
        string value)
    {
        switch (propertyName)
        {
            case nameof(AnimationController.Idle):
                controller.Idle = value;
                break;

            case nameof(AnimationController.Walk):
                controller.Walk = value;
                break;

            case nameof(AnimationController.Run):
                controller.Run = value;
                break;

            case nameof(AnimationController.Jump):
                controller.Jump = value;
                break;

            case nameof(AnimationController.Fall):
                controller.Fall = value;
                break;

            case nameof(AnimationController.Land):
                controller.Land = value;
                break;
        }
    }

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
