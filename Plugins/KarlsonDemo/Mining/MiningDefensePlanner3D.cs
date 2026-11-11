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
public sealed partial class MiningDefensePlanner3D:Component
{
 [SceneObjectReference] public Guid WorkshopObjectId{get;set;}
 [SceneObjectReference] public Guid CameraObjectId{get;set;}
 public float SentryDamage{get;set;}=18;public float SentryInterval{get;set;}=.55f;public float RepeaterDamage{get;set;}=8;public float RepeaterInterval{get;set;}=.25f;public float MortarDamage{get;set;}=45;public float MortarInterval{get;set;}=5;public float MineDamage{get;set;}=75;public float DefenceRange{get;set;}=22;public float MortarRange{get;set;}=28;public float MortarBlastRadius{get;set;}=10;public float MineBlastRadius{get;set;}=5;
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
 public float DefenceMinimumMuzzleHeight{get;set;}=1.4f;
 public float PlacementRange{get;set;}=30;
 public float MinimumSpacing{get;set;}=1.4f;
 public Key BuildKey{get;set;}=Key.B;
 public Key SentryKey{get;set;}=Key.F1;public Key RepeaterKey{get;set;}=Key.F2;public Key MortarKey{get;set;}=Key.F3;public Key MineKey{get;set;}=Key.F4;public Key SupplyKey{get;set;}=Key.F5;public Key BarricadeKey{get;set;}=Key.F6;public Key ExtraRefineryKey{get;set;}=Key.F7;public Key ResearchStationKey{get;set;}=Key.F8;
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
 public IEnumerable<MiningDefense3D> LiveDefences=>placed.Where(p=>p.Root.Scene==GameObject.Scene).Select(p=>p.Root.GetComponent<MiningDefense3D>()).OfType<MiningDefense3D>();
 public int MineReplacementOreCost{get;set;}=10;
 public int ReplacementCost(MiningDefenseKind kind)=>retired.Any(p=>p.Kind==kind&&p.Destroyed)?Math.Max(0,kind==MiningDefenseKind.Mine?MineReplacementOreCost:ReplacementOreCost):0;
 public void Retire(MiningDefense3D defence,bool destroyed)
 {
  var item=placed.FirstOrDefault(p=>p.Root==defence.GameObject);if(item.Root==null)return;
  bool purchased=purchasedMines.Remove(item.Root.Id);var v=item.Root.Transform.WorldPosition;retired.Add(new(){Purchased=purchased,Kind=item.Kind,X=v.X,Y=v.Y,Z=v.Z,Yaw=GetYaw(item.Root),Destroyed=destroyed||item.Kind==MiningDefenseKind.Mine});placed.Remove(item);
  Save();
 }
 public void RearmMines(){spentMines=0;Save();}

