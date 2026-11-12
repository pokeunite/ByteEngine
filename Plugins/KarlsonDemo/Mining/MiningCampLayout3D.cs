using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
/// <summary>Between-operation layout editing; previews are ghosts, committed moves preserve object identity and state.</summary>
public sealed partial class MiningCampLayout3D:Component
{
 [SceneObjectReference] public Guid OperationObjectId{get;set;}
 [SceneObjectReference] public Guid InterfaceObjectId{get;set;}
 [SceneObjectReference] public Guid NavigationObjectId{get;set;}
 [SceneObjectReference] public Guid EditButtonObjectId{get;set;}
 [SceneObjectReference] public Guid SpawnClearanceObjectId{get;set;}
 public Key EditKey{get;set;}=Key.Tab;
 public Vector2 BuildMinimum{get;set;}=new(-25,-85);
 public Vector2 BuildMaximum{get;set;}=new(45,26);
 public float GroundHeight{get;set;}
 public float GridSize{get;set;}=.5f;
 public float RotationStep{get;set;}=15;
 public float SpawnClearance{get;set;}=1.2f;
 public float CameraWidth{get;set;}=60;
 public bool PersistProgress{get;set;}=true;
 public string SaveSlot{get;set;}="mining-camp-layout-v1.json";
 public AssetReference Font{get;set;}=AssetReference.Empty;
 public bool IsEditing{get;private set;}
 public bool CanEdit=>initialized&&Op?.CanUseIndustry==true&&Ui?.Screen==MiningScreen.None;
 public Guid SelectedObjectId{get;private set;}
 public Vector3 PreviewPosition{get;private set;}
 public float PreviewYaw{get;private set;}
 public bool PreviewValid{get;private set;}
 public string Status{get;private set;}="";
 public override int UpdateOrder=>-600;
 public override int? RenderOrder=>1100;
 MiningOperation3D? Op=>GameObject.Scene?.FindGameObject(OperationObjectId)?.GetComponent<MiningOperation3D>();
 MiningBusinessUi3D? Ui=>GameObject.Scene?.FindGameObject(InterfaceObjectId)?.GetComponent<MiningBusinessUi3D>();
 MiningDefensePlanner3D? Planner=>GameObject.GetComponent<MiningDefensePlanner3D>();
 GameObject? Selected=>GameObject.Scene?.FindGameObject(SelectedObjectId);
 readonly Dictionary<Guid,BoundingBox3D> bounds=[];
 GameObject? editCamera,ghost;Guid oldCameraId;bool oldCameraActive;int warmup;bool initialized;
 bool restoreControl,restoreView,restoreBoom,restoreMotor,restoreTop;
 Vector3 grabOffset;bool followPointer;double committedAt=-1000;
 readonly Material outline=new(){Shading=MaterialShadingMode.Unlit};
 IEnumerable<GameObject> Objects=>GameObject.Scene!.GameObjects.Where(o=>o.ActiveInHierarchy&&(o.GetComponent<MiningCampObject3D>() is {Enabled:true,AllowRelocation:true}||Planner?.Owns(o)==true));
 public bool ToggleEdit(){if(IsEditing){ExitEdit();return true;}return EnterEdit();}
 public bool EnterEdit()
 {
  if(!CanEdit){Status="Close other menus before arranging the camp.";return false;}
  Planner?.Cancel();var player=GameObject;
  if(player.GetComponent<PlayerController3D>() is {} control){restoreControl=control.Enabled;control.Enabled=false;}
  if(player.GetComponent<MiningThirdPerson3D>() is {} view){restoreView=view.Enabled;view.Enabled=false;}
  if(player.GetComponent<CameraBoom3D>() is {} boom){restoreBoom=boom.Enabled;boom.Enabled=false;}
  if(player.GetComponent<CharacterController3D>() is {} motor){restoreMotor=motor.Enabled;motor.Move(Vector3.Zero);motor.Enabled=false;}
  if(player.GetComponent<TopDownController3D>() is {} top){restoreTop=top.InputEnabled;top.InputEnabled=false;}
  if(GameObject.Scene!.ActiveCamera is {} camera){oldCameraId=camera.GameObject.Id;oldCameraActive=camera.ActiveGameCamera;camera.ActiveGameCamera=false;}
  editCamera=GameObject.Scene.CreateGameObject("Temporary camp arrangement camera");var focusIds=new[]{Ui?.AmmoMachineObjectId??Guid.Empty,Ui?.RefineryMachineObjectId??Guid.Empty,Ui?.TerminalObjectId??Guid.Empty,Op?.DrillObjectId??Guid.Empty};var focusObjects=focusIds.Select(id=>GameObject.Scene.FindGameObject(id)).Where(o=>o!=null).ToArray();var centre=focusObjects.Length>0?new Vector3(focusObjects.Average(o=>o!.Transform.WorldPosition.X),GroundHeight,focusObjects.Average(o=>o!.Transform.WorldPosition.Z)):new Vector3((BuildMinimum.X+BuildMaximum.X)/2,GroundHeight,(BuildMinimum.Y+BuildMaximum.Y)/2);var eye=centre+new Vector3(0,45,24);var target=centre;
  editCamera.Transform.WorldPosition=eye;Matrix4x4.Invert(Matrix4x4.CreateLookAt(eye,target,Vector3.UnitY),out var matrix);editCamera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(matrix);editCamera.AddComponent(new Camera3D{ActiveGameCamera=true,Orthographic=true,OrthographicWidth=Math.Clamp(CameraWidth,30,100)});
  draggingView=false;IsEditing=true;Input.RequestUiPointer(this,true);UiNavigation.Focus(null);Status="Choose a machine or placed defence to move.";return true;
 }
 public void ExitEdit()
 {
  if(!IsEditing)return;committedAt=Time.TotalTime;CancelMove();IsEditing=false;Input.RequestUiPointer(this,false);UiNavigation.Focus(null);
  if(editCamera!=null&&editCamera.Scene==GameObject.Scene)GameObject.Scene!.DestroyGameObject(editCamera);editCamera=null;
  if(GameObject.Scene?.FindGameObject(oldCameraId)?.GetComponent<Camera3D>() is {} camera)camera.ActiveGameCamera=oldCameraActive;
  if(GameObject.GetComponent<PlayerController3D>() is {} control)control.Enabled=restoreControl;
  if(GameObject.GetComponent<MiningThirdPerson3D>() is {} view)view.Enabled=restoreView;
  if(GameObject.GetComponent<CameraBoom3D>() is {} boom)boom.Enabled=restoreBoom;
  if(GameObject.GetComponent<CharacterController3D>() is {} motor)motor.Enabled=restoreMotor;
  if(GameObject.GetComponent<TopDownController3D>() is {} top)top.InputEnabled=restoreTop;
 }
 public bool BeginMove(Guid id)
 {
  if(!IsEditing||Objects.FirstOrDefault(o=>o.Id==id) is not {} root)return false;
  CancelMove();SelectedObjectId=id;PreviewPosition=root.Transform.WorldPosition;PreviewYaw=Yaw(root);grabOffset=Vector3.Zero;followPointer=true;PreviewValid=true;Status="Move the preview, then click to confirm.";CreateGhost(root);return true;
 }
 public void SetPreview(Vector3 point,float yaw)
 {
  if(Selected==null)return;PreviewPosition=new(point.X,GroundHeight,point.Z);PreviewYaw=NormalizeYaw(yaw);PreviewValid=Validate(Selected,PreviewPosition,PreviewYaw,out var reason);Status=PreviewValid?"Ready to place — no cost.":reason;
  if(ghost!=null){ghost.Transform.WorldPosition=PreviewPosition;ghost.Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,PreviewYaw*MathF.PI/180);}
 }
 public void RotatePreview(float degrees)=>SetPreview(PreviewPosition,PreviewYaw+degrees);
 public void CancelMove(){SelectedObjectId=Guid.Empty;PreviewValid=false;followPointer=false;if(ghost!=null&&ghost.Scene==GameObject.Scene)GameObject.Scene!.DestroyGameObject(ghost);ghost=null;}
 protected override void OnUpdate()
 {
  if(!initialized){if(++warmup<3)return;initialized=true;if(PersistProgress)LoadLayout();}
  if(IsEditing&&(Op?.CanUseIndustry!=true||Ui?.Screen!=MiningScreen.None))ExitEdit();
  if(Input.IsKeyPressed(EditKey)&&Ui?.Screen==MiningScreen.None)ToggleEdit();
  UpdateButton();
  if(!IsEditing)return;
  bool panningView=UpdateCampCamera();
  if(Input.IsKeyPressed(Key.Escape)||Input.IsMouseButtonPressed(MouseButton.Right)){if(Selected!=null)CancelMove();else ExitEdit();return;}
  if(Input.IsKeyPressed(Key.Q))RotatePreview(-Math.Clamp(RotationStep,1,90));if(Input.IsKeyPressed(Key.E))RotatePreview(Math.Clamp(RotationStep,1,90));
  if(!panningView&&(PickCampTray()||PickCampIcon()))return;
  if(panningView||!PointerGround(out var point))return;
  if(Selected!=null&&followPointer){var p=point-grabOffset;float grid=Math.Clamp(GridSize,.1f,4);p.X=MathF.Round(p.X/grid)*grid;p.Z=MathF.Round(p.Z/grid)*grid;SetPreview(p,PreviewYaw);}
  if(!Input.IsMouseButtonPressedForUi(MouseButton.Left)||ButtonHovered())return;
  if(Selected!=null){ConfirmMove();return;}
  var chosen=Pick(point);if(chosen!=null&&BeginMove(chosen.Id)){grabOffset=point-chosen.Transform.WorldPosition;grabOffset.Y=0;followPointer=true;}
 }
 void UpdateButton()
 {
  if(GameObject.Scene?.FindGameObject(EditButtonObjectId)?.GetComponent<UiWidget>() is not {} button)return;
  button.GameObject.Active=false;button.Visible=false;button.Interactable=false;
 }
 bool ButtonHovered(){float s=Math.Max(.01f,Math.Min(Input.GameViewSize.X/1280,Input.GameViewSize.Y/720));var p=Input.GameViewMousePosition/s;return campTrayHits.Any(t=>p.X>=t.Rect.X&&p.X<=t.Rect.X+t.Rect.Z&&p.Y>=t.Rect.Y&&p.Y<=t.Rect.Y+t.Rect.W);}
 bool PointerGround(out Vector3 point)
 {
  point=default;if(editCamera?.GetComponent<Camera3D>() is not {} camera)return false;
  var pointer=Input.GameViewPointerNormalized;var size=Input.GameViewSize;Matrix4x4.Invert(camera.GetRenderViewMatrix()*camera.GetProjectionMatrix(size.X/Math.Max(1,size.Y)),out var inv);
  var a=Vector4.Transform(new Vector4(pointer.X*2-1,1-pointer.Y*2,-1,1),inv);var b=Vector4.Transform(new Vector4(pointer.X*2-1,1-pointer.Y*2,1,1),inv);var from=new Vector3(a.X,a.Y,a.Z)/a.W;var to=new Vector3(b.X,b.Y,b.Z)/b.W;var ray=to-from;if(Math.Abs(ray.Y)<.0001f)return false;float t=(GroundHeight-from.Y)/ray.Y;point=from+ray*t;return t>=0&&float.IsFinite(point.X)&&float.IsFinite(point.Z)&&Input.IsGameViewHovered;
 }
 GameObject? Pick(Vector3 point)=>Objects.Where(o=>Contains(WorldBounds(o,o.Transform.WorldPosition,Yaw(o)),point,.1f)).OrderBy(o=>Vector3.DistanceSquared(o.Transform.WorldPosition,point)).FirstOrDefault();
 void CreateGhost(GameObject root)
 {
  ghost=GameObject.Scene!.CreateGameObject("Camp move preview");Matrix4x4.Decompose(root.Transform.WorldMatrix,out var scale,out var rotation,out var position);ghost.Transform.WorldPosition=position;ghost.Transform.WorldRotation=rotation;ghost.Transform.LocalScale=scale;Matrix4x4.Invert(root.Transform.WorldMatrix,out var inverse);
  foreach(var o in GameObject.Scene.GameObjects.Where(o=>o.ActiveInHierarchy&&MiningCampGeometry.Under(o,root)).ToArray())
  {
   var skin=o.GetComponent<SkeletalMeshRenderer>();var primitive=o.GetComponent<MeshRenderer>();if(skin is not {Visible:true}&&primitive is not {Visible:true})continue;
   var copy=GameObject.Scene.CreateGameObject("Camp ghost visual");copy.SetParent(ghost,false);Matrix4x4.Decompose(o.Transform.WorldMatrix*inverse,out var s,out var r,out var p);copy.Transform.LocalPosition=p;copy.Transform.LocalRotation=r;copy.Transform.LocalScale=s;
   if(skin!=null){var visual=copy.AddComponent(new SkeletalMeshRenderer{Model=skin.Model,PlayOnStart=false,Visible=true});for(int i=0;i<32;i++){visual.GetMaterialOverrides(i).Opacity=.35f;visual.GetMaterialOverrides(i).BaseColor=new(.12f,.85f,1,1);}}
   else if(primitive!=null)copy.AddComponent(new MeshRenderer{Mesh=primitive.Mesh,UsePrimitive=primitive.UsePrimitive,Primitive=primitive.Primitive,CastShadows=false,Material=new(){BaseColor=new(.12f,.85f,1,.35f),BlendMode=BlendMode3D.AlphaBlend,DepthWriteMode=DepthWriteMode3D.Disabled}});
  }
 }
 protected override void OnStop()=>ExitEdit();
 protected override void OnDestroy(){ExitEdit();Input.RequestUiPointer(this,false);}
 static float NormalizeYaw(float value)=>float.IsFinite(value)?((value%360)+360)%360:0;
 static float Yaw(GameObject o){var q=o.Transform.WorldRotation;return NormalizeYaw(MathF.Atan2(2*(q.W*q.Y+q.X*q.Z),1-2*(q.Y*q.Y+q.Z*q.Z))*180/MathF.PI);}
 string Name(GameObject o)=>o.GetComponent<MiningCampObject3D>()?.DisplayName??o.GetComponent<MiningDefense3D>()?.Kind.ToString()??o.Name;
 protected override void OnRender(RenderContext c)
 {
  if(!IsEditing)return;DrawDefenceRanges(c);float s=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f),w=c.TargetWidth/s,h=c.TargetHeight/s;
  SupportUiArt.Panel(c,s,16,16,330,50);SupportUiArt.T(c,s,"ARRANGE & BUILD",28,23,20,true);SupportUiArt.T(c,s,"Select a machine or defence to move",28,47,13);
  DrawCampTray(c,s,w,h);
  if(Selected is {} selected)
  {
   var b=WorldBounds(selected,PreviewPosition,PreviewYaw);outline.BaseColor=PreviewValid?new(.15f,.9f,1,1):new(1,.24f,.1f,1);
   void Bar(Vector3 p,Vector3 size)=>c.RenderWorld.Submit(c.Renderer3D.GetPrimitive(PrimitiveMeshType.Cube),outline,Matrix4x4.CreateScale(size)*Matrix4x4.CreateTranslation(p),RenderQueue3D.Opaque,true,false,false);
   foreach(float x in new[]{b.Minimum.X,b.Maximum.X})Bar(new(x,GroundHeight+.08f,(b.Minimum.Z+b.Maximum.Z)/2),new(.08f,.05f,b.Size.Z));foreach(float z in new[]{b.Minimum.Z,b.Maximum.Z})Bar(new((b.Minimum.X+b.Maximum.X)/2,GroundHeight+.08f,z),new(b.Size.X,.05f,.08f));
  }
  if(editCamera?.GetComponent<Camera3D>() is {} camera)DrawCampIcons(c,camera,s,w,h);
 }
}
