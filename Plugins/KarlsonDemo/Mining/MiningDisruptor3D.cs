using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics.ThreeD;
namespace KarlsonDemo.Plugin;
/// <summary>Support counter: fixed, visible wind-up before a short local defence shutdown.</summary>
public sealed class MiningDisruptor3D:Component
{
 [SceneObjectReference]public Guid OperationObjectId{get;set;}
 public float PulseRange{get;set;}=5;
 public float WindupSeconds{get;set;}=1;
 public float ShutdownSeconds{get;set;}=3;
 public float CooldownSeconds{get;set;}=7;
 public float WindupRemaining{get;private set;}=-1;
 float cooldown;GameObject? marker;Vector3 locked;
 public static bool IsDisruptor(MiningWavePreview w,int index){if(w.Raiders<=0||w.Disruptors<=0||index<0)return false;int before=index-index*w.Hunters/Math.Max(1,w.Total);return (before+1)*w.Disruptors/w.Raiders>before*w.Disruptors/w.Raiders;}
 protected override void OnStart(){if(GameObject.Scene is not {} scene)return;marker=scene.CreateGameObject("Disruptor pulse warning");marker.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Cylinder,Material=new(){BaseColor=new(.08f,.75f,1,.45f)}});marker.Active=false;var badge=scene.CreateGameObject("Disruptor cyan crest");badge.SetParent(GameObject,false);badge.Transform.LocalPosition=new(0,1.1f,0);badge.Transform.LocalScale=new(.25f,.45f,.25f);badge.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Sphere,Material=new(){BaseColor=new(.05f,.8f,1,1)}});}
 public void Tick(float dt){if(dt<=0||!float.IsFinite(dt)||GameObject.Scene is not {} scene)return;if(GameObject.GetComponent<HealthComponent>()?.IsDead==true){if(marker!=null)marker.Active=false;WindupRemaining=-1;return;}var planner=scene.FindGameObject(OperationObjectId)?.GetComponent<MiningOperation3D>()?.DefencePlanner;if(planner==null)return;cooldown=Math.Max(0,cooldown-dt);if(WindupRemaining>=0){WindupRemaining-=dt;if(marker!=null){marker.Transform.WorldPosition=locked+Vector3.UnitY*.08f;marker.Transform.LocalScale=new(PulseRange*2,.035f,PulseRange*2);marker.Active=true;}if(WindupRemaining<=0){foreach(var d in planner.LiveDefences)if(d.CanBeAttacked&&Vector3.DistanceSquared(d.Transform.WorldPosition,locked)<=PulseRange*PulseRange)d.DisableFor(ShutdownSeconds);WindupRemaining=-1;cooldown=CooldownSeconds;if(marker!=null)marker.Active=false;}return;}if(cooldown<=0&&planner.LiveDefences.Any(d=>d.CanBeAttacked&&Vector3.DistanceSquared(d.Transform.WorldPosition,Transform.WorldPosition)<=PulseRange*PulseRange)){locked=Transform.WorldPosition;WindupRemaining=Math.Max(.3f,WindupSeconds);}}
 protected override void OnUpdate()=>Tick((float)Time.DeltaTime);
 protected override void OnDestroy(){if(marker?.Scene is {} s)s.DestroyGameObject(marker);}
}
public sealed partial class MiningOperation3D
{
 void ConfigureDisruptor(GameObject enemy){enemy.Name="Disruptor spider";enemy.AddComponent(new MiningDisruptor3D{OperationObjectId=GameObject.Id});if(enemy.GetComponent<MiningRaiderPriorities3D>() is {} priorities)priorities.DefenceNoticeRadius=14;}
}
