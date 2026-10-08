using ByteEngine.Core.Gameplay;
using System.Numerics;

using ByteEngine.Core;

using ByteEngine.Core.Runtime;

using ByteEngine.Core.Scene;

using ByteEngine.Core.InputSystem;

using ByteEngine.Core.Audio;

using ByteEngine.Core.Graphics.ThreeD;

using ByteEngine.Core.Serialization;

using DuneCompany;

using DesertTerrain;

namespace ByteEngine.Tests;

/// <summary>Loads the actual packed content through the player bootstrap, never the source project.</summary>

internal sealed class DunePackageDiagnostic : ByteEngineApplication

{

 readonly string _directory;GameContentSession? _content;GameProjectRuntime? _runtime;

 public DunePackageDiagnostic(string directory):base(1280,720,"Packaged terrain and vehicle validation"){_directory=Path.GetFullPath(directory);IsVisible=false;}

 protected override bool ShouldUpdateScene=>false;protected override bool ShouldRenderSceneToWindow=>false;

 protected override void OnEngineStart(){GamePackageExporter.ValidatePackage(_directory);_content=new(_directory);_runtime=new(_content.ProjectFile,Console.WriteLine);var scene=_runtime.LoadStartupScene();if(scene.GameObjects.SelectMany(o=>o.Components).OfType<MissingComponent>().Any())throw new InvalidOperationException("Packaged scene has unavailable components");Scenes.SceneFactory=path=>_runtime.LoadScene(path);Scenes.LoadScene(scene);if(scene.Name=="Dune Company - Main Menu"){scene.RequestLoad("Scenes/Workshop.bytescene");Time.Update(1f/60);Scenes.UpdateInternal();scene=Scenes.ActiveScene!;Console.WriteLine("PACKAGED PASS: Main menu -> Workshop scene transition");}if(scene.Name=="Dune Company - Interactive Sand Lab"){

  Tick(scene);var probe=scene.GameObjects.SelectMany(o=>o.Components).OfType<SandLabProbe3D>().Single();var startProbe=probe.Transform.WorldPosition;for(int i=0;i<30;i++)Tick(scene,Key.W);if(Vector3.Distance(startProbe,probe.Transform.WorldPosition)<1)throw new Exception("Packed sand probe is not interactive");

  scene=_runtime.LoadScene("Scenes/SandVehicleLab.bytescene");if(scene.GameObjects.SelectMany(o=>o.Components).OfType<MissingComponent>().Any())throw new Exception("Packed vehicle lab has missing components");Console.WriteLine("PACKAGED PASS: interactive sand startup and vehicle lab scene loading");

 }var terrain=scene.GameObjects.SelectMany(o=>o.Components).OfType<DesertTerrain3D>().Single();if(!terrain.UseReferenceSandSurface&&(terrain.Cells!=512||terrain.Spacing is not (2 or 8)||terrain.HeightmapContentHash.Length<20))throw new InvalidOperationException("Packaged terrain did not load the authored source");

  Scenes.LoadScene(scene);var vehicle=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().Single();Tick(scene);

  if(Environment.GetEnvironmentVariable("DUNE_SAVE_RESTART") is {} phase){string saves=_runtime.SaveDirectory;Console.WriteLine("SAVE DIRECTORY "+saves);if(phase=="write"){vehicle.AddBlock(1,vehicle.Blocks[0].Id,new(0,2.5f,0),Quaternion.Identity);vehicle.Command("Save vehicle");foreach(var key in new[]{Key.R,Key.E,Key.S,Key.T,Key.A,Key.R,Key.T}){Tick(scene,key);Tick(scene);}Tick(scene,Key.Enter);Tick(scene);if(!File.Exists(Path.Combine(saves,"vehicle.json"))||Directory.GetFiles(Path.Combine(saves,"Blueprints"),"*.json").Length!=1)throw new Exception("Runtime named save failed");typeof(DuneWorkshop3D).GetMethod("RecordContractCompletion",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.Invoke(vehicle,null);Console.WriteLine("PASS First process saved a named blueprint and completed-contract history");}else{string file=Directory.GetFiles(Path.Combine(saves,"Blueprints"),"*.json").Single();int count=System.Text.Json.JsonDocument.Parse(File.ReadAllText(file)).RootElement.GetArrayLength();if(vehicle.Blocks.Count!=count)throw new Exception("Restart did not restore saved build");vehicle.Command("Open blueprints");Tick(scene);var active=typeof(DuneWorkshop3D).GetField("_loadedBlueprint",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(vehicle) as string;if(active!=file)throw new Exception("Active blueprint identity lost after exit");vehicle.AddBlock(1,vehicle.Blocks[0].Id,new(0,3,0),Quaternion.Identity);vehicle.Command("Save vehicle");if(Directory.GetFiles(Path.Combine(saves,"Blueprints"),"*.json").Length!=1)throw new Exception("Restarted Save changes created a duplicate");vehicle.Command("Close garage overlay");vehicle.Command("Open contracts");Tick(scene);if(scene.FindGameObject("Contract expand")!.ActiveInHierarchy)throw new Exception("Completed job remains Available");Console.WriteLine("PASS Second process restored the named build, updated the same blueprint, and kept completed job out of Available");}Scenes.UnloadScene();_runtime.Dispose();_content.Dispose();if(!File.Exists(Path.Combine(saves,"vehicle.json")))throw new Exception("Content cleanup deleted player saves");Console.WriteLine("PASS Player saves survive content disposal");Close();return;}
  if(Environment.GetEnvironmentVariable("DUNE_CONTRACT_NAV")=="1"){vehicle.Command("Open contracts");Tick(scene);if(scene.FindGameObject("Choose winch contract")?.ActiveInHierarchy!=true)throw new Exception("Winch selector not visible in Contracts");vehicle.Command("Select winch contract");Time.Update(1f/60);Scenes.UpdateInternal();scene=Scenes.ActiveScene!;Tick(scene);vehicle=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneWorkshop3D>().Single();if(!vehicle.WinchMission||scene.FindGameObject("Contracts overlay")?.ActiveInHierarchy!=true||scene.FindGameObject("Contract details")?.ActiveInHierarchy!=true||scene.GameObjects.SelectMany(o=>o.Components).OfType<MissingComponent>().Any())throw new Exception("Packed winch contract navigation unavailable");if(!File.Exists(Path.Combine(_runtime.Root,"Assets/DuneParts/glb/33_powered_winch.glb")))throw new Exception("Packed winch model missing");Console.WriteLine("PASS Exported Workshop Contracts -> Winch rescue -> expanded details, scene and winch asset present");Scenes.UnloadScene();_runtime.Dispose();_content.Dispose();Close();return;}
  var music=scene.GameObjects.SelectMany(o=>o.Components).OfType<AudioSource3D>().FirstOrDefault(s=>s.GameObject.Name=="Menu music - garage");if(music==null||!music.ClipLoaded||!music.Loop||music.Spatial)throw new Exception("Packaged garage music unavailable");foreach(string name in new[]{"garage","main-menu"}){if(!AudioClip.TryLoadWave(Path.Combine(_runtime.Root,"Assets/Audio/Music/"+name+".wav"),out var clip))throw new Exception("Packaged music missing: "+name);clip.Dispose();}vehicle.Command("Open blueprints");Tick(scene);if(!Directory.Exists(Path.Combine(_runtime.SaveDirectory,"BlueprintPreviews")))throw new Exception("Packaged blueprint previews not generated");vehicle.Command("Close garage overlay");Console.WriteLine("PACKAGED PASS: CC0 menu/garage clips and runtime generated blueprint thumbnails");
  var blueprint=Path.Combine(_runtime.Root,"Design/VehicleBlueprints/builds/01-dust-jackal.json"); // Design is intentionally not runtime content; create the same minimal grounded fixture in memory.

  while(vehicle.Blocks.Count>1)vehicle.RemoveBlock(vehicle.Blocks.Last().Id);

  vehicle.AddBlock(3,0,new(0,.5f,0),Quaternion.Identity);int deck=vehicle.Blocks.Last().Id;vehicle.AddBlock(23,deck,new(0,1,.55f),Quaternion.Identity);vehicle.AddBlock(25,deck,new(0,1,-.6f),Quaternion.Identity);foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})vehicle.AddBlock(13,deck,new(x*.9f,-.15f,z*.72f),Quaternion.CreateFromAxisAngle(Vector3.UnitY,-x*MathF.PI*.5f));vehicle.Command("Toggle drive");if(vehicle.Building)throw new InvalidOperationException("Packaged fixture cannot enter drive");for(int i=0;i<120;i++)Tick(scene);var start=vehicle.Transform.WorldPosition;for(int i=0;i<180;i++)Tick(scene,Key.W);float distance=Vector3.Distance(start,vehicle.Transform.WorldPosition);terrain.TryGetSand(vehicle.Transform.WorldPosition,out var ground);if(distance<2||vehicle.Transform.WorldPosition.Y<ground.Position.Y-.5f||!float.IsFinite(distance))throw new InvalidOperationException("Packaged vehicle falls through or cannot drive on the heightfield");var sounds=scene.GameObjects.SelectMany(o=>o.Components).OfType<AudioSource3D>().Where(s=>s.GameObject.Name.StartsWith("Vehicle audio - ")).ToArray();if(sounds.Length!=6||sounds.Any(s=>!s.ClipLoaded))throw new InvalidOperationException("Audio missing from packed game");

  var tyre=vehicle.Blocks.First(b=>vehicle.Catalog[b.Type].Wheel);
  if(vehicle.HasMountSupport(23,tyre.P,tyre.Q,Vector3.UnitY,tyre.Id))throw new Exception("Engine can mount on a tyre");
  var dial=scene.FindGameObject("Driving speedometer")?.GetComponent<ByteEngine.Core.Graphics.UiWidget>();
  var speedText=scene.FindGameObject("Driving speed readout")?.GetComponent<ByteEngine.Core.Graphics.UiText>();
  if(dial==null||speedText==null||speedText.Text!=$"{Math.Abs(vehicle.Speed)*3.6f:0} km/h")throw new Exception("Speedometer does not follow actual vehicle speed");
  Console.WriteLine("PACKAGED PASS: engine-on-tyre placement rejected; live speedometer " + speedText.Text);
  if(terrain.SandAlbedoPath.Contains("aerial_beach_01")){
   using var frame=new ByteEngine.Editor.SceneFramebuffer();frame.RenderGame(Renderer,Renderer3D,scene,ByteEngine.Editor.EditorMode.Play,1280,720,1280,720);
   var surface=scene.GameObjects.SelectMany(o=>o.Components).OfType<ByteEngine.Core.Gameplay.InteractiveSand3D>().Single();
   var material=(Material)typeof(ByteEngine.Core.Gameplay.InteractiveSand3D).GetField("_material",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!.GetValue(surface)!;
   if(material.MainTexture==null||material.NormalTexture==null||material.RoughnessTexture==null||material.HeightFieldTexture==null)throw new Exception("Selected sand material or displacement missing");
   if(Math.Abs(material.UvTiling.X-surface.SurfaceWidth/30)>0.001f)throw new Exception("Selected sand scan has incorrect world scale");
   int cx=surface.Columns/2,cz=surface.Rows/2;float before=surface.HeightAt(cx,cz);surface.Stamp(new(0,0,0),new(.25f,0,0),.4f);if(surface.HeightAt(cx,cz)>=before-.05f)throw new Exception("Selected material disabled sand deformation");
   Console.WriteLine("PACKAGED PASS: selected albedo / GL normal / roughness bound on displaced surface; 30m scale; contact changes height");
  }
  Console.WriteLine($"PACKAGED PASS: heightmap/collision; drove {distance:0.0}m; terrain={ground.Position.Y:0.00}, vehicle={vehicle.Transform.WorldPosition.Y:0.00}; six SFX clips; plugins={terrain.GetType().Assembly.GetName().Name}");

  Scenes.UnloadScene();_runtime.Dispose();_content.Dispose();Close();

 }

 static void Tick(Scene scene,params Key[] keys){var raw=new RawInputSnapshot();raw.KeysDown.UnionWith(keys);Input.UpdatePortable(raw,false);Input.SetGameViewPointer(new(.5f,.5f),new(1280,720),true);Time.Update(1f/60);scene.UpdateInternal();}

}
