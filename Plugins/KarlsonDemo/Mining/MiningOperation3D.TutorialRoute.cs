using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningOperation3D
{
 [Category("New game tutorial")]public float TutorialRouteWidth{get;set;}=.07f;
 [Category("New game tutorial")]public float TutorialRouteHeight{get;set;}=.10f;
 [Category("New game tutorial")]public float TutorialRouteRefreshSeconds{get;set;}=.3f;
 [Category("New game tutorial")]public float TutorialRouteVisibleDistance{get;set;}=40;
 [Browsable(false)]public bool TutorialRouteReady{get;private set;}
 [Browsable(false)]public int TutorialRoutePointCount=>tutorialPath.Count;
 Mesh? tutorialRibbon,tutorialGlow;Vector3 tutorialLastStart=new(float.MaxValue),tutorialLastTarget=new(float.MaxValue);
 readonly Material tutorialGlowMaterial=new(){Shading=MaterialShadingMode.Unlit,BaseColor=new(1,.7f,.06f,.16f),BlendMode=BlendMode3D.AlphaBlend,DepthWriteMode=DepthWriteMode3D.Disabled};
 void DisposeTutorialRoute(){tutorialRibbon?.Dispose();tutorialGlow?.Dispose();tutorialRibbon=tutorialGlow=null;}
 void BuildTutorialRoute()
 {
  DisposeTutorialRoute();if(tutorialPath.Count<2)return;
  var points=new List<Vector3>{tutorialPath[0]};float remaining=Math.Clamp(TutorialRouteVisibleDistance,3,100);
  foreach(var b in tutorialPath.Skip(1)){var a=points[^1];float distance=Vector3.Distance(a,b);if(distance<.05f)continue;if(distance>=remaining){points.Add(Vector3.Lerp(a,b,remaining/distance));break;}points.Add(b);remaining-=distance;}
  Mesh Strip(float width){var vertices=new List<float>();var indices=new List<uint>();for(int i=0;i<points.Count;i++){
   var direction=points[Math.Min(points.Count-1,i+1)]-points[Math.Max(0,i-1)];direction.Y=0;if(direction.LengthSquared()<.0001f)direction=Vector3.UnitZ;direction=Vector3.Normalize(direction);var side=new Vector3(direction.Z,0,-direction.X)*width/2;
   foreach(var p in new[]{points[i]-side,points[i]+side})vertices.AddRange([p.X,p.Y+Math.Clamp(TutorialRouteHeight,.02f,.3f),p.Z,0,1,0,0,0]);
   if(i>0){uint n=(uint)(i*2);indices.AddRange([n-2,n-1,n,n-1,n+1,n]);}
  }return new Mesh(vertices.ToArray(),indices.ToArray());}
  float w=Math.Clamp(TutorialRouteWidth,.025f,.2f);tutorialRibbon=Strip(w);tutorialGlow=Strip(w*3);
 }
 void DrawTutorialRoute(RenderContext c)
 {
  if(tutorialRibbon==null||tutorialGlow==null)return;
  c.RenderWorld.Submit(tutorialGlow,tutorialGlowMaterial,Matrix4x4.Identity,RenderQueue3D.Transparent,true,false,false);
  c.RenderWorld.Submit(tutorialRibbon,tutorialLineMaterial,Matrix4x4.Identity,RenderQueue3D.Opaque,true,false,false);
  // Three small animated chevrons communicate travel direction without a carpet of blocks.
  for(int arrow=0;arrow<3;arrow++){
   float at=1+(float)(Time.TotalTime*1.4+arrow*3)%9;
   for(int i=1;i<tutorialPath.Count;i++){var a=tutorialPath[i-1];var b=tutorialPath[i];float length=Vector3.Distance(a,b);if(at>length){at-=length;continue;}if(length<.01f)break;var dir=(b-a)/length;dir.Y=0;dir=Vector3.Normalize(dir);var p=Vector3.Lerp(a,b,at)+Vector3.UnitY*(TutorialRouteHeight+.012f);var side=new Vector3(dir.Z,0,-dir.X);
    foreach(float sign in new[]{-1f,1f}){var end=p-dir*.2f+side*.16f*sign;var delta=end-p;float yaw=MathF.Atan2(delta.X,delta.Z);c.RenderWorld.Submit(c.Renderer3D.GetPrimitive(PrimitiveMeshType.Cube),tutorialLineMaterial,Matrix4x4.CreateScale(.035f,.012f,delta.Length())*Matrix4x4.CreateRotationY(yaw)*Matrix4x4.CreateTranslation((p+end)/2),RenderQueue3D.Opaque,true,false,false);}break;
   }
  }
 }
 void DrawTutorialDestination(RenderContext c,AssetReference font)
 {
  if(TutorialDestination(TutorialUi) is not {} goal||Player is not {} player)return;
  var bounds=MiningCampGeometry.Transform(MiningCampGeometry.LocalBounds(goal),goal.Transform.WorldMatrix);var position=(bounds.Minimum+bounds.Maximum)/2+Vector3.UnitY;
  float s=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f),width=c.TargetWidth/s,height=c.TargetHeight/s;
  var clip=Vector4.Transform(new Vector4(position,1),c.GetViewMatrix3D()*c.GetProjectionMatrix3D());
  float x=clip.W>.1f?(clip.X/clip.W*.5f+.5f)*width:clip.X<0?35:width-35;
  float y=clip.W>.1f?(.5f-clip.Y/clip.W*.5f)*height:height*.6f;x=Math.Clamp(x,35,width-35);y=Math.Clamp(y,220,height-160);
  c.DrawUiQuad(new((x-17)*s,(y-17)*s),new(34*s),new(.02f,.035f,.05f,.85f));
  if(AnimationRuntimeAssets.TryGet(out var assets)&&assets!=null){string icon=TutorialStep==CampTutorialStep.AmmoPress?"ammo":TutorialStep==CampTutorialStep.StartDrill?"drill":TutorialStep==CampTutorialStep.Refinery?"gear":"company";c.DrawUiImage(assets.LoadTexture(new AssetReference("Assets/UI/Mining/Artwork/research-"+icon+".png")),new((x-12)*s,(y-12)*s),new(24*s),new(1,.8f,.15f,1));}
  var d=position-player.Transform.WorldPosition;float distance=MathF.Sqrt(d.X*d.X+d.Z*d.Z);c.DrawUiText($"{distance:0}m",font,(int)(14*s),new(x*s,(y+19)*s),Vector4.One,UiAnchor.TopCenter);
 }
}
