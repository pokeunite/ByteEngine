namespace GoblinScrapper.Construction;
public sealed record BlockSetting(string Key,string Label,float Default,float Min,float Max,string Unit="",bool Toggle=false);
public static class BlockTuning {
 private static readonly System.Collections.Concurrent.ConcurrentDictionary<int,BlockSetting[]> Cache=new();
 public static BlockSetting[] Settings(int reference)=>Cache.GetOrAdd(reference,Create);
 private static BlockSetting[] Create(int reference)=>reference switch {
 2 or 46 => [new("speed","Wheel speed",1,.25f,4,"x"),new("torque","Drive torque",120,30,600,"Nm"),new("acceleration","Response",1,.25f,3,"x"),new("grip","Tyre grip",.6f,.2f,1.5f),new("autoBrake","Auto brake",1,0,1,Toggle:true),new("reverse","Reverse direction",0,0,1,Toggle:true)],
 40 or 50 or 60 or 86 => [new("grip","Tyre grip",.6f,.2f,1.5f)],
 13 or 28 => [new("angle","Steering limit",40,10,70,"deg"),new("steerSpeed","Turn speed",100,30,240,"deg/s"),new("returnSpeed","Return speed",60,20,180,"deg/s"),new("autoReturn","Return to centre",1,0,1,Toggle:true),new("reverse","Reverse steering",0,0,1,Toggle:true)],
 16 => [new("stiffness","Spring stiffness",4,1,12,"Hz"),new("damping","Damping",1,.2f,2)],
 18 or 9 => [new("travel","Travel",reference==9?.3f:.4f,.05f,reference==9?.3f:.4f,"m"),new("linearSpeed","Travel speed",2,.2f,5,"m/s"),new("force","Actuator force",1500,100,5000,"N")],
 14 or 17 or 22 or 39 or 48 => [new("speed","Rotation speed",1,.1f,3,"x"),new("torque","Motor torque",120,20,600,"Nm"),new("reverse","Reverse direction",0,0,1,Toggle:true)],
 11 or 53 or 61 => [new("shotPower","Projectile speed",1,.5f,2,"x")], _=>[] };
 public static float Value(AssemblyPart part,int reference,string key) {
 var s=Settings(reference).FirstOrDefault(s=>s.Key==key);if(s==null)return 0;
 return part.Tuning?.TryGetValue(key,out var v)==true&&float.IsFinite(v)?Math.Clamp(v,s.Min,s.Max):s.Default;
 }
 public static AssemblyPart Set(AssemblyPart part,int reference,string key,float value) {
 var s=Settings(reference).FirstOrDefault(s=>s.Key==key)??throw new ArgumentException("Setting is not supported by this block.");
 if(!float.IsFinite(value))throw new ArgumentException("Setting must be finite.");
 var values=part.Tuning==null?new Dictionary<string,float>():new Dictionary<string,float>(part.Tuning);
 values[key]=s.Toggle?(value>=.5f?1:0):Math.Clamp(value,s.Min,s.Max);return part with {Tuning=values};
 }
}
