using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningWeaponCampUxCheck
{
 public static void Run(Scene scene,MiningOperation3D op,Action<bool,string> check)
 {
  var player=scene.FindGameObject(op.PlayerObjectId)!;var camp=player.GetComponent<MiningCampLayout3D>()!;
  check(camp.EnterEdit(),"camp view opens for camera navigation checks");
  var old=camp.ViewCentre;camp.PanView(new(0,-20));check(camp.ViewCentre.Y<old.Y-1,"camp camera pans toward distant front defences");
  camp.BeginMove(op.DrillObjectId);var pose=camp.PreviewPosition;camp.PanView(new(5,-5));check(camp.SelectedObjectId==op.DrillObjectId&&camp.PreviewPosition==pose,"panning keeps the selected object and does not move its placement preview");
  camp.PanView(new(100000,-100000));check(camp.ViewCentre.X<=camp.BuildMaximum.X&&camp.ViewCentre.Y>=camp.BuildMinimum.Y,"camp panning is safely bounded by the playable build area");
  var camera=scene.ActiveCamera!;camp.ZoomView(1000);check(Math.Abs(camera.OrthographicWidth-camp.MinimumCameraWidth)<.01,"camp zoom respects its near limit");camp.ZoomView(-1000);check(Math.Abs(camera.OrthographicWidth-camp.MaximumCameraWidth)<.01,"camp zoom can reveal a wider camp without exceeding its far limit");camp.ExitEdit();check(!camp.IsEditing,"closing camp view restores gameplay camera mode");
  var shotgun=scene.GameObjects.SelectMany(o=>o.Components.OfType<WeaponDefinition3D>()).Single(w=>w.DisplayName=="Shotgun");check(shotgun.SpreadDegrees==1.75f&&shotgun.MovingInaccuracyDegrees<=.3f&&shotgun.PelletsPerShot==8,"shotgun uses a tighter cone and smaller movement penalty while retaining eight pellets");
  var ui=scene.GameObjects.SelectMany(o=>o.Components.OfType<MiningBusinessUi3D>()).Single();var lines=ui.ResearchDescriptionLines("Shotgun pellets gently home across up to 3 visible enemies in a 25-degree aiming cone within 12 metres. Split the existing pellet count across targets; no extra pellets or damage and no through-wall targeting.");check(lines.Length>3&&lines.All(l=>l.Length<=45),"long upgrade description wraps into bounded scrollable lines rather than overlapping its requirement label");
 }
}
