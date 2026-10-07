using GoblinScrapper.Construction;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
namespace ByteEngine.Tests;
internal sealed partial class GoblinVehicleDiagnostic {
 private void CheckNativeTuning(Scene scene,VehicleBuilder3D builder){
  var c=VehiclePartCatalog.Load(Path.Combine(_project!.ProjectRoot,"Assets/GarageUI/parts-catalog.json"));builder.RestoreAssembly(ContraptionTests.Build(c).ToJson());
  for(int i=0;i<30;i++)TickInput(scene,new(.98f,.5f),[]);
  int id=builder.Assembly!.Parts.Values.First(p=>c[p.File].ReferenceId is 2 or 46).Id;
  TickInput(scene,new(832f/1280,24f/720),[],true);TickInput(scene,new(.98f,.5f),[]);
  var picked=builder.Assembly.Parts[id];var bounds=c[picked.File].Bounds;var world=builder.Transform.WorldPosition+picked.Position+Vector3.Transform((bounds.Min+bounds.Max)*.5f,picked.Rotation);var camera=scene.ActiveCamera!;var clip=Vector4.Transform(new Vector4(world,1),camera.GetViewMatrix()*camera.GetProjectionMatrix(1280f/720));var pointer=new Vector2((clip.X/clip.W+1)/2,(1-clip.Y/clip.W)/2);TickInput(scene,pointer,[]);TickInput(scene,pointer,[],true);TickInput(scene,pointer,[]);
  Assert(scene.GameObjects.Single(o=>o.Name=="Block tuning panel").Active,"Wrench tool world selection failed");
  Assert(builder.TuneBlock(id),"Tuning did not open");TickInput(scene,new(.98f,.5f),[]);
  var bar=new Vector2(1096f/1280,166f/720);TickInput(scene,bar,[],true);TickInput(scene,bar,[]);
  var wheel=builder.Assembly.Parts[id];Assert(Math.Abs(BlockTuning.Value(wheel,c[wheel.File].ReferenceId,"speed")-2.13f)<.02f,"Native speed slider failed");
  var panel=scene.GameObjects.Single(o=>o.Name=="Block tuning panel").GetComponent<ByteEngine.Core.Graphics.UiWidget>()!;var savedOffset=panel.Offset;panel.Offset-=new Vector2(150,0);
  var track=scene.GameObjects.First(o=>o.Variables.TryGet("GoblinAuthoringKey",out var k)&&k!.String=="Button 944,158#1").GetComponent<ByteEngine.Core.Graphics.UiWidget>()!;
  var rect=ByteEngine.Core.Graphics.UiLayout.Resolve(track.GameObject,track.Anchor,track.Offset,track.Size,Input.GameViewSize);
  var left=(rect.Position+new Vector2(2,9))/Input.GameViewSize;var right=(rect.Position+new Vector2(rect.Size.X*.75f,9))/Input.GameViewSize;
  TickInput(scene,left,[],true);TickInput(scene,right,[],true);TickInput(scene,right,[]);Assert(Math.Abs(BlockTuning.Value(builder.Assembly.Parts[id],c[wheel.File].ReferenceId,"speed")-3.06f)<.02f,"Moved panel slider used a hard-coded screen coordinate");panel.Offset=savedOffset;
  // Holding the primary pointer over UI must not terminate dragging.
  TickInput(scene,new(950f/1280,166f/720),[],true);
  TickInput(scene,new(1200f/1280,166f/720),[],true);TickInput(scene,new(1200f/1280,166f/720),[]);
  Assert(BlockTuning.Value(builder.Assembly.Parts[id],c[wheel.File].ReferenceId,"speed")>3.3f,"Held slider drag stopped at the initial click");
  TickInput(scene,new(1200f/1280,135f/720),[],true);TickInput(scene,new(1200f/1280,135f/720),[]);
  foreach(var key in new[]{Key.D2,Key.Period,Key.D5}){TickInput(scene,new(.98f,.5f),[key]);TickInput(scene,new(.98f,.5f),[]);}
  TickInput(scene,new(.98f,.5f),[Key.Enter]);TickInput(scene,new(.98f,.5f),[]);
  Assert(BlockTuning.Value(builder.Assembly.Parts[id],c[wheel.File].ReferenceId,"speed")==2.5f,"Exact typed wheel speed was not applied");
  Screenshot(scene,".artifacts/tuning-043.png");
  Assert(builder.SetBlockTuning(id,"torque",300),"Torque adjustment failed");
  TickInput(scene,new(1120f/1280,510f/720),[],true);TickInput(scene,new(.98f,.5f),[]);
  Assert(builder.Assembly.Parts.Values.Where(p=>p.File==wheel.File).All(p=>BlockTuning.Value(p,c[p.File].ReferenceId,"torque")==300),"Apply to same type failed");
  string save=Path.Combine(_project.ProjectRoot,"Saves/vehicle-build.json");byte[]? previous=File.Exists(save)?File.ReadAllBytes(save):null;
  try{builder.SaveBuild();builder.RestoreAssembly(ContraptionTests.Build(c).ToJson());builder.LoadBuild();Assert(BlockTuning.Value(builder.Assembly!.Parts[id],c[wheel.File].ReferenceId,"speed")>2,"Disk load lost settings");}
  finally{if(previous!=null)File.WriteAllBytes(save,previous);else if(File.Exists(save))File.Delete(save);}
  Assert(builder.TuneBlock(id),"Reloaded tuning failed");for(int i=0;i<5;i++)TickInput(scene,new(.98f,.5f),[]);Screenshot(scene,".artifacts/tuning-panel.png");
  Assert(builder.SetBlockTuning(id,"speed",3)&&builder.UndoBuild(),"Tuning undo failed");Assert(BlockTuning.Value(builder.Assembly!.Parts[id],c[wheel.File].ReferenceId,"speed")<3,"Undo did not restore setting");
  TickInput(scene,new(1237f/1280,100f/720),[],true);TickInput(scene,new(.98f,.5f),[]);builder.RestoreAssembly(new VehicleAssembly(c).ToJson());
  Console.WriteLine("PASS: native tuning panel, slider, same-type application, undo and disk save/reload.");
 }
}
