using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Variables;
namespace GoblinScrapper.Construction;
public static class SavedSceneBinding {
 public static T BindSceneComponent<T>(this GameObject obj,T proposed) where T:Component => obj.GetComponent<T>()??obj.AddComponent(proposed);
}
public sealed partial class VehicleBuilder3D {
 public bool UseBuiltInToolbarActions {get;set;}=true;
 private static readonly HashSet<string> EditableToolbarNames=["Run / build","Undo","Redo","Save machine","Load machine","Place blocks","Move branch","Rotate mount","Change mount face","Copy part","Erase blocks","Recover machine","Tune blocks"];
 public void ChoosePlaceTool(){if(!Building)return;CloseTuning();CancelMove();_movePick=_copyPick=false;SetEraseTool(false);}
 public void ChooseMoveTool(){ChoosePlaceTool();_movePick=Building;}
 public void ChooseCopyTool(){ChoosePlaceTool();_copyPick=Building;}
 public void ChooseEraseTool(){ChoosePlaceTool();SetEraseTool(true);}
 public void ChooseTuneTool(){if(Building)BeginTuning();}
 public bool UseAuthoredScene {get;set;}
 public float BattleDuration {get;set;}=120;
 public float EnemyMoveSpeed {get;set;}=1.5f;
 public float EnemyAttackDamage {get;set;}=5;
 public float EnemyAttackInterval {get;set;}=1.5f;
 public string EnemyModel {get;set;}="Assets/Battlefield/red-goblin.glb";
 private readonly Dictionary<string,int> _authorCounts=new();
 private readonly Dictionary<string,GameObject> _authoredByKey=new();
 private readonly HashSet<GameObject> _authoredObjects=new();
 private Vector3 _workshopStart;
 private void BindAuthoredScene(){
  _authorCounts.Clear();_authoredByKey.Clear();_authoredObjects.Clear();_workshopStart=Transform.WorldPosition;
  if(!UseAuthoredScene)return;
  foreach(var obj in GameObject.Scene!.GameObjects){if(obj.Variables.TryGet("GoblinAuthoringKey",out var value)){_authoredByKey[value!.String]=obj;_authoredObjects.Add(obj);}if(obj.Variables.TryGet("GoblinEditorPreview",out var preview)&&preview!.Boolean)obj.Active=false;}
 }
 private void AddAuthoredObstacles(){
  if(!UseAuthoredScene||_contraption==null)return;
  foreach(var obj in GameObject.Scene!.GameObjects.Where(o=>o.ActiveInHierarchy)){
   if(obj.GetComponent<BoxCollider3D>() is not {IsTrigger:false} box||obj==GameObject||obj.IsDescendantOf(GameObject))continue;
   var size=Vector3.Abs(Vector3.Multiply(box.Size,obj.Transform.WorldScale));if(size.X<=0||size.Y<=0||size.Z<=0)continue;
   _contraption.AddObstacle(Vector3.Transform(box.Center,obj.Transform.WorldMatrix),size,obj.Transform.WorldRotation);
  }
 }
}
