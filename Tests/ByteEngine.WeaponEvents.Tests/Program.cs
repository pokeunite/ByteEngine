using System.Numerics;
using System.Reflection;
using ByteEngine.Core;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Physics;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
using ByteEngine.Core.VisualLogic;
using KarlsonDemo.Plugin;
using ByteEngine.Editor.Panels;
using ByteEngine.Core.Animation;

HuntCombatTests.Run();
CombatPolishTests.Run();
NavigationFireballTests.Run();
EnemyTacticsTests.Run();
BossOpeningTests.Run();
GunProjectileTests.Run();
AIBehaviorTests.Run();
AICombatRoleTests.Run();
try { AIAdvancedTests.Run(); } catch(Exception e) { Console.Error.WriteLine(e); Environment.Exit(1); }

const string Prefix="bytebard.karlsondemo.";
{
 var filled=UiWidget.ResolveFill(new(10,20),new(200,30),.25f,UiFillDirection.RightToLeft);
 if(filled.Position!=new Vector2(160,20)||filled.Size!=new Vector2(50,30))throw new Exception("Reversed progress geometry is incorrect");
 var vertical=UiWidget.ResolveFill(Vector2.Zero,new(40,60),.5f,UiFillDirection.BottomToTop);
 if(vertical.Position.Y!=30||vertical.Size.Y!=30)throw new Exception("Cooldown fill geometry is incorrect");
 Console.WriteLine("PASS mirrored health and vertical cooldown fills crop without stretching");
}
Check(Math.Abs(AnimationBlendWeights.PlaybackScale(52.083334f,new float[]{0,1},new float[]{52.083334f,.625f})-83.333336f)<.001f,"Long idle references preserve short run-cycle timing instead of clamping to slow motion");
{
 var directional=new AnimationBlendSpace { TwoDimensional=true, Blend2DMode=AnimationBlend2DMode.DirectionalSpeed,
 Samples=[new(){Clip="idle"},new(){Clip="forward",Y=2},new(){Clip="right",X=2},new(){Clip="back",Y=-2},new(){Clip="left",X=-2},new(){Clip="run",Y=6}] };
 var weights=new float[6];
 for(int angle=0;angle<360;angle++)
 {
  float r=angle*MathF.PI/180;AnimationBlendWeights.Evaluate(directional,2*MathF.Sin(r),2*MathF.Cos(r),weights);
  CheckBlend(weights,angle);
 }
 void CheckBlend(float[] w,int angle) {if(w[0]>.0001f||w[5]>.0001f||Math.Abs(w.Sum()-1)>.0001f||w.Any(v=>!float.IsFinite(v)||v<0))throw new Exception("Walking blend leaks idle/run at angle "+angle);}
 Check(true,"Every walking angle blends walking clips only, without idle/run leaking into diagonals");
 AnimationBlendWeights.Evaluate(directional,0,4,weights);Check(Math.Abs(weights[1]-.5f)<.0001f&&Math.Abs(weights[5]-.5f)<.0001f,"Forward speed interpolates walk to run independently of direction");
 AnimationBlendWeights.Evaluate(directional,0,0,weights);Check(weights[0]==1,"Directional blending preserves idle at zero speed");
}
typeof(ByteEnginePluginRegistry).GetMethod("BeginPlugin",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,["bytebard.karlsondemo"]);
var pluginContext=(ByteEnginePluginContext)Activator.CreateInstance(typeof(ByteEnginePluginContext),BindingFlags.Instance|BindingFlags.NonPublic,null,["bytebard.karlsondemo"],null)!;new KarlsonDemoPlugin().Register(pluginContext);
var registry=VisualLogicRegistry.CreateDefault();
var scene=new Scene("Weapon events regression");var player=scene.CreateGameObject("Player");
var camera=scene.CreateGameObject("Camera");camera.SetParent(player,false);camera.AddComponent(new Camera3D{ActiveGameCamera=true});
var gun=scene.CreateGameObject("Gun");gun.SetParent(camera,false);
var definition=gun.AddComponent(new WeaponDefinition3D{MagazineCapacity=20,ReserveCapacity=100,RoundsPerMinute=600,EquipSeconds=0,ReloadSeconds=.1f,EmptyReloadSeconds=.1f,MuzzleFlashEnabled=false,ShowShotTrace=false});
var weapon=gun.AddComponent(new WeaponInstance3D{CurrentAmmo=20,ReserveAmmo=40});
var equipment=player.AddComponent(new FpsGunLab3D{CameraObjectId=camera.Id,InventoryRootId=camera.Id,UseBuiltInFireInput=false});
var target=scene.CreateGameObject("Target");target.Transform.LocalPosition=new(0,0,-5);target.AddComponent(new BoxCollider3D{Size=new(2)});target.AddComponent(new HealthComponent{CurrentHealth=1000,MaxHealth=1000});
typeof(Scene).GetMethod("LoadInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(scene,null);
void Tick(float dt=.2f)=>typeof(WeaponInstance3D).GetMethod("Tick",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(weapon,[dt]);
void Check(bool value,string label){if(!value)throw new Exception(label);Console.WriteLine("PASS "+label);}
equipment.Select(0);Tick();scene.Physics.Step(scene,1f/60);
EventExecutionContext Context(GameObject? self=null)=>new(){Scene=scene,Self=self??gun,Globals=new VariableStore(),WarningSink=message=>throw new Exception(message)};
VisualInstruction I(string id,params (string Key,EventValue Value)[] args)=>new(){Id=id,Arguments=args.ToDictionary(a=>a.Key,a=>a.Value)};
void Action(VisualInstruction instruction,EventExecutionContext context){if(!registry.TryGetAction(instruction.Id,out var action))throw new Exception(instruction.Id);action!.Execute(instruction,context);}
bool Condition(VisualInstruction instruction,EventExecutionContext context){if(!registry.TryGetCondition(instruction.Id,out var condition))throw new Exception(instruction.Id);return condition!.Evaluate(instruction,context);}
var hitCondition=I(Prefix+"hit");var otherHitCondition=I(Prefix+"hit");var firedCondition=I(Prefix+"fired");
Check(!Condition(hitCondition,Context())&&!Condition(otherHitCondition,Context(player))&&!Condition(firedCondition,Context()),"Observers establish a clean baseline before firing");
var context=Context();Action(I(Prefix+"fire"),context);
Check(weapon.CurrentAmmo==19&&weapon.ShotsFired==1,"Fire action accepts a weapon-component Self target");
Check(context.RaycastPerformed&&context.LastRaycastHit?.GameObject==target,"Fire action publishes its actual shot hit immediately");
var point=context.LastRaycastHit!.Value.Point;
Check(Vector3.Distance(point,new(0,0,-4))<.001f&&context.LastRaycastDirection==-Vector3.UnitZ,"Shot point, distance and direction come from the actual collision");
Check(Condition(hitCondition,context)&&Condition(otherHitCondition,Context(player)),"Weapon and equipment conditions each receive the shot without consuming each other");
Action(I("physics.saveRayHit",("prefix",EventValue.String("Shot"))),context);
Check(gun.Variables.TryGet("ShotObjectId",out var id)&&id!.String==target.Id.ToString(),"Native Save Raycast Result accepts a weapon shot");
Action(I("object.setActive",("target",EventValue.String("Last Ray Hit")),("active",EventValue.Boolean(false))),context);
Check(!target.Active,"Native object actions resolve Last Ray Hit to the gun's hit object");target.Active=true;
target.Transform.LocalPosition=new(10,0,-5);
Action(I(Prefix+"useShotResult"),context);
Check(context.LastRaycastHit?.Point==point,"Reading the shot result does not cast a second ray after the target moves");
Tick();Action(I(Prefix+"fire"),context);
Check(context.RaycastPerformed&&context.LastRaycastHit==null&&weapon.ShotHits.Count==0,"A miss clears the previous shot hit");
Action(I("physics.saveRayHit",("prefix",EventValue.String("Shot"))),context);
Check(gun.Variables.TryGet("ShotObjectId",out var cleared)&&cleared!.String=="","Saving a miss clears old hit-object data");
Action(I(Prefix+"fire"),context);
Check(!context.RaycastPerformed&&context.LastRaycastHit==null,"A rejected cooldown shot cannot republish a stale hit");
target.Transform.LocalPosition=new(0,0,-5);Tick();Action(I(Prefix+"fire"),context);Tick();
Check(Condition(hitCondition,Context()),"Hit signal survives frame reset when the observing sheet runs before the firing sheet");
Check(!Condition(hitCondition,Context()),"Hit signal is not repeated in a later update");
Tick();definition.PelletsPerShot=8;Action(I(Prefix+"fire"),context);
Check(weapon.ShotHits.Count==8,"All actual shotgun pellet hits are retained");
Action(I(Prefix+"useShotResult",("hitIndex",EventValue.Number(7))),context);
Check(context.LastRaycastHit?.GameObject==target,"A specific pellet result is available without recasting");
Action(I(Prefix+"useShotResult",("hitIndex",EventValue.Number(99))),context);
Check(context.LastRaycastHit==null,"An invalid pellet index cannot return an old hit");
var reloadStarted=I(Prefix+"reloadStarted");Condition(reloadStarted,Context());
Action(I(Prefix+"reload"),Context());Check(Condition(reloadStarted,Context()),"Reload start is exposed to an event sheet");
var cancelled=I(Prefix+"reloadCancelled");Condition(cancelled,Context());equipment.CancelReload();
Check(Condition(cancelled,Context()),"Reload cancellation is exposed to an event sheet");
Check(typeof(WeaponDefinition3D).GetProperty("FireAnimation")==null,"Weapon Definition no longer hides animation routing in component fields");
// Reproduce a novice's explicitly wired AND/tag/destroy rule, with a shot guard.
var tag=scene.Classification.AddTag("Target")!;target.AddTag(tag.Id);
var other=scene.CreateGameObject("Other target");other.Transform.LocalPosition=new(5,0,-5);other.AddComponent(new BoxCollider3D{Size=new(2)});other.AddTag(tag.Id);
var shot=I(Prefix+"hit",("target",EventValue.String("Player")));
shot.Arguments["checkLastRayHit"]=EventValue.Boolean(true);
shot.Arguments["hitObject"]=EventValue.String("Self");
var tagged=I("object.hasTag",("tag",EventValue.String(tag.Id.ToString())));
var and=I("logic.and");and.ConditionInputIds=[shot.InstanceId,tagged.InstanceId];
var module=new EventModuleDefinition{Name="Tagged target hit"};module.Rules.Add(new(){HasExplicitConditionFlow=true,ConnectedConditionIds=[and.InstanceId],Conditions=[shot,and,tagged],Actions=[I("object.destroy",("target",EventValue.String("Self")))]});
var firstRuntime=new EventModuleRuntime(registry);var secondRuntime=new EventModuleRuntime(registry);
Tick();firstRuntime.Update(module,new(),scene,target);secondRuntime.Update(module,new(),scene,other);
Check(scene.FindGameObject(target.Id)!=null&&scene.FindGameObject(other.Id)!=null,"Wired tag rules do not destroy targets without a new shot");
Tick();weapon.SetAmmo(20,40);Action(I(Prefix+"fire"),Context());firstRuntime.Update(module,new(),scene,target);secondRuntime.Update(module,new(),scene,other);
Check(scene.FindGameObject(target.Id)==null&&scene.FindGameObject(other.Id)!=null,"Explicit AND graph destroys only the tagged object actually hit by the gun");

var first=I("first");var last=I("last");var draft=I("draft");
first.NextActionId=last.InstanceId;
var sheetRule=new EventRuleDefinition{HasExplicitExecutionFlow=true,FirstActionId=first.InstanceId,Actions=[last,draft,first]};
var before=System.Text.Json.JsonSerializer.Serialize(sheetRule);
var projected=EventSheetProjection.Actions(sheetRule);
Check(projected[0].Instruction==first&&projected[1].Instruction==last,"Sheet follows execution links instead of action storage order");
Check(projected[2].Instruction==draft&&!projected[2].Connected,"Unconnected actions are clearly separated from executable actions");
Check(before==System.Text.Json.JsonSerializer.Serialize(sheetRule),"Viewing the event sheet does not mutate saved logic or links");
last.NextActionId=first.InstanceId;
Check(EventSheetProjection.Actions(sheetRule).Any(r=>r.Note=="Action loop"),"Sheet safely identifies action cycles");
last.NextActionId=null;
var branch=I("flow.branch");branch.TrueActionId=first.InstanceId;branch.FalseActionId=draft.InstanceId;
sheetRule.Actions.Add(branch);sheetRule.FirstActionId=branch.InstanceId;
projected=EventSheetProjection.Actions(sheetRule);
Check(projected.Any(r=>r.Note=="If true")&&projected.Any(r=>r.Note=="If false")&&projected.All(r=>r.Connected),"Both branch paths are represented without becoming a flat action chain");
var any=I("logic.or");var a=I("a");var b=I("b");var unused=I("unused");any.ConditionInputIds=[b.InstanceId,a.InstanceId];
sheetRule.Conditions=[a,unused,any,b];sheetRule.HasExplicitConditionFlow=true;sheetRule.ConnectedConditionIds=[any.InstanceId];
var projectedConditions=EventSheetProjection.Conditions(sheetRule);
Check(projectedConditions[0].Instruction==any&&projectedConditions[1].Instruction==b&&projectedConditions[1].Depth==1&&projectedConditions[2].Instruction==a,"Sheet retains nested OR structure and input order");
Check(projectedConditions.Last().Instruction==unused&&!projectedConditions.Last().Connected,"Unconnected conditions are not presented as required conditions");
var folded=EventSheetProjection.Conditions(sheetRule,new HashSet<Guid>{any.InstanceId});
Check(folded.Count==2&&folded[0].Instruction==any&&folded[1].Instruction==unused,"Collapsed OR hides its children without misclassifying them as disconnected");
Check(any.ConditionInputIds.SequenceEqual(new[]{b.InstanceId,a.InstanceId}),"Collapsing a logic block leaves its executable inputs intact");
any.ConditionInputIds.Add(any.InstanceId);
Check(EventSheetProjection.Conditions(sheetRule).Any(r=>r.Note=="Circular condition link"),"Sheet safely identifies circular condition groups");

var legacyText=scene.CreateGameObject("Legacy HUD").AddComponent(new UiText{Text="USER TEXT"});equipment.StatusTextObjectId=legacyText.GameObject.Id;
typeof(FpsGunLab3D).GetMethod("OnUpdate",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(equipment,null);
Check(legacyText.Text=="USER TEXT","Equipment no longer overwrites a bound UI label behind the event sheet");
weapon.SetAmmo(17,63);definition.DisplayName="CUSTOM RIFLE";
var hudLabels=new Dictionary<string,UiText>();foreach(string name in new[]{"HUD Weapon Name","HUD Magazine Ammo","HUD Reserve Ammo","HUD Reload State"})hudLabels[name]=scene.CreateGameObject(name).AddComponent(new UiText{FontSize=27,Color=new(.2f,.8f,1,1)});
var hudModule=new EventModuleSerializer().Load("Plugins/KarlsonDemo/Samples/Weapon HUD.byteevents");var hudRuntime=new EventModuleRuntime(registry);
hudRuntime.Update(hudModule,new(),scene,player,message=>throw new Exception(message));
Check(hudLabels["HUD Weapon Name"].Text=="CUSTOM RIFLE"&&hudLabels["HUD Magazine Ammo"].Text=="17 / 20"&&hudLabels["HUD Reserve Ammo"].Text=="RESERVE 63","Native HUD event sheet binds separate name, magazine and reserve labels");
Check(hudLabels["HUD Magazine Ammo"].FontSize==27&&hudLabels["HUD Magazine Ammo"].Color==new Vector4(.2f,.8f,1,1),"HUD text updates preserve the user's font size and colour");
Check(!hudLabels["HUD Reload State"].Visible,"HUD sheet hides reload state while ready");
weapon.CancelReload();weapon.Reload();Tick(.025f);hudRuntime.Update(hudModule,new(),scene,player,message=>throw new Exception(message));
Check(hudLabels["HUD Reload State"].Visible&&weapon.ReloadProgress>0&&weapon.ReloadProgress<1&&equipment.ReloadRemainingSeconds>0,"Reload visibility and normalized progress are exposed to UI logic");
weapon.CancelReload();hudRuntime.Update(hudModule,new(),scene,player,message=>throw new Exception(message));Check(!hudLabels["HUD Reload State"].Visible,"Cancelling reload updates the authored UI visibility");
Action(I(Prefix+"readValue",("field",EventValue.String("Weapon name")),("variable",EventValue.String("ReadName"))),Context());
Check(gun.Variables["ReadName"].String=="CUSTOM RIFLE","Read weapon value exposes text data without component code");
var otherWeaponObject=scene.CreateGameObject("Other weapon");otherWeaponObject.SetParent(camera,false);otherWeaponObject.AddComponent(new WeaponDefinition3D{Slot=1,MagazineCapacity=8,ReserveCapacity=80});var otherWeapon=otherWeaponObject.AddComponent(new WeaponInstance3D{CurrentAmmo=8,ReserveAmmo=50});
Action(I(Prefix+"setAmmo",("target",EventValue.String("Other weapon")),("current",EventValue.Number(3)),("reserve",EventValue.Number(21))),Context());
Check(otherWeapon.CurrentAmmo==3&&otherWeapon.ReserveAmmo==21&&weapon.CurrentAmmo==17,"Ammo actions affect the selected source object rather than another equipped weapon");
var output=scene.CreateGameObject("HUD data owner");Action(I(Prefix+"readValue",("target",EventValue.String("Other weapon")),("field",EventValue.String("Magazine ammo")),("scope",EventValue.String("Object")),("destination",EventValue.String("HUD data owner")),("variable",EventValue.String("Ammo"))),Context());
Check(output.Variables["Ammo"].Number==3,"Weapon data can be copied to a separately selected owner");

foreach(string state in new[]{"Held","Pressed","Released"})Check(registry.TryGetCondition("input.mouse"+state,out var mouse)&&mouse!.Arguments.Any(a=>a.Name=="button"),"Mouse "+state+" is discoverable with editable button");
{var inventoryScene=new Scene("Inventory regression");
 var owner=inventoryScene.CreateGameObject("Carrier");var eye=inventoryScene.CreateGameObject("Inventory Camera");eye.SetParent(owner,false);eye.AddComponent(new Camera3D());
 var inventory=owner.AddComponent(new FpsGunLab3D{CameraObjectId=eye.Id,InventoryRootId=eye.Id,StartWithNoWeapons=true,CarryLimit=1,UseBuiltInFireInput=false});
 var firstRig=inventoryScene.CreateGameObject("First rig");firstRig.SetParent(eye,false);firstRig.AddComponent(new WeaponDefinition3D{Slot=0,EquipSeconds=0});var firstRigWeapon=firstRig.AddComponent(new WeaponInstance3D{CurrentAmmo=7});
 var second=inventoryScene.CreateGameObject("Second rig");second.SetParent(eye,false);second.AddComponent(new WeaponDefinition3D{Slot=1});var secondWeapon=second.AddComponent(new WeaponInstance3D());
 var pickupObject=inventoryScene.CreateGameObject("First pickup");var firstRigPickup=pickupObject.AddComponent(new WeaponPickup3D{WeaponObjectId=firstRig.Id});
 var secondObject=inventoryScene.CreateGameObject("Second pickup");var secondPickup=secondObject.AddComponent(new WeaponPickup3D{WeaponObjectId=second.Id});
 typeof(Scene).GetMethod("LoadInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(inventoryScene,null);typeof(FpsGunLab3D).GetMethod("OnUpdate",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(inventory,null);
 Check(inventory.WeaponCount==0&&!inventory.HasWeapon&&!firstRig.Active&&!second.Active,"Start empty hides all rigs and owns no weapons");inventory.Select(0);Check(!inventory.HasWeapon,"Slot selection cannot grant an unowned weapon");
 pickupObject.Transform.WorldPosition=new(100,0,0);Check(!inventory.PickUp(firstRigPickup),"Pickup enforces interaction distance");pickupObject.Transform.WorldPosition=Vector3.Zero;
 Check(inventory.PickUp(firstRigPickup)&&inventory.WeaponCount==1&&firstRigWeapon.Carried&&inventory.HasWeapon&&!pickupObject.Active,"Pickup grants and equips one weapon and hides its world object");
 Check(inventory.InventoryFull&&!inventory.PickUp(secondPickup)&&!inventory.PickUp(firstRigPickup),"Carry limit and duplicate pickup are enforced");
 var dropFloor=inventoryScene.CreateGameObject("Drop floor");dropFloor.Transform.WorldPosition=new(0,-.025f,0);dropFloor.AddComponent(new BoxCollider3D{Size=new(20,.05f,20)});
 eye.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitX,-1f);inventory.AimMode=WeaponAimMode.TopDownCursor;
 firstRigWeapon.SetAmmo(4,17);Check(inventory.Drop()&&inventory.WeaponCount==0&&!inventory.HasWeapon&&pickupObject.Active&&!firstRigWeapon.Carried,"Drop clears ownership, unequips and restores a world pickup");
 Check(Math.Abs(pickupObject.Transform.WorldPosition.Y-firstRigPickup.DropGroundClearance)<.001f&&pickupObject.Transform.WorldPosition.Z<-1,"Top-down drop ignores downward camera pitch and places pickup above ground in front of player");
 Check(inventory.PickUp(firstRigPickup)&&firstRigWeapon.CurrentAmmo==4&&firstRigWeapon.ReserveAmmo==17,"Re-pickup preserves magazine and reserve ammunition");
 var inventoryContext=new EventExecutionContext{Scene=inventoryScene,Self=owner,Globals=new VariableStore()};Action(I(Prefix+"drop"),inventoryContext);Action(I(Prefix+"pickUp",("pickup",EventValue.String("Nearest"))),inventoryContext);
 Check(inventory.WeaponCount==1,"Event-sheet pickup and drop actions use native equipment state");
}


{var movementScene=new Scene("Viewmodel locomotion regression");var actor=movementScene.CreateGameObject("Moving player");var motor=actor.AddComponent(new CharacterController3D());var eye=movementScene.CreateGameObject("Nested camera");eye.SetParent(actor,false);
 typeof(CharacterController3D).GetProperty("IsGrounded")!.SetValue(motor,true);
 foreach(string name in new[]{"Pistol","Shotgun","Rifle"}){
  var rig=movementScene.CreateGameObject(name);rig.SetParent(eye,false);var animation=rig.AddComponent(new ByteEngine.Core.Animation.AnimationController{RunThreshold=4});
  motor.SetVelocity(new(0,0,3));typeof(ByteEngine.Core.Animation.AnimationController).GetMethod("ObserveLocomotion",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(animation,null);
  Check(animation.State==ByteEngine.Core.Animation.LocomotionState.Walk&&animation.LocomotionSpeed==3,name+" child rig observes parent movement as Walk");
  motor.SetVelocity(new(0,0,6));typeof(ByteEngine.Core.Animation.AnimationController).GetMethod("ObserveLocomotion",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(animation,null);
  Check(animation.State==ByteEngine.Core.Animation.LocomotionState.Run&&animation.LocomotionSpeed==6,name+" child rig observes parent movement as Run");
 }
 var movementContext=new EventExecutionContext{Scene=movementScene,Self=actor,Globals=new VariableStore()};Action(I("character.setMoveSpeed",("speed",EventValue.Number(3))),movementContext);Check(motor.MoveSpeed==3,"Native movement-speed action supports editable walk/run input rules");
}
foreach(bool emptyStart in new[]{false,true}){
 var startScene=new Scene("Starting inventory");var actor=startScene.CreateGameObject("Starter");var eye=startScene.CreateGameObject("Root");eye.SetParent(actor,false);eye.AddComponent(new Camera3D());var inventory=actor.AddComponent(new FpsGunLab3D{InventoryRootId=eye.Id,StartingSlot=2,CarryLimit=1,StartWithNoWeapons=emptyStart,UseBuiltInFireInput=false});
 for(int slot=0;slot<3;slot++){var item=startScene.CreateGameObject("Rig "+slot);item.SetParent(eye,false);item.AddComponent(new WeaponDefinition3D{Slot=slot});item.AddComponent(new WeaponInstance3D{StartInInventory=slot!=1});}
 typeof(Scene).GetMethod("LoadInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(startScene,null);typeof(FpsGunLab3D).GetMethod("OnUpdate",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(inventory,null);
 Check(emptyStart?inventory.WeaponCount==0:inventory.WeaponCount==1&&inventory.SelectedSlot==2,"Starting flags and carry limit respect the configured starting slot (empty="+emptyStart+")");
}

// Equipment must change the character's base stance, never a shared profile asset.
{
 var stanceScene=new Scene("Weapon character stance");var actor=stanceScene.CreateGameObject("Character");
 var controller=actor.AddComponent(new AnimationController());var root=stanceScene.CreateGameObject("Hand weapons");root.SetParent(actor,false);
 var inventory=actor.AddComponent(new FpsGunLab3D{InventoryRootId=root.Id,UseBuiltInFireInput=false,StartWithNoWeapons=true});
 var profile=new AnimationProfile();
 foreach(string name in new[]{"Unarmed","One Hand","Two Hand"})profile.BlendSpaces.Add(new AnimationBlendSpace{Name=name,Samples=[new AnimationBlendSample{Clip=name+" Idle"}]});
 profile.Locomotion.BlendSpace="Unarmed";
 var weapons=new List<WeaponInstance3D>();var pickups=new List<WeaponPickup3D>();
 for(int slot=0;slot<2;slot++){var rig=stanceScene.CreateGameObject("Gun "+slot);rig.SetParent(root,false);rig.AddComponent(new WeaponDefinition3D{Slot=slot,LocomotionBlendSpace=slot==0?"One Hand":"Two Hand"});weapons.Add(rig.AddComponent(new WeaponInstance3D()));var item=stanceScene.CreateGameObject("Pickup "+slot);pickups.Add(item.AddComponent(new WeaponPickup3D{WeaponObjectId=rig.Id}));}
 typeof(Scene).GetMethod("LoadInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(stanceScene,null);
 typeof(AnimationController).GetMethod("ConfigureAdvancedProfile",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(controller,[profile]);
 void Pose()=>typeof(AnimationController).GetMethod("UpdateAdvancedPoseControls",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(controller,null);
 Check(inventory.PickUp(pickups[0]),"One-hand pickup succeeds");Pose();Check(controller.CurrentBlendSpace=="One Hand","Pickup selects one-hand character blendspace");
 Check(inventory.PickUp(pickups[1],false),"Two-hand inventory-only pickup succeeds");Pose();Check(controller.CurrentBlendSpace=="One Hand","Storing a gun does not change the equipped stance");
 inventory.Select(1);Pose();Check(controller.CurrentBlendSpace=="Two Hand","Equip switches to two-hand stance");
 Check(profile.Locomotion.BlendSpace=="Unarmed","Weapon switching leaves the shared animation profile unchanged");
 weapons[1].Definition!.LocomotionBlendSpace="Missing";Check(!inventory.RefreshWeaponLocomotion()&&inventory.LocomotionBindingError.Length>0,"Invalid weapon blendspace reports a binding error");Pose();Check(controller.CurrentBlendSpace=="Unarmed","Invalid binding clears stale previous weapon stance");
 weapons[1].Definition!.LocomotionBlendSpace="Two Hand";inventory.RefreshWeaponLocomotion();inventory.Drop();Pose();Check(controller.CurrentBlendSpace=="One Hand","Dropping current gun selects the remaining gun stance");
 inventory.Drop();Pose();Check(controller.CurrentBlendSpace=="Unarmed","Dropping final gun restores unarmed profile locomotion");
 Check(!controller.SetLocomotionBlendSpace("Missing")&&controller.SetLocomotionBlendSpace(" two hand "),"Blendspace names validate and match without case or whitespace sensitivity");
 typeof(AnimationController).GetField("_actionActive",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(controller,true);Pose();Check(controller.CurrentBlendSpace=="","Named actions suppress weapon locomotion");
 typeof(AnimationController).GetField("_actionActive",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(controller,false);Pose();Check(controller.CurrentBlendSpace=="Two Hand","Weapon stance resumes after an action");
 var stanceContext=new EventExecutionContext{Scene=stanceScene,Self=actor,Globals=new VariableStore()};
 Action(I("animation.setLocomotionBlendSpace",("blendSpace",EventValue.String("One Hand"))),stanceContext);Pose();Check(controller.CurrentBlendSpace=="One Hand","Native event action selects character locomotion");
 typeof(AnimationController).GetProperty("State")!.SetValue(controller,LocomotionState.Fall);Pose();Check(controller.CurrentBlendSpace=="","Weapon stance does not replace airborne animation");
 typeof(AnimationController).GetProperty("State")!.SetValue(controller,LocomotionState.Idle);
 var another=stanceScene.CreateGameObject("Separate character");var otherController=another.AddComponent(new AnimationController());
 typeof(AnimationController).GetMethod("ConfigureAdvancedProfile",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(otherController,[profile]);
 inventory.AnimationControllerObjectId=another.Id;inventory.UnarmedBlendSpace="Two Hand";Check(inventory.RefreshWeaponLocomotion()&&otherController.LocomotionBlendSpaceOverride=="Two Hand"&&controller.LocomotionBlendSpaceOverride=="","Explicit controller binding switches target and clears old character stance");
}

{
 var s=new Scene("Top-down weapon aim");var actor=s.CreateGameObject("Top-down player");var root=s.CreateGameObject("Weapons");root.SetParent(actor,false);var topGun=s.CreateGameObject("topGun");topGun.SetParent(root,false);
 topGun.AddComponent(new WeaponDefinition3D{EquipSeconds=0,SpreadDegrees=0,MuzzleFlashEnabled=false,ShowShotTrace=false});var w=topGun.AddComponent(new WeaponInstance3D());
 var e=actor.AddComponent(new FpsGunLab3D{InventoryRootId=root.Id,AimMode=WeaponAimMode.TopDownCursor,UseBuiltInFireInput=false});
 var aimTarget=s.CreateGameObject("Forward aimTarget");aimTarget.Transform.WorldPosition=new(0,0,-5);aimTarget.AddComponent(new BoxCollider3D{Size=new(2)});
 typeof(Scene).GetMethod("LoadInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(s,null);e.Select(0);s.Physics.Step(s,1f/60);
 Check(e.Fire()&&w.ShotHits.Count==1&&w.ShotHits[0].Hit.GameObject==aimTarget,"Top-down guns fire forward from the muzzle without a first-person camera");
}

{
 var path=Path.Combine(Path.GetTempPath(),"byteengine-masked-action-"+Guid.NewGuid()+".byteanim");
 try{var profile=new AnimationProfile{Actions=[new AnimationActionProfile{Name="Reload",Clip="ReloadClip",Mask=new AnimationBoneMask{Kind=AnimationBoneMaskKind.UpperBody,RootBone="Spine_01"}}]};AnimationProfileSerializer.Save(path,profile);var saved=AnimationProfileSerializer.Load(path);Check(saved.Actions[0].Mask.Kind==AnimationBoneMaskKind.UpperBody&&saved.Actions[0].Mask.RootBone=="Spine_01","Named action body mask round-trips through the native profile serializer");Check(new AnimationActionProfile().Mask.Kind==AnimationBoneMaskKind.FullBody,"Existing animation actions keep full-body behaviour by default");}finally{File.Delete(path);}
}

{
 var path=Path.Combine(Path.GetTempPath(),"byteengine-authored-layer-"+Guid.NewGuid()+".byteanim");
 try
 {
  var profile=new AnimationProfile
  {
   Layers=[new AnimationLayerProfile{Name="Weapon Actions",Clip="",PlaybackMode=AnimationLayerPlaybackMode.Independent,
    BlendSpaceFilter="One Hand, Two Hand",StabilizeRootRotation=true,Mask=new AnimationBoneMask{Kind=AnimationBoneMaskKind.UpperBody,RootBone="Spine_01"}}],
   Actions=[new AnimationActionProfile{Name="Fire",Clip="Shoot",Layer="Weapon Actions"}]
  };
  AnimationProfileSerializer.Save(path,profile);var saved=AnimationProfileSerializer.Load(path);
  Check(saved.Actions[0].Layer=="Weapon Actions"&&saved.Layers[0].Mask.RootBone=="Spine_01"&&saved.Layers[0].StabilizeRootRotation,
   "Action binding and stabilized upper-body layer round-trip through the native profile serializer");
  Check(saved.Layers[0].PlaybackMode==AnimationLayerPlaybackMode.Independent&&saved.Layers[0].BlendSpaceFilter=="One Hand, Two Hand",
   "Independent layer playback and blendspace activation are retained");
  Check(new AnimationLayerProfile().PlaybackMode==AnimationLayerPlaybackMode.LocomotionPhase&&string.IsNullOrEmpty(new AnimationActionProfile().Layer),
   "Existing profiles retain synchronized layers and legacy action behavior");
 }finally{File.Delete(path);}
}

{
 var controller=new AnimationController();
 var flags=BindingFlags.Instance|BindingFlags.NonPublic;
 var actions=(Dictionary<string,AnimationActionProfile>)typeof(AnimationController).GetField("_namedActions",flags)!.GetValue(controller)!;
 actions.Add("Broken",new AnimationActionProfile{Name="Broken",Clip="Fire",Layer="Missing Layer"});
 typeof(AnimationController).GetField("_actionActive",flags)!.SetValue(controller,true);
 typeof(AnimationController).GetField("_currentAction",flags)!.SetValue(controller,"Existing");
 Check(!controller.PlayNamedAction("Broken",true,false,true)&&controller.CurrentAction=="Existing",
  "Missing animation layer rejects a request without interrupting the current action");
}

{
 var s=new Scene("Independent world weapon pickups");var actor=s.CreateGameObject("Collector");
 var inventory=actor.AddComponent(new FpsGunLab3D{StartWithNoWeapons=true,UseBuiltInFireInput=false,CarryLimit=1});
 var item=s.CreateGameObject("Self-contained world pistol");item.Transform.LocalScale=new(1.5f);
 item.AddComponent(new WeaponDefinition3D{Slot=0,EquipSeconds=0});var instance=item.AddComponent(new WeaponInstance3D{CurrentAmmo=7});
 var collider=item.AddComponent(new BoxCollider3D());var worldBody=item.AddComponent(new Rigidbody3D());
 var pickup=item.AddComponent(new WeaponPickup3D());
 var separate=s.CreateGameObject("Independent rifle rig");separate.AddComponent(new WeaponDefinition3D{Slot=1});var rifle=separate.AddComponent(new WeaponInstance3D());
 var world=s.CreateGameObject("Rifle pickup");var linked=world.AddComponent(new WeaponPickup3D{WeaponObjectId=separate.Id});
 typeof(Scene).GetMethod("LoadInternal",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(s,null);
 Check(inventory.PickUp(pickup)&&inventory.Owns(instance)&&item.ActiveInHierarchy,"Root-level self-contained world gun can be picked up without inventory parenting");
 typeof(WeaponPickup3D).GetMethod("OnUpdate",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(pickup,null);
 Check(!collider.Enabled&&!worldBody.Enabled,"Self-contained weapon suspends world physics while carried");
 Check(item.ActiveInHierarchy&&!inventory.CanPickUp(rifle),"Equipped self-pickup stays visible and carry limit rejects a second world gun");
 Check(inventory.Drop()&&pickup.Available&&item.Parent==null&&instance.CurrentAmmo==7&&item.Transform.WorldScale==new Vector3(1.5f),"Self-contained gun drops visibly, retains ammo/scale, and becomes collectible again");
 Check(collider.Enabled&&worldBody.Enabled,"Dropped world gun restores its original collider and body state");
 actor.Transform.WorldPosition=world.Transform.WorldPosition;
 Check(inventory.PickUp(linked)&&inventory.Owns(rifle),"Linked rig outside Weapon Root can be picked up");
 var foreign=new Scene("Other scene");var foreignWeapon=foreign.CreateGameObject("Foreign");foreignWeapon.AddComponent(new WeaponDefinition3D());
 Check(!inventory.CanPickUp(foreignWeapon.AddComponent(new WeaponInstance3D())),"Pickup rejects a weapon from another scene");
}
{
 Check(CharacterMotionDebugHud3D.DirectionName(-Vector3.UnitZ,-Vector3.UnitZ,Vector3.UnitX)=="Forward"&&
       CharacterMotionDebugHud3D.DirectionName(Vector3.UnitX,-Vector3.UnitZ,Vector3.UnitX)=="Right"&&
       CharacterMotionDebugHud3D.DirectionName(Vector3.UnitZ,-Vector3.UnitZ,Vector3.UnitX)=="Backward",
       "Movement HUD labels actual velocity relative to character facing");
 var path=Path.Combine(Path.GetTempPath(),"byte-turn-profile-"+Guid.NewGuid()+".byteanim");
 try
 {
  var profile=new AnimationProfile();profile.Locomotion.Turning.Enabled=true;
  profile.Locomotion.Turning.Sets.Add(new AnimationTurnSet{BlendSpace="Two Hand",Left90="Left",Right90="Right",Around180="Back"});
  AnimationProfileSerializer.Save(path,profile);var loaded=AnimationProfileSerializer.Load(path);
  Check(loaded.Locomotion.Turning.Enabled&&loaded.Locomotion.Turning.Sets[0].Around180=="Back","Turn-in-place stance clips persist in the native Animation Profile");
 }finally{File.Delete(path);}
 Check(registry.TryGetCondition("animation.isTurning",out _)&&registry.TryGetAction("animation.setTurnInPlace",out _)&&registry.TryGetAction("characterDebug.setVisible",out _),"Turning and diagnostic HUD controls are exposed in event sheets");
}

{
 var type=typeof(ByteEngine.Core.Serialization.ComponentSerializer).GetNestedType("AnimationControllerCodec",BindingFlags.NonPublic)!;
 var codec=(ByteEngine.Core.Serialization.IComponentCodec)Activator.CreateInstance(type,true)!;
 var data=codec.Serialize(new AnimationController{TurnInPlaceEnabled=false},null!);
 var restored=(AnimationController)codec.Deserialize(data,null!);
 Check(!restored.TurnInPlaceEnabled,"Per-character turn animation enable setting persists in scene serialization");
 data.Properties.Remove("turnInPlaceEnabled");
 Check(((AnimationController)codec.Deserialize(data,null!)).TurnInPlaceEnabled,"Older scenes retain compatible turn animation defaults");
}

{
 var s=new Scene("Pickup key flow and horizontal radius");var actor=s.CreateGameObject("Collector");
 var inventory=actor.AddComponent(new FpsGunLab3D{StartWithNoWeapons=true,UseBuiltInFireInput=false});
 var gunObject=s.CreateGameObject("World gun");gunObject.Transform.WorldPosition=new(2.49f,.7f,0);
 gunObject.AddComponent(new WeaponDefinition3D{Slot=0});gunObject.AddComponent(new WeaponInstance3D());
 var pickup=gunObject.AddComponent(new WeaponPickup3D());
 typeof(Scene).GetMethod("LoadInternal",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(s,null);
 Check(pickup.CanCollect(inventory)&&pickup.GetCollectionStatus(inventory)=="Ready","Horizontal pickup radius is not reduced by the model origin height");
 gunObject.Transform.WorldPosition=new(2,3,0);
 Check(!pickup.CanCollect(inventory)&&pickup.GetCollectionStatus(inventory).Contains("above"),"Separate pickup height limit prevents grabbing weapons through floors");
 gunObject.Transform.WorldPosition=new(2.49f,.7f,0);
 var key=I("input.keyPressed",("key",EventValue.String("E")));var gate=I(Prefix+"inputActive");var allowed=I(Prefix+"canPickUp",("nearest",EventValue.Boolean(true)));
 var collect=I(Prefix+"pickUp",("nearest",EventValue.Boolean(true)),("equip",EventValue.Boolean(true)));
 var pickupKeyModule=new EventModuleDefinition{Rules=[new EventRuleDefinition{HasExplicitConditionFlow=true,ConnectedConditionIds=[key.InstanceId,gate.InstanceId,allowed.InstanceId],Conditions=[key,gate,allowed],Actions=[collect]}]};
 var flags=BindingFlags.NonPublic|BindingFlags.Static;var inject=typeof(Input).GetMethod("UpdatePortable",flags)!;
 inject.Invoke(null,[new ByteEngine.Core.InputSystem.RawInputSnapshot(),false]);
 var raw=new ByteEngine.Core.InputSystem.RawInputSnapshot();raw.KeysDown.Add(Key.E);inject.Invoke(null,[raw,false]);
 ByteEngine.Core.InputSystem.InputActions.Update(raw,false);
 new EventModuleRuntime(registry).Update(pickupKeyModule,new VariableStore(),s,actor);
 Check(!inventory.HasWeapon,"Inactive gameplay input cannot pick up a weapon from editor keyboard input");
 ByteEngine.Core.InputSystem.InputActions.Update(raw,true);
 new EventModuleRuntime(registry).Update(pickupKeyModule,new VariableStore(),s,actor);
 Check(inventory.HasWeapon&&inventory.WeaponCount==1&&!Input.IsGameInputCaptured,"E-key pickup works with active gameplay and a free, uncaptured cursor");
 Check(!inventory.PickUp(null)&&inventory.LastPickupResult.Contains("not found"),"Missing explicit pickup reports its own failure instead of a stale success message");
 var broken=s.CreateGameObject("Broken pickup").AddComponent(new WeaponPickup3D{WeaponObjectId=Guid.NewGuid()});
 Check(broken.BindingError.Length>0&&inventory.GetCharacterDebugText().Contains("missing"),"Broken pickup links expose an actionable diagnostic instead of silently failing");
 inject.Invoke(null,[new ByteEngine.Core.InputSystem.RawInputSnapshot(),false]);
}

{
 var s=new Scene("Hybrid cursor facing");var actor=s.CreateGameObject("Walker");actor.AddComponent(new CharacterController3D());
 var cam=s.CreateGameObject("Aim camera");cam.Transform.WorldPosition=new(0,10,10);cam.Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitX,-MathF.PI/4);cam.AddComponent(new Camera3D());
 var control=actor.AddComponent(new TopDownController3D{Facing=TopDownFacing.MovementUntilAiming,CameraObjectId=cam.Id,CameraRelativeMovement=false,TurnSharpness=0,AimReleaseDelay=.25f});
 var map=ByteEngine.Core.InputSystem.InputMap.CreateDefault();map.Add("Aim",ByteEngine.Core.InputSystem.InputActionType.Button).Bindings.Add(ByteEngine.Core.InputSystem.InputBinding.MouseButtonBinding(MouseButton.Right));
 ByteEngine.Core.InputSystem.InputActions.Configure(map);
 var raw=new ByteEngine.Core.InputSystem.RawInputSnapshot();raw.KeysDown.Add(Key.D);
 Input.SetGameViewPointer(new(.5f,.2f),new(1000,700),true);
 var update=typeof(TopDownController3D).GetMethod("OnUpdate",BindingFlags.NonPublic|BindingFlags.Instance)!;
 var time=typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!;double prior=Time.DeltaTime;
 try
 {
  time.Invoke(null,[.1d]);ByteEngine.Core.InputSystem.InputActions.Update(raw,true);update.Invoke(control,null);
  Check(!control.IsFacingCursor&&Vector3.Dot(actor.Transform.Forward,Vector3.UnitX)>.999f,"Hybrid mode faces walking direction without aim input");
  raw.MouseButtonsDown.Add(MouseButton.Right);ByteEngine.Core.InputSystem.InputActions.Update(raw,true);update.Invoke(control,null);
  Check(control.IsFacingCursor&&control.HasCursorAim&&Vector3.Dot(actor.Transform.Forward,Vector3.Normalize(new Vector3(control.AimWorldPoint.X,0,control.AimWorldPoint.Z)))>.999f&&control.MoveDirection.X>.99f,"Holding configurable Aim turns toward cursor while preserving world-right movement");
  raw.MouseButtonsDown.Clear();ByteEngine.Core.InputSystem.InputActions.Update(raw,true);update.Invoke(control,null);
  Check(control.IsFacingCursor,"Aim release delay prevents facing flicker between shots");
  for(int i=0;i<3;i++)update.Invoke(control,null);
  Check(!control.IsFacingCursor&&actor.Transform.Forward.X>.999f,"Releasing aim returns to facing travel after the delay");
  raw.MouseButtonsDown.Add(MouseButton.Left);ByteEngine.Core.InputSystem.InputActions.Update(raw,true);update.Invoke(control,null);
  Check(control.IsFacingCursor,"Firing can request cursor facing without a weapon dependency");
  ByteEngine.Core.InputSystem.InputActions.Update(raw,false);update.Invoke(control,null);Check(!control.IsFacingCursor,"Inactive gameplay clears temporary aim requests");
  control.Facing=TopDownFacing.Cursor;raw.MouseButtonsDown.Clear();ByteEngine.Core.InputSystem.InputActions.Update(raw,true);update.Invoke(control,null);
  Check(control.IsFacingCursor,"Always-cursor mode remains independent of aim/fire buttons");
 }finally{time.Invoke(null,[prior]);ByteEngine.Core.InputSystem.InputActions.Configure(null);}
 var codec=new ByteEngine.Core.Plugins.ReflectionPluginComponentCodec<TopDownController3D>("TopDownController3D");
 control.Facing=TopDownFacing.MovementUntilAiming;control.AimReleaseDelay=.4f;
 var restored=(TopDownController3D)codec.Deserialize(codec.Serialize(control,null!),null!);
 Check(restored.Facing==TopDownFacing.MovementUntilAiming&&restored.AimAction.Name=="Aim"&&restored.AimReleaseDelay==.4f,"Facing mode and configurable aim controls survive scene serialization");
 Check(registry.TryGetCondition("topDown.isFacingCursor",out _)&&registry.TryGetAction("topDown.setAiming",out _),"Hybrid aiming is exposed in Event Sheets");
}

{
 var s=new Scene("Gradual stationary turning");var actor=s.CreateGameObject("Turn tester");
 var controller=actor.AddComponent(new AnimationController());var control=actor.AddComponent(new TopDownController3D());
 var flags=BindingFlags.Instance|BindingFlags.NonPublic;
 var profile=new AnimationProfile();profile.Locomotion.Turning.Enabled=true;profile.Locomotion.Turning.MinimumAngle=15;
 typeof(AnimationController).GetMethod("ConfigureAdvancedProfile",flags)!.Invoke(controller,[profile]);
 typeof(TopDownController3D).GetProperty("HasCursorAim")!.SetValue(control,true);
 typeof(TopDownController3D).GetProperty("IsFacingCursor")!.SetValue(control,true);
 typeof(TopDownController3D).GetProperty("SignedAimAngleDegrees")!.SetValue(control,2f);
 typeof(TopDownController3D).GetProperty("YawSpeedDegrees")!.SetValue(control,30f);
 var updateTime=typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!;double prior=Time.DeltaTime;
 try
 {
  updateTime.Invoke(null,[1d/60]);
  for(int i=0;i<40;i++)typeof(AnimationController).GetMethod("UpdateTurnInPlace",flags)!.Invoke(controller,null);
  Check(controller.PendingTurnAngle>19,"Gradual cursor turning accumulates rotation even when instantaneous aim error stays below threshold");
  typeof(AnimationController).GetProperty("State")!.SetValue(controller,LocomotionState.Walk);
  typeof(AnimationController).GetMethod("UpdateTurnInPlace",flags)!.Invoke(controller,null);
  Check(controller.PendingTurnAngle==0,"Walking clears idle turn accumulation");
 }finally{updateTime.Invoke(null,[prior]);}
}

{
 var s=new Scene("Collision-safe dash");var floor=s.CreateGameObject("Floor");floor.Transform.WorldPosition=new(0,-.5f,0);floor.AddComponent(new BoxCollider3D{Size=new(30,1,30)});
 var wall=s.CreateGameObject("Wall");wall.Transform.WorldPosition=new(0,1,-2);wall.AddComponent(new BoxCollider3D{Size=new(6,3,.2f)});
 var actor=s.CreateGameObject("Dasher");actor.AddComponent(new CapsuleCollider3D{Radius=.3f,Height=1.8f,Center=new(0,.9f,0)});
 var motor=actor.AddComponent(new CharacterController3D());var meter=actor.AddComponent(new ResourceMeter3D());var health=actor.AddComponent(new HealthComponent());
 var dash=actor.AddComponent(new DashAbility3D{RequireGrounded=false,Distance=5,Duration=.2f,EnergyCost=25,Cooldown=1});
 var flags=BindingFlags.NonPublic|BindingFlags.Instance;typeof(Scene).GetMethod("LoadInternal",flags)!.Invoke(s,null);
 Check(dash.TryDash()&&meter.Current==75,"Dash spends energy exactly once");Check(!dash.TryDash()&&meter.Current==75,"Active dash cannot double-spend energy");
 var clock=typeof(Time).GetMethod("Update",BindingFlags.Static|BindingFlags.NonPublic)!;double old=Time.DeltaTime;
 try{clock.Invoke(null,[1d/60]);for(int i=0;i<20;i++)typeof(Scene).GetMethod("UpdateInternal",flags)!.Invoke(s,null);
 Check(actor.Transform.WorldPosition.Z > -1.8f&&actor.Transform.WorldPosition.Z < -.5f,"Dash sweeps into a wall without passing through it");
 Check(!dash.IsDashing&&motor.Speed<.01f,"Dash ends without carrying burst velocity into normal locomotion");
 Check(!dash.TryDash(),"Dash cooldown blocks early reuse");
 for(int i=0;i<60;i++)typeof(Scene).GetMethod("UpdateInternal",flags)!.Invoke(s,null);
 Check(dash.CanDash&&dash.CanRegenerateEnergy,"Cooldown and regeneration delay recover independently");
 meter.SetAmount(10);Check(!dash.TryDash()&&meter.Current==10,"Insufficient energy never starts a dash");meter.SetAmount(100);health.Kill();Check(!dash.TryDash(),"Dead characters cannot dash");
 }finally{clock.Invoke(null,[old]);typeof(Scene).GetMethod("UnloadInternal",flags)?.Invoke(s,null);}
 Check(registry.TryGetAction("dash.activate",out _)&&registry.TryGetAction("dash.updateHud",out _),"Dash activation and HUD bindings are exposed to Event Sheets");
}

{
 var s=new Scene("Recoil and accuracy");var shooter=s.CreateGameObject("Shooter");var rig=s.CreateGameObject("Test weapon");
 var def=rig.AddComponent(new WeaponDefinition3D{EquipSeconds=0,RoundsPerMinute=600,RecoilPitchPerShot=2,MaximumRecoilDegrees=3,RecoilRecoveryDegreesPerSecond=0,MuzzleFlashEnabled=false,ShowShotTrace=false});
 var w=rig.AddComponent(new WeaponInstance3D());var board=s.CreateGameObject("Target");board.Transform.WorldPosition=new(0,0,-10);board.AddComponent(new BoxCollider3D{Size=new(30,30,1)});
 typeof(Scene).GetMethod("LoadInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(s,null);w.Equip(true);var tick=typeof(WeaponInstance3D).GetMethod("Tick",BindingFlags.Instance|BindingFlags.NonPublic)!;
 Check(w.Fire(Vector3.Zero,-Vector3.UnitZ,shooter)&&w.RecoilAngles.X==2,"A successful shot accumulates editable recoil");
 Check(!w.Fire(Vector3.Zero,-Vector3.UnitZ,shooter)&&w.RecoilAngles.X==2,"Rejected shots do not add recoil");
 tick.Invoke(w,[.11f]);Check(w.Fire(Vector3.Zero,-Vector3.UnitZ,shooter)&&w.RecoilAngles.X==3&&w.ShotHits.First().Direction.Y>.02f,"Follow-up shots climb with bounded ballistic recoil");
 def.RecoilRecoveryDegreesPerSecond=10;def.BloomPerShotDegrees=.5f;tick.Invoke(w,[.11f]);w.Fire(Vector3.Zero,-Vector3.UnitZ,shooter);Check(w.AccuracyBloom==.5f,"Accuracy bloom accumulates per successful shot");
 tick.Invoke(w,[1f]);Check(w.RecoilAngles==Vector2.Zero&&w.AccuracyBloom==0,"Recoil and accuracy recover over time");
 Check(registry.TryGetAction(Prefix+"tuneAccuracy",out _)&&registry.TryGetAction(Prefix+"writeHudText",out _),"Accuracy tuning and formatted weapon HUD text are exposed to Event Sheets");
}

// Inventory-driven HUD and independent default world weapon.
{
 var s=new Scene("Dynamic inventory HUD");var actor=s.CreateGameObject("Owner");
 var inventory=actor.AddComponent(new FpsGunLab3D{StartWithNoWeapons=true,CarryLimit=2,UseBuiltInFireInput=false});
 WeaponInstance3D Gun(int slot){var o=s.CreateGameObject("Gun "+slot);o.AddComponent(new WeaponDefinition3D{Slot=slot,MagazineCapacity=12,ReserveCapacity=60});var w=o.AddComponent(new WeaponInstance3D{StartInInventory=false, ReserveAmmo=30});o.AddComponent(new WeaponPickup3D());return w;}
 var hudGunA=Gun(0);var hudGunB=Gun(1);var third=Gun(2);var canvas=s.CreateGameObject("Canvas");canvas.AddComponent(new UiCanvas());
 var card=s.CreateGameObject("Card template");card.Active=false;card.AddComponent(new UiWidget{Kind=UiWidgetKind.Image,Size=new(140,90),Offset=new(-14,-83)});
 var key=s.CreateGameObject("Key template");key.Active=false;key.AddComponent(new UiText{FontSize=23,Offset=new(-6,-137)});
 var reserve=s.CreateGameObject("Reserve template");reserve.Active=false;reserve.AddComponent(new UiWidget{Kind=UiWidgetKind.SegmentedBar,SegmentAngleDegrees=-28,SegmentLength=7,Color=Vector4.Zero});
 var hud=canvas.AddComponent(new WeaponInventoryHud3D{EquipmentObjectId=actor.Id,CardTemplateId=card.Id,KeyTemplateId=key.Id,ReserveTemplateId=reserve.Id});
 typeof(Scene).GetMethod("LoadInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(s,null);
 typeof(FpsGunLab3D).GetMethod("OnUpdate",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(inventory,null);
 hud.Refresh();Check(hud.DisplayedWeaponCount==0,"Unarmed inventory creates no weapon cards");
 Check(inventory.GiveWeapon(hudGunB)&&inventory.GiveWeapon(hudGunA)&&!inventory.GiveWeapon(third),"Event grants respect carry limits without parenting or pickup distance");
 hud.Refresh();Check(hud.DisplayedWeaponCount==2,"HUD creates exactly one native card per carried weapon");
 var bars=s.GameObjects.Where(o=>o.Name.StartsWith("HUD carried")&&o.Name.EndsWith(" reserve")).Select(o=>o.GetComponent<UiWidget>()!).ToArray();
 Check(bars.All(w=>w.Maximum==5&&w.Value==2.5f&&w.SegmentAngleDegrees==-28),"Reserve markers represent full and fractional magazines and preserve template styling");
 inventory.SelectInventoryIndex(0);Check(inventory.SelectedWeapon==hudGunB,"Hotkeys select carried position independently of definition slot");
 Check(s.GameObjects.Where(o=>o.Name.StartsWith("HUD carried")&&o.Name.EndsWith(" key")).All(o=>o.GetComponent<UiText>()!.FontSize==23),"Generated cards preserve authored UI text styling");
 inventory.Drop();hud.Refresh();Check(hud.DisplayedWeaponCount==1,"Dropping removes a card and compacts inventory");
 inventory.Drop();hud.Refresh();Check(hud.DisplayedWeaponCount==0,"Dropping last weapon clears the inventory HUD");
 Check(registry.TryGetAction(Prefix+"refreshInventoryHud",out _)&&registry.TryGetAction(Prefix+"updateReserveHud",out _)&&registry.TryGetAction(Prefix+"giveWeapon",out _)&&registry.TryGetAction(Prefix+"equipInventoryIndex",out _),"HUD, reserve markers, grants and carried selection are exposed to Event Sheets");
}
foreach(bool empty in new[]{false,true})
{
 var s=new Scene("Direct default weapon");var actor=s.CreateGameObject("Owner");var defaultGun=s.CreateGameObject("Distant world default");defaultGun.Transform.WorldPosition=new(100,0,100);defaultGun.Transform.WorldScale=new(2);
 defaultGun.AddComponent(new WeaponDefinition3D{Slot=7});var w=defaultGun.AddComponent(new WeaponInstance3D{StartInInventory=false, ReserveAmmo=30});defaultGun.AddComponent(new WeaponPickup3D());var collider=defaultGun.AddComponent(new BoxCollider3D());
 var inventory=actor.AddComponent(new FpsGunLab3D{DefaultWeaponObjectId=defaultGun.Id,StartWithNoWeapons=empty,UseBuiltInFireInput=false});
 typeof(Scene).GetMethod("LoadInternal",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(s,null);typeof(FpsGunLab3D).GetMethod("OnUpdate",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(inventory,null);
 Check(empty?inventory.WeaponCount==0:inventory.SelectedWeapon==w,"Direct default respects Start With No Weapons ("+empty+")");
 if(empty)Check(defaultGun.Active&&collider.Enabled,"Start empty keeps the default gun available as a world pickup");
 if(!empty){Check(!collider.Enabled,"Default world gun suspends pickup physics");Check(inventory.Drop()&&collider.Enabled&&defaultGun.Transform.WorldScale==new Vector3(2),"Dropping a default restores world scale and physics");}
}
{
 var type=typeof(ByteEngine.Core.Serialization.ComponentSerializer).GetNestedType("UiWidgetCodec",BindingFlags.NonPublic)!;
 var codec=(ByteEngine.Core.Serialization.IComponentCodec)Activator.CreateInstance(type,true)!;
 var widget=new UiWidget{Kind=UiWidgetKind.SegmentedBar,SegmentCount=8,SegmentAngleDegrees=-28,SegmentThickness=4,SegmentLength=9,SegmentEmptyColor=new(.1f,.2f,.3f,.4f),Value=2.5f,Maximum=8};
 var restored=(UiWidget)codec.Deserialize(codec.Serialize(widget,null!),null!);
 Check(restored.Kind==widget.Kind&&restored.Value==2.5f&&restored.Maximum==8&&restored.SegmentCount==8&&restored.SegmentAngleDegrees==-28&&restored.SegmentThickness==4&&restored.SegmentLength==9&&restored.SegmentEmptyColor==widget.SegmentEmptyColor,"Native reserve marker appearance and fractional fill survive save/load");
}

{
 var blend=new AnimationBlendSpace{TwoDimensional=true,Blend2DMode=AnimationBlend2DMode.DirectionalSpeed,Samples=[new(){Clip="idle"},new(){Clip="walk",Y=2},new(){Clip="strafe",X=2},new(){Clip="back",Y=-2},new(){Clip="left",X=-2},new(){Clip="run",Y=6}]};
 Check(Math.Abs(AnimationBlendWeights.DirectionalPlaybackRate(blend,6,0)-3)<.001f,"Missing sprint strafe plays the walk cycle three times faster at 6 versus 2 m/s");
 Check(AnimationBlendWeights.DirectionalPlaybackRate(blend,0,6)==1,"Dedicated forward sprint clip retains normal playback");
 Check(AnimationBlendWeights.DirectionalPlaybackRate(blend,2,0)==1&&AnimationBlendWeights.DirectionalPlaybackRate(blend,0,0)==1,"Walking and idle playback remain normal");
 Check(registry.GetArguments("ui.setBarFromHealth").Any(a=>a.Name=="source"&&a.IsObjectReference)&&registry.GetArguments("ui.setTextFromHealth").Any(a=>a.Name=="source"&&a.IsObjectReference),"Health HUD owners use scene object pickers and readable names in Event Sheets");
}

{
 var checkScene=new Scene("Empty muzzle authoredPoint");var muzzleRig=checkScene.CreateGameObject("Gun");muzzleRig.Transform.WorldPosition=new(3,2,1);muzzleRig.Transform.WorldScale=new(2);
 var muzzleDefinition=muzzleRig.AddComponent(new WeaponDefinition3D{MuzzleSource=WeaponMuzzleSource.SceneObject});var instance=muzzleRig.AddComponent(new WeaponInstance3D());
 var empty=checkScene.CreateGameObject("Barrel tip");empty.SetParent(muzzleRig,false);empty.Transform.LocalPosition=new(0,0,-.5f);muzzleDefinition.MuzzleObjectId=empty.Id;
 Check(instance.TryGetMuzzleWorldPosition(out var muzzle)&&muzzle==empty.Transform.WorldPosition,"Scene empty muzzle follows parent scale without any skeleton");
 muzzleRig.Transform.EulerAngles=new(0,90,0);Check(instance.TryGetMuzzleWorldPosition(out muzzle)&&muzzle==empty.Transform.WorldPosition,"Scene muzzle follows rotating weapon");
 muzzleDefinition.MuzzleObjectId=Guid.Empty;Check(!instance.TryGetMuzzleWorldPosition(out _),"Missing explicit scene muzzle is reported rather than resolving a wrong authoredPoint");
 var imported=new ByteEngine.Core.Assets.Importers.ImportedModel{Guid=Guid.NewGuid(),Name="Static test gun"};
 var model=(ByteEngine.Core.Assets.ModelAsset)Activator.CreateInstance(typeof(ByteEngine.Core.Assets.ModelAsset),BindingFlags.Instance|BindingFlags.NonPublic,null,[imported],null)!;
 var authoredPoint=new SkeletalSocketDefinition{Name="Muzzle",PositionOffset=new(.1f,.2f,-.5f)};model.ReplaceSockets([authoredPoint]);
 var renderer=muzzleRig.AddComponent(new ByteEngine.Core.Graphics.ThreeD.SkeletalMeshRenderer());
 typeof(ByteEngine.Core.Graphics.ThreeD.SkeletalMeshRenderer).GetField("_model",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(renderer,model);
 Check(SkeletalSocketResolver.TryGetSocketModelTransform(renderer,authoredPoint,out var rootPoint)&&rootPoint.Position==authoredPoint.PositionOffset,"Model-root attachment points resolve without a skeleton or bone");
 Check(SkeletalSocketResolver.TryGetSocketWorldTransform(renderer,"Muzzle",out var worldPoint)&&Vector3.Distance(worldPoint.Position,Vector3.Transform(authoredPoint.PositionOffset,muzzleRig.Transform.WorldMatrix))<.001f,"Model authoredPoint matches transformed preview position");
}
