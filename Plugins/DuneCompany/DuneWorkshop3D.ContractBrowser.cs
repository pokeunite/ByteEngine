using System.Text.Json;
using System.Numerics;
using ByteEngine.Core.Graphics;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 bool _completedContracts,_contractExpanded;int _completedCount;
 string ContractHistory=>HistoryPath(WinchMission);
 sealed record ContractProgress(int SchemaVersion,string ContractId,int Completions);
 string ContractId(bool winch)=>winch?"well-truck-rescue":"stranded-buggy";
 string HistoryPath(bool winch)=>Path.Combine(SaveRoot,winch?"winch-progress-v1.json":"tow-progress-v1.json");
 int ContractCompletions(bool winch){try{
 var path=HistoryPath(winch);
 if(File.Exists(path)){var state=JsonSerializer.Deserialize<ContractProgress>(File.ReadAllText(path));return state is {SchemaVersion:1}&&state.ContractId==ContractId(winch)?Math.Max(0,state.Completions):0;}
 // Editor authoring history is local to the project. Packaged games start a trusted
 // progress schema instead of inheriting legacy test/payment-derived completions.
 if(!ByteEngine.Core.Runtime.GameSaveStorage.IsRuntimeProject(ProjectRoot)){var legacy=Path.Combine(SaveRoot,winch?"winch-history.json":"recovery-history.json");if(File.Exists(legacy))return Math.Max(0,JsonSerializer.Deserialize<int>(File.ReadAllText(legacy)));}
 return 0;
 }catch(Exception e)when(e is IOException or JsonException){return 0;}}
 void BindContractBrowser(){_completedCount=ContractCompletions(WinchMission);
 Bind("Contracts available",()=>{_completedContracts=false;_contractExpanded=false;RefreshContractBrowser();});
 Bind("Contracts completed",()=>{_completedContracts=true;_contractExpanded=false;RefreshContractBrowser();});
 foreach(var (name,winch) in new[]{("Contract row tow",false),("Contract row winch",true)})Bind(name,()=>{if(winch==SelectedContractWinch){_contractExpanded=!_contractExpanded;RefreshContractBrowser();}else SelectRecoveryContract(winch);});
 RefreshContractBrowser();}
 void RecordContractCompletion(){_completedCount=ContractCompletions(WinchMission)+1;AtomicSave(ContractHistory,JsonSerializer.Serialize(new ContractProgress(1,ContractId(WinchMission),_completedCount)));_contractExpanded=false;RefreshContractBrowser();}
 void RefreshContractBrowser(){
 GarageActive("Contract expand",false);GarageActive("Choose tow contract",false);GarageActive("Choose winch contract",false);
 int visible=0;bool currentVisible=false;
 foreach(var (name,winch,title,reward) in new[]{("Contract row tow",false,"Stranded in the sand",250),("Contract row winch",true,"The Well Ate My Truck",500)}){
 int count=ContractCompletions(winch);bool show=_completedContracts?count>0:count==0;GarageActive(name,show);if(winch==SelectedContractWinch)currentVisible=show;
 if(_ui.TryGetValue(name,out var row)){var w=row.GetComponent<UiWidget>()!;w.Offset=new(27,132+visible*38);w.Label=title+(_completedContracts?" / Completed":" / $"+reward)+(winch==SelectedContractWinch&&_contractExpanded?"   -":"   +");w.Color=winch==SelectedContractWinch&&_contractExpanded?new(.27f,.22f,.14f,1):new(.105f,.108f,.105f,1);}if(show)visible++;
 }
 GarageActive("Contract details",currentVisible&&_contractExpanded);if(_ui.TryGetValue("Contract details",out var details))details.GetComponent<UiWidget>()!.Offset=new(27,132+visible*38+8);
 Text("Contracts empty",visible>0?"":(_completedContracts?"No completed contracts yet.":"All contracts completed. See Completed."));
 Text("Contracts subtitle",_completedContracts?"Completed jobs / Select a job to view its details":"Available jobs / Select a contract to inspect its requirements");
 foreach(string name in new[]{"Contracts available","Contracts completed"})if(_ui.TryGetValue(name,out var obj))obj.GetComponent<UiWidget>()!.Color=(name=="Contracts completed")==_completedContracts?new(.27f,.22f,.14f,1):new(.105f,.108f,.105f,1);
 Text("Contract history",_completedContracts?"JOB COMPLETED":"LOCAL RECOVERY / ONE STRANDED BUGGY");
 if(_ui.TryGetValue("Deploy contract",out var deploy)){deploy.Active=!_completedContracts;deploy.GetComponent<UiWidget>()!.Interactable=!_completedContracts;}
 }
 void RefreshContractDetails(){
  bool winch=SelectedContractWinch;
  Text("Contract brief",winch?"Reach the abandoned extraction site via its winding ramp. Park on the firm apron and winch the buggy out of the pit. Bring it down the access road to the depot; stop both vehicles in the green bay. Build for climbing, stability and braking.":"Follow the dry riverbed to a stranded buggy. Connect your trailer hinge, then bring it home. The dune shortcut is steep; the western track is longer and flatter. Stop both vehicles in the green depot bay. Build for clearance and control under tow.");
  int type=winch?33:20;
  if(_ui.TryGetValue("Contract required 3",out var icon))icon.GetComponent<UiWidget>()!.ImageReference=new(_catalog[type].Preview);
  Text("Contract required label 3",winch?"Powered winch":"Trailer hinge");
 }
 void RefreshRequirementIcons(){foreach(var (name,type,present) in new[]{("seat",25,_blocks.Any(b=>b.Type is 25 or 26)),("engine",23,_blocks.Any(b=>b.Type is 23 or 24)),("wheel",13,_blocks.Any(b=>_catalog[b.Type].Wheel))})if(_ui.TryGetValue("Requirement "+name,out var obj)){var w=obj.GetComponent<UiWidget>()!;w.ImageReference=new(_catalog[type].Preview);w.Color=present?new(.55f,1,.55f,1):new(1,.22f,.18f,1);}Text("Garage drive hint",ReadyToDrive?"":"Two wheels required for a test drive");}
}
