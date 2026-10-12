using System.Text.Json;
using System.Numerics;
using ByteEngine.Core.Graphics;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 bool _completedContracts,_contractExpanded;int _completedCount;
 string ContractHistory=>HistoryPath(CurrentContract);
 sealed record ContractProgress(int SchemaVersion,string ContractId,int Completions);
 string ContractId(int id)=>id==4?"scrapyard-sprint":id==3?"no-brakes":id==2?"down-with-patient":id==1?"well-truck-rescue":"stranded-buggy";
 string HistoryPath(int id)=>Path.Combine(SaveRoot,id==4?"sprint-progress-v1.json":id==3?"no-brakes-progress-v1.json":id==2?"anchor-progress-v1.json":id==1?"winch-progress-v1.json":"tow-progress-v1.json");
 int ContractCompletions(int id){try{
 var path=HistoryPath(id);
 if(File.Exists(path)){var state=JsonSerializer.Deserialize<ContractProgress>(File.ReadAllText(path));return state is {SchemaVersion:1}&&state.ContractId==ContractId(id)?Math.Max(0,state.Completions):0;}
 // Editor authoring history is local to the project. Packaged games start a trusted
 // progress schema instead of inheriting legacy test/payment-derived completions.
 if(id<2&&!ByteEngine.Core.Runtime.GameSaveStorage.IsRuntimeProject(ProjectRoot)){var legacy=Path.Combine(SaveRoot,id==1?"winch-history.json":"recovery-history.json");if(File.Exists(legacy))return Math.Max(0,JsonSerializer.Deserialize<int>(File.ReadAllText(legacy)));}
 return 0;
 }catch(Exception e)when(e is IOException or JsonException){return 0;}}
 void BindContractBrowser(){
 EnsureSprintContractRow();_completedCount=ContractCompletions(CurrentContract);
 if(!_ui.ContainsKey("Contracts recovery category")&&_ui.TryGetValue("Contracts overlay",out var overlay)){
 var category=GameObject.Scene!.CreateGameObject("Contracts recovery category");category.SetParent(overlay,false);
 category.AddComponent(new UiText{Text="RECOVERY OPERATIONS",Offset=new(27,130),FontSize=16,FontReference=new("Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf"),Color=new(.94f,.66f,.29f,1),OrderInLayer=35});_ui[category.Name]=category;
 }
 // Leave room for the category, four jobs and the expanded brief inside its background.
 if(_ui.TryGetValue("Contracts overlay",out var panel)&&panel.GetComponent<UiWidget>() is {} panelWidget)panelWidget.Size=new(panelWidget.Size.X,Math.Max(panelWidget.Size.Y,604));
 Bind("Contracts available",()=>{_completedContracts=false;_contractExpanded=false;RefreshContractBrowser();});
 Bind("Contracts completed",()=>{_completedContracts=true;_contractExpanded=false;RefreshContractBrowser();});
 foreach(var (name,id) in new[]{("Contract row tow",0),("Contract row winch",1),("Contract row anchor",2),("Contract row relay",3),("Contract row sprint",4)})Bind(name,()=>{if(id==SelectedContract){_contractExpanded=!_contractExpanded;RefreshContractBrowser();}else SelectRecoveryContract(id);});
 RefreshContractBrowser();}
 void RecordContractCompletion(){_completedCount=ContractCompletions(CurrentContract)+1;AtomicSave(ContractHistory,JsonSerializer.Serialize(new ContractProgress(1,ContractId(CurrentContract),_completedCount)));_contractExpanded=false;RefreshContractBrowser();}
 void RefreshContractBrowser(){
 GarageActive("Contract expand",false);GarageActive("Choose tow contract",false);GarageActive("Choose winch contract",false);
 int visible=0;bool currentVisible=false;int recoveryVisible=Enumerable.Range(0,4).Count(id=>_completedContracts?ContractCompletions(id)>0:ContractCompletions(id)==0);GarageActive("Contracts racing category",(_completedContracts?ContractCompletions(4)>0:ContractCompletions(4)==0));
 foreach(var (name,id) in new[]{("Contract row tow",0),("Contract row winch",1),("Contract row anchor",2),("Contract row relay",3),("Contract row sprint",4)}){
 int count=ContractCompletions(id);bool show=_completedContracts?count>0:count==0;GarageActive(name,show);if(id==SelectedContract)currentVisible=show;
 if(_ui.TryGetValue(name,out var row)){var w=row.GetComponent<UiWidget>()!;w.Offset=new(27,148+visible*27+(id==4?18:0));w.Size=new(w.Size.X,25);w.Label=ContractTitle(id)+(_completedContracts?" / Completed":" / $"+ContractReward(id))+(id==SelectedContract&&_contractExpanded?"   -":"   +");w.Color=id==SelectedContract&&_contractExpanded?new(.27f,.22f,.14f,1):new(.105f,.108f,.105f,1);}if(show)visible++;
 }
 if(_ui.TryGetValue("Contracts racing category",out var racingCategory))racingCategory.GetComponent<UiText>()!.Offset=new(27,148+(visible-((_completedContracts?ContractCompletions(4)>0:ContractCompletions(4)==0)?1:0))*27);
 Text("Contracts recovery category",$"RECOVERY OPERATIONS / {recoveryVisible} {(recoveryVisible==1?"JOB":"JOBS")}");GarageActive("Contracts recovery category",recoveryVisible>0);
 GarageActive("Contract details",currentVisible&&_contractExpanded);if(_ui.TryGetValue("Contract details",out var details))details.GetComponent<UiWidget>()!.Offset=new(27,148+visible*27+26);
 Text("Contracts empty",visible>0?"":(_completedContracts?"No completed contracts yet.":"All contracts completed. See Completed."));
 Text("Contracts subtitle",_completedContracts?"Completed jobs / Select a job to view its details":"Available jobs / Select a contract to inspect its requirements");
 foreach(string name in new[]{"Contracts available","Contracts completed"})if(_ui.TryGetValue(name,out var obj))obj.GetComponent<UiWidget>()!.Color=(name=="Contracts completed")==_completedContracts?new(.27f,.22f,.14f,1):new(.105f,.108f,.105f,1);
 Text("Contract history",_completedContracts?"JOB COMPLETED":SelectedContract==4?"SPRINT RACE / ONE PHYSICAL RIVAL":SelectedContract==3?"EMERGENCY RECOVERY / RUNAWAY CARGO TRUCK":"LOCAL RECOVERY / ONE STRANDED BUGGY");
 if(_ui.TryGetValue("Deploy contract",out var deploy)){deploy.Active=!_completedContracts;deploy.GetComponent<UiWidget>()!.Interactable=!_completedContracts;}
 }
 void RefreshContractDetails(){
  bool winch=SelectedContractWinch;
  GarageActive("Contract required 3",SelectedContract!=4);GarageActive("Contract required label 3",SelectedContract!=4);
  Text("Contract brief",SelectedContract==4?"Beat the rival through the dry riverbed. Gates must be passed in order. Keep your line through the bends; the rival shares the track and can make contact. Win the sprint to earn $1000.":SelectedContract==3?"A loaded truck has lost its brakes. Catch it before the broken bridge and bring it to a stop inside the recovery yard. The lower road is still open.":SelectedContract==2?"A survey vehicle disappeared into an old excavation. Its driver is safe. Recover the vehicle and return it to the depot. The access bank is unstable.":winch?"The buggy is below the rim of a deep excavation. Inspect the banks: the south cut is the only gentle exit. Find firm footing and a clear cable angle; the closest parking spot may not work. Recover the buggy, then bring both vehicles back to the depot.":"Follow the dry riverbed to a stranded buggy. Connect your trailer hinge, then bring it home. The dune shortcut is steep; the western track is longer and flatter. Stop both vehicles in the depot bay. Build for clearance and control under tow.");
  int type=winch?33:20;
  if(_ui.TryGetValue("Contract required 3",out var icon))icon.GetComponent<UiWidget>()!.ImageReference=new(_catalog[type].Preview);
  Text("Contract required label 3",winch?"Powered winch":"Trailer hinge");
 }
 void RefreshRequirementIcons(){foreach(var (name,type,present) in new[]{("seat",25,_blocks.Any(b=>b.Type is 25 or 26)),("engine",23,_blocks.Any(b=>b.Type is 23 or 24 or 34 or 38)),("wheel",13,_blocks.Any(b=>_catalog[b.Type].Wheel))})if(_ui.TryGetValue("Requirement "+name,out var obj)){var w=obj.GetComponent<UiWidget>()!;w.ImageReference=new(_catalog[type].Preview);w.Color=present?new(.55f,1,.55f,1):new(1,.22f,.18f,1);}Text("Garage drive hint",ReadyToDrive?"":"Two wheels required for a test drive");}
}
