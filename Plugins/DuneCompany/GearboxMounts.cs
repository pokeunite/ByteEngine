using System.Numerics;
namespace DuneCompany;
public static class GearboxMounts
{
 public static bool IsEngine(int type)=>type is 23 or 24 or 34 or 38;
 public static Vector3 Position(PlacedBlock engine,DunePart part)
 {
  // Authored casting sockets, rather than exhaust-pipe or mounting-base bounds.
  var socket=engine.Type==38?new Vector3(0,.513f,.4085f):engine.Type==34?new Vector3(0,.486f,.387f):new Vector3((part.Low.X+part.High.X)*.5f,.30f,part.High.Z-.10f);
  return engine.P+Vector3.Transform(socket+new Vector3(0,-.20f,.35f),engine.Q);
 }
 public static bool Connected(PlacedBlock gearbox,IReadOnlyList<PlacedBlock> blocks,IReadOnlyDictionary<int,DunePart> catalog)
 {
  var engine=blocks.FirstOrDefault(b=>b.Id==gearbox.Parent);
  return engine!=null&&IsEngine(engine.Type)&&Vector3.Distance(gearbox.P,Position(engine,catalog[engine.Type]))<.015f&&Math.Abs(Quaternion.Dot(gearbox.Q,engine.Q))>.999f&&!gearbox.MovingMount;
 }
}
