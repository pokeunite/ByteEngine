namespace ByteEngine.Core.Graphics.ThreeD;
/// <summary>Core submits opaque items in stable submission order. Preserve that
/// order without a large-struct sort; only the transparent/custom tail needs sorting.</summary>
internal static class PortableRenderOrdering
{
    internal static bool IsOpaque(RenderSubmission item)=>item.Queue==RenderQueue3D.Opaque&&!item.Material.WaterSurface;
    private static int Queue(RenderSubmission item)=>item.Material.WaterSurface?3500:(int)item.Queue;
    internal static int Compare(RenderSubmission a,RenderSubmission b)
    {
        int queue=Queue(a).CompareTo(Queue(b));if(queue!=0)return queue;
        float left=a.Queue==RenderQueue3D.Transparent?-a.DistanceSquaredToCamera:a.SubmissionIndex;
        float right=b.Queue==RenderQueue3D.Transparent?-b.DistanceSquaredToCamera:b.SubmissionIndex;
        int order=left.CompareTo(right);return order!=0?order:a.SubmissionIndex.CompareTo(b.SubmissionIndex);
    }
    internal static void Complete(List<RenderSubmission> opaque,List<RenderSubmission> other)
    {
        bool fallback=false;
        for(int i=1;i<opaque.Count;i++)if(opaque[i-1].SubmissionIndex>opaque[i].SubmissionIndex){fallback=true;break;}
        foreach(var item in other)if(Queue(item)<(int)RenderQueue3D.Opaque){fallback=true;break;}
        if(!fallback&&other.Count>1)other.Sort(Compare);
        opaque.AddRange(other);
        if(fallback)opaque.Sort(Compare);
    }
}
