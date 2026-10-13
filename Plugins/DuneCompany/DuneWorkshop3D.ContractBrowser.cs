using System.Text.Json;
using System.Numerics;
using ByteEngine.Core.Graphics;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 bool _completedContracts,_contractExpanded;int _completedCount;
 string ContractHistory=>HistoryPath(CurrentContract);
 sealed record ContractProgress(int SchemaVersion,string ContractId,int Completions);
 string ContractId(int id)=>id==7?"red-rock-rally":id==6?"dead-straight":id==5?"dust-bowl-drift":id==4?"scrapyard-sprint":id==3?"no-brakes":id==2?"down-with-patient":id==1?"well-truck-rescue":"stranded-buggy";
 string HistoryPath(int id)=>Path.Combine(SaveRoot,id==7?"rally-progress-v1.json":id==6?"drag-progress-v1.json":id==5?"drift-progress-v1.json":id==4?"sprint-progress-v1.json":id==3?"no-brakes-progress-v1.json":id==2?"anchor-progress-v1.json":id==1?"winch-progress-v1.json":"tow-progress-v1.json");
 int ContractCompletions(int id){try{
 var path=HistoryPath(id);
 if(File.Exists(path)){var state=JsonSerializer.Deserialize<ContractProgress>(File.ReadAllText(path));return state is {SchemaVersion:1}&&state.ContractId==ContractId(id)?Math.Max(0,state.Completions):0;}
 // Editor authoring history is local to the project. Packaged games start a trusted
 // progress schema instead of inheriting legacy test/payment-derived completions.
 if(id<2&&!ByteEngine.Core.Runtime.GameSaveStorage.IsRuntimeProject(ProjectRoot)){var legacy=Path.Combine(SaveRoot,id==1?"winch-history.json":"recovery-history.json");if(File.Exists(legacy))return Math.Max(0,JsonSerializer.Deserialize<int>(File.ReadAllText(legacy)));}
 return 0;
 }catch(Exception e)when(e is IOException or JsonException){return 0;}}
 void BindContractBrowser(){
 EnsureSprintContractRow();EnsureMotorsportRows();_contractPage=CurrentContract/4;_completedCount=ContractCompletions(CurrentContract);
 if(!_ui.ContainsKey("Contracts recovery category")&&_ui.TryGetValue("Contracts overlay",out var overlay)){
 var category=GameObject.Scene!.CreateGameObject("Contracts recovery category");category.SetParent(overlay,false);
 category.AddComponent(new UiText{Text="RECOVERY OPERATIONS",Offset=new(27,130),FontSize=16,FontReference=new("Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf"),Color=new(.94f,.66f,.29f,1),OrderInLayer=35});_ui[category.Name]=category;
 }
 // Leave room for the category, four jobs and the expanded brief inside its background.
 if(_ui.TryGetValue("Contracts overlay",out var panel)&&panel.GetComponent<UiWidget>() is {} panelWidget)panelWidget.Size=new(panelWidget.Size.X,Math.Max(panelWidget.Size.Y,604));
 Bind("Contracts available",()=>{_completedContracts=false;_contractExpanded=false;RefreshContractBrowser();});
 Bind("Contracts completed",()=>{_completedContracts=true;_contractExpanded=false;RefreshContractBrowser();});
 foreach(var (name,id) in ContractRows)Bind(name,()=>{if(id==SelectedContract){_contractExpanded=true;RefreshContractBrowser();}else SelectRecoveryContract(id);});
 RefreshContractBrowser();}
 void RecordContractCompletion(){_completedCount=ContractCompletions(CurrentContract)+1;AtomicSave(ContractHistory,JsonSerializer.Serialize(new ContractProgress(1,ContractId(CurrentContract),_completedCount)));_contractExpanded=false;RefreshContractBrowser();}
 void RefreshContractBrowser(){
 GarageActive("Contract expand",false);GarageActive("Choose tow contract",false);GarageActive("Choose winch contract",false);
 int visible=0;bool currentVisible=false;int start=_contractPage*4;
 foreach(var (name,id) in ContractRows){
  int count=ContractCompletions(id);bool show=id>=start&&id<start+4&&(_completedContracts?count>0:count==0);GarageActive(name,show);if(id==SelectedContract)currentVisible=show;
  if(_ui.TryGetValue(name,out var row)){var w=row.GetComponent<UiWidget>()!;w.Offset=new(27,164+visible*64);w.Size=new(273,56);w.FontSize=17;w.Label=ContractTitle(id)+(_completedContracts?"\nCOMPLETED":"\n$"+ContractReward(id)+" / "+(id>=4?"MOTORSPORT":"RECOVERY"));w.Color=id==SelectedContract&&_contractExpanded?new(.27f,.22f,.14f,1):new(.105f,.108f,.105f,1);}if(show)visible++;
 }
 GarageActive("Contracts recovery category",_contractPage==0);GarageActive("Contracts racing category",_contractPage==1);
 Text("Contracts recovery category",$"RECOVERY OPERATIONS / {visible} JOB{(visible==1?"":"S")}");Text("Contracts racing category",$"DESERT MOTORSPORT / {visible} EVENT{(visible==1?"":"S")}");
 if(_ui.TryGetValue("Contracts racing category",out var category))category.GetComponent<UiText>()!.Offset=new(27,130);
 foreach(var (name,page) in new[]{("Contracts recovery page",0),("Contracts motorsport page",1)})if(_ui.TryGetValue(name,out var button))button.GetComponent<UiWidget>()!.Color=_contractPage==page?new(.27f,.22f,.14f,1):new(.105f,.108f,.105f,1);
 GarageActive("Contract details",currentVisible&&_contractExpanded);
 Text("Contracts empty",visible>0?"":(_completedContracts?"No completed jobs in this category.":"Category completed. See Completed."));
 Text("Contracts subtitle",_completedContracts?"Completed jobs / Select a job to view its details":"Choose Recovery or Motorsport, then select a contract");
 foreach(string name in new[]{"Contracts available","Contracts completed"})if(_ui.TryGetValue(name,out var obj))obj.GetComponent<UiWidget>()!.Color=(name=="Contracts completed")==_completedContracts?new(.27f,.22f,.14f,1):new(.105f,.108f,.105f,1);
 Text("Contract history",_completedContracts?"JOB COMPLETED":SelectedContract==5?"DRIFT SCORE CHALLENGE":SelectedContract>=4?"RACE / ONE PHYSICAL RIVAL":SelectedContract==3?"EMERGENCY RECOVERY":"LOCAL RECOVERY");
 if(_ui.TryGetValue("Deploy contract",out var deploy)){deploy.Active=!_completedContracts;deploy.GetComponent<UiWidget>()!.Interactable=!_completedContracts;}
 }
 void RefreshContractDetails(){
  bool winch=SelectedContractWinch;
  GarageActive("Contract required 3",SelectedContract<4||SelectedContract==6);GarageActive("Contract required label 3",SelectedContract<4||SelectedContract==6);
  Text("Contract brief",SelectedContract>=4?MotorsportBrief(SelectedContract):SelectedContract==3?"A loaded truck has lost its brakes. Catch it before the broken bridge and bring it to a stop inside the recovery yard. The lower road is still open.":SelectedContract==2?"A survey vehicle disappeared into an old excavation. Its driver is safe. Recover the vehicle and return it to the depot. The access bank is unstable.":winch?"The buggy is below the rim of a deep excavation. Inspect the banks: the south cut is the only gentle exit. Find firm footing and a clear cable angle; the closest parking spot may not work. Recover the buggy, then bring both vehicles back to the depot.":"Follow the dry riverbed to a stranded buggy. Connect your trailer hinge, then bring it home. The dune shortcut is steep; the western track is longer and flatter. Stop both vehicles in the depot bay. Build for clearance and control under tow.");
  int type=SelectedContract==6?39:winch?33:20;
  if(_ui.TryGetValue("Contract required 3",out var icon))icon.GetComponent<UiWidget>()!.ImageReference=new(_catalog[type].Preview);
  Text("Contract required label 3",SelectedContract==6?(HasGearbox?"Gearbox fitted":"Gearbox required"):winch?"Powered winch":"Trailer hinge");
 }
 void RefreshRequirementIcons(){foreach(var (name,type,present) in new[]{("seat",25,_blocks.Any(b=>b.Type is 25 or 26)),("engine",23,_blocks.Any(b=>b.Type is 23 or 24 or 34 or 38)),("wheel",13,_blocks.Any(b=>_catalog[b.Type].Wheel))})if(_ui.TryGetValue("Requirement "+name,out var obj)){var w=obj.GetComponent<UiWidget>()!;w.ImageReference=new(_catalog[type].Preview);w.Color=present?new(.55f,1,.55f,1):new(1,.22f,.18f,1);}Text("Garage drive hint",ReadyToDrive?"":"Two wheels required for a test drive");}
}
