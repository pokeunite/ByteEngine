using System.Numerics;
using System.Reflection;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
using ByteEngine.Editor;
using ByteEngine.Editor.Commands;
using ByteEngine.Editor.Panels;
using DesertTerrain;
using ImGuiNET;
namespace ByteEngine.Tests;
internal static class TerrainSculptTests
{
 public static void Run(string file)
 {
  using var project=EditorProjectContext.Open(file,Console.WriteLine);var scene=new Scene("Terrain authoring regression");var terrain=scene.CreateGameObject("Editable landscape").AddComponent(new DesertTerrain3D{Cells=64,Spacing=1,DuneHeight=0,FlatCenterRadius=100,AutoLoadSavedSand=false});terrain.EnsureGenerated();
  void Check(bool result,string message)=>DesertTerrainTests.Check(result,message);
  for(int i=0;i<10;i++)terrain.Sculpt(Vector3.Zero,8,4,.1f,TerrainBrushMode.Raise,0);
  Check(Math.Abs(terrain.HeightAt(32,32)-4)<.001f&&terrain.HeightAt(44,32)==0,"Raise brush shapes a mound with unchanged terrain outside its radius");
  terrain.Sculpt(Vector3.Zero,8,4,.1f,TerrainBrushMode.Lower,0);Check(Math.Abs(terrain.HeightAt(32,32)-3.6f)<.001f,"Lower reverses height edits");
  float before=terrain.HeightAt(32,32);for(int i=0;i<30;i++)terrain.Sculpt(Vector3.Zero,8,15,.1f,TerrainBrushMode.Flatten,2);Check(Math.Abs(terrain.HeightAt(32,32)-2)<.01f,"Flatten converges to the requested plateau height");
  terrain.Sculpt(Vector3.Zero,.6f,20,.1f,TerrainBrushMode.Raise,0);float spike=terrain.HeightAt(32,32);for(int i=0;i<20;i++)terrain.Sculpt(Vector3.Zero,6,20,.1f,TerrainBrushMode.Smooth,0);Check(terrain.HeightAt(32,32)<spike-.5f,"Smooth removes a local height spike");
  string sculpt=terrain.ExportSculpt();terrain.ResetSand();float authored=terrain.HeightAt(32,32);terrain.Regenerate();Check(Math.Abs(terrain.HeightAt(32,32)-authored)<.00001f,"Source regeneration preserves the authored layer");
  Check(terrain.Cast(new(0,50,0),-Vector3.UnitY,0,100,out float distance,out _)&&Math.Abs(50-distance-authored)<.001f,"Terrain collision uses the sculpted height immediately");
  terrain.StampTrack(new(-.5f,authored,0),new(.5f,authored,0),1,.05f);Check(terrain.HeightAt(32,32)<authored,"Tyre ruts remain interactive on sculpted terrain");terrain.ResetSand();Check(Math.Abs(terrain.HeightAt(32,32)-authored)<.00001f,"Reset tracks preserves authored terrain");
  string save=Path.Combine(Path.GetTempPath(),"TerrainSculpt-"+Guid.NewGuid().ToString("N")+".bytescene");project.Scenes.Save(scene,save);var restored=project.Scenes.Load(save);var restoredTerrain=restored.GameObjects.SelectMany(o=>o.Components).OfType<DesertTerrain3D>().Single();Check(Math.Abs(restoredTerrain.HeightAt(32,32)-authored)<.00001f,"Scene save/reload restores authored terrain separately from ruts");File.Delete(save);
  var state=new EditorState{EditorScene=scene,Project=project.Project,ProjectFilePath=file,SelectedObject=terrain.GameObject};var active=scene;state.Undo=new UndoManager(project.Scenes,s=>{foreach(var o in active.GameObjects.ToArray())active.DestroyGameObject(o);active=s;});state.Undo.Reset(state,true);
  state.Undo.BeginGesture(state,"Sculpt Terrain");for(int i=0;i<8;i++)terrain.Sculpt(new(15,0,15),5,4,.1f,TerrainBrushMode.Raise,0);state.Undo.CommitGesture(state);state.Undo.Undo(state);var undone=state.EditorScene.GameObjects.SelectMany(o=>o.Components).OfType<DesertTerrain3D>().Single();Check(undone.HeightAt(47,47)==0,"Undo restores the complete brush stroke");state.Undo.Redo(state);terrain=state.EditorScene.GameObjects.SelectMany(o=>o.Components).OfType<DesertTerrain3D>().Single();Check(terrain.HeightAt(47,47)>3,"Redo restores the complete authored stroke");
  // Exercise the native Scene View mouse brush and its undo transaction.
  state.SelectedObject=terrain.GameObject;state.Camera3D.Position=new(0,25,15);state.Camera3D.Yaw=-90;state.Camera3D.Pitch=-MathF.Atan2(25,15)*180/MathF.PI;
  nint previousContext=ImGui.GetCurrentContext(),context=ImGui.CreateContext();try{
   ImGui.SetCurrentContext(context);var io=ImGui.GetIO();io.DisplaySize=new(800,600);io.DeltaTime=1f/60;io.Fonts.GetTexDataAsRGBA32(out nint pixels,out int width,out int height);var tool=new TerrainSculptTool();typeof(TerrainSculptTool).GetField("_enabled",BindingFlags.NonPublic|BindingFlags.Instance)!.SetValue(tool,true);
   float start=terrain.HeightAt(32,32);for(int frame=0;frame<14;frame++){io.AddMousePosEvent(400,300);if(frame==1)io.AddMouseButtonEvent(0,true);if(frame==12)io.AddMouseButtonEvent(0,false);ImGui.NewFrame();ImGui.Begin("Sculpt Test");tool.DrawToolbar(state);Check(tool.Update(state,true,Vector2.Zero,new(800,600)),"Sculpt brush owns scene pointer while enabled");ImGui.End();ImGui.EndFrame();}
   Check(terrain.HeightAt(32,32)>start+.1f,"Native mouse drag modifies the selected terrain");Check(state.Undo.UndoName=="Sculpt Terrain","Native mouse release commits a single undo gesture");state.Undo.Undo(state);var original=state.EditorScene.GameObjects.SelectMany(o=>o.Components).OfType<DesertTerrain3D>().Single();Check(Math.Abs(original.HeightAt(32,32)-start)<.0001f,"Native brush drag can be undone");
  }finally{ImGui.DestroyContext(context);ImGui.SetCurrentContext(previousContext);}
  foreach(var o in active.GameObjects.ToArray())active.DestroyGameObject(o);foreach(var o in restored.GameObjects.ToArray())restored.DestroyGameObject(o);Console.WriteLine("Terrain sculpt authoring tests passed.");
 }
}
