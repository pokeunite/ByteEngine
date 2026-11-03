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
        private sealed class MeshGroup {internal readonly List<List<RenderSubmission>> Variants=new();internal bool Indexed;}
        private readonly Dictionary<Mesh,MeshGroup> _meshGroups=new(ReferenceEqualityComparer.Instance);
        private readonly List<MeshGroup> _meshGroupPool=new();
        private int _meshGroupsUsed;
        private readonly Dictionary<(Mesh,Texture2D?,float),List<RenderSubmission>> _shadowGroups=new();
        private readonly List<List<RenderSubmission>> _pool=new();
        private readonly List<Batch> _result=new();
        private int _used;
        internal Builder(){_groups=new(_comparer);}
        private List<RenderSubmission> Rent()
        {
            if(_used==_pool.Count)_pool.Add(new());
            return _pool[_used++];
        }
        private static bool CanBatch(RenderSubmission item)=>item.Queue==RenderQueue3D.Opaque && item.Material.DepthTest && item.Material.ResolveDepthWrite();
        internal IReadOnlyList<Batch> Build(IReadOnlyList<RenderSubmission> submissions,bool shadows=false)
        {
            foreach(var list in _pool)list.Clear();
            _used=0;_meshGroupsUsed=0;_meshGroups.Clear();_groups.Clear();_shadowGroups.Clear();_result.Clear();_comparer.Reset();
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
            {
                var key=(item.Mesh,item.Material,item.ReceiveShadows);
                if(CanBatch(item))
                {
                    if(!_meshGroups.TryGetValue(item.Mesh,out var variants))
                    {
                        if(_meshGroupsUsed==_meshGroupPool.Count)_meshGroupPool.Add(new());
                        variants=_meshGroupPool[_meshGroupsUsed++];variants.Variants.Clear();variants.Indexed=false;_meshGroups.Add(item.Mesh,variants);
                    }
                    List<RenderSubmission>? group=null;
                    if(variants.Indexed)_groups.TryGetValue(key,out group);
                    else for(int i=0;i<variants.Variants.Count;i++)
                    {
                        var candidate=variants.Variants[i];var first=candidate[0];
                        if(first.ReceiveShadows==item.ReceiveShadows&&MaterialRenderStateComparer.Instance.Equals(first.Material,item.Material)){group=candidate;break;}
                    }
                    if(group==null)
                    {
                        group=Rent();variants.Variants.Add(group);_result.Add(new(item,group));
                        if(variants.Indexed)_groups.Add(key,group);
                        else if(variants.Variants.Count>8)
                        {
                            // A mesh with many material variants switches to hashing;
                            // ordinary one-material scenery avoids hashing every shader field.
                            variants.Indexed=true;
                            for(int i=0;i<variants.Variants.Count-1;i++){var entry=variants.Variants[i];var first=entry[0];_groups.Add((first.Mesh,first.Material,first.ReceiveShadows),entry);}
                            _groups.Add(key,group);
                        }
                    }
                    group.Add(item);
                }
                else {var list=Rent();list.Add(item);_result.Add(new(item,list));}
            }
            return _result;
        }
    }
}
