using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningCampLayout3D
{
 public bool ShowDefenceRanges{get;set;}=true;
 public float RangeConeDegrees{get;set;}=90;
 readonly Material rangeMaterial=new(){BaseColor=new(.24f,.5f,.56f,1),EmissionEnabled=true,EmissionColor=new(.1f,.3f,.4f),EmissionIntensity=.35f};
 readonly Material selectedRangeMaterial=new(){BaseColor=new(.12f,.9f,1,1),EmissionEnabled=true,EmissionColor=new(.1f,.7f,1),EmissionIntensity=.6f};
 void DrawDefenceRanges(RenderContext c)
 {
  if(!ShowDefenceRanges||editCamera?.GetComponent<Camera3D>() is not {} camera)return;
  var hovered=PointerGround(out var ground)?Pick(ground)?.Id:Guid.Empty;
  foreach(var root in Objects.Where(o=>o.Id==SelectedObjectId||o.Id==hovered))
  {
   if(root.GetComponent<MiningDefense3D>() is not {} d||d.Spent||d.Kind==MiningDefenseKind.SupplyCache)continue;
   bool selected=SelectedObjectId==root.Id;Vector3 p=selected?PreviewPosition:root.Transform.WorldPosition;float yaw=selected?PreviewYaw:Yaw(root);
   float radius=d.Kind==MiningDefenseKind.Mine?d.EffectiveBlastRadius:d.Range;
   void Line(Vector3 a,Vector3 b,float width=.045f){var delta=b-a;float length=delta.Length();if(length<.001f)return;float heading=MathF.Atan2(delta.X,delta.Z);c.RenderWorld.Submit(c.Renderer3D.GetPrimitive(PrimitiveMeshType.Cube),selected?selectedRangeMaterial:rangeMaterial,Matrix4x4.CreateScale(width,.035f,length)*Matrix4x4.CreateRotationY(heading)*Matrix4x4.CreateTranslation((a+b)/2),RenderQueue3D.Opaque,true,false,false);}
   Vector3 Point(float angle,float r)=>p+new Vector3(MathF.Sin(angle)*r,.12f,MathF.Cos(angle)*r);
   for(int i=0;i<64;i++)Line(Point(i*MathF.Tau/64,radius),Point((i+1)*MathF.Tau/64,radius));
   if(d.Kind==MiningDefenseKind.Mine){for(int i=0;i<40;i++)Line(Point(i*MathF.Tau/40,d.TriggerRadius),Point((i+1)*MathF.Tau/40,d.TriggerRadius),.07f);}
   else
   {
    float heading=yaw*MathF.PI/180+MathF.PI,half=Math.Clamp(RangeConeDegrees,10,180)*MathF.PI/360;
    Line(p+Vector3.UnitY*.12f,Point(heading-half,radius),.09f);Line(p+Vector3.UnitY*.12f,Point(heading+half,radius),.09f);
    for(int i=0;i<24;i++)Line(Point(heading-half+i*half*2/24,radius),Point(heading-half+(i+1)*half*2/24,radius),.09f);
   }
   if(selected)
   {
    float s=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f);string detail=$"{d.Kind} | {d.EffectiveDamage:0.#} DAMAGE | {radius:0.#}m RANGE";
    string help=d.Kind==MiningDefenseKind.Mine?$"Inner ring: {d.TriggerRadius:0.#}m trigger / outer: blast":d.Kind==MiningDefenseKind.Mortar?$"360° targeting / {d.EffectiveBlastRadius:0.#}m impact radius":"Cone: facing / outer ring: 360° tracking range";
    SupportUiArt.Panel(c,s,16,76,430,53);
    SupportUiArt.T(c,s,detail,27,82,16,true,SupportUiArt.Gold);SupportUiArt.T(c,s,help,27,107,13);

   }
  }
 }
}
