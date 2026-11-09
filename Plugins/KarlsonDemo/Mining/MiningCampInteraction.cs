using System.Numerics;
using ByteEngine.Core.Scene;
namespace KarlsonDemo.Plugin;
/// <summary>One proximity choice shared by prompts and E handling; neighbouring machines never open together.</summary>
internal static class MiningCampInteraction
{
 internal static GameObject? Nearest(Scene scene,MiningOperation3D op)
 {
  var player=scene.FindGameObject(op.PlayerObjectId);if(player==null)return null;
  var ui=scene.GetComponentsSnapshot<MiningBusinessUi3D>().FirstOrDefault();
  var candidates=new List<GameObject>();
  foreach(var id in new[]{op.DrillObjectId,ui?.AmmoMachineObjectId??Guid.Empty,ui?.RefineryMachineObjectId??Guid.Empty,ui?.TerminalObjectId??Guid.Empty})if(scene.FindGameObject(id) is {} o)candidates.Add(o);
  candidates.AddRange(scene.GetComponentsSnapshot<MiningAuxiliaryRefinery3D>().Where(r=>r.Enabled&&r.OperationObjectId==op.GameObject.Id).Select(r=>r.GameObject));
  if(op.GameObject.GetComponent<MiningResearchStation3D>()?.DeployedStation is {} station)candidates.Add(station);
  var p=player.Transform.WorldPosition;GameObject? best=null;float bestDistance=float.MaxValue,bestCentre=float.MaxValue;
  foreach(var obj in candidates.Where(o=>o.ActiveInHierarchy)){
   float d=obj.Id==ui?.TerminalObjectId?Vector3.Distance(p,obj.Transform.WorldPosition):MiningCampGeometry.SurfaceDistance(obj,p);
   float centre=Vector3.DistanceSquared(p,obj.Transform.WorldPosition);float range=obj.GetComponent<MiningAuxiliaryRefinery3D>()?.InteractionDistance??ui?.InteractionDistance??2;
   if(d<range&&(d<bestDistance-.01f||Math.Abs(d-bestDistance)<=.01f&&centre<bestCentre)){best=obj;bestDistance=d;bestCentre=centre;}
  }
  return best;
 }
}
