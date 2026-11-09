namespace ByteEngine.Core.Graphics.ThreeD;
/// <summary>Shared camera rejection for all backends. Shadow casters use their own light frusta.</summary>
internal static class RenderVisibility {
 internal static int Collect(IReadOnlyList<RenderSubmission> source,RenderView3D view,List<RenderSubmission> visible){
  visible.Clear();
  for(int i=0;i<source.Count;i++){var item=source[i];if(!item.FrustumCullingEnabled||view.Frustum.Intersects(item.WorldBounds))visible.Add(item);}
  return source.Count-visible.Count;
 }
}
