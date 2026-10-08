using System.Numerics;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using DesertTerrain;
namespace ByteEngine.Tests;
internal static class LargeSandTests
{
 public static void Run(){var scene=new Scene("Large sand coverage");var terrain=scene.CreateGameObject("Terrain").AddComponent(new DesertTerrain3D { Cells=256,Spacing=1,UseReferenceSandSurface=true,ContactRutDepth=.26f });
 var patch=terrain.EnableSimulationPatch(new(64,1,48));var field=scene.GameObjects.SelectMany(o=>o.Components).OfType<InteractiveSand3D>().Single();
 if(field.Columns!=2049||field.CellSpacing!=.125f||field.SurfaceWidth!=256)throw new Exception("Large map lost contact resolution");
 field.Stamp(new(64,0,48),new(68,0,48),.4f,1);field.TrySampleWorld(new(66,0,48),out var track,out _);if(track.Y>-.20f)throw new Exception("Far-map rut too shallow");
 terrain.FocusSimulationPatch(new(68,0,48));if(!patch.TrySampleWorld(new(66,0,48),out var collision,out _)||Math.Abs(collision.Y-track.Y)>.0001f)throw new Exception("Moving collision and rendered field diverge");
 terrain.FocusSimulationPatch(new(-70,0,-60));terrain.FocusSimulationPatch(new(68,0,48));field.TrySampleWorld(new(66,0,48),out var persistent,out _);if(persistent.Y!=track.Y)throw new Exception("Travel erased tracks");
 field.AdvanceWind(10);field.TrySampleWorld(new(66,0,48),out var held,out _);if(Math.Abs(held.Y-track.Y)>.001f)throw new Exception("Wind erased fresh tracks");field.AdvanceWind(20);field.TrySampleWorld(new(66,0,48),out var fading,out _);if(fading.Y<=track.Y+.05f||fading.Y>-.02f)throw new Exception("Wind did not gradually fill tracks");field.AdvanceWind(15);field.TrySampleWorld(new(66,0,48),out var filled,out _);if(Math.Abs(filled.Y)>.04f)throw new Exception("Wind failed to close tracks");
 var demo=scene.CreateGameObject("Demo default").AddComponent(new InteractiveSand3D());if(demo.Columns!=257||demo.CellSpacing!=.125f)throw new Exception("Standalone demo changed");
 Console.WriteLine("PASS 256m field keeps 12.5cm detail, far-map ruts, moving collision equality and persistent tracks; wind delay and gradual closure; default demo unchanged");
 }
}
