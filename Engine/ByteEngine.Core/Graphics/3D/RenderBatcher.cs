namespace ByteEngine.Core.Graphics.ThreeD;
/// <summary>Opaque shared mesh/material batches. Transparent/overlay order and independent shadow participation remain intact.</summary>
public static class RenderBatcher
{
    private sealed class BatchKeyComparer : IEqualityComparer<(Mesh Mesh,Material Material,bool Shadows)>
    {
        private readonly Dictionary<Material,int> _hashes=new(ReferenceEqualityComparer.Instance);
        internal void Reset()=>_hashes.Clear();
        public bool Equals((Mesh Mesh,Material Material,bool Shadows) x,(Mesh Mesh,Material Material,bool Shadows) y) =>
            ReferenceEquals(x.Mesh,y.Mesh) && x.Shadows==y.Shadows && MaterialRenderStateComparer.Instance.Equals(x.Material,y.Material);
        public int GetHashCode((Mesh Mesh,Material Material,bool Shadows) value) {
            if(!_hashes.TryGetValue(value.Material,out int hash))_hashes[value.Material]=hash=MaterialRenderStateComparer.Instance.GetHashCode(value.Material);
            return HashCode.Combine(System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(value.Mesh),hash,value.Shadows);
        }
    }
    public sealed record Batch(RenderSubmission First, IReadOnlyList<RenderSubmission> Instances);
    public static IReadOnlyList<Batch> Build(IReadOnlyList<RenderSubmission> submissions) => new Builder().Build(submissions);
    internal static IReadOnlyList<Batch> BuildShadows(IReadOnlyList<RenderSubmission> submissions) => new Builder().Build(submissions, shadows:true);

    /// <summary>Pass-owned storage. Results remain valid until the next Build on this builder.</summary>
    internal sealed class Builder
    {
        private readonly BatchKeyComparer _comparer=new();
        private readonly Dictionary<(Mesh,Material,bool),List<RenderSubmission>> _groups;
        private readonly Dictionary<(Mesh,Texture2D?,float),List<RenderSubmission>> _shadowGroups=new();
        private readonly HashSet<(Mesh,Material,bool)> _emitted;
        private readonly List<List<RenderSubmission>> _pool=new();
        private readonly List<Batch> _result=new();
        private int _used;
        internal Builder(){_groups=new(_comparer);_emitted=new(_comparer);}
        private List<RenderSubmission> Rent()
        {
            if(_used==_pool.Count)_pool.Add(new());
            return _pool[_used++];
        }
        private static bool CanBatch(RenderSubmission item)=>item.Queue==RenderQueue3D.Opaque && item.Material.DepthTest && item.Material.ResolveDepthWrite();
        internal IReadOnlyList<Batch> Build(IReadOnlyList<RenderSubmission> submissions,bool shadows=false)
        {
            foreach(var list in _pool)list.Clear();
            _used=0;_groups.Clear();_shadowGroups.Clear();_emitted.Clear();_result.Clear();_comparer.Reset();
            if(shadows)
            {
                foreach(var item in submissions)
                {
                    bool cutout=item.Material.BlendMode==BlendMode3D.Cutout && item.Material.MainTexture!=null;
                    var key=(item.Mesh,cutout?item.Material.MainTexture:null,cutout?Math.Clamp(item.Material.AlphaCutoff,0,1):0);
                    if(!_shadowGroups.TryGetValue(key,out var list))
                    {list=Rent();_shadowGroups.Add(key,list);_result.Add(new(item,list));}
                    list.Add(item);
                }
                return _result;
            }
            foreach(var item in submissions)
                if(CanBatch(item))
                {
                    var key=(item.Mesh,item.Material,item.ReceiveShadows);
                    if(!_groups.TryGetValue(key,out var list)){list=Rent();_groups.Add(key,list);}
                    list.Add(item);
                }
            foreach(var item in submissions)
            {
                var key=(item.Mesh,item.Material,item.ReceiveShadows);
                if(CanBatch(item))
                {if(_emitted.Add(key))_result.Add(new(item,_groups[key]));}
                else {var list=Rent();list.Add(item);_result.Add(new(item,list));}
            }
            return _result;
        }
    }
}
