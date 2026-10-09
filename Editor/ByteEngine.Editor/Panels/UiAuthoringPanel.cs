using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ImGuiNET;
namespace ByteEngine.Editor.Panels;
/// <summary>Interactive resolution preview of actual authored UI. Selection uses the same layout/clip rules as gameplay.</summary>
internal sealed class UiAuthoringPanel:IDisposable
{
 private readonly SceneFramebuffer _framebuffer=new();
 private readonly (string Name,int Width,int Height)[] _sizes=[("1280 x 720",1280,720),("1920 x 1080",1920,1080),("800 x 600",800,600),("1600 x 600",1600,600),("720 x 1280",720,1280)];
 private int _size;
 private bool _outlines=true;
 public bool IsOpen {get;set;}
 public void Draw(EditorState state,Renderer2D renderer,Renderer3D renderer3D,int windowWidth,int windowHeight)
 {
  if(!IsOpen)return;bool open=IsOpen;
  bool visible=ImGui.Begin("UI Authoring",ref open);IsOpen=open;
  if(!visible){ImGui.End();return;}
  var scene=state.DisplayedScene;
  if(ImGui.BeginCombo("Preview resolution",_sizes[_size].Name)){for(int i=0;i<_sizes.Length;i++)if(ImGui.Selectable(_sizes[i].Name,_size==i))_size=i;ImGui.EndCombo();}
  ImGui.SameLine();ImGui.Checkbox("Widget outlines",ref _outlines);
  ImGui.TextDisabled("Click a widget to edit it in Inspector. Preview size does not change project settings.");
  var size=_sizes[_size];var viewport=new Vector2(size.Width,size.Height);
  _framebuffer.RenderGame(renderer,renderer3D,scene,EditorMode.Edit,size.Width,size.Height,windowWidth,windowHeight,uiOnly:true);
  var available=Vector2.Max(ImGui.GetContentRegionAvail(),Vector2.One);float scale=Math.Min(available.X/size.Width,available.Y/size.Height);var imageSize=viewport*scale;
  var start=ImGui.GetCursorScreenPos();ImGui.Image(_framebuffer.TextureId,imageSize,new(0,1),new(1,0));
  var widgets=scene.GameObjects.Where(o=>o.ActiveInHierarchy).SelectMany(o=>o.Components.OfType<UiWidget>()).Where(w=>w.Enabled&&w.Visible&&UiLayout.IsVisible(w.GameObject)).OrderBy(w=>w.OrderInLayer).ToArray();
  if(_outlines)
  {
   var draw=ImGui.GetWindowDrawList();draw.PushClipRect(start,start+imageSize,true);
   foreach(var widget in widgets){var rect=UiLayout.Resolve(widget.GameObject,widget.Anchor,widget.Offset,widget.Size,viewport);uint color=ReferenceEquals(state.SelectedObject,widget.GameObject)?0xFF40BBFF:0x7068C0A0;draw.AddRect(start+rect.Position*scale,start+(rect.Position+rect.Size)*scale,color);}
   draw.PopClipRect();
  }
  if(state.Mode==EditorMode.Edit&&ImGui.IsItemHovered()&&ImGui.IsMouseClicked(ImGuiMouseButton.Left))
  {
   var point=(ImGui.GetMousePos()-start)/scale;
   if(widgets.Reverse().FirstOrDefault(w=>w.Contains(point,viewport)) is {} selected)state.SelectedObject=selected.GameObject;
  }
  ImGui.End();
 }
 public void Dispose()=>_framebuffer.Dispose();
}
