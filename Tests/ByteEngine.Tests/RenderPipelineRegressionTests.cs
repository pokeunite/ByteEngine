using System.Reflection;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
namespace ByteEngine.Tests;
internal static class RenderPipelineRegressionTests {
 sealed class RenderProbe:Component{protected override void OnRender(RenderContext context){} }
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static void Run(){
  var scene=new Scene("Persistent rendering");
  for(int i=0;i<10000;i++)scene.CreateGameObject("Static metadata").StartInternal();
  var visible=scene.CreateGameObject("Visible");visible.AddComponent(new RenderProbe());visible.StartInternal();
  var method=typeof(Scene).GetMethod("GetRenderOrder",BindingFlags.NonPublic|BindingFlags.Instance)!;
  List<GameObject> Members()=>(List<GameObject>)method.Invoke(scene,[false])!;
  Check(Members().SequenceEqual(new[]{visible}),"Pure simulation objects entered the render registry");
  var added=scene.GameObjects[1].AddComponent(new RenderProbe());Check(Members().Count==2,"Adding a renderer failed to refresh membership");
  scene.GameObjects[1].RemoveComponent(added);Check(Members().SequenceEqual(new[]{visible}),"Removed renderer remained registered");
  Check(((List<GameObject>)method.Invoke(scene,[true])!).Count==10001,"Editor traversal lost non-rendering objects");
  var camera=scene.CreateGameObject("Camera").AddComponent(new Camera3D());var actor=scene.CreateGameObject("Actor");var skeletal=actor.AddComponent(new SkeletalMeshRenderer());
  var interval=typeof(SkeletalMeshRenderer).GetMethod("EffectiveAnimationInterval",BindingFlags.NonPublic|BindingFlags.Instance)!;
  float Rate()=>(float)interval.Invoke(skeletal,null)!;
  actor.Transform.WorldPosition=new(0,0,10);Check(Rate()==0,"Near animation was throttled");
  actor.Transform.WorldPosition=new(0,0,30);Check(Math.Abs(Rate()-1f/30)<.00001f,"Mid-distance animation budget wrong");
  actor.Transform.WorldPosition=new(0,0,100);Check(Math.Abs(Rate()-1f/15)<.00001f,"Distant animation budget wrong");
  skeletal.AutomaticAnimationLod=false;Check(Rate()==0,"Animation budget opt-out ignored");skeletal.AnimationUpdateInterval=.1f;Check(Rate()==.1f,"Authored sampling interval ignored");
  Console.WriteLine("PASS render pipeline: 10,000 non-rendering objects excluded; live renderer add/remove; editor traversal; near/mid/far animation budgets and explicit opt-out");
 }
}