 public int ReplacementOreCost{get;set;}=20;
 readonly List<Placement> retired=[];
 readonly HashSet<Guid> purchasedMines=[];
 readonly List<(MiningDefenseKind Kind,GameObject Root)> placed=[];int spentMines,spentCaches,round=-1;GameObject? marker;
 MiningTrade3D? Trade=>GameObject.Scene?.FindGameObject(WorkshopObjectId)?.GetComponent<MiningTrade3D>();
 MiningResearchStation3D? Research=>Trade?.Operation?.GameObject.GetComponent<MiningResearchStation3D>();
 public int Allowance(MiningDefenseKind kind)=>kind==MiningDefenseKind.ResearchStation?(Research?.StationKitAvailable==true?1:0):kind==MiningDefenseKind.ExtraRefinery?Research?.RefineryStock??0:kind==MiningDefenseKind.Barricade?Research?.BarricadesOwned??0:Trade is {} t?Math.Clamp((int)t.Stat(kind switch{MiningDefenseKind.Sentry=>"AutogunGrant",MiningDefenseKind.Repeater=>"UnlockMachineGun",MiningDefenseKind.Mortar=>"MortarStrikeGrant",MiningDefenseKind.SupplyCache=>"UnlockAirdrop",_=>"LandMineGrant"}),0,12):0;
 public int Available(MiningDefenseKind kind)=>kind==MiningDefenseKind.Mine?(Research?.MineStock??0)+retired.Count(p=>p.Kind==kind&&p.Purchased)+Math.Max(0,Allowance(kind)-placed.Count(p=>p.Kind==kind&&!purchasedMines.Contains(p.Root.Id))):kind==MiningDefenseKind.ResearchStation?(Research?.StationKitAvailable==true?1:0):kind==MiningDefenseKind.ExtraRefinery?Research?.RefineryStock??0:kind==MiningDefenseKind.Barricade?Research?.BarricadeStock??0:Math.Max(0,Allowance(kind)-placed.Count(p=>p.Kind==kind)-(kind==MiningDefenseKind.Mine?spentMines:kind==MiningDefenseKind.SupplyCache?spentCaches:0));
 public void CycleBuild()
 {if(GameObject.GetComponent<MiningCampLayout3D>()?.IsEditing==true)return;var kinds=Enum.GetValues<MiningDefenseKind>().Where(k=>Available(k)>0).ToArray();if(kinds.Length==0){BuildMode=false;Status="Research a defence licence at the company tent.";return;}int index=BuildMode?Array.IndexOf(kinds,SelectedKind)+1:0;if(index>=kinds.Length){BuildMode=false;return;}SelectedKind=kinds[index];BuildMode=true;}
 public bool SelectBuild(MiningDefenseKind kind){if(GameObject.GetComponent<MiningCampLayout3D>()?.IsEditing==true||!Enum.IsDefined(kind)||Trade?.Operation?.Phase==MiningPhase.Failed||Available(kind)<=0)return false;SelectedKind=kind;BuildMode=true;Status=ReplacementCost(kind)>0?$"Replacement: {ReplacementCost(kind)} refined ore on placement.":"Choose a placement.";return true;}
 public void Cancel(){BuildMode=false;HideBarricadePreview();}
 public bool TryPlace(Vector3 point)
 {
  var op=Trade?.Operation;if(op==null||op.Phase==MiningPhase.Failed||Available(SelectedKind)<=0||!float.IsFinite(point.X)||!float.IsFinite(point.Y)||!float.IsFinite(point.Z)||Vector3.Distance(point,Transform.WorldPosition)>Math.Clamp(PlacementRange,1,100)||placed.Any(p=>Vector3.Distance(p.Root.Transform.WorldPosition,point)<MinimumSpacing))return false;
  if(GameplayQuery3D.OverlapSphere(GameObject.Scene!,point+Vector3.UnitY*.7f,.5f,GameObject,includeTriggers:false).Any())return false;
  if(SelectedKind==MiningDefenseKind.ResearchStation){if(Research?.PlaceStation(point,PlacementYaw)!=true)return false;placementTime=Time.TotalTime;BuildMode=Available(SelectedKind)>0;Status="Field R&D deployed.";return true;}
  if(SelectedKind==MiningDefenseKind.ExtraRefinery){if(Research?.PlaceRefinery(point,PlacementYaw)!=true)return false;placementTime=Time.TotalTime;BuildMode=Available(SelectedKind)>0;Status="Extra refinery deployed.";return true;}
  if(SelectedKind==MiningDefenseKind.Barricade){if(Research?.PlaceBarricade(point,PlacementYaw)!=true)return false;placementTime=Time.TotalTime;BuildMode=Research.BarricadeStock>0;Status=BuildMode?"Barricade placed. Click to place another / RMB to finish.":"Barricade deployed. No kits remaining.";return true;}
  var replacement=retired.FirstOrDefault(p=>p.Kind==SelectedKind&&p.Destroyed);if(replacement!=null){int repair=ReplacementCost(SelectedKind);if(!op.SpendRefinedOre(repair)){Status=$"Replacement needs {repair} refined ore.";return false;}retired.Remove(replacement);}
  bool newKit=replacement==null&&SelectedKind==MiningDefenseKind.Mine&&Research?.MineStock>0;bool purchased=newKit||replacement?.Purchased==true;Spawn(SelectedKind,point,PlacementYaw,purchased);if(newKit)Research!.ConsumeMineKit();placementTime=Time.TotalTime;BuildMode=Available(SelectedKind)>0;Status="Defence deployed.";Save();return true;
 }
 void Spawn(MiningDefenseKind kind,Vector3 point,float yaw=0,bool purchased=false,bool depleted=false)
 {
  var t=Trade!;var root=GameObject.Scene!.CreateGameObject("Company "+kind);if(purchased)purchasedMines.Add(root.Id);root.Transform.WorldPosition=point;root.Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw*MathF.PI/180);
  root.AddComponent(new MiningDefense3D{ProjectileSpeed=DefenceProjectileSpeed,TracerLength=DefenceTracerLength,TracerWidth=DefenceTracerWidth,ProjectileRadius=DefenceProjectileRadius,OperationObjectId=t.OperationObjectId,WorkshopObjectId=WorkshopObjectId,Kind=kind,VisualModel=kind switch{MiningDefenseKind.Sentry=>SentryModel,MiningDefenseKind.Repeater=>RepeaterModel,MiningDefenseKind.Mortar=>MortarModel,MiningDefenseKind.Mine=>MineModel,_=>SupplyCacheModel},VisualScale=DefenceModelScale,MinimumGunMuzzleHeight=DefenceMinimumMuzzleHeight,BaseDamage=kind==MiningDefenseKind.Mine?MineDamage:kind==MiningDefenseKind.Mortar?MortarDamage:kind==MiningDefenseKind.Repeater?RepeaterDamage:SentryDamage,BaseInterval=kind==MiningDefenseKind.Mortar?MortarInterval:kind==MiningDefenseKind.Repeater?RepeaterInterval:SentryInterval,Range=kind==MiningDefenseKind.Mine?3:kind==MiningDefenseKind.Mortar?MortarRange:DefenceRange,BlastRadius=kind==MiningDefenseKind.Mine?MineBlastRadius:kind==MiningDefenseKind.Mortar?MortarBlastRadius:4});if(depleted)root.GetComponent<MiningDefense3D>()!.RestoreDepleted();placed.Add((kind,root));
 }
 protected override void OnStart()
 {
  if(!PersistProgress)return;try{if(File.Exists(PathName)){var s=JsonSerializer.Deserialize<State>(File.ReadAllText(PathName));if(s?.Version==1){spentCaches=Math.Clamp(s.SpentCaches,0,100);spentMines=Math.Clamp(s.SpentMines,0,100);round=s.Round;retired.AddRange(s.Retired??[]);foreach(var lost in retired.Where(p=>p.Kind==MiningDefenseKind.Mine))lost.Destroyed=true;spentMines=0;foreach(var p in (s.Placements??[]).Take(10000))if(Enum.IsDefined(p.Kind)&&float.IsFinite(p.X)&&float.IsFinite(p.Y)&&float.IsFinite(p.Z))Spawn(p.Kind,new(p.X,p.Y,p.Z),float.IsFinite(p.Yaw)?p.Yaw:0,p.Purchased,p.Depleted);}}}catch(Exception e)when(e is IOException or JsonException or UnauthorizedAccessException){Status=e.Message;}
 }
 protected override void OnUpdate()
 {
  if(Trade?.Operation is not {} op)return;
  CollectRemoved();
  if(round!=op.CompletedRounds){round=op.CompletedRounds;spentMines=spentCaches=0;Save();}
  // A HUD click can select a defence while the host still has the UI cursor.
  // Preserve that selection until the host restores gameplay capture.
  if(!Input.IsGameInputCaptured||Input.IsUiPointerRequested){PlacementValid=false;if(marker!=null)marker.Active=false;HideBarricadePreview();return;}
  if(Input.IsKeyPressed(BuildKey))CycleBuild();if(BuildMode){if(Input.IsKeyPressed(Key.Q))RotatePlacement(-15);if(Input.IsKeyPressed(Key.E))RotatePlacement(15);}
  var shortcuts=new[]{SentryKey,RepeaterKey,MortarKey,MineKey,SupplyKey,BarricadeKey,ExtraRefineryKey,ResearchStationKey};for(int i=0;i<shortcuts.Length;i++)if(Input.IsKeyPressed(shortcuts[i]))SelectBuild((MiningDefenseKind)i);if(BuildMode&&Input.IsMouseButtonPressed(MouseButton.Right))Cancel();
  if(marker==null){marker=GameObject.Scene!.CreateGameObject("Defence placement preview");marker.Transform.LocalScale=new(1,.03f,1);marker.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Cylinder,Material=new(){BaseColor=new(.2f,.9f,.3f,1)}});}
  marker.Active=false;if(!BuildMode)HideBarricadePreview();
  if(!BuildMode)return;
  PlacementValid=false;
  if(GameObject.Scene?.FindGameObject(CameraObjectId) is {} camera&&GameplayQuery3D.Raycast(GameObject.Scene,camera.Transform.WorldPosition,camera.Transform.Forward,out var hit,150,GameObject,includeTriggers:false))
  {PlacementPoint=hit.Point;PlacementValid=hit.Normal.Y>.6f&&Vector3.Distance(PlacementPoint,Transform.WorldPosition)<=PlacementRange&&!placed.Any(p=>Vector3.Distance(p.Root.Transform.WorldPosition,PlacementPoint)<MinimumSpacing);}
  UpdateBarricadePreview();
  marker.Transform.WorldPosition=PlacementPoint+Vector3.UnitY*.03f;marker.GetComponent<MeshRenderer>()!.Material.BaseColor=PlacementValid?new(.2f,.9f,.3f,1):new(1,.15f,.05f,1);
  if(PlacementValid&&Input.IsMouseButtonPressed(MouseButton.Left))TryPlace(PlacementPoint);
 }

 string PathName{get{if(System.IO.Path.GetFileName(SaveSlot)!=SaveSlot)throw new InvalidDataException("Defence save must be a filename.");return System.IO.Path.Combine(GameSaveStorage.GetDirectory(GameObject.Scene!.ProjectRoot!),SaveSlot);}}
 public bool LastSaveSucceeded{get;private set;}=true;
 public bool Owns(GameObject root)=>placed.Any(p=>p.Root==root&&root.Scene==GameObject.Scene);
 public bool SaveRelocation(GameObject root){if(!Owns(root))return false;Save();return LastSaveSucceeded;}
 static float GetYaw(GameObject root){var q=root.Transform.WorldRotation;return MathF.Atan2(2*(q.W*q.Y+q.X*q.Z),1-2*(q.Y*q.Y+q.Z*q.Z))*180/MathF.PI;}
 void CollectRemoved(){foreach(var p in placed.Where(p=>p.Root.Scene!=GameObject.Scene).ToArray()){bool purchased=purchasedMines.Remove(p.Root.Id);bool destroyed=p.Root.GetComponent<MiningDefense3D>()?.DestroyedByEnemy==true;if(destroyed||p.Kind==MiningDefenseKind.Mine){var v=p.Root.Transform.WorldPosition;retired.Add(new(){Purchased=purchased,Kind=p.Kind,X=v.X,Y=v.Y,Z=v.Z,Yaw=GetYaw(p.Root),Destroyed=true});}if(p.Kind==MiningDefenseKind.SupplyCache)spentCaches++;placed.Remove(p);}}

 public void Save(){LastSaveSucceeded=true;CollectRemoved();if(!PersistProgress||AttachedGameObject?.Scene==null)return;try{Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathName)!);File.WriteAllText(PathName+".tmp",JsonSerializer.Serialize(new State{Retired=retired.ToArray(),SpentCaches=spentCaches,SpentMines=spentMines,Round=round,Placements=placed.Where(p=>p.Root.Scene==GameObject.Scene).Select(p=>new Placement{Depleted=p.Root.GetComponent<MiningDefense3D>()?.Spent==true,Purchased=purchasedMines.Contains(p.Root.Id),Kind=p.Kind,X=p.Root.Transform.WorldPosition.X,Y=p.Root.Transform.WorldPosition.Y,Z=p.Root.Transform.WorldPosition.Z,Yaw=GetYaw(p.Root)}).ToArray()}));File.Move(PathName+".tmp",PathName,true);}catch(Exception e)when(e is IOException or UnauthorizedAccessException){LastSaveSucceeded=false;Status=e.Message;}}
 protected override void OnStop(){HideBarricadePreview();Save();}
 public sealed class State{public int Version{get;set;}=1;public int Round{get;set;}public int SpentMines{get;set;}public int SpentCaches{get;set;}public Placement[]? Retired{get;set;}public Placement[]? Placements{get;set;}}
 public sealed class Placement{public bool Depleted{get;set;}public bool Purchased{get;set;}public bool Destroyed{get;set;}public MiningDefenseKind Kind{get;set;}public float X{get;set;}public float Y{get;set;}public float Z{get;set;}public float Yaw{get;set;}}
}
