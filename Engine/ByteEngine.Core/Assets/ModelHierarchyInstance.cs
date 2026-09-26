using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Assets;

/// <summary>
/// Marks the root of one imported model hierarchy.
///
/// MeshRenderer references identify model sub-assets, but they cannot
/// distinguish two instances of the same model beneath one parent. This
/// marker keeps hierarchy-level operations scoped to the correct instance.
///
/// Per-instance skinned-mesh visibility also lives here. The editor exposes
/// that data as the user-facing "Mesh Parts" list while SkeletalMeshRenderer
/// remains runtime plumbing created by AnimationController when needed.
/// </summary>
public sealed class ModelHierarchyInstance : Component
{
    private readonly List<string> _hiddenMeshKeys = new();

    public AssetReference Model { get; set; } = AssetReference.Empty;

    public float AppliedImportScale { get; set; } = 1.0f;
    public bool AutoGrounded { get; set; }

    /// <summary>
    /// Stable imported mesh keys that should not be rendered for this model
    /// instance. Empty means every skinned mesh is visible.
    ///
    /// Keys, rather than display names, are persisted so duplicate mesh names
    /// and importer-friendly labels cannot make visibility ambiguous.
    /// </summary>
    public IReadOnlyList<string> HiddenMeshKeys =>
        _hiddenMeshKeys;

    public bool IsMeshVisible(string meshKey)
    {
        if (string.IsNullOrWhiteSpace(meshKey))
        {
            return true;
        }

        return !_hiddenMeshKeys.Any(
            key =>
                string.Equals(
                    key,
                    meshKey,
                    StringComparison.Ordinal));
    }

    public void SetMeshVisible(
        string meshKey,
        bool visible)
    {
        if (string.IsNullOrWhiteSpace(meshKey))
        {
            return;
        }

        string normalized =
            meshKey.Trim();

        bool wasHidden =
            _hiddenMeshKeys.RemoveAll(
                key =>
                    string.Equals(
                        key,
                        normalized,
                        StringComparison.Ordinal)) >
            0;

        if (!visible)
        {
            _hiddenMeshKeys.Add(
                normalized);
        }

        bool isHidden =
            !visible;

        if (wasHidden != isHidden)
        {
            SynchronizeSkeletalRenderer();
        }
    }

    public void SetHiddenMeshKeys(
        IEnumerable<string>? meshKeys)
    {
        string[] normalized =
            meshKeys?
                .Where(key => !string.IsNullOrWhiteSpace(key))
                .Select(key => key.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToArray()
            ?? Array.Empty<string>();

        bool changed =
            _hiddenMeshKeys.Count != normalized.Length ||
            normalized.Any(
                key =>
                    !_hiddenMeshKeys.Contains(
                        key,
                        StringComparer.Ordinal));

        if (!changed)
        {
            SynchronizeSkeletalRenderer();
            return;
        }

        _hiddenMeshKeys.Clear();
        _hiddenMeshKeys.AddRange(
            normalized);

        SynchronizeSkeletalRenderer();
    }

    /// <summary>
    /// AnimationController creates the skeletal renderer dynamically. Sync in
    /// LateUpdate so the renderer can remain invisible to the authoring model
    /// while still receiving the persisted instance visibility before render.
    /// </summary>
    protected override void OnLateUpdate()
    {
        SynchronizeSkeletalRenderer();
    }

    private void SynchronizeSkeletalRenderer()
    {
        GameObject? gameObject =
            AttachedGameObject;

        if (gameObject == null)
        {
            return;
        }

        SkeletalMeshRenderer? renderer =
            gameObject.GetComponent<SkeletalMeshRenderer>();

        if (renderer == null)
        {
            return;
        }

        /*
         * This call is intentionally cheap when nothing changed. The renderer
         * compares the desired keys and current index counts before touching
         * GPU data. Reapplying each LateUpdate also covers a renderer whose
         * runtime meshes were rebuilt without requiring ModelHierarchyInstance
         * to know about renderer-internal resource generations.
         */
        renderer.SetHiddenMeshKeys(
            _hiddenMeshKeys);
    }
}
