using System.Numerics;

namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>Exact inputs of one owned depth target; never substitutes hashes for equality.</summary>
internal sealed class ShadowDepthCache
{
    internal readonly record struct Key(Matrix4x4 Projection, Vector3 Position, float Range, int Resolution, int Context);
    private readonly record struct Caster(Mesh Mesh, int Version, Matrix4x4 Model,
        Texture2D? Mask, int MaskVersion, float Cutoff)
    {
        internal static Caster From(RenderSubmission item)
        {
            var mask = item.Material.BlendMode == BlendMode3D.Cutout ? item.Material.MainTexture : null;
            return new(item.Mesh, item.Mesh.GeometryVersion, item.ModelMatrix, mask,
                mask?.ContentVersion ?? 0, mask == null ? 0 : Math.Clamp(item.Material.AlphaCutoff, 0, 1));
        }
    }
    private readonly List<Caster> _casters = new();
    private Key _key;
    private bool _valid;
    internal string Status { get; private set; } = "Empty";

    internal bool Matches(Key key, IReadOnlyList<RenderSubmission> casters)
    {
        if (!_valid) return false;
        if (_key != key) { Status = "Projection/light changed"; return false; }
        if (_casters.Count != casters.Count) { Status = "Caster count changed"; return false; }
        for (int i = 0; i < casters.Count; i++)
            if (casters[i].Mesh.IsDynamic || _casters[i] != Caster.From(casters[i]))
            { Status = "Caster changed"; return false; }
        Status = "Reused";
        return true;
    }

    internal void Invalidate(string? reason = null)
    {
        _valid = false;
        if (reason != null) Status = reason;
    }

    internal void MarkDynamic()
    {
        _valid = false;
        _casters.Clear();
        Status = "Dynamic caster";
    }

    internal void Capture(Key key, IReadOnlyList<RenderSubmission> casters)
    {
        _valid = false;
        _casters.Clear();
        foreach (var item in casters)
        {
            // Dynamic/skinned geometry always renders, even if a caller missed a version update.
            if (item.Mesh.IsDynamic) { _casters.Clear(); Status = "Dynamic caster"; return; }
            _casters.Add(Caster.From(item));
        }
        _key = key;
        _valid = true;
    }
}
