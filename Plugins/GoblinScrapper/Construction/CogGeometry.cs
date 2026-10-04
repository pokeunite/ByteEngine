using System.Numerics;
namespace GoblinScrapper.Construction;
public static class CogGeometry
{
 public static bool IsCog(AssemblyPartDefinition d)=>d.ReferenceId is 38 or 39 or 51;
 public static float PitchRadius(AssemblyPartDefinition d)=>d.ReferenceId==51?1.084f:.542f;
 public static int Teeth(AssemblyPartDefinition d)=>d.ReferenceId==51?24:12;
 public static Vector3 Centre(AssemblyPartDefinition d,Vector3 p,Quaternion q)=>p+Vector3.Transform(((d.MovingCollision??d.MovingBounds!).Min+(d.MovingCollision??d.MovingBounds!).Max)*.5f,q);
 public static bool Meshes(AssemblyPartDefinition a,Vector3 pa,Quaternion qa,AssemblyPartDefinition b,Vector3 pb,Quaternion qb)
 {
  if(!IsCog(a)||!IsCog(b))return false;var axis=Vector3.Transform(a.Axis,qa);var other=Vector3.Transform(b.Axis,qb);
  var delta=Centre(b,pb,qb)-Centre(a,pa,qa);float axial=Vector3.Dot(delta,axis);
  return Vector3.Dot(axis,other)>.995f&&Math.Abs(axial)<.04f&&Math.Abs((delta-axis*axial).Length()-PitchRadius(a)-PitchRadius(b))<.065f;
 }
 public static Dictionary<int,float> Phases(VehicleAssembly assembly)
 {
  var parts=assembly.Parts.Values.Where(p=>IsCog(assembly.Catalog[p.File])).OrderBy(p=>assembly.Catalog[p.File].ReferenceId==39?0:1).ThenBy(p=>p.Id).ToArray();var angles=new Dictionary<int,float>();
  foreach(var seed in parts){if(angles.ContainsKey(seed.Id))continue;angles[seed.Id]=0;bool changed;
   do{changed=false;foreach(var a in parts.Where(p=>angles.ContainsKey(p.Id)).ToArray())foreach(var b in parts.Where(p=>!angles.ContainsKey(p.Id)).ToArray()){
    var da=assembly.Catalog[a.File];var db=assembly.Catalog[b.File];if(!Meshes(da,a.Position,a.Rotation,db,b.Position,b.Rotation))continue;
    var delta=Centre(db,b.Position,b.Rotation)-Centre(da,a.Position,a.Rotation);var va=Vector3.Transform(delta,Quaternion.Inverse(a.Rotation));var vb=Vector3.Transform(-delta,Quaternion.Inverse(b.Rotation));
    float fa=MathF.Atan2(va.Y,va.X),fb=MathF.Atan2(vb.Y,vb.X);angles[b.Id]=(Teeth(da)*(fa+angles[a.Id])+Teeth(db)*fb-MathF.PI)/Teeth(db);changed=true;
   }}while(changed);
  }return angles;
 }
 public static Matrix4x4 PhaseTransform(AssemblyPartDefinition d,float angle)=>Matrix4x4.CreateTranslation(-d.Pivot)*Matrix4x4.CreateFromAxisAngle(d.Axis,angle)*Matrix4x4.CreateTranslation(d.Pivot);
}
