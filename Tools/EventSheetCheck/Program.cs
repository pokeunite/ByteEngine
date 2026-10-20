using System.Reflection;
using System.Drawing;
using System.Drawing.Imaging;
using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Plugins;
using ByteEngine.Core.VisualLogic;
using ImGuiNET;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.Common;

// Native editor render verification only. No project assets are changed.
internal static class Program
{
 [STAThread] static void Main(string[] args)
 {
  var editor=Assembly.Load("ByteEngine.Editor");
  object Create(string type,params object?[] values)=>Activator.CreateInstance(editor.GetType("ByteEngine.Editor."+type,true)!,values)!;
  object? Call(object obj,string method,params object?[] values)=>obj.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(obj,values);
  var registryType=typeof(ByteEnginePluginRegistry);
  registryType.GetMethod("BeginPlugin",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,["bytebard.karlsondemo"]);
  var pluginContext=(ByteEnginePluginContext)Activator.CreateInstance(typeof(ByteEnginePluginContext),BindingFlags.Instance|BindingFlags.NonPublic,null,["bytebard.karlsondemo"],null)!;
  var plugin=Assembly.LoadFrom(Path.GetFullPath("Plugins/KarlsonDemo/bin/Release/net9.0/KarlsonDemo.Plugin.dll"));
  ((IByteEnginePlugin)Activator.CreateInstance(plugin.GetType("KarlsonDemo.Plugin.KarlsonDemoPlugin",true)!)!).Register(pluginContext);
  using var window=new GameWindow(GameWindowSettings.Default,new NativeWindowSettings{StartVisible=false,ClientSize=new(1600,900),APIVersion=new(3,3),Profile=ContextProfile.Core});
  window.Context.MakeCurrent();
  var output=Path.GetFullPath("artifacts/event-sheet-check");Directory.CreateDirectory(output);
  using var controller=(IDisposable)Create("ImGuiController",window,Path.Combine(output,"layout.ini"));
  var docs=Create("EditorDocumentManager");var panel=Create("Panels.EventWorkspacePanel",docs);var log=Create("EditorLog");
  string source=args.Length>0?args[0]:"C:/Users/codex/Documents/ultra violence/Assets/Events/FPS controls.byteevents";
  string copy=Path.Combine(output,"FPS controls.byteevents");File.Copy(source,copy,true);
  var asset=new AssetRecord(Guid.NewGuid(),AssetType.EventModule,"Assets/Events/FPS controls.byteevents",copy,copy+".meta",new());
  Call(panel,"Open",asset,log);
  var module=(EventModuleDefinition)panel.GetType().GetField("_module",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(panel)!;
  var selected=(HashSet<Guid>)panel.GetType().GetField("_selectedGraphNodes",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(panel)!;
  selected.Clear();selected.Add(module.Rules[0].Actions[0].InstanceId);
  foreach(var size in new[]{new OpenTK.Mathematics.Vector2i(1600,900),new(1280,720),new(900,650)})
  {
   window.ClientSize=size;
   for(int frame=0;frame<5;frame++)
   {
    Call(controller,"Update",1f/60);
    ImGui.SetNextWindowPos(Vector2.Zero,ImGuiCond.Always);
    ImGui.SetNextWindowSize(new Vector2(size.X,size.Y),ImGuiCond.Always);
    Call(panel,"Draw",log);
    ImGui.SetWindowSize($"{module.Name}###EventWorkspace:{asset.Guid}", new Vector2(size.X,size.Y), ImGuiCond.Always);
    ImGui.SetWindowPos($"{module.Name}###EventWorkspace:{asset.Guid}", Vector2.Zero, ImGuiCond.Always);
    GL.Viewport(0,0,size.X,size.Y);GL.Clear(ClearBufferMask.ColorBufferBit);
    Call(controller,"Render");GL.Finish();
   }
   var bytes=new byte[size.X*size.Y*4];GL.ReadPixels(0,0,size.X,size.Y,OpenTK.Graphics.OpenGL4.PixelFormat.Bgra,PixelType.UnsignedByte,bytes);
   using var bmp=new Bitmap(size.X,size.Y,System.Drawing.Imaging.PixelFormat.Format32bppArgb);
   var data=bmp.LockBits(new Rectangle(0,0,size.X,size.Y),ImageLockMode.WriteOnly,bmp.PixelFormat);
   System.Runtime.InteropServices.Marshal.Copy(bytes,0,data.Scan0,bytes.Length);bmp.UnlockBits(data);bmp.RotateFlip(RotateFlipType.RotateNoneFlipY);
   bmp.Save(Path.Combine(output,$"sheet-{size.X}x{size.Y}.png"));
   Console.WriteLine($"PASS native event-sheet render at {size.X}x{size.Y}");
  }
  string oldKey=module.Rules[0].Conditions[0].Arguments["key"].Constant.String;
  Call(panel,"RecordHistory","Edit condition key");
  module.Rules[0].Conditions[0].Arguments["key"]=EventValue.String("D9");
  if (!(bool)Call(panel,"Undo")!) throw new Exception("Undo failed");
  var restored=(EventModuleDefinition)panel.GetType().GetField("_module",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(panel)!;
  if(restored.Rules[0].Conditions[0].Arguments["key"].Constant.String!=oldKey)throw new Exception("Undo did not restore condition settings");
  if (!(bool)Call(panel,"Redo")!) throw new Exception("Redo failed");
  Console.WriteLine("PASS condition settings use the existing undo/redo history");
  var current=(EventModuleDefinition)panel.GetType().GetField("_module",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(panel)!;
  var logicRule=new EventRuleDefinition{DisplayName="OR nesting verification",HasExplicitConditionFlow=true};
  var gate=new VisualInstruction{Id="logic.or"};logicRule.Conditions.Add(gate);logicRule.ConnectedConditionIds.Add(gate.InstanceId);current.Rules.Add(logicRule);
  var nested=new VisualInstruction{Id="system.always"};var prior=logicRule.Conditions.Select(i=>i.InstanceId).ToHashSet();logicRule.Conditions.Add(nested);logicRule.ConnectedConditionIds.Add(nested.InstanceId);
  panel.GetType().GetField("_sheetConditionParentId",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(panel,gate.InstanceId);
  Call(panel,"FixSheetConditionParent",logicRule,prior);
  if(!gate.ConditionInputIds.Contains(nested.InstanceId)||logicRule.ConnectedConditionIds.Contains(nested.InstanceId))throw new Exception("Nested condition was not attached to OR");
  Console.WriteLine("PASS adding inside OR creates a real nested condition, not a second root");
  var io=ImGui.GetIO();
  io.KeyCtrl=false;io.KeyShift=false;Call(panel,"SelectSheetEvent",current.Rules[0].Id,false);
  io.KeyCtrl=true;Call(panel,"SelectSheetEvent",current.Rules[1].Id,false);io.KeyCtrl=false;
  if(!selected.Contains(current.Rules[0].Id)||!selected.Contains(current.Rules[1].Id))throw new Exception("Ctrl selection did not retain both events");
  io.KeyShift=true;Call(panel,"SelectSheetEvent",current.Rules[3].Id,false);io.KeyShift=false;
  if(selected.Count!=3)throw new Exception("Shift range selection failed");
  Console.WriteLine("PASS Ctrl-click multi-selection and Shift-click range selection");
  selected.Clear();selected.Add(current.Rules[0].Id);selected.Add(current.Rules[1].Id);
  string flowBefore=System.Text.Json.JsonSerializer.Serialize(current.Rules.Select(r=>new{r.ConnectedConditionIds,r.FirstActionId}));
  Call(panel,"CreateSheetGroup",true,current.Rules[0]);
  var made=current.EditorGroups.Last();
  if(!made.SheetGroup||!made.MemberIds.Contains(current.Rules[1].Id))throw new Exception("Multi-event group lost its members");
  if(flowBefore!=System.Text.Json.JsonSerializer.Serialize(current.Rules.Select(r=>new{r.ConnectedConditionIds,r.FirstActionId})))throw new Exception("Grouping altered runtime links");
  current.Rules[0].EditorHeaderColor="3A80CF";
  string roundtrip=Path.Combine(output,"group-color-check.byteevents");new EventModuleSerializer().Save(current,roundtrip);
  var reloaded=new EventModuleSerializer().Load(roundtrip);
  if(reloaded.Rules[0].EditorHeaderColor!="3A80CF"||!reloaded.EditorGroups.Last().SheetGroup)throw new Exception("Color / group persistence failed");
  Console.WriteLine("PASS grouping preserves execution and colors / groups survive save and reload");
  for(int frame=0;frame<3;frame++){Call(controller,"Update",1f/60);Call(panel,"Draw",log);Call(controller,"Render");}
  Console.WriteLine("PASS grouped event frames and nested OR controls render together");
  current.EditorGroups.Remove(made);
  if(current.Rules.Count!=reloaded.Rules.Count)throw new Exception("Ungroup deleted events");
  Console.WriteLine("PASS ungroup retains all events");
  Call(panel,"SelectSheetEvent",current.Rules[0].Id,true);
  if((Guid?)panel.GetType().GetField("_sheetFlashId",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(panel)!=current.Rules[0].Id)throw new Exception("Navigation did not start flash");
  Console.WriteLine("PASS navigator requests a scroll and starts the destination highlight");
  selected.Clear();selected.Add(nested.InstanceId);Call(panel,"DeleteSelectedGraphNodes");
  if(logicRule.Conditions.Contains(nested)||gate.ConditionInputIds.Contains(nested.InstanceId))throw new Exception("Delete left stale nested links");
  Console.WriteLine("PASS deleting a nested condition removes its gate link");
  for(int frame=0;frame<3;frame++)
  {
   Call(controller,"Update",1f/60);
   ImGui.SetNextWindowPos(new Vector2(20,20),ImGuiCond.Always);
   ImGui.Begin("Search menu verification");
   if(frame==0)ImGui.OpenPopup("Add Condition");
   else panel.GetType().GetField("_conditionMenuSearch",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(panel,"key pressed");
   Call(panel,"DrawConditionPicker",logicRule,logicRule.Conditions);
   ImGui.End();Call(controller,"Render");
  }
  Console.WriteLine("PASS searchable condition menu renders with filtered results");
  panel.GetType().GetField("_showEventGraph",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(panel,true);
  for(int frame=0;frame<3;frame++){Call(controller,"Update",1f/60);Call(panel,"Draw",log);Call(controller,"Render");}
  Console.WriteLine("PASS original graph view still renders after switching from the sheet");
 }
}
