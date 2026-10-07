using ByteEngine.Core.VisualLogic;
namespace GoblinScrapper;
public static class GoblinWorkshopUiTemplate {
 public static EventModuleDefinition Create(){
  var module=new EventModuleDefinition{Name="Goblin Workshop - Editable UI",RequiredComponents=["VehicleBuilder3D"]};
  foreach(var pair in new[]{("Run / build","toggleMode"),("Undo","undo"),("Redo","redo"),("Save machine","save"),("Load machine","load"),("Place blocks","placeTool"),("Move branch","moveTool"),("Rotate mount","rotatePreview"),("Change mount face","cycleMountFace"),("Copy part","copyTool"),("Erase blocks","eraseToolMode"),("Recover machine","resetPosition"),("Tune blocks","tuneTool")}){
   var condition=new VisualInstruction{Id="ui.buttonClicked",Arguments=new(){["target"]=EventValue.String(pair.Item1)}};
   var action=new VisualInstruction{Id="bytebard.goblinscrapper."+pair.Item2,Arguments=new(){["target"]=EventValue.String("Self")}};
   if(pair.Item2=="rotatePreview")action.Arguments["degrees"]=EventValue.Number(90);
   module.Rules.Add(new(){DisplayName=pair.Item1,Conditions=[condition],Actions=[action]});
  }
  return module;
 }
}
