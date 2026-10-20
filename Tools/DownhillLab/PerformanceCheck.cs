using System.Diagnostics;
using System.Numerics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Scene;
using Downhill;

internal sealed class PerformanceCheck
{
 readonly Scene scene;readonly BoardController3D board;readonly Stopwatch clock=Stopwatch.StartNew();readonly bool early;readonly SkeletalMeshRenderer renderer;
 readonly List<double> frames=[];readonly List<string> results=[];double last,animation;
 internal PerformanceCheck(Scene scene,bool early=false){this.scene=scene;this.early=early;renderer=scene.FindGameObject("Rider")!.GetComponent<SkeletalMeshRenderer>()!;board=scene.FindGameObject("Board")!.GetComponent<BoardController3D>()!;board.Enabled=false;board.Body!.Enabled=false;scene.FindGameObject("Run director")!.GetComponent<DownhillRun3D>()!.Enabled=false;scene.FindGameObject("Follow camera")!.GetComponent<BoardCamera3D>()!.Enabled=false;}
 void Set(string key,object value)=>typeof(BoardController3D).GetProperty(key)!.SetValue(board,value);
 internal bool Step(int frame){renderer.EvaluatePoseAfterPhysics=!early;int phase=(frame-1)/240,local=(frame-1)%240;string name=new[]{"Ride","Slide","Tuck"}[Math.Min(2,phase)];board.TuckInput=phase==2;board.SlideInput=phase==1;board.PushInput=false;board.BrakeInput=false;board.SteerInput=phase==1?.7f:0;Set("Speed",18f);Set("IsSliding",phase==1);Set("Grounded",true);Set("SmoothedSteer",board.SteerInput);Set("SlipAngle",phase==1?-45f:0f);
  double now=clock.Elapsed.TotalMilliseconds;if(local>60){frames.Add(now-last);animation+=scene.LastFrameMetrics.AnimationMs;}last=now;
  if(local==239){frames.Sort();string line=$"{name}: FPS={1000/frames.Average():0.0}, p95 frame={frames[(int)(frames.Count*.95)]:0.00} ms, animation={animation/frames.Count:0.00} ms";Console.WriteLine(line);results.Add(line);frames.Clear();animation=0;}
  if(frame==720){File.WriteAllLines(Program.Root+"/Preview/Performance verification.txt",results);return true;}return false;
 }
}
