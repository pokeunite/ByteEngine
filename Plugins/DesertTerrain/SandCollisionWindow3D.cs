using System.Numerics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics.ThreeD;
namespace DesertTerrain;
// Collision follows the vehicle; deformation remains persistent across the entire map.
public sealed class SandCollisionWindow3D:HeightfieldCollider3D
{
 internal InteractiveSand3D Source=null!;Vector2 _minimum;bool _focused;
 public override int Columns=>129;public override int Rows=>129;public override float CellSpacing=>.125f;
 public override Vector2 GridMinimum=>_minimum;
 public override Vector3 Size{get=>new(16,1,16);set{}}
 public override BoundingBox3D LocalTerrainBounds=>new(new(_minimum.X,-.4f,_minimum.Y),new(_minimum.X+16,.2f,_minimum.Y+16));
 public override float HeightAt(int x,int z){int ox=(int)MathF.Round((_minimum.X-Source.GridMinimum.X)/CellSpacing),oz=(int)MathF.Round((_minimum.Y-Source.GridMinimum.Y)/CellSpacing);return Source.HeightAt(ox+x,oz+z);}
 internal void Focus(Vector3 world){Matrix4x4.Invert(Source.SurfaceMatrix,out var inverse);var p=Vector3.Transform(world,inverse);
  if(_focused&&p.X>=_minimum.X+4&&p.X<=_minimum.X+12&&p.Z>=_minimum.Y+4&&p.Z<=_minimum.Y+12)return;
  float low=Source.GridMinimum.X,high=low+Source.SurfaceWidth-16;
  _minimum=new(Math.Clamp(MathF.Floor((p.X-8)/2)*2,low,high),Math.Clamp(MathF.Floor((p.Z-8)/2)*2,low,high));_focused=true;
 }
}
