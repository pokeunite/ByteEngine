using GoblinScrapper;
using GoblinScrapper.Construction;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using ByteEngine.Core.VisualLogic;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Variables;
using System.Numerics;
namespace ByteEngine.Tests;
internal sealed partial class GoblinVehicleDiagnostic {
 private void RestoreAuthoredMaterials(Scene scene,VehicleBuilder3D builder){
  var floor=scene.GameObjects.Single(o=>o.Name=="Test yard floor").GetComponent<ByteEngine.Core.Graphics.ThreeD.MeshRenderer>()!;
  if(!floor.MaterialAssetReference.IsEmpty)return;
  var material=new ByteEngine.Core.Graphics.ThreeD.Material {BaseColor=new(.75f,.70f,.59f,1),MainTexture=_project!.Assets.LoadTexture(new ByteEngine.Core.Assets.AssetReference("Assets/WorkshopMaterials/concrete-diffuse.jpg")),NormalTexture=_project.Assets.LoadTexture(new ByteEngine.Core.Assets.AssetReference("Assets/WorkshopMaterials/concrete-normal.jpg")),PackedPbrTexture=_project.Assets.LoadTexture(new ByteEngine.Core.Assets.AssetReference("Assets/WorkshopMaterials/concrete-arm.jpg")),PbrMapMode=ByteEngine.Core.Assets.MaterialPbrMapMode.Packed,DecodeColorTexturesSrgb=true,UvTiling=new(12),NormalStrength=.55f};
  floor.MaterialAssetReference=ByteEngine.Editor.LocalMaterialAssetFactory.Save(_project,"Workshop_concrete",material);
  material.BaseColor=new(.8f,.76f,.67f,1);material.UvTiling=new(1.5f);material.NormalStrength=.45f;
  var pad=ByteEngine.Editor.LocalMaterialAssetFactory.Save(_project,"Build_pad_concrete",material);
  foreach(var o in scene.GameObjects.Where(o=>o.Name=="Build pad"))o.GetComponent<ByteEngine.Core.Graphics.ThreeD.MeshRenderer>()!.MaterialAssetReference=pad;
  string modelPath=builder.PartsDirectory+"/goblin_single_wooden_block.glb";
  _project.AssetDatabase.TryGetAsset(modelPath,out var record);
  var model=_project.Assets.LoadModel(new ByteEngine.Core.Assets.AssetReference(modelPath));
  var wood=model.Materials.First(m=>m.Name.Contains("wood"));
  var extracted=ByteEngine.Editor.ImportedMaterialExtraction.Extract(record!,_project,[wood.Key]).Single();
  foreach(var o in scene.GameObjects.Where(o=>o.Name.Contains("workbench",StringComparison.OrdinalIgnoreCase)||o.Name=="Scrap crate"))
   if(o.GetComponent<ByteEngine.Core.Graphics.ThreeD.MeshRenderer>() is {} r)r.MaterialAssetReference=new(extracted.Guid,extracted.ProjectPath);
 }
 private void CheckAuthoredScene(Scene scene,VehicleBuilder3D builder,string scenePath){
  Assert(builder.UseAuthoredScene,"Authored scene flag missing");
  RestoreAuthoredMaterials(scene,builder);
  var help=scene.GameObjects.FirstOrDefault(o=>o.Name=="Tuning help")?.GetComponent<UiText>();if(help?.Text=="Drag bars / click switches. Changes apply on deployment.")help.Text="Drag bars or click values to type. Enter applies / Esc cancels.";
  var panel=scene.GameObjects.Single(o=>o.Name=="Block tuning panel");
  for(int i=0;i<6;i++){int y=132+i*57;string key=$"Button 1158,{y}#1";if(scene.GameObjects.Any(o=>o.Variables.TryGet("GoblinAuthoringKey",out var k)&&k!.String==key))continue;
   var field=scene.CreateGameObject("Tuning numeric value "+i);field.SetParent(panel,false);field.Variables.Set("GoblinAuthoringKey",VariableValue.FromString(key));field.AddComponent(new UiWidget{Kind=UiWidgetKind.Button,Label="",Offset=new(230,y-78),Size=new(90,22),FontSize=13,FontReference=new("Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf"),Color=new(.16f,.23f,.13f,1),OrderInLayer=34});
  }
  var sheet="Assets/Events/GoblinWorkshopUI.byteevents";new EventModuleSerializer().Save(GoblinWorkshopUiTemplate.Create(),Path.Combine(_project!.ProjectRoot,sheet));_project.AssetDatabase.Scan();
  var events=builder.GameObject.GetComponent<EventModuleComponent>()??builder.GameObject.AddComponent(new EventModuleComponent());events.AddModuleReference(new(sheet));builder.UseBuiltInToolbarActions=false;_project.Scenes.Save(scene,scenePath);
  scene=_project.Scenes.Load(scenePath);builder=scene.GameObjects.SelectMany(o=>o.Components).OfType<VehicleBuilder3D>().Single();
  Assert(scene.GameObjects.Count(o=>o.Variables.TryGet("GoblinEnemySpawn",out var v)&&v!.Boolean)==18,"Saved goblin spawns missing");
  var toolbar=scene.GameObjects.Single(o=>o.Name=="Toolbar").GetComponent<UiWidget>()!;var beforeOffset=toolbar.Offset;toolbar.Offset+=new Vector2(0,2);
  var cover=scene.GameObjects.First(o=>o.Name=="Battlefield cover");cover.Transform.WorldPosition+=new Vector3(1,0,0);var expectedCover=cover.Transform.WorldPosition;
  string edited=Path.GetFullPath(".artifacts/authored-edit-check.bytescene");_project.Scenes.Save(scene,edited);scene=_project.Scenes.Load(edited);builder=scene.GameObjects.SelectMany(o=>o.Components).OfType<VehicleBuilder3D>().Single();
  toolbar=scene.GameObjects.Single(o=>o.Name=="Toolbar").GetComponent<UiWidget>()!;cover=scene.GameObjects.First(o=>o.Name=="Battlefield cover");Assert(toolbar.Offset==beforeOffset+new Vector2(0,2)&&cover.Transform.WorldPosition==expectedCover,"Editor changes did not reload");
  int widgets=scene.GameObjects.Count(o=>o.GetComponent<UiWidget>()!=null);Screenshot(scene,".artifacts/authored-edit-mode.png",1280,720,true);
  Scenes.LoadScene(scene);for(int i=0;i<10;i++)TickInput(scene,new(.98f,.5f),[]);
  Assert(scene.GameObjects.Count(o=>o.GetComponent<UiWidget>()!=null)==widgets,"Play duplicated authored UI");Assert(toolbar.Offset==beforeOffset+new Vector2(0,2)&&cover.Transform.WorldPosition==expectedCover,"Play overwrote editor changes");
  CheckNativeTuning(scene,builder);
  TickInput(scene,new(832f/1280,24f/720),[],true);TickInput(scene,new(.98f,.5f),[]);Assert(scene.GameObjects.Single(o=>o.Name=="Tune blocks").GetComponent<UiWidget>()!.Color.X>.1f,"Editable toolbar event did not choose tuning");
  builder.ChoosePlaceTool();builder.RestoreAssembly(ContraptionTests.Build(VehiclePartCatalog.Load(Path.Combine(_project.ProjectRoot,"Assets/GarageUI/parts-catalog.json"))).ToJson());
  TickInput(scene,new(24f/1280,24f/720),[],true);TickInput(scene,new(.98f,.5f),[]);Assert(!builder.Building&&builder.BattleRunning,"Editable Run button did not deploy");
  for(int i=0;i<180;i++)TickInput(scene,new(.98f,.5f),[Key.W]);Assert(builder.Speed>2,"Saved arena did not support driving");
  builder.ReturnToBuild();for(int i=0;i<3;i++)TickInput(scene,new(.98f,.5f),[]);Assert(scene.GameObjects.Count(o=>o.Variables.TryGet("GoblinEnemySpawn",out var v)&&v!.Boolean&&o.Active)==18,"Return did not restore authored spawns");
  Screenshot(scene,".artifacts/authored-playing.png");Console.WriteLine("PASS: editor assets visible before Play; edit/save/reload; no duplicate HUD; preserved edited layout; native UI event sheet; driving and spawn reset.");
 }
}
