using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Serialization;

using System.Text.Json;

namespace ByteEngine.Core.Animation;

/// <summary>
/// Identifies the kind of name collision found when adding a baked animation
/// to a model-owned animation library.
/// </summary>
public enum ModelOwnedAnimationConflictKind
{
    None,
    ImportedAnimation,
    BakedAnimation
}

/// <summary>
/// Editor-facing source information retained with a baked animation. Runtime
/// playback does not depend on this source asset; it only exists so tooling can
/// explain where the animation came from and support a future Re-Retarget flow.
/// </summary>
public sealed class ModelOwnedAnimationProvenance
{
    public Guid SourceModelGuid { get; set; }

    public string SourceProjectPath { get; set; } =
        string.Empty;

    public string SourceClipKey { get; set; } =
        string.Empty;

    public string SourceClipName { get; set; } =
        string.Empty;
}

public readonly record struct ModelOwnedAnimationBakeResult(
    ImportedAnimation Animation,
    bool Replaced,
    string LibraryPath);

/// <summary>
/// Lightweight editor-facing information about one baked animation. Keeping
/// this separate from the full animation payload lets authoring UI cache names
/// and provenance without repeatedly walking or deserializing keyframe data.
/// </summary>
public readonly record struct ModelOwnedAnimationSummary(
    string Name,
    ModelOwnedAnimationProvenance Provenance);

/// <summary>
/// Persistent model-owned animation storage introduced by C9.5.
///
/// Retargeted animations are NOT written back into the FBX/GLTF source file.
/// They are stored in project-internal data keyed by the target model GUID and
/// merged into ModelAsset.Animations whenever the target model is loaded.
///
/// This gives the editor/runtime the same simple ownership model as a native
/// embedded clip while keeping source model files untouched and reimport-safe.
/// </summary>
public static class ModelOwnedAnimationStore
{
    private const int CurrentVersion =
        1;

    /*
     * Animation libraries can contain thousands of keyframes. Retarget editor
     * UI must never deserialize that JSON every frame, so keep the parsed
     * library hot and invalidate it only when the backing file changes.
     */
    private static readonly Dictionary<string, CachedLibrary> LibraryCache =
        new(StringComparer.OrdinalIgnoreCase);

    public static string GetLibraryPath(
        string projectRoot,
        Guid targetModelGuid)
    {
        if (string.IsNullOrWhiteSpace(
                projectRoot))
        {
            throw new ArgumentException(
                "Project root cannot be empty.",
                nameof(projectRoot));
        }

        if (targetModelGuid ==
            Guid.Empty)
        {
            throw new ArgumentException(
                "Target model GUID cannot be empty.",
                nameof(targetModelGuid));
        }

        return Path.Combine(
            Path.GetFullPath(projectRoot),
            ".byteengine",
            "ModelAnimations",
            $"{targetModelGuid:N}.animations.json");
    }

    public static ModelOwnedAnimationConflictKind GetConflict(
        string projectRoot,
        ModelAsset targetModel,
        string animationName)
    {
        ArgumentNullException.ThrowIfNull(
            targetModel);

        string name =
            NormalizeName(
                animationName);

        ModelOwnedAnimationLibrary library =
            LoadLibrary(
                projectRoot,
                targetModel.Guid);

        if (library.Animations.Any(
                entry =>
                    string.Equals(
                        entry.Animation.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase)))
        {
            return
                ModelOwnedAnimationConflictKind.BakedAnimation;
        }

        if (targetModel.Animations.Any(
                animation =>
                    string.Equals(
                        animation.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase)))
        {
            return
                ModelOwnedAnimationConflictKind.ImportedAnimation;
        }

        return
            ModelOwnedAnimationConflictKind.None;
    }

    public static bool IsBakedAnimation(
        string projectRoot,
        ModelAsset targetModel,
        string animationName)
    {
        return GetConflict(
                projectRoot,
                targetModel,
                animationName) ==
            ModelOwnedAnimationConflictKind.BakedAnimation;
    }

