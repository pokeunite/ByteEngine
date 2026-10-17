using ByteEngine.Core.Scene;
using System.Text.Json;
namespace ByteEngine.Core.Gameplay;

/// <summary>Fixed-time medal challenge. First start command latches the clock; scripted
/// completion stops it. Missing a threshold lowers the medal, never fails the level.</summary>
public sealed class TimedChallenge3D:Component
{
 public string StartVariable {get;set;}="Started";
 public string FinishVariable {get;set;}="Completed";
 public double ThreeStarSeconds {get;set;}=30;
 public double TwoStarSeconds {get;set;}=45;
 public string RecordKey {get;set;}="";
 public double BestTime {get;private set;}
 public int BestStars=>BestTime<=0?0:BestTime<=Math.Max(0,ThreeStarSeconds)?3:BestTime<=Math.Max(ThreeStarSeconds,TwoStarSeconds)?2:1;
 public string BestDisplayTime=>BestTime>0?TimeSpan.FromSeconds(BestTime).ToString(@"mm\:ss\.f"):"--:--.-";
 public bool NewRecord {get;private set;}
 public string LastError {get;private set;}="";
 string? recordPath;
 public bool Started {get;private set;}
 public bool Finished {get;private set;}
 public double ElapsedSeconds {get;private set;}
 public double RevealSeconds {get;private set;}
 public int Stars=>ElapsedSeconds<=Math.Max(0,ThreeStarSeconds)?3:ElapsedSeconds<=Math.Max(ThreeStarSeconds,TwoStarSeconds)?2:1;
 public string DisplayTime=>TimeSpan.FromSeconds(Math.Max(0,ElapsedSeconds)).ToString(@"mm\:ss\.f");
 protected override void OnStart(){string key=new(RecordKey.Where(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '_').ToArray());if(key.Length==0)return;recordPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"ByteEngine","Records",key+".json");try{if(File.Exists(recordPath)){double value=JsonSerializer.Deserialize<double>(File.ReadAllText(recordPath));if(double.IsFinite(value)&&value>0)BestTime=value;}}catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException){LastError=e.Message;}}
 protected override void OnFixedUpdate(){
  var variables=GameObject.Scene!.Variables;
  if(!Started&&variables.TryGet(StartVariable,out var start)&&start!.Boolean)Started=true;
  if(!Started)return;
  if(!Finished&&variables.TryGet(FinishVariable,out var finish)&&finish!.Boolean){Finished=true;NewRecord=BestTime<=0||ElapsedSeconds<BestTime;if(NewRecord){BestTime=ElapsedSeconds;if(recordPath!=null)try{Directory.CreateDirectory(Path.GetDirectoryName(recordPath)!);File.WriteAllText(recordPath+".tmp",JsonSerializer.Serialize(BestTime));File.Move(recordPath+".tmp",recordPath,true);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){LastError=e.Message;}}}
  if(Finished)RevealSeconds+=Time.FixedDeltaTime;else ElapsedSeconds+=Time.FixedDeltaTime;
 }
}
