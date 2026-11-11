using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 public void RenderIndependentRefinery(RenderContext c,MiningAuxiliaryRefinery3D local)
 {
  float scale=System.Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f);
  RenderFieldMachine(c,c.TargetWidth/scale,c.TargetHeight/scale,scale,local);
 }
}
