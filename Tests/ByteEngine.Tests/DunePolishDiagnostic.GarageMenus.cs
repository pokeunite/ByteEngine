using System.Numerics;
using System.Text.Json;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Scene;
namespace ByteEngine.Tests;
internal sealed partial class DunePolishDiagnostic
{
 void RunGarageMenuChecks(Scene scene,DuneCompany.DuneWorkshop3D workshop,string root,Action<string> click)
 {
  string dir=Path.Combine(root,"Saves/Blueprints");Directory.CreateDirectory(dir);int before=Directory.GetFiles(dir,"*.json").Length;
  click("Blueprint new");if(!scene.FindGameObject("Blueprint naming")!.ActiveInHierarchy)throw new Exception("Save new did not ask for a name");int original=workshop.Blocks.Count;
  foreach(var key in new[]{Key.T,Key.O,Key.W,Key.Space,Key.R,Key.I,Key.G}){Tick(scene,new(.5f),false,key);Tick(scene,new(.5f),false);}Tick(scene,new(.5f),false,Key.Enter);Tick(scene,new(.5f),false);
  if(workshop.Blocks.Count!=original||scene.FindGameObject("Blueprint naming")!.ActiveInHierarchy)throw new Exception("Name entry changed vehicle or did not close");
  string active=File.ReadAllText(Path.Combine(root,"Saves/active-blueprint.txt")),file=Path.Combine(dir,active);if(!File.ReadAllText(file+".name").Contains("tow rig")||Directory.GetFiles(dir,"*.json").Length!=before+1)throw new Exception("Named save failed");
  if(!workshop.AddBlock(1,workshop.Blocks[0].Id,new(0,2,0),Quaternion.Identity))throw new Exception("Fixture edit failed");click("Blueprint update");if(Directory.GetFiles(dir,"*.json").Length!=before+1||JsonDocument.Parse(File.ReadAllText(file)).RootElement.GetArrayLength()!=original+1)throw new Exception("Save changes duplicated or lost edit");
  click("Blueprint row 0");if(workshop.Blocks.Count!=original+1||scene.FindGameObject("Blueprints overlay")!.ActiveInHierarchy)throw new Exception("Updated blueprint load failed");click("Blueprints tab");Capture(scene,Path.Combine(root,"Preview/menus-blueprints.png"),false);
  click("Blueprint delete 0");if(!File.Exists(file))throw new Exception("Delete skipped confirmation");Capture(scene,Path.Combine(root,"Preview/menus-delete-confirm.png"),false);click("Blueprint delete 0");if(File.Exists(file)||workshop.Blocks.Count!=original+1)throw new Exception("Delete failed or destroyed current vehicle");
  if(Directory.GetFiles(Path.Combine(root,"Saves/BlueprintPreviews"),"*.png").Length==0)throw new Exception("Vehicle thumbnails missing");
  click("Blueprint new");Tick(scene,new(.5f),false,Key.Escape);Tick(scene,new(.5f),false);if(scene.FindGameObject("Blueprint naming")!.ActiveInHierarchy)throw new Exception("Name cancellation failed");click("Contracts tab");click("Contracts completed");Capture(scene,Path.Combine(root,"Preview/menus-completed.png"),false);click("Contracts available");click("Contract expand");if(!scene.FindGameObject("Contract details")!.ActiveInHierarchy)throw new Exception("Contract expansion failed");Capture(scene,Path.Combine(root,"Preview/menus-contracts.png"),false);click("Close garage overlay");
  var palette=typeof(DuneCompany.DuneWorkshop3D).GetMethod("Palette",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;foreach(int category in new[]{0,1,2,3,4}){typeof(DuneCompany.DuneWorkshop3D).GetField("_category",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.SetValue(workshop,category);if(((DuneCompany.DunePart[])palette.Invoke(workshop,null)!).Any(p=>p.Id is 11 or 27))throw new Exception("Retired parts are still selectable");}
  var music=scene.FindGameObject("Menu music - garage")?.GetComponent<AudioSource3D>();if(music==null||!music.Loop||music.Spatial)throw new Exception("Garage music missing or wrong mode");
  var blocksField=typeof(DuneCompany.DuneWorkshop3D).GetField("_blocks",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;var refresh=typeof(DuneCompany.DuneWorkshop3D).GetMethod("RefreshGarageState",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;var originalBlocks=blocksField.GetValue(workshop);blocksField.SetValue(workshop,new List<DuneCompany.PlacedBlock>());refresh.Invoke(workshop,null);foreach(string name in new[]{"seat","engine","wheel"})if(scene.FindGameObject("Requirement "+name)!.GetComponent<UiWidget>()!.Color.Y>.3f)throw new Exception("Missing part icon is not red");Capture(scene,Path.Combine(root,"Preview/menus-missing-parts.png"),false);blocksField.SetValue(workshop,new List<DuneCompany.PlacedBlock>{new(){Id=0,Type=13},new(){Id=1,Type=23},new(){Id=2,Type=25}});refresh.Invoke(workshop,null);foreach(string name in new[]{"seat","engine","wheel"})if(scene.FindGameObject("Requirement "+name)!.GetComponent<UiWidget>()!.Color.Y<.9f)throw new Exception("One installed part did not make its icon green");if(workshop.ReadyToDrive)throw new Exception("Two-wheel driving requirement lost");blocksField.SetValue(workshop,originalBlocks);refresh.Invoke(workshop,null);Console.WriteLine("PASS Empty build gives three red icons; one wheel, engine and seat give three green icons; two-wheel drive requirement retained");
  Console.WriteLine("PASS Named save, typing isolation, update without duplicate, thumbnail cache, load, confirmed delete retaining build, naming cancellation, contract categories/details/required images, retired catalogue parts, non-spatial garage music");
 }
}
