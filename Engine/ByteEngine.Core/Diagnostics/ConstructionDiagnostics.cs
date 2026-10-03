using System.Globalization;
namespace ByteEngine.Core.Diagnostics;
/// <summary>Opt-in construction trace shared by native components and plugins; no gameplay dependencies.</summary>
public static class ConstructionDiagnostics
{
 private static readonly object Gate=new();
 private static readonly Queue<string> Events=new(),Samples=new();
 private static bool _enabled; private static int _generation;private static long _sequence;
 public static bool Enabled { get {lock(Gate)return _enabled;} set {lock(Gate){if(value&&!_enabled)_generation++;_enabled=value;}} }
 public static int Generation {get {lock(Gate)return _generation;} }
 public static void Record(string category,string message,bool sample=false)
 {
  lock(Gate)
  {
   if(!_enabled)return;
   if(message.Length>65536)message=message[..65536]+" [truncated]";
   var queue=sample?Samples:Events;
   queue.Enqueue($"#{++_sequence} {DateTimeOffset.Now.ToString("HH:mm:ss.fff zzz",CultureInfo.InvariantCulture)} [{category}] {message}");
   while(queue.Count>(sample?512:512)||queue.Sum(x=>x.Length)>524288)queue.Dequeue();
  }
 }
 public static string GetTrace(){lock(Gate)return "Construction diagnostics: values in metres, seconds, radians; groundNear is a height estimate, not solver contact.\nEVENTS / BUILD SNAPSHOTS\n"+string.Join("\n",Events)+"\nPHYSICS SAMPLES\n"+string.Join("\n",Samples);}
 public static void Clear(){lock(Gate){Events.Clear();Samples.Clear();_sequence=0;_generation++;}}
}
