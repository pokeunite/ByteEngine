using System.Numerics;
using ByteEngine.Core.Graphics;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 int _contractPage;
 static readonly (string name,int id)[] ContractRows=[("Contract row tow",0),("Contract row winch",1),("Contract row anchor",2),("Contract row relay",3),("Contract row sprint",4),("Contract row drift",5),("Contract row drag",6),("Contract row rally",7)];
 void EnsureMotorsportRows(){
  if(!_ui.TryGetValue("Contracts overlay",out var parent))return;
  void Row(string name,string label,Vector2 offset,Vector2 size,Action action){if(_ui.ContainsKey(name))return;var o=GameObject.Scene!.CreateGameObject(name);o.SetParent(parent,false);o.AddComponent(new UiWidget{Kind=UiWidgetKind.Button,Offset=offset,Size=size,Label=label,FontSize=15,FontReference=new("Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf"),Color=new(.105f,.108f,.105f,1),HoverColor=new(.30f,.23f,.13f,1),OrderInLayer=35});_ui[name]=o;Bind(name,action);}
  foreach(var (name,id) in ContractRows.Where(r=>r.id>=5)){int selected=id;Row(name,ContractTitle(id),new(27,164),new(273,56),()=>SelectRecoveryContract(selected));}
  Row("Contracts recovery page","RECOVERY",new(27,442),new(132,36),()=>{_contractPage=0;_contractExpanded=false;RefreshContractBrowser();});
  Row("Contracts motorsport page","MOTORSPORT",new(168,442),new(132,36),()=>{_contractPage=1;_contractExpanded=false;RefreshContractBrowser();});
 }
 string MotorsportBrief(int id)=>id switch {
  5=>"The quarry is yours for two minutes. Bank 6,000 drift points. Controlled slides build the multiplier; straighten up to bank your chain. Hitting an obstacle loses the current chain. Use the broad outer turns or link the figure-eight.",
  6=>$"A flat 400-metre run against a physical rival. Fit a gearbox directly to the engine's rear. {DriveKey("Shift up")} shifts up; {DriveKey("Shift down")} shifts down. Shift in the green RPM band. Early shifts lose acceleration; late shifts hit the limiter. Hold the accelerator through the countdown. Launch on green, use all six gears, then stop in the long braking zone.",
  7=>"Beat the rival along the 900-metre Red Rock stage. Pass all six gates in order. Carry speed through the sweeps, settle the car for the rock passage, and land straight over the crest. The rival shares the road.",
  _=>"Beat the rival through the dry riverbed. Gates must be passed in order. Keep your line through the bends; the rival shares the track and can make contact. Win the sprint to earn $1000."};
}
