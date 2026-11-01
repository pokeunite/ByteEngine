using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
namespace ByteEngine.Tests;
internal static class EnginePerformanceIntegrationTests
{
 public static void Run()
 {
  void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS ENGINE REVIEW "+text);}
  var values=new int[257];JobRunner.For(values.Length,i=>values[i]=i+1);Check(values.Select((v,i)=>v==i+1).All(v=>v),"worker helper processes every index once into ordered results");
  var scene=new Scene("Engine review correctness");var first=scene.CreateGameObject("Camera one").AddComponent(new Camera3D());Check(scene.ActiveCamera==first,"camera registry supplies fallback");
  var secondRoot=scene.CreateGameObject("Camera two");var second=secondRoot.AddComponent(new Camera3D{ActiveGameCamera=true});Check(scene.ActiveCamera==second,"adding and selecting a camera invalidates the cached registry");second.Enabled=false;Check(scene.ActiveCamera==first,"disabled active camera falls back");second.Enabled=true;secondRoot.Active=false;Check(scene.ActiveCamera==first,"inactive hierarchy camera falls back");secondRoot.Active=true;scene.DestroyGameObject(secondRoot);Check(scene.ActiveCamera==first,"destroying a camera invalidates cached lookup");
  var parent=scene.CreateGameObject("Queued parent");var child=scene.CreateGameObject("Queued child");child.SetParent(parent,false);int destroyed=0;parent.AddComponent(new DestroyProbe(()=>destroyed++));child.AddComponent(new DestroyProbe(()=>destroyed++));var runner=scene.CreateGameObject("Destroy dispatcher");runner.AddComponent(new Once(()=>{scene.DestroyGameObject(parent);scene.DestroyGameObject(child);}));scene.LoadInternal();Time.Update(.02);scene.UpdateInternal();Check(destroyed==2&&scene.FindGameObject(parent.Id)==null&&scene.FindGameObject(child.Id)==null,"queued parent/child destruction occurs exactly once and leaves no lookup entries");scene.UnloadInternal();
  var fixedScene=new Scene("Fixed delta review"){FixedSimulation=true};var probe=fixedScene.CreateGameObject().AddComponent(new FixedProbe());fixedScene.LoadInternal();Time.FixedDeltaTime=.123;Time.Update(.05);fixedScene.UpdateInternal();Check(probe.Ticks>0&&probe.Last>0&&Math.Abs(probe.Last-.123)>.01,"fixed callbacks receive the simulation step rather than stale fixed delta");fixedScene.UnloadInternal();
 }
 sealed class DestroyProbe(Action destroyed):Component{protected override void OnDestroy()=>destroyed();}
 sealed class Once(Action action):Component{bool done;protected override void OnUpdate(){if(done)return;done=true;action();}}
 sealed class FixedProbe:Component{public int Ticks;public double Last;protected override void OnFixedUpdate(){Ticks++;Last=Time.FixedDeltaTime;}}
}
