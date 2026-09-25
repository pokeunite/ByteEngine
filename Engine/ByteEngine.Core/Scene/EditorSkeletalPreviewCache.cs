using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Assets.Importers;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Core.Scene;

/// <summary>
/// Keeps the editor's skinned model preview separate from serializable scene data.
/// The proxy uses the same SkeletalMeshRenderer and imported pose as Play mode.
/// </summary>
internal sealed class EditorSkeletalPreviewCache : IDisposable
{
    private sealed class Preview
    {
        public required GameObject Source { get; init; }
        public required ModelAsset Model { get; init; }
        public required GameObject Proxy { get; init; }
        public required SkeletalMeshRenderer Renderer { get; init; }
        public required HashSet<string> SkinnedMeshKeys { get; init; }
    }

    private readonly Dictionary<Guid, Preview> _previews = new();
    private readonly List<(MeshRenderer Renderer, bool Enabled)> _hiddenMeshes = new();
    private readonly HashSet<MeshRenderer> _hiddenSet = new();
    private Scene? _scene;

    internal void Prepare(Scene scene)
    {
        RestoreStaticMeshes();
        if (!ReferenceEquals(_scene, scene))
        {
            Clear();
            _scene = scene;
        }
        if (!AnimationRuntimeAssets.TryGet(out AssetManager? assets) || assets == null)
            return;

        var activeIds = new HashSet<Guid>();
        foreach (GameObject item in scene.GameObjects)
        {
            ModelHierarchyInstance? instance = item.GetComponent<ModelHierarchyInstance>();
            if (instance == null || instance.Model.IsEmpty || !item.ActiveInHierarchy)
                continue;

            ModelAsset model;
            try { model = assets.LoadModel(instance.Model); }
            catch { continue; }

            if (model.Skeleton == null) continue;
            HashSet<string> keys = model.Meshes
                .Where(mesh => mesh.JointIndices.Length > 0 &&
                    mesh.JointWeights.Length == mesh.JointIndices.Length)
                .Select(mesh => mesh.Key)
                .ToHashSet(StringComparer.Ordinal);
            if (keys.Count == 0) continue;

            activeIds.Add(item.Id);
            SkeletalMeshRenderer? authored = item.GetComponent<SkeletalMeshRenderer>();
            if (authored != null)
            {
                Remove(item.Id);
                if (authored.Enabled && authored.Visible)
                    HideBindPoseMeshes(item, instance.Model, keys);
                continue;
            }

            if (!_previews.TryGetValue(item.Id, out Preview? preview) ||
                !ReferenceEquals(preview.Source, item) ||
                !ReferenceEquals(preview.Model, model))
            {
                Remove(item.Id);
                GameObject proxy = new("_EditorSkeletalPreview");
                var renderer = proxy.AddComponent(new SkeletalMeshRenderer
                {
                    Model = instance.Model,
                    SkeletonKey = model.Skeleton.Key,
                    PlayOnStart = false,
                    TransitionDuration = 0f
                });
                if (!renderer.ResolveRuntimeResources())
                {
                    proxy.RemoveComponent(renderer);
                    continue;
                }

                string preferredIdle = FindIdleClip(item, assets);
                ImportedAnimation? clip = model.Animations.FirstOrDefault(animation =>
                    animation.Name.Equals(preferredIdle, StringComparison.OrdinalIgnoreCase));
                if (clip != null)
                {
                    try
                    {
                        if (renderer.Play(clip.Name, true, 0f))
                        {
                            renderer.Seek(0f);
                            renderer.Pause();
                        }
                    }
                    catch
                    {
                        // A bad preview clip must not hide the imported model.
                        proxy.RemoveComponent(renderer);
                        continue;
                    }
                }
                preview = new Preview
                {
                    Source = item,
                    Model = model,
                    Proxy = proxy,
                    Renderer = renderer,
                    SkinnedMeshKeys = keys
                };
                _previews[item.Id] = preview;
            }

            preview.Proxy.Transform.WorldPosition = item.Transform.WorldPosition;
            preview.Proxy.Transform.WorldRotation = item.Transform.WorldRotation;
            preview.Proxy.Transform.WorldScale = item.Transform.WorldScale;
            HideBindPoseMeshes(item, instance.Model, preview.SkinnedMeshKeys);
        }

        foreach (Guid stale in _previews.Keys.Where(id => !activeIds.Contains(id)).ToArray())
            Remove(stale);
    }

    internal bool TryGetWorldBounds(GameObject source, out BoundingBox3D bounds)
    {
        SkeletalMeshRenderer? renderer = source.GetComponent<SkeletalMeshRenderer>();
        if (renderer == null && _previews.TryGetValue(source.Id, out Preview? preview) &&
            ReferenceEquals(preview.Source, source))
            renderer = preview.Renderer;

        if (renderer != null && renderer.TryGetCurrentModelBounds(out BoundingBox3D local))
        {
            bounds = local.Transform(source.Transform.WorldMatrix);
            return bounds.IsValid;
        }

        bounds = BoundingBox3D.Empty;
        return false;
    }

    internal void Render(RenderContext context)
    {
        foreach (Preview preview in _previews.Values)
            if (preview.Source.ActiveInHierarchy)
                preview.Proxy.RenderEditorInternal(context);
    }

    internal void RestoreStaticMeshes()
    {
        foreach ((MeshRenderer renderer, bool enabled) in _hiddenMeshes)
            renderer.Enabled = enabled;
        _hiddenMeshes.Clear();
        _hiddenSet.Clear();
    }

    private void HideBindPoseMeshes(GameObject root, AssetReference model, HashSet<string> keys)
    {
        void Visit(GameObject item)
        {
            foreach (MeshRenderer renderer in item.Components.OfType<MeshRenderer>())
            {
                ModelMeshReference? reference = renderer.MeshReference;
                if (reference == null || !keys.Contains(reference.SubAssetKey) ||
                    !SameAsset(reference.Model, model))
                    continue;
                if (!_hiddenSet.Add(renderer)) continue;
                _hiddenMeshes.Add((renderer, renderer.Enabled));
                renderer.Enabled = false;
            }
            foreach (GameObject child in item.Children) Visit(child);
        }
        Visit(root);
    }

    private static string FindIdleClip(GameObject modelRoot, AssetManager assets)
    {
        for (GameObject? item = modelRoot; item != null; item = item.Parent)
            if (item.GetComponent<AnimationController>() is { } controller)
            {
                if (!controller.AnimationProfile.IsEmpty)
                {
                    try { return assets.LoadAnimationProfile(controller.AnimationProfile).Locomotion.Idle; }
                    catch { /* The unlinked controller value remains usable. */ }
                }
                return controller.Idle;
            }
        return "Idle";
    }

    private static bool SameAsset(AssetReference left, AssetReference right) =>
        left.Guid != Guid.Empty && right.Guid != Guid.Empty
            ? left.Guid == right.Guid
            : string.Equals(left.CachedProjectPath, right.CachedProjectPath,
                StringComparison.OrdinalIgnoreCase);

    private void Remove(Guid id)
    {
        if (!_previews.Remove(id, out Preview? preview)) return;
        preview.Proxy.RemoveComponent(preview.Renderer);
    }

    private void Clear()
    {
        RestoreStaticMeshes();
        foreach (Guid id in _previews.Keys.ToArray()) Remove(id);
        _scene = null;
    }

    public void Dispose() => Clear();
}
