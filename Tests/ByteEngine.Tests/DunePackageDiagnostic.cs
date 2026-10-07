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

  var blueprint=Path.Combine(_runtime.Root,"Design/VehicleBlueprints/builds/01-dust-jackal.json"); // Design is intentionally not runtime content; create the same minimal grounded fixture in memory.

  while(vehicle.Blocks.Count>1)vehicle.RemoveBlock(vehicle.Blocks.Last().Id);

  vehicle.AddBlock(3,0,new(0,.5f,0),Quaternion.Identity);int deck=vehicle.Blocks.Last().Id;vehicle.AddBlock(23,deck,new(0,1,.55f),Quaternion.Identity);vehicle.AddBlock(25,deck,new(0,1,-.6f),Quaternion.Identity);foreach(int x in new[]{-1,1})foreach(int z in new[]{-1,1})vehicle.AddBlock(13,deck,new(x*.9f,-.15f,z*.72f),Quaternion.CreateFromAxisAngle(Vector3.UnitY,-x*MathF.PI*.5f));vehicle.Command("Toggle drive");if(vehicle.Building)throw new InvalidOperationException("Packaged fixture cannot enter drive");for(int i=0;i<120;i++)Tick(scene);var start=vehicle.Transform.WorldPosition;for(int i=0;i<180;i++)Tick(scene,Key.W);float distance=Vector3.Distance(start,vehicle.Transform.WorldPosition);terrain.TryGetSand(vehicle.Transform.WorldPosition,out var ground);if(distance<2||vehicle.Transform.WorldPosition.Y<ground.Position.Y-.5f||!float.IsFinite(distance))throw new InvalidOperationException("Packaged vehicle falls through or cannot drive on the heightfield");var sounds=scene.GameObjects.SelectMany(o=>o.Components).OfType<AudioSource3D>().Where(s=>s.GameObject.Name.StartsWith("Vehicle audio - ")).ToArray();if(sounds.Length!=6||sounds.Any(s=>!s.ClipLoaded))throw new InvalidOperationException("Audio missing from packed game");

  Console.WriteLine($"PACKAGED PASS: heightmap/collision; drove {distance:0.0}m; terrain={ground.Position.Y:0.00}, vehicle={vehicle.Transform.WorldPosition.Y:0.00}; six SFX clips; plugins={terrain.GetType().Assembly.GetName().Name}");

  Scenes.UnloadScene();_runtime.Dispose();_content.Dispose();Close();

 }

 static void Tick(Scene scene,params Key[] keys){var raw=new RawInputSnapshot();raw.KeysDown.UnionWith(keys);Input.UpdatePortable(raw,false);Input.SetGameViewPointer(new(.5f,.5f),new(1280,720),true);Time.Update(1f/60);scene.UpdateInternal();}

}