    public static ModelOwnedAnimationProvenance? GetProvenance(
        string projectRoot,
        ModelAsset targetModel,
        string animationName)
    {
        ArgumentNullException.ThrowIfNull(
            targetModel);

        string name =
            NormalizeName(
                animationName);

        ModelOwnedAnimationEntry? entry =
            LoadLibrary(
                    projectRoot,
                    targetModel.Guid)
                .Animations
                .FirstOrDefault(
                    candidate =>
                        string.Equals(
                            candidate.Animation.Name,
                            name,
                            StringComparison.OrdinalIgnoreCase));

        return entry?.Provenance;
    }

    /// <summary>
    /// Persists a temporary retarget result as an animation owned by the target
    /// model. Imported/native clips can never be silently replaced. An existing
    /// baked clip may only be replaced when replaceExistingBaked is explicitly
    /// true.
    /// </summary>
    public static ModelOwnedAnimationBakeResult Bake(
        string projectRoot,
        ModelAsset targetModel,
        ImportedAnimation temporaryAnimation,
        string animationName,
        Guid sourceModelGuid,
        string? sourceProjectPath,
        string? sourceClipKey,
        string? sourceClipName,
        bool replaceExistingBaked)
    {
        ArgumentNullException.ThrowIfNull(
            targetModel);

        ArgumentNullException.ThrowIfNull(
            temporaryAnimation);

        string name =
            NormalizeName(
                animationName);

        ModelOwnedAnimationLibrary library =
            LoadLibrary(
                projectRoot,
                targetModel.Guid);

        ModelOwnedAnimationEntry? existing =
            library.Animations.FirstOrDefault(
                entry =>
                    string.Equals(
                        entry.Animation.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase));

        if (existing ==
            null &&
            targetModel.Animations.Any(
                animation =>
                    string.Equals(
                        animation.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                $"Animation '{name}' already exists inside the imported target model. Choose a different name; native model clips are never overwritten by a retarget bake.");
        }

        if (existing !=
                null &&
            !replaceExistingBaked)
        {
            throw new InvalidOperationException(
                $"A baked animation named '{name}' already belongs to '{targetModel.Name}'. Explicit replacement approval is required.");
        }

        bool replaced =
            existing !=
            null;

        string key =
            existing?.Animation.Key ??
            $"owned-retarget:{targetModel.Guid:N}:{Guid.NewGuid():N}";

        var ownedAnimation =
            new ImportedAnimation
            {
                Key =
                    key,

                Name =
                    name,

                Duration =
                    Math.Max(
                        temporaryAnimation.Duration,
                        0.0f),

                Channels =
                    temporaryAnimation.Channels
                        .ToList()
            };

        var provenance =
            new ModelOwnedAnimationProvenance
            {
                SourceModelGuid =
                    sourceModelGuid,

                SourceProjectPath =
                    sourceProjectPath ??
                    string.Empty,

                SourceClipKey =
                    sourceClipKey ??
                    string.Empty,

                SourceClipName =
                    sourceClipName ??
                    string.Empty
            };

        var entry =
            new ModelOwnedAnimationEntry
            {
                Animation =
                    ownedAnimation,

                Provenance =
                    provenance
            };

        if (existing !=
            null)
        {
            int index =
                library.Animations.IndexOf(
                    existing);

            library.Animations[index] =
                entry;
        }
        else
        {
            library.Animations.Add(
                entry);
        }

        library.Version =
            CurrentVersion;

        library.TargetModelGuid =
            targetModel.Guid;

        string path =
            GetLibraryPath(
                projectRoot,
                targetModel.Guid);

        JsonSerialization.WriteAtomic(
            path,
            library);

        CacheLibrary(
            path,
            library);

        /*
         * Make the successful bake visible immediately through the SAME
         * ModelAsset.Animations collection used by the existing Asset Browser
         * FBX expand-arrow and every current animation picker.
         */
        targetModel.RegisterRuntimeAnimation(
            ownedAnimation);

        return
            new ModelOwnedAnimationBakeResult(
                ownedAnimation,
                replaced,
                path);
    }

    /// <summary>
    /// Merges previously baked target-owned animations into a freshly imported
    /// model. Called by AssetManager on first load and every model reimport.
    /// </summary>
    internal static int MergeInto(
        string projectRoot,
        ModelAsset targetModel)
    {
        ArgumentNullException.ThrowIfNull(
            targetModel);

        ModelOwnedAnimationLibrary library =
            LoadLibraryForMerge(
                projectRoot,
                targetModel.Guid);

        int merged =
            0;

        foreach (ModelOwnedAnimationEntry entry
                 in library.Animations)
        {
            if (entry.Animation ==
                    null ||
                string.IsNullOrWhiteSpace(
                    entry.Animation.Name) ||
                string.IsNullOrWhiteSpace(
                    entry.Animation.Key))
            {
                continue;
            }

            targetModel.RegisterRuntimeAnimation(
                entry.Animation);

            merged++;
        }

        return merged;
    }

    public static IReadOnlyList<ModelOwnedAnimationSummary> GetBakedAnimationSummaries(
        string projectRoot,
        Guid targetModelGuid)
    {
        if (targetModelGuid ==
            Guid.Empty)
        {
            return Array.Empty<ModelOwnedAnimationSummary>();
        }

        return LoadLibrary(
                projectRoot,
                targetModelGuid)
            .Animations
            .Where(
                entry =>
                    entry.Animation !=
                        null &&
                    !string.IsNullOrWhiteSpace(
                        entry.Animation.Name))
            .Select(
                entry =>
                    new ModelOwnedAnimationSummary(
                        entry.Animation.Name,
                        entry.Provenance))
            .OrderBy(
                item =>
                    item.Name,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<string> GetBakedAnimationNames(
        string projectRoot,
        Guid targetModelGuid)
    {
        if (targetModelGuid ==
            Guid.Empty)
        {
            return Array.Empty<string>();
        }

        return LoadLibrary(
                projectRoot,
                targetModelGuid)
            .Animations
            .Where(
                entry =>
                    entry.Animation !=
                        null &&
                    !string.IsNullOrWhiteSpace(
                        entry.Animation.Name))
            .Select(
                entry =>
                    entry.Animation.Name)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .OrderBy(
                name =>
                    name,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>
    /// Removes one retargeted/model-owned animation from persistent storage and
    /// from the currently loaded target ModelAsset. Native imported animations
    /// are not represented by this store and therefore cannot be removed here.
    /// </summary>
    public static bool RemoveBakedAnimation(
        string projectRoot,
        ModelAsset targetModel,
        string animationName)
    {
        ArgumentNullException.ThrowIfNull(
            targetModel);

        string name =
            NormalizeName(
                animationName);

        ModelOwnedAnimationLibrary library =
            LoadLibrary(
                projectRoot,
                targetModel.Guid);

        int index =
            library.Animations.FindIndex(
                entry =>
                    entry.Animation !=
                        null &&
                    string.Equals(
                        entry.Animation.Name,
                        name,
                        StringComparison.OrdinalIgnoreCase));

        if (index <
            0)
        {
            return false;
        }

        ModelOwnedAnimationEntry removed =
            library.Animations[index];

        library.Animations.RemoveAt(
            index);

        string path =
            GetLibraryPath(
                projectRoot,
                targetModel.Guid);

        if (library.Animations.Count ==
            0)
        {
            if (File.Exists(
                    path))
            {
                File.Delete(
                    path);
            }

            LibraryCache.Remove(
                path);
        }
        else
        {
            JsonSerialization.WriteAtomic(
                path,
                library);

            CacheLibrary(
                path,
                library);
        }

        targetModel.RemoveRuntimeAnimation(
            removed.Animation.Key);

        return true;
    }

    internal static int BakedAnimationCount(
        string projectRoot,
        Guid targetModelGuid)
    {
        return LoadLibrary(
                projectRoot,
                targetModelGuid)
            .Animations
            .Count;
    }

    private static ModelOwnedAnimationLibrary LoadLibrary(
        string projectRoot,
        Guid targetModelGuid)
    {
        string path =
            GetLibraryPath(
                projectRoot,
                targetModelGuid);

        if (!File.Exists(
                path))
        {
            LibraryCache.Remove(
                path);

            return NewLibrary(
                targetModelGuid);
        }

        FileInfo file =
            new(path);

        if (LibraryCache.TryGetValue(
                path,
                out CachedLibrary? cached) &&
            cached.Length ==
                file.Length &&
            cached.LastWriteTimeUtc ==
                file.LastWriteTimeUtc)
        {
            return cached.Library;
        }

        try
        {
            ModelOwnedAnimationLibrary? library =
                JsonSerializer.Deserialize<ModelOwnedAnimationLibrary>(
                    File.ReadAllText(
                        path),
                    JsonSerialization.Options);

            if (library ==
                    null ||
                library.TargetModelGuid !=
                    targetModelGuid)
            {
                return NewLibrary(
                    targetModelGuid);
            }

            library.Animations ??=
                new List<ModelOwnedAnimationEntry>();

            foreach (ModelOwnedAnimationEntry entry
                     in library.Animations)
            {
                entry.Provenance ??=
                    new ModelOwnedAnimationProvenance();
            }

            LibraryCache[path] =
                new CachedLibrary(
                    file.Length,
                    file.LastWriteTimeUtc,
                    library);

            return library;
        }
        catch (Exception exception)
        {
            LibraryCache.Remove(
                path);

            throw new InvalidDataException(
                $"Model-owned animation library for '{targetModelGuid}' is corrupt. ByteEngine will not overwrite it until it is repaired or removed.",
                exception);
        }
    }

    private static void CacheLibrary(
        string path,
        ModelOwnedAnimationLibrary library)
    {
        FileInfo file =
            new(path);

        LibraryCache[path] =
            new CachedLibrary(
                file.Exists
                    ? file.Length
                    : 0L,
                file.Exists
                    ? file.LastWriteTimeUtc
                    : DateTime.MinValue,
                library);
    }

    private static ModelOwnedAnimationLibrary LoadLibraryForMerge(
        string projectRoot,
        Guid targetModelGuid)
    {
        try
        {
            return LoadLibrary(
                projectRoot,
                targetModelGuid);
        }
        catch
        {
            /*
             * Corrupt generated animation data must never prevent the source
             * FBX/GLTF itself from loading. Editor Bake remains strict so it
             * cannot silently overwrite the broken library.
             */
            return NewLibrary(
                targetModelGuid);
        }
    }

    private static ModelOwnedAnimationLibrary NewLibrary(
        Guid targetModelGuid)
    {
        return
            new ModelOwnedAnimationLibrary
            {
                Version =
                    CurrentVersion,

                TargetModelGuid =
                    targetModelGuid
            };
    }

    private static string NormalizeName(
        string animationName)
    {
        if (string.IsNullOrWhiteSpace(
                animationName))
        {
            throw new ArgumentException(
                "Animation name cannot be empty.",
                nameof(animationName));
        }

        return animationName.Trim();
    }

    private sealed record CachedLibrary(
        long Length,
        DateTime LastWriteTimeUtc,
        ModelOwnedAnimationLibrary Library);

    private sealed class ModelOwnedAnimationLibrary
    {
        public ModelOwnedAnimationLibrary()
        {
        }

        public int Version { get; set; } =
            CurrentVersion;

        public Guid TargetModelGuid { get; set; }

        public List<ModelOwnedAnimationEntry> Animations { get; set; } =
            new();
    }

    private sealed class ModelOwnedAnimationEntry
    {
        public ModelOwnedAnimationEntry()
        {
        }

        public ImportedAnimation Animation { get; set; } =
            new();

        public ModelOwnedAnimationProvenance Provenance { get; set; } =
            new();
    }
}
