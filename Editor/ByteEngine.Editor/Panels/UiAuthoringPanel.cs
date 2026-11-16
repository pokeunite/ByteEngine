using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ImGuiNET;
namespace ByteEngine.Editor.Panels;
/// <summary>Interactive resolution preview of actual authored UI. Selection uses the same layout/clip rules as gameplay.</summary>
internal sealed class UiAuthoringPanel:IDisposable
{
 private readonly SceneFramebuffer _framebuffer=new();
 private readonly UiSelectionTool _selection = new();
 private readonly (string Name,int Width,int Height)[] _sizes=[("1280 x 720",1280,720),("1920 x 1080",1920,1080),("800 x 600",800,600),("1600 x 600",1600,600),("720 x 1280",720,1280)];
 private int _size;
 private bool _outlines=true;
 public bool IsOpen {get;set;}
 public void Draw(EditorState state,Renderer2D renderer,Renderer3D renderer3D,int windowWidth,int windowHeight)
 {
  if(!IsOpen){_selection.Finish(state);return;}bool open=IsOpen;
  bool visible=ImGui.Begin("UI Authoring",ref open);IsOpen=open;
  if(!visible){_selection.Finish(state);ImGui.End();return;}
  var scene=state.DisplayedScene;
  if(ImGui.BeginCombo("Preview resolution",_sizes[_size].Name)){for(int i=0;i<_sizes.Length;i++)if(ImGui.Selectable(_sizes[i].Name,_size==i))_size=i;ImGui.EndCombo();}
  ImGui.SameLine();ImGui.Checkbox("Element outlines",ref _outlines);
  ImGui.TextDisabled("Click text or widgets to select. Drag to move. Alt-click cycles overlaps.");
  var size=_sizes[_size];var viewport=new Vector2(size.Width,size.Height);
  _framebuffer.RenderGame(renderer,renderer3D,scene,EditorMode.Edit,size.Width,size.Height,windowWidth,windowHeight,uiOnly:true);
  var available=Vector2.Max(ImGui.GetContentRegionAvail(),Vector2.One);float scale=Math.Min(available.X/size.Width,available.Y/size.Height);var imageSize=viewport*scale;
  var start=ImGui.GetCursorScreenPos();ImGui.Image(_framebuffer.TextureId,imageSize,new(0,1),new(1,0));
  _selection.Draw(state, start, imageSize, viewport, ImGui.IsItemHovered(), renderer.MeasureText, _outlines);
  ImGui.End();
 }
 public void Dispose()=>_framebuffer.Dispose();
}
