using System.Numerics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Navigation;
/// <summary>Authored traversal connection included when its region is baked. Endpoints must lie on walkable cells.</summary>
public sealed class NavigationLink3D : Component
{
 public Vector3 EndOffset {get;set;}=new(2,0,0);
 public float Cost {get;set;}=2;
 public bool Bidirectional {get;set;}=true;
 public Vector3 Start=>Transform.WorldPosition;
 public Vector3 End=>Vector3.Transform(EndOffset,Transform.WorldMatrix);
}
