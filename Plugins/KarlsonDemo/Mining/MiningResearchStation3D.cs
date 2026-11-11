using System.Numerics;
using System.ComponentModel;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;
using Component=ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;
/// <summary>Early jam support shop. Unlock owns the station; each order is a separate consumable kit.</summary>
public sealed partial class MiningResearchStation3D:Component
{
 [SceneObjectReference]public Guid OperationObjectId{get;set;}
 public AssetReference StationBlueprint{get;set;}=AssetReference.Empty;
 public AssetReference DroneBlueprint{get;set;}=AssetReference.Empty;
 public AssetReference BarricadeBlueprint{get;set;}=AssetReference.Empty;
 public Vector3 StationPosition{get;set;}=new(-4,0,8);
 public string UnlockId{get;set;}="rd";
 public int DroneCost{get;set;}=108;
 public int BarricadeOreCost{get;set;}=10;
 public int MineOreCost{get;set;}=15;
 public int MineStock{get;private set;}
 public int MaximumDrones{get;set;}=3;
 public float DroneBatterySeconds{get;set;}=45;
 public int ActiveDroneCount=>GameObject.Scene?.GetComponentsSnapshot<MiningSecurityDrone3D>().Count(d=>!d.Detonated&&d.OperationObjectId==OperationObjectId)??0;
 public AssetReference RefineryBlueprint{get;set;}=new("Assets/Blueprints/Mining/Research/ExtraRefinery.byteblueprint");
 public int ExtraRefineryCost{get;set;}=250;
 public int RefineryStock{get;private set;}
 public GameObject? DeployedStation=>station?.Scene==GameObject.Scene?station:null;
 public bool StationKitAvailable=>DeployedStation==null&&Op?.GameObject.GetComponent<MiningTrade3D>()?.HasResearch(UnlockId)==true;
 readonly List<GameObject> extraRefineries=[];
 public int MaximumBarricades{get;set;}=0;
 public bool IsOpen{get;private set;}
 public string Status{get;private set;}="";
 public override int UpdateOrder=>-1200;
 public override int? RenderOrder=>950;
 GameObject? station;readonly List<GameObject> barricades=[];bool restoreControl,loaded;int selected;float saveClock;
 public IReadOnlyList<GameObject> DeployedBarricades=>barricades;
 public int BarricadeStock{get;private set;}
 public int BarricadesOwned=>BarricadeStock+barricades.Count(b=>b.Scene==GameObject.Scene&&b.GetComponent<HealthComponent>() is {IsDead:false});
 MiningOperation3D? Op=>AttachedGameObject?.Scene?.FindGameObject(OperationObjectId)?.GetComponent<MiningOperation3D>();
 GameObject? Player=>Op is {} op?GameObject.Scene?.FindGameObject(op.PlayerObjectId):null;
 MiningBusinessUi3D? Ui=>GameObject.Scene?.GetComponentsSnapshot<MiningBusinessUi3D>().FirstOrDefault();
 protected override void OnUpdate()
 {
  var op=Op;if(op==null)return;var trade=op.GameObject.GetComponent<MiningTrade3D>();
  if(!loaded){loaded=true;op.Initialize();Restore();}
  if((saveClock-=(float)Time.DeltaTime)<=0){saveClock=1;Capture();}
  if(station?.Scene!=GameObject.Scene)station=null;
  bool near=station!=null&&MiningCampInteraction.Nearest(GameObject.Scene!,op)==station;
  if(IsOpen&&(!near||!op.CanUseIndustry)){Close();return;}
  if(!IsOpen){if(near&&Player?.GetComponent<MiningDefensePlanner3D>()?.BuildMode!=true&&op.CanUseIndustry&&Ui?.Screen==MiningScreen.None&&!Input.IsUiPointerRequested&&Input.IsKeyPressed(Key.E))Open();return;}
  if(Input.IsKeyPressed(Key.Escape)){MiningPauseMenu3D.IgnoreEscapeAt=Time.TotalTime;Close();return;}
  TickShopInput();
 }
 public void Open(){if(IsOpen||station==null||Op?.CanUseIndustry!=true)return;IsOpen=true;Input.ConsumeGameplayPointer();Input.RequestUiPointer(this,true);if(Player?.GetComponent<PlayerController3D>() is {} control){restoreControl=control.Enabled;control.Enabled=false;}Status="Choose support. Orders are consumed when destroyed or depleted.";}
 public void Close(){if(!IsOpen)return;IsOpen=false;Input.ConsumeGameplayPointer();Input.RequestUiPointer(this,false);if(Player?.GetComponent<PlayerController3D>() is {} control)control.Enabled=restoreControl;}
 public bool PlaceStation(Vector3 point,float yaw=0){if(!StationKitAvailable||Op is not {} op||!op.CanUseIndustry||!float.IsFinite(point.LengthSquared())||StationBlueprint.IsEmpty)return false;station=RuntimeSpawnService.SpawnBlueprint(GameObject.Scene!,StationBlueprint,point);if(station==null)return false;station.Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw*MathF.PI/180)*station.Transform.WorldRotation;StationPosition=point;PersistSupport();Status="Field R&D deployed.";return true;}
 public bool Buy(int item)
 {
  var op=Op;var player=Player;if(op==null||player==null||!op.CanUseIndustry||station==null)return false;
  if(item==0){
   if(ActiveDroneCount>=MaximumDrones){Status=$"All {MaximumDrones} drone slots are in use.";return false;}
   if(op.Credits<DroneCost){Status=$"Need {DroneCost} camp credits.";return false;}
   var root=RuntimeSpawnService.SpawnBlueprint(GameObject.Scene,DroneBlueprint,player.Transform.WorldPosition+new Vector3(1,1.7f,0));if(root==null){Status="Drone blueprint is unavailable.";return false;}
   if(!op.SpendCredits(DroneCost)){GameObject.Scene.DestroyGameObject(root);return false;}if(root.GetComponent<MiningSecurityDrone3D>() is {} drone){drone.OperationObjectId=OperationObjectId;drone.BatterySeconds=DroneBatterySeconds;drone.RestoreBattery(drone.BatterySeconds);}Status=$"Drone deployed / {ActiveDroneCount} of {MaximumDrones}.";
  }else if(item==1){
   if(MaximumBarricades>0&&BarricadesOwned>=MaximumBarricades){Status="Barricade limit reached.";return false;}
   if(BarricadeBlueprint.IsEmpty){Status="Barricade blueprint is unavailable.";return false;}
   if(!op.SpendRefinedOre(BarricadeOreCost)){Status=$"Need {BarricadeOreCost} refined ore.";return false;}
   BarricadeStock++;Status="Barricade kit ready. Close this menu, then use F6 or B to place it.";
  }else if(item==3){if(RefineryBlueprint.IsEmpty||!op.SpendCredits(ExtraRefineryCost)){Status="Not enough credits for another refinery.";return false;}RefineryStock++;Status="Refinery kit ready. Close menu, then F7 or B to place.";
  }else if(item==4){if(!op.SpendRefinedOre(MineOreCost)){Status=$"Need {MineOreCost} refined ore.";return false;}MineStock++;Status="Mine kit ready / F4 to place.";
  }else if(item==2){if(!op.UpgradeCargo()){Status="Not enough camp credits for cargo expansion.";return false;}Status=$"Cargo capacity increased to {op.CargoCapacity}.";}else return false;
  Capture();op.SaveProgress();MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.TerminalPurchase);return true;
 }
 public bool ConsumeMineKit(){if(MineStock<=0)return false;MineStock--;PersistSupport();return true;}
 public bool PlaceBarricade(Vector3 point,float? yaw=null)
 {
  if(BarricadeStock<=0||Op is not {} op||op.Phase==MiningPhase.Failed||!float.IsFinite(point.LengthSquared())||Player is not {} player)return false;
  var root=RuntimeSpawnService.SpawnBlueprint(GameObject.Scene!,BarricadeBlueprint,point);if(root==null){Status="Barricade blueprint is unavailable. Kit retained.";return false;}
  root.Transform.WorldRotation=yaw is {} angle?Quaternion.CreateFromAxisAngle(Vector3.UnitY,angle*MathF.PI/180):player.Transform.WorldRotation;barricades.Add(root);BarricadeStock--;Capture();op.SaveProgress();Status="Barricade deployed.";return true;
 }
 public bool PlaceRefinery(Vector3 point,float yaw=0){if(RefineryStock<=0||Op is not {} op||!float.IsFinite(point.LengthSquared()))return false;var root=RuntimeSpawnService.SpawnBlueprint(GameObject.Scene!,RefineryBlueprint,point);if(root==null)return false;root.Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw*MathF.PI/180)*root.Transform.WorldRotation;if(root.GetComponent<MiningAuxiliaryRefinery3D>() is {} local)local.OperationObjectId=OperationObjectId;RefineryStock--;extraRefineries.Add(root);Capture();op.SaveProgress();return true;}
 public void PersistSupport(){Capture();Op?.SaveProgress();}
 protected override void OnRender(RenderContext c)
 {
  if(station==null||Op is not {} op||Player is not {} player)return;float s=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f),w=c.TargetWidth/s,h=c.TargetHeight/s;var font=Ui?.BodyFont??AssetReference.Empty;var yellow=new Vector4(1,.78f,.1f,1);
  void Q(float x,float y,float width,float height,Vector4 col)=>c.DrawUiQuad(new(x*s,y*s),new(width*s,height*s),col);
  void T(string text,float x,float y,int size=18,Vector4? color=null)=>c.DrawUiText(text,font,(int)(size*s),new(x*s,y*s),color??Vector4.One,UiAnchor.TopLeft);
  if(!IsOpen){if(op.CanUseIndustry&&Ui?.Screen==MiningScreen.None&&MiningCampGeometry.SurfaceDistance(station,player.Transform.WorldPosition)<2.8f){Q(w/2-130,h-125,260,36,new(.03f,.04f,.05f,.95f));T("[E] Field R&D",w/2-65,h-118,20,yellow);}return;}
  DrawShop(c,op,s,w,h);
 }
 public sealed class SupportSave{public Vector3? Station{get;set;}public Quaternion StationRotation{get;set;}=Quaternion.Identity;public int MineStock{get;set;}public List<DroneSave> Drones{get;set;}=[];public int BarricadeStock{get;set;}public int RefineryStock{get;set;}public List<RefinerySave> Refineries{get;set;}=[];public List<BarrierSave> Barriers{get;set;}=[];public float DroneBattery{get;set;}public Vector3 DronePosition{get;set;}}
 public sealed class DroneSave{public Vector3 Position{get;set;}public float Battery{get;set;}}
 public sealed class RefinerySave{public Vector3 Position{get;set;}public Quaternion Rotation{get;set;}public bool AutoLoad{get;set;}public int Raw{get;set;}public int Completed{get;set;}public int BatchLevel{get;set;}public int SpeedLevel{get;set;}public int YieldLevel{get;set;}public float Remaining{get;set;}public double Fraction{get;set;}public int Capacity{get;set;}=50;}
 public sealed class BarrierSave{public Vector3 Position{get;set;}public Quaternion Rotation{get;set;}public float Health{get;set;}}
 static readonly System.Text.Json.JsonSerializerOptions SaveOptions=new(){IncludeFields=true};
 void Capture(){if(!loaded||Op is not {} op)return;var drone=GameObject.Scene?.GetComponentsSnapshot<MiningSecurityDrone3D>().FirstOrDefault(d=>!d.Detonated&&d.OperationObjectId==OperationObjectId);var state=new SupportSave{MineStock=MineStock,Drones=GameObject.Scene!.GetComponentsSnapshot<MiningSecurityDrone3D>().Where(d=>!d.Detonated&&d.OperationObjectId==OperationObjectId).Select(d=>new DroneSave{Position=d.Transform.WorldPosition,Battery=d.BatteryRemaining}).ToList(),RefineryStock=RefineryStock,BarricadeStock=BarricadeStock,Station=station?.Transform.WorldPosition,StationRotation=station?.Transform.WorldRotation??Quaternion.Identity,DroneBattery=drone?.BatteryRemaining??0,DronePosition=drone?.Transform.WorldPosition??default};foreach(var b in barricades)if(b.Scene==GameObject.Scene&&b.GetComponent<HealthComponent>() is {IsDead:false} hp)state.Barriers.Add(new(){Position=b.Transform.WorldPosition,Rotation=b.Transform.WorldRotation,Health=hp.CurrentHealth});foreach(var r in extraRefineries)if(r.Scene==GameObject.Scene&&r.GetComponent<MiningAuxiliaryRefinery3D>() is {} extra)state.Refineries.Add(new(){Position=r.Transform.WorldPosition,Rotation=r.Transform.WorldRotation,AutoLoad=extra.AutoLoad,Raw=extra.LoadedRaw,Completed=extra.CompletedOre,BatchLevel=extra.BatchLevel,SpeedLevel=extra.SpeedLevel,YieldLevel=extra.YieldLevel,Remaining=extra.SavedRemaining,Fraction=extra.SavedRemainder,Capacity=extra.LoadCapacity});op.ResearchSupportState=System.Text.Json.JsonSerializer.Serialize(state,SaveOptions);}
 void Restore(){if(Op is not {} op||string.IsNullOrEmpty(op.ResearchSupportState))return;try{var state=System.Text.Json.JsonSerializer.Deserialize<SupportSave>(op.ResearchSupportState,SaveOptions);if(state==null)return;MineStock=Math.Clamp(state.MineStock,0,1000000);RefineryStock=Math.Clamp(state.RefineryStock,0,1000000);foreach(var r in state.Refineries){if(!float.IsFinite(r.Position.LengthSquared()))continue;var root=RuntimeSpawnService.SpawnBlueprint(GameObject.Scene!,RefineryBlueprint,r.Position);if(root==null)continue;root.Transform.WorldRotation=r.Rotation;extraRefineries.Add(root);if(root.GetComponent<MiningAuxiliaryRefinery3D>() is {} extra){extra.OperationObjectId=OperationObjectId;extra.AutoLoad=r.AutoLoad;extra.LoadCapacity=Math.Clamp(r.Capacity,1,50);extra.Restore(r.Raw,r.Completed);extra.RestoreSettings(r.BatchLevel,r.SpeedLevel,r.YieldLevel,r.Remaining,r.Fraction);}}
 BarricadeStock=Math.Clamp(state.BarricadeStock,0,MaximumBarricades>0?MaximumBarricades:1000000);if(state.Station is {} pos&&float.IsFinite(pos.LengthSquared())){StationPosition=pos;station=RuntimeSpawnService.SpawnBlueprint(GameObject.Scene!,StationBlueprint,pos);if(station!=null&&float.IsFinite(state.StationRotation.LengthSquared())&&state.StationRotation.LengthSquared()>.5f)station.Transform.WorldRotation=Quaternion.Normalize(state.StationRotation);}foreach(var b in state.Barriers.Take(MaximumBarricades>0?Math.Max(0,MaximumBarricades-BarricadeStock):10000)){if(!float.IsFinite(b.Position.LengthSquared()))continue;var root=RuntimeSpawnService.SpawnBlueprint(GameObject.Scene!,BarricadeBlueprint,b.Position);if(root==null)continue;if(float.IsFinite(b.Rotation.LengthSquared())&&b.Rotation.LengthSquared()>.5f)root.Transform.WorldRotation=Quaternion.Normalize(b.Rotation);if(root.GetComponent<HealthComponent>() is {} hp)hp.CurrentHealth=Math.Clamp(b.Health,1,hp.MaxHealth);barricades.Add(root);}var savedDrones=state.Drones.Count>0?state.Drones:state.DroneBattery>0?new List<DroneSave>{new(){Position=state.DronePosition,Battery=state.DroneBattery}}:[];foreach(var saved in savedDrones.Take(MaximumDrones)){if(saved.Battery<=0||!float.IsFinite(saved.Position.LengthSquared()))continue;var root=RuntimeSpawnService.SpawnBlueprint(GameObject.Scene!,DroneBlueprint,saved.Position);if(root?.GetComponent<MiningSecurityDrone3D>() is {} restored){restored.OperationObjectId=OperationObjectId;restored.BatterySeconds=DroneBatterySeconds;restored.RestoreBattery(saved.Battery);}}}catch(System.Text.Json.JsonException){Status="Support save could not be restored.";}}
 protected override void OnStop(){Close();Capture();if(loaded)Op?.SaveProgress();}
}
