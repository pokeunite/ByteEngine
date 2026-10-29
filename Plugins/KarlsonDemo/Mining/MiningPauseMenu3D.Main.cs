using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Runtime;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningPauseMenu3D
{
 public Vector3 MainMenuCameraOffset{get;set;}=new(22,11,26);
 public Vector3 MainMenuLookOffset{get;set;}=new(0,3,3);
 public float MainMenuFieldOfView{get;set;}=55;
 Camera3D? presentationCamera;Vector3 cameraPosition;Quaternion cameraRotation;float cameraFov;
 readonly List<UiWidget> hiddenWidgets=[];
 readonly List<ByteEngine.Core.Scene.Component> hiddenPlayerMeshes=[];
 void BeginMainPresentation()
 {
  var scene=GameObject.Scene!;
  foreach(var w in scene.GameObjects.SelectMany(o=>o.Components.OfType<UiWidget>()).Where(w=>w.Visible)){hiddenWidgets.Add(w);w.Visible=false;}
  var op=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningOperation3D>()).FirstOrDefault();
  if(op!=null&&scene.FindGameObject(op.PlayerObjectId) is {} player)foreach(var c in scene.GameObjects.Where(o=>o==player||o.IsDescendantOf(player)).SelectMany(o=>o.Components).Where(c=>c.Enabled&&c is MeshRenderer or SkeletalMeshRenderer)){hiddenPlayerMeshes.Add(c);c.Enabled=false;}
  presentationCamera=scene.ActiveCamera;if(presentationCamera==null)return;
  cameraPosition=presentationCamera.Transform.WorldPosition;cameraRotation=presentationCamera.Transform.WorldRotation;cameraFov=presentationCamera.FieldOfView;
  Vector3 centre=op!=null?scene.FindGameObject(op.DrillObjectId)?.Transform.WorldPosition??Vector3.Zero:Vector3.Zero;
  var eye=centre+MainMenuCameraOffset;presentationCamera.Transform.WorldPosition=eye;
  Matrix4x4.Invert(Matrix4x4.CreateLookAt(eye,centre+MainMenuLookOffset,Vector3.UnitY),out var pose);presentationCamera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(pose);presentationCamera.FieldOfView=MainMenuFieldOfView;
 }
 void EndMainPresentation()
 {
  if(presentationCamera!=null){presentationCamera.Transform.WorldPosition=cameraPosition;presentationCamera.Transform.WorldRotation=cameraRotation;presentationCamera.FieldOfView=cameraFov;presentationCamera=null;}
  var live=GameObject.Scene!.GameObjects.SelectMany(o=>o.Components).ToHashSet();
  foreach(var w in hiddenWidgets)if(live.Contains(w))w.Visible=true;hiddenWidgets.Clear();
  foreach(var c in hiddenPlayerMeshes)if(live.Contains(c))c.Enabled=true;hiddenPlayerMeshes.Clear();
 }

 public bool HasSavedGame {
  get { try {var op=GameObject.Scene?.GameObjects.SelectMany(o=>o.Components.OfType<MiningOperation3D>()).FirstOrDefault();if(op==null)return false;var path=Path.Combine(GameSaveStorage.GetDirectory(GameObject.Scene!.ProjectRoot!),Path.GetFileName(op.SaveSlot));if(!File.Exists(path))return false;return System.Text.Json.JsonSerializer.Deserialize<MiningOperation3D.MiningSave>(File.ReadAllText(path))?.Version==1;}catch(Exception e)when(e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException){return false;} }
 }
 void BuildMain(float w,float h)
 {
  float x=w*.13f,y=h*.38f;
  if(HasSavedGame)items.Add(new("CONTINUE",x,y,270,46,Resume));
  items.Add(new("NEW GAME",x,y+58,270,46,()=>ShowPage(MiningPausePage.NewCompany)));
  items.Add(new("SETTINGS",x,y+116,270,46,()=>ShowPage(MiningPausePage.Settings)));
  items.Add(new("QUIT",x,y+174,270,46,()=>ShowPage(MiningPausePage.Quit)));
 }
 void RenderMain(RenderContext c)
 {
  float s=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f),w=c.TargetWidth/s,h=c.TargetHeight/s;
  for(int i=0;i<160;i++){int x0=(int)MathF.Round(c.TargetWidth*i/160f),x1=(int)MathF.Round(c.TargetWidth*(i+1)/160f);float alpha=.95f*MathF.Pow(1-i/160f,2.1f);c.DrawUiQuad(new(x0,0),new(x1-x0,c.TargetHeight),new(0,0,0,alpha));}
  void T(string value,float x,float y,int size,Vector4 color)=>c.DrawUiText(value,HeadingFont,(int)(size*s),new(x*s,y*s),color,UiAnchor.TopLeft);
  T(string.IsNullOrWhiteSpace(CompanyTitle)?"MAIN MENU":CompanyTitle.ToUpperInvariant(),w*.13f,h*.22f,34,text);
  items.Clear();BuildMain(w,h);bool hovered=items.Any(i=>Inside(i.X,i.Y,i.W,i.H));
  for(int n=0;n<items.Count;n++){var i=items[n];bool selected=Inside(i.X,i.Y,i.W,i.H)||!hovered&&focus==n;T(i.Label,i.X,i.Y+6,28,selected?accent:text);if(selected){c.DrawUiQuad(new((i.X-90)*s,(i.Y+20)*s),new(9*s),accent);c.DrawUiLine(new((i.X-70)*s,(i.Y+24)*s),new((i.X-22)*s,(i.Y+24)*s),s,accent);}}

 }
 void StartNewCompany()
 {
  var scene=GameObject.Scene!;string root=GameSaveStorage.GetDirectory(scene.ProjectRoot!);
  string backup=Path.Combine(root,"PreviousCompany-"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(backup);
  foreach(var component in scene.GameObjects.SelectMany(o=>o.Components))
  {
   var persist=component.GetType().GetProperty("PersistProgress");var slot=component.GetType().GetProperty("SaveSlot");
   if(persist?.PropertyType!=typeof(bool)||slot?.GetValue(component) is not string name||Path.GetFileName(name)!=name)continue;
   string path=Path.Combine(root,name);if(File.Exists(path))File.Move(path,Path.Combine(backup,name),true);
   persist.SetValue(component,false);
  }
  skipNextStartupProject=scene.ProjectRoot;scene.RequestRestart();
 }
}
