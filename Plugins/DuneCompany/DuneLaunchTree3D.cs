using System.Numerics;
using ByteEngine.Core.AI;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace DuneCompany;
/// <summary>Illuminates the Blender tree's actual lens positions from the race director.</summary>
public sealed class DuneLaunchTree3D:Component
{
 readonly List<(GameObject node,MeshRenderer renderer,int lamp)> _lamps=[];
 RaceDirector3D? _race;
 protected override void OnStart(){foreach(float x in new[]{-.19f,.19f})for(int i=0;i<5;i++){
  var o=GameObject.Scene!.CreateGameObject("Drag signal lens "+_lamps.Count);o.Transform.WorldPosition=Vector3.Transform(new(x,2.54f-i*.30f,.211f),Transform.WorldMatrix);o.Transform.LocalScale=new(.196f,.196f,.036f);o.Transform.WorldRotation=Transform.WorldRotation;
  var r=o.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Sphere,CastShadows=false,Material=new(){Shading=MaterialShadingMode.Unlit,BaseColor=new(.06f,.07f,.04f,1)}});_lamps.Add((o,r,i));}}
 protected override void OnUpdate(){_race??=GameObject.Scene?.GameObjects.SelectMany(o=>o.Components).OfType<RaceDirector3D>().FirstOrDefault();var race=_race;foreach(var (o,r,i) in _lamps){o.Active=GameObject.ActiveInHierarchy&&race!=null;bool on=race?.State==RaceState.Countdown&&i<3&&race.Remaining<=3-i||race?.State==RaceState.Racing&&i==4;r.Material.BaseColor=on?(i==4?new(.18f,1,.12f,1):new(1,.58f,.04f,1)):new(.055f,.060f,.04f,1);}}
 protected override void OnStop(){foreach(var (o,_,_) in _lamps)if(o.Scene is {} scene)scene.DestroyGameObject(o);_lamps.Clear();}
}
