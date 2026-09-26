namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>
/// Per-imported-mesh visibility for skeletal models.
///
/// Visibility changes only the drawable index data. Pose evaluation, CPU
/// skinning, root motion, bounds and sockets continue to consume the complete
/// skeleton and complete vertex set, so hiding FPS body parts cannot change
/// character physics or attachment transforms.
/// </summary>
public sealed partial class SkeletalMeshRenderer
{
    private readonly HashSet<string> _hiddenMeshKeys =
        new(StringComparer.Ordinal);

    public IReadOnlyCollection<string> HiddenMeshKeys =>
        _hiddenMeshKeys;

    public bool IsMeshVisible(string meshKey)
    {
        return string.IsNullOrWhiteSpace(meshKey) ||
               !_hiddenMeshKeys.Contains(meshKey);
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

        if (visible)
        {
            _hiddenMeshKeys.Remove(
                normalized);
        }
        else
        {
            _hiddenMeshKeys.Add(
                normalized);
        }

        ApplyMeshVisibility();
    }

    /// <summary>
    /// Fast path used by ModelHierarchyInstance/editor preview. Its list is
    /// already normalized and stable, so no per-frame temporary collection is
    /// needed while an editor preview is being refreshed.
    /// </summary>
    public void SetHiddenMeshKeys(
        IReadOnlyList<string> meshKeys)
    {
        bool changed =
            _hiddenMeshKeys.Count != meshKeys.Count;

        if (!changed)
        {
            for (int index = 0;
                 index < meshKeys.Count;
                 index++)
            {
                string key =
                    meshKeys[index];

                if (!_hiddenMeshKeys.Contains(key))
                {
                    changed =
                        true;
                    break;
                }
            }
        }

        if (changed)
        {
            _hiddenMeshKeys.Clear();

            for (int index = 0;
                 index < meshKeys.Count;
                 index++)
            {
                string key =
                    meshKeys[index];

                if (!string.IsNullOrWhiteSpace(key))
                {
                    _hiddenMeshKeys.Add(
                        key);
                }
            }
        }

        /*
         * Apply even when the key set did not change. Runtime meshes can have
         * been rebuilt since the last call (model resolve/reload), and their
         * fresh index buffers still need the persisted visibility state.
         */
        ApplyMeshVisibility();
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

        SetHiddenMeshKeys(
            (IReadOnlyList<string>)normalized);
    }

    private void ApplyMeshVisibility()
    {
        foreach (RuntimeSkinnedMesh runtime in _runtimeMeshes)
        {
            bool hidden =
                _hiddenMeshKeys.Contains(
                    runtime.Source.Key);

            int desiredIndexCount =
                hidden
                    ? 0
                    : runtime.Source.Indices.Length;

            if (runtime.Mesh.IndexCount ==
                desiredIndexCount)
            {
                continue;
            }

            runtime.Mesh.ReplaceData(
                runtime.DeformedVertices,
                hidden
                    ? Array.Empty<uint>()
                    : runtime.Source.Indices);
        }
    }
}
