using System.Numerics;
using System.Text.Json;
namespace DuneCompany;
public sealed class DunePart
{
 public int Id{get;set;}public string Name{get;set;}="";public string Label{get;set;}="";public string File{get;set;}="";public string Preview{get;set;}="";public string Group{get;set;}="";public string Function{get;set;}="";
 public float[] Min{get;set;}=[-.25f,0,-.5f];public float[] Max{get;set;}=[.25f,.5f,.5f];public float SupportWidth{get;set;}=.5f;public float SupportLength{get;set;}=.5f;public float WheelCenterZ{get;set;}=-.18f;public float MountInset{get;set;}=0;public float Mass{get;set;}=20;public float Radius{get;set;}=.55f;public float Power{get;set;}=0;
 public Vector3 Low=>new(Min[0],Min[1],Min[2]);public Vector3 High=>new(Max[0],Max[1],Max[2]);public bool Wheel=>Id is >=13 and <=15;
}
public sealed class PlacedBlock
{
 public int Id{get;set;}public int Type{get;set;}public int Parent{get;set;}=-1;public float[] Position{get;set;}=[0,0,0];public float[] Rotation{get;set;}=[0,0,0,1];
 public bool MovingMount{get;set;}
 public float Power{get;set;}=1;public float Steering{get;set;}=30;public float Grip{get;set;}=1;public float Travel{get;set;}=.17f;public float Angle{get;set;}=35;public float Stroke{get;set;}=.42f;
 public float WinchRate{get;set;}=1;
 public float Paint{get;set;}=0;public float SpringRate{get;set;}=4;public float Damping{get;set;}=1;public float Preload{get;set;}=0;public float SpeedLimit{get;set;}=32;public float BrakeStrength{get;set;}=1;
 public Vector3 P=>new(Position[0],Position[1],Position[2]);public Quaternion Q=>Quaternion.Normalize(new(Rotation[0],Rotation[1],Rotation[2],Rotation[3]));
 public void Pose(Vector3 p,Quaternion q){Position=[p.X,p.Y,p.Z];Rotation=[q.X,q.Y,q.Z,q.W];}
}
public static class BuildStore
{
 public static readonly JsonSerializerOptions Json=new(){PropertyNameCaseInsensitive=true,WriteIndented=true};
 public static List<PlacedBlock> Read(string json,IReadOnlyDictionary<int,DunePart> catalog)
 {
  var list=JsonSerializer.Deserialize<List<PlacedBlock>>(json,Json)??throw new InvalidDataException("Missing vehicle blocks");
  if(list.Count is <1 or >256||list.Select(p=>p.Id).Distinct().Count()!=list.Count||!list.Any(p=>p.Id==0&&p.Parent==-1)||list.Count(p=>p.Parent==-1)!=1)throw new InvalidDataException("Invalid vehicle root or block count");
  var seen=new HashSet<int>();foreach(var p in list){if(!catalog.ContainsKey(p.Type)||p.Position.Length!=3||p.Rotation.Length!=4||p.Position.Concat(p.Rotation).Any(v=>!float.IsFinite(v))||p.Rotation.Sum(v=>v*v)<.01f||p.P.Length()>100||p.Parent!=-1&&!list.Any(b=>b.Id==p.Parent))throw new InvalidDataException("Invalid vehicle block or connection");if(new[]{p.WinchRate,p.Power,p.Steering,p.Grip,p.Travel,p.Angle,p.Stroke,p.Paint,p.SpringRate,p.Damping,p.Preload,p.SpeedLimit,p.BrakeStrength}.Any(v=>!float.IsFinite(v)))throw new InvalidDataException("Invalid tuning values");p.WinchRate=Math.Clamp(p.WinchRate,.25f,1.5f);p.Power=Math.Clamp(p.Power,0,2);p.Steering=Math.Clamp(p.Steering,-60,60);p.Grip=Math.Clamp(p.Grip,.2f,2);p.Travel=Math.Clamp(p.Travel,0,.5f);p.Angle=Math.Clamp(p.Angle,-90,90);p.Stroke=Math.Clamp(p.Stroke,0,.42f);p.Paint=Math.Clamp(MathF.Round(p.Paint),0,5);p.SpringRate=Math.Clamp(p.SpringRate,1,12);p.Damping=Math.Clamp(p.Damping,.15f,2);p.Preload=Math.Clamp(p.Preload,-.1f,.1f);p.SpeedLimit=Math.Clamp(p.SpeedLimit,4,50);p.BrakeStrength=Math.Clamp(p.BrakeStrength,.1f,2);seen.Add(p.Id);}
  var sorted=new List<PlacedBlock>();var pending=list.ToList();while(pending.Count>0){var ready=pending.Where(p=>p.Parent==-1||sorted.Any(b=>b.Id==p.Parent)).ToArray();if(ready.Length==0)throw new InvalidDataException("Vehicle connections contain a cycle");foreach(var p in ready){sorted.Add(p);pending.Remove(p);}}return sorted;
 }
}
