namespace ByteEngine.Core.Graphics.ThreeD;
/// <summary>Opaque shared mesh/material batches. Transparent/overlay order and independent shadow participation remain intact.</summary>
public static class RenderBatcher
{
    public sealed record Batch(RenderSubmission First, IReadOnlyList<RenderSubmission> Instances);
    public static IReadOnlyList<Batch> Build(IReadOnlyList<RenderSubmission> submissions)
    {
        var groups=new Dictionary<(Mesh,Material,bool),List<RenderSubmission>>();
        foreach(var item in submissions)
            if(item.Queue==RenderQueue3D.Opaque && item.Material.ResolveDepthWrite())
            {var key=(item.Mesh,item.Material,item.ReceiveShadows);if(!groups.TryGetValue(key,out var list))groups[key]=list=new();list.Add(item);}
        var emitted=new HashSet<(Mesh,Material,bool)>();var result=new List<Batch>();
        foreach(var item in submissions)
        {
            var key=(item.Mesh,item.Material,item.ReceiveShadows);
            if(item.Queue==RenderQueue3D.Opaque && item.Material.ResolveDepthWrite() && groups.TryGetValue(key,out var list))
            {if(emitted.Add(key))result.Add(new(item,list));}
            else result.Add(new(item,[item]));
        }
        return result;
    }
}
