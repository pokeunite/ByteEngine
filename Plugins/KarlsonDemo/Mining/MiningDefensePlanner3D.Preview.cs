using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningDefensePlanner3D
{
 public AssetReference BarricadePreviewModel{get;set;}=new("Assets/Models/Mining/Research/FieldBarricade.glb");
 public AssetReference RefineryPreviewModel{get;set;}=new("Assets/Models/Mining/Refinery.glb");
 public AssetReference ResearchPreviewModel{get;set;}=new("Assets/Models/Mining/Research/ResearchStation.glb");
 public float RefineryPreviewScale{get;set;}=.69f;
 public float ResearchPreviewScale{get;set;}=1.26f;
 MiningDefenseKind? previewKind;GameObject? previewBody;
 public float PlacementYaw{get;private set;}
 GameObject? barricadePreview;SkeletalMeshRenderer? barricadeVisual;
 public void RotatePlacement(float degrees){if(float.IsFinite(degrees))PlacementYaw=(PlacementYaw+degrees+360)%360;}
 void HideBarricadePreview(){if(barricadePreview!=null)barricadePreview.Active=false;}
 void UpdateBarricadePreview()
 {
  if(!BuildMode){HideBarricadePreview();return;}
  if(barricadePreview==null||previewKind!=SelectedKind){
   if(barricadePreview?.Scene==GameObject.Scene)GameObject.Scene!.DestroyGameObject(barricadePreview);previewKind=SelectedKind;
   var model=SelectedKind switch{MiningDefenseKind.Sentry=>SentryModel,MiningDefenseKind.Repeater=>RepeaterModel,MiningDefenseKind.Mortar=>MortarModel,MiningDefenseKind.Mine=>MineModel,MiningDefenseKind.SupplyCache=>SupplyCacheModel,MiningDefenseKind.Barricade=>BarricadePreviewModel,MiningDefenseKind.ExtraRefinery=>RefineryPreviewModel,_=>ResearchPreviewModel};
   if(model.IsEmpty)model=new("Assets/Models/Mining/Defences/"+SelectedKind+".glb");
   float scale=SelectedKind==MiningDefenseKind.Barricade?1:SelectedKind==MiningDefenseKind.ExtraRefinery?RefineryPreviewScale:SelectedKind==MiningDefenseKind.ResearchStation?ResearchPreviewScale:DefenceModelScale;
   barricadePreview=GameObject.Scene!.CreateGameObject(SelectedKind+" hologram preview");previewBody=GameObject.Scene.CreateGameObject("Placement model");previewBody.SetParent(barricadePreview,false);previewBody.Transform.LocalScale=new(scale);if(SelectedKind is MiningDefenseKind.ExtraRefinery or MiningDefenseKind.ResearchStation)previewBody.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI);
   barricadeVisual=previewBody.AddComponent(new SkeletalMeshRenderer{Model=model,PlayOnStart=true,DefaultAnimation=SelectedKind==MiningDefenseKind.ExtraRefinery?"Operate":SelectedKind==MiningDefenseKind.ResearchStation?"ResearchStation_Idle":SelectedKind==MiningDefenseKind.Barricade?"FieldBarricade_Idle":SelectedKind+"_Idle",Speed=0});
  }
  barricadePreview.Active=true;barricadePreview.Transform.WorldPosition=PlacementPoint;barricadePreview.Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,PlacementYaw*MathF.PI/180);
  if(SelectedKind is MiningDefenseKind.Sentry or MiningDefenseKind.Repeater){previewBody!.Transform.LocalPosition=Vector3.Zero;float lift=MiningDefense3D.RequiredGunSupport(barricadeVisual!,SelectedKind,PlacementPoint.Y,DefenceMinimumMuzzleHeight);previewBody.Transform.LocalPosition=Vector3.UnitY*lift;}
  var color=PlacementValid?new Vector4(.08f,.6f,1,.28f):new Vector4(1,.15f,.12f,.28f);
  for(int i=0;i<32;i++){var m=barricadeVisual!.GetMaterialOverrides(i);m.BaseColor=color;m.Opacity=.28f;m.EmissionColor=new(color.X,color.Y,color.Z);m.EmissionIntensity=1.2f;}
 }
}
