namespace ByteEngine.Core.Graphics.ThreeD;
/// <summary>Depth-order opaque geometry before batching; retain transparent back-to-front order.</summary>
internal static class PortableRenderOrdering
{
    internal static bool IsOpaque(RenderSubmission item)=>item.Queue==RenderQueue3D.Opaque&&!item.Material.WaterSurface;
    private static int Queue(RenderSubmission item)=>item.Material.WaterSurface?3500:(int)item.Queue;
    internal static int Compare(RenderSubmission a,RenderSubmission b)
    {
        int queue=Queue(a).CompareTo(Queue(b));if(queue!=0)return queue;
        float left=a.Queue==RenderQueue3D.Transparent?-a.DistanceSquaredToCamera:IsOpaque(a)?a.DistanceSquaredToCamera:a.SubmissionIndex;
        float right=b.Queue==RenderQueue3D.Transparent?-b.DistanceSquaredToCamera:IsOpaque(b)?b.DistanceSquaredToCamera:b.SubmissionIndex;
        int order=left.CompareTo(right);return order!=0?order:a.SubmissionIndex.CompareTo(b.SubmissionIndex);
    }
    internal static void Complete(List<RenderSubmission> opaque,List<RenderSubmission> other)
    {
        opaque.AddRange(other);
        opaque.Sort(Compare);
    }
}
