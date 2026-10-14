using System.Numerics;
namespace DuneCompany;
public static class GearboxMounts
{
 public static bool IsEngine(int type)=>type is 23 or 24 or 34 or 38;
 public static bool RearFace(int type,Vector3 normal)=>type is 34 or 38?normal.Z<-.5f:normal.Z>.5f;
 public static Quaternion Rotation(PlacedBlock engine)=>Quaternion.Normalize(engine.Q*(engine.Type is 23 or 24?Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI):Quaternion.Identity));
 public static Vector3 Position(PlacedBlock engine,DunePart part)
 {
  // Authored casting sockets, rather than exhaust-pipe or mounting-base bounds.
  var socket=engine.Type==38?new Vector3(0,.513f,-.4085f):engine.Type==34?new Vector3(0,.486f,-.387f):new Vector3((part.Low.X+part.High.X)*.5f,.30f,part.High.Z-.10f);
  return engine.P+Vector3.Transform(socket,engine.Q)-Vector3.Transform(new Vector3(0,.20f,.35f),Rotation(engine));
 }
 public static bool Connected(PlacedBlock gearbox,IReadOnlyList<PlacedBlock> blocks,IReadOnlyDictionary<int,DunePart> catalog)
 {
  var engine=blocks.FirstOrDefault(b=>b.Id==gearbox.Parent);
  return engine!=null&&IsEngine(engine.Type)&&Vector3.Distance(gearbox.P,Position(engine,catalog[engine.Type]))<.015f&&Math.Abs(Quaternion.Dot(gearbox.Q,Rotation(engine)))>.999f&&!gearbox.MovingMount;
 }
}
