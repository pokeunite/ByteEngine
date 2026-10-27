using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Runtime;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
public sealed class MiningDefensePlanner3D:Component
{
 [SceneObjectReference] public Guid WorkshopObjectId{get;set;}
 [SceneObjectReference] public Guid CameraObjectId{get;set;}
 public float SentryDamage{get;set;}=12;public float SentryInterval{get;set;}=.8f;public float RepeaterDamage{get;set;}=8;public float RepeaterInterval{get;set;}=.25f;public float MortarDamage{get;set;}=45;public float MortarInterval{get;set;}=5;public float MineDamage{get;set;}=75;public float DefenceRange{get;set;}=18;public float MortarRange{get;set;}=28;public float MineBlastRadius{get;set;}=3;
 public AssetReference SentryModel{get;set;}=AssetReference.Empty;
 public AssetReference RepeaterModel{get;set;}=AssetReference.Empty;
 public AssetReference MortarModel{get;set;}=AssetReference.Empty;
 public AssetReference MineModel{get;set;}=AssetReference.Empty;
 public AssetReference SupplyCacheModel{get;set;}=AssetReference.Empty;
 public float DefenceProjectileSpeed{get;set;}=75;
 public float DefenceTracerLength{get;set;}=2;
 public float DefenceTracerWidth{get;set;}=.035f;
 public float DefenceProjectileRadius{get;set;}=.025f;
 public float DefenceModelScale{get;set;}=.65f;
 public float PlacementRange{get;set;}=8;
 public float MinimumSpacing{get;set;}=1.4f;
 public Key BuildKey{get;set;}=Key.B;
 public Key SentryKey{get;set;}=Key.F1;public Key RepeaterKey{get;set;}=Key.F2;public Key MortarKey{get;set;}=Key.F3;public Key MineKey{get;set;}=Key.F4;public Key SupplyKey{get;set;}=Key.F5;
 public bool PersistProgress{get;set;}=true;
 public string SaveSlot{get;set;}="mining-defences-v1.json";
 public bool BuildMode{get;private set;}
 double placementTime=-1000;
 public bool SuppressWeaponFire=>GameObject.GetComponent<MiningCampLayout3D>()?.SuppressWeaponFire==true||BuildMode||Math.Abs(Time.TotalTime-placementTime)<.000001;
 public MiningDefenseKind SelectedKind{get;private set;}
 public string Status{get;private set;}="";
 public Vector3 PlacementPoint{get;private set;}
 public bool PlacementValid{get;private set;}
 public int PlacedCount=>placed.Count;
 readonly List<(MiningDefenseKind Kind,GameObject Root)> placed=[];int spentMines,spentCaches,round=-1;GameObject? marker;
 MiningTrade3D? Trade=>GameObject.Scene?.FindGameObject(WorkshopObjectId)?.GetComponent<MiningTrade3D>();
 public int Allowance(MiningDefenseKind kind)=>Trade is {} t?Math.Clamp((int)t.Stat(kind switch{MiningDefenseKind.Sentry=>"AutogunGrant",MiningDefenseKind.Repeater=>"UnlockMachineGun",MiningDefenseKind.Mortar=>"MortarStrikeGrant",MiningDefenseKind.SupplyCache=>"UnlockAirdrop",_=>"LandMineGrant"}),0,12):0;
 public int Available(MiningDefenseKind kind)=>Math.Max(0,Allowance(kind)-placed.Count(p=>p.Kind==kind)-(kind==MiningDefenseKind.Mine?spentMines:kind==MiningDefenseKind.SupplyCache?spentCaches:0));
 public void CycleBuild()
 {if(GameObject.GetComponent<MiningCampLayout3D>()?.IsEditing==true)return;var kinds=Enum.GetValues<MiningDefenseKind>().Where(k=>Available(k)>0).ToArray();if(kinds.Length==0){BuildMode=false;Status="Research a defence licence at the company tent.";return;}int index=BuildMode?Array.IndexOf(kinds,SelectedKind)+1:0;if(index>=kinds.Length){BuildMode=false;return;}SelectedKind=kinds[index];BuildMode=true;}
 public bool SelectBuild(MiningDefenseKind kind){if(GameObject.GetComponent<MiningCampLayout3D>()?.IsEditing==true||!Enum.IsDefined(kind)||Trade?.Operation?.Phase==MiningPhase.Failed||Available(kind)<=0)return false;SelectedKind=kind;BuildMode=true;return true;}
 public void Cancel()=>BuildMode=false;
 public bool TryPlace(Vector3 point)
 {
  var op=Trade?.Operation;if(op==null||op.Phase==MiningPhase.Failed||Available(SelectedKind)<=0||!float.IsFinite(point.X)||!float.IsFinite(point.Y)||!float.IsFinite(point.Z)||Vector3.Distance(point,Transform.WorldPosition)>Math.Clamp(PlacementRange,1,20)||placed.Any(p=>Vector3.Distance(p.Root.Transform.WorldPosition,point)<MinimumSpacing))return false;
  if(GameplayQuery3D.OverlapSphere(GameObject.Scene!,point+Vector3.UnitY*.7f,.5f,GameObject,includeTriggers:false).Any())return false;
  Spawn(SelectedKind,point);placementTime=Time.TotalTime;BuildMode=false;Status="Defence deployed.";Save();return true;
 }
 void Spawn(MiningDefenseKind kind,Vector3 point,float yaw=0)
 {
  var t=Trade!;var root=GameObject.Scene!.CreateGameObject("Company "+kind);root.Transform.WorldPosition=point;root.Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw*MathF.PI/180);
  root.AddComponent(new MiningDefense3D{ProjectileSpeed=DefenceProjectileSpeed,TracerLength=DefenceTracerLength,TracerWidth=DefenceTracerWidth,ProjectileRadius=DefenceProjectileRadius,OperationObjectId=t.OperationObjectId,WorkshopObjectId=WorkshopObjectId,Kind=kind,VisualModel=kind switch{MiningDefenseKind.Sentry=>SentryModel,MiningDefenseKind.Repeater=>RepeaterModel,MiningDefenseKind.Mortar=>MortarModel,MiningDefenseKind.Mine=>MineModel,_=>SupplyCacheModel},VisualScale=DefenceModelScale,BaseDamage=kind==MiningDefenseKind.Mine?MineDamage:kind==MiningDefenseKind.Mortar?MortarDamage:kind==MiningDefenseKind.Repeater?RepeaterDamage:SentryDamage,BaseInterval=kind==MiningDefenseKind.Mortar?MortarInterval:kind==MiningDefenseKind.Repeater?RepeaterInterval:SentryInterval,Range=kind==MiningDefenseKind.Mine?3:kind==MiningDefenseKind.Mortar?MortarRange:DefenceRange,BlastRadius=kind==MiningDefenseKind.Mine?MineBlastRadius:4});placed.Add((kind,root));
 }
 protected override void OnStart()
 {
  if(!PersistProgress)return;try{if(File.Exists(PathName)){var s=JsonSerializer.Deserialize<State>(File.ReadAllText(PathName));if(s?.Version==1){spentCaches=Math.Clamp(s.SpentCaches,0,100);spentMines=Math.Clamp(s.SpentMines,0,100);round=s.Round;foreach(var p in (s.Placements??[]).Take(36))if(Enum.IsDefined(p.Kind)&&float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z))Spawn(p.Kind,new(p.X,p.Y,p.Z),float.IsFinite(p.Yaw)?p.Yaw:0);}}}catch(Exception e)when(e is IOException or JsonException or UnauthorizedAccessException){Status=e.Message;}
 }
 protected override void OnUpdate()
 {
  if(Trade?.Operation is not {} op)return;
  if(round!=op.CompletedRounds){round=op.CompletedRounds;spentMines=spentCaches=0;Save();}
  foreach(var p in placed.Where(p=>p.Root.Scene!=GameObject.Scene).ToArray()){if(p.Kind==MiningDefenseKind.Mine)spentMines++;if(p.Kind==MiningDefenseKind.SupplyCache)spentCaches++;placed.Remove(p);Save();}
  // A HUD click can select a defence while the host still has the UI cursor.
  // Preserve that selection until the host restores gameplay capture.
  if(!Input.IsGameInputCaptured||Input.IsUiPointerRequested){PlacementValid=false;if(marker!=null)marker.Active=false;return;}
  if(Input.IsKeyPressed(BuildKey))CycleBuild();
  var shortcuts=new[]{SentryKey,RepeaterKey,MortarKey,MineKey,SupplyKey};for(int i=0;i<shortcuts.Length;i++)if(Input.IsKeyPressed(shortcuts[i]))SelectBuild((MiningDefenseKind)i);if(BuildMode&&Input.IsMouseButtonPressed(MouseButton.Right))Cancel();
  if(marker==null){marker=GameObject.Scene!.CreateGameObject("Defence placement preview");marker.Transform.LocalScale=new(1,.03f,1);marker.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Cylinder,Material=new(){BaseColor=new(.2f,.9f,.3f,1)}});}
  marker.Active=BuildMode;
  if(!BuildMode)return;
  PlacementValid=false;
  if(GameObject.Scene?.FindGameObject(CameraObjectId) is {} camera&&GameplayQuery3D.Raycast(GameObject.Scene,camera.Transform.WorldPosition,camera.Transform.Forward,out var hit,60,GameObject,includeTriggers:false))
  {PlacementPoint=hit.Point;PlacementValid=hit.Normal.Y>.6f&&Vector3.Distance(PlacementPoint,Transform.WorldPosition)<=PlacementRange&&!placed.Any(p=>Vector3.Distance(p.Root.Transform.WorldPosition,PlacementPoint)<MinimumSpacing);}
  marker.Transform.WorldPosition=PlacementPoint+Vector3.UnitY*.03f;marker.GetComponent<MeshRenderer>()!.Material.BaseColor=PlacementValid?new(.2f,.9f,.3f,1):new(1,.15f,.05f,1);
  if(PlacementValid&&Input.IsMouseButtonPressed(MouseButton.Left))TryPlace(PlacementPoint);
 }

 string PathName{get{if(System.IO.Path.GetFileName(SaveSlot)!=SaveSlot)throw new InvalidDataException("Defence save must be a filename.");return System.IO.Path.Combine(GameSaveStorage.GetDirectory(GameObject.Scene!.ProjectRoot!),SaveSlot);}}
 public bool LastSaveSucceeded{get;private set;}=true;
 public bool Owns(GameObject root)=>placed.Any(p=>p.Root==root&&root.Scene==GameObject.Scene);
 public bool SaveRelocation(GameObject root){if(!Owns(root))return false;Save();return LastSaveSucceeded;}
 static float GetYaw(GameObject root){var q=root.Transform.WorldRotation;return MathF.Atan2(2*(q.W*q.Y+q.X*q.Z),1-2*(q.Y*q.Y+q.Z*q.Z))*180/MathF.PI;}
 public void Save(){LastSaveSucceeded=true;foreach(var p in placed.Where(p=>p.Root.Scene!=GameObject.Scene).ToArray()){if(p.Kind==MiningDefenseKind.Mine)spentMines++;if(p.Kind==MiningDefenseKind.SupplyCache)spentCaches++;placed.Remove(p);}if(!PersistProgress||AttachedGameObject?.Scene==null)return;try{Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathName)!);File.WriteAllText(PathName+".tmp",JsonSerializer.Serialize(new State{SpentCaches=spentCaches,SpentMines=spentMines,Round=round,Placements=placed.Where(p=>p.Root.Scene==GameObject.Scene).Select(p=>new Placement{Kind=p.Kind,X=p.Root.Transform.WorldPosition.X,Y=p.Root.Transform.WorldPosition.Y,Z=p.Root.Transform.WorldPosition.Z,Yaw=GetYaw(p.Root)}).ToArray()}));File.Move(PathName+".tmp",PathName,true);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){LastSaveSucceeded=false;Status=e.Message;}}
 protected override void OnStop()=>Save();
 public sealed class State{public int Version{get;set;}=1;public int Round{get;set;}public int SpentMines{get;set;}public int SpentCaches{get;set;}public Placement[]? Placements{get;set;}}
 public sealed class Placement{public MiningDefenseKind Kind{get;set;}public float X{get;set;}public float Y{get;set;}public float Z{get;set;}public float Yaw{get;set;}}
}
