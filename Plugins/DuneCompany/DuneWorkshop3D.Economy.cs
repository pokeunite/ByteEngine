using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Graphics;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 static bool IsWinch(int type)=>type is 33 or 35 or 36;
 sealed class EconomyState{public int Version{get;set;}=2;public int Balance{get;set;}public Dictionary<int,int> Owned{get;set;}=[];public HashSet<int> Tuning{get;set;}=[];public HashSet<string> Rewards{get;set;}=[];}
 EconomyState _economy=new();
 string EconomyFile=>Path.Combine(SaveRoot,"workshop-economy-v1.json");
 int PartPrice(int type)=>_catalog.TryGetValue(type,out var part)?Math.Max(0,part.Price):0;
 int TuningPrice(int type)=>_catalog.TryGetValue(type,out var part)?Math.Max(0,part.TuningCost):25;
 bool TuningUnlocked=>_tuning>=0&&_economy.Tuning.Contains(_blocks.First(b=>b.Id==_tuning).Type);
 void LoadEconomy(){if(File.Exists(EconomyFile)){var state=JsonSerializer.Deserialize<EconomyState>(File.ReadAllText(EconomyFile));if(state is null||state.Version is not (1 or 2)||state.Balance<0||state.Owned==null||state.Tuning==null||state.Rewards==null)throw new InvalidDataException("Workshop economy save is invalid.");if(state.Version==1){foreach(var type in state.Owned.Keys.ToArray()){int count=state.Owned[type];state.Balance+=Math.Max(0,count-1)*PartPrice(type);state.Owned[type]=count>0?1:0;}state.Version=2;AtomicSave(EconomyFile,JsonSerializer.Serialize(state));}_economy=state;_credits=state.Balance;}else{_economy=new(){Balance=_credits};AtomicSave(EconomyFile,JsonSerializer.Serialize(_economy));}}
 bool CommitEconomy(Action<EconomyState> change){var next=JsonSerializer.Deserialize<EconomyState>(JsonSerializer.Serialize(_economy))!;change(next);try{AtomicSave(EconomyFile,JsonSerializer.Serialize(next));}catch(IOException){_message="Could not save the purchase. No money was charged.";return false;}_economy=next;_credits=next.Balance;RefreshBalanceDisplay();return true;}
 bool PartUnlocked(int type)=>PartPrice(type)==0||_economy.Owned.GetValueOrDefault(type)>0;
 bool OwnsBuild(IEnumerable<PlacedBlock> blocks){foreach(int type in blocks.Select(b=>b.Type).Distinct())if(!PartUnlocked(type)){_message=$"Unlock {_catalog[type].Label} before loading this build.";return false;}return true;}
 bool CanPlacePaid(int type){if(PartUnlocked(type))return true;_message="Unlock this part type once in the inspector before placing it.";return false;}
 public bool BuyPart(int type){if(!_catalog.ContainsKey(type))return false;if(PartUnlocked(type))return true;int price=PartPrice(type);if(_credits<price){_message=$"Need ${price-_credits} more to unlock {_catalog[type].Label}.";return false;}bool ok=CommitEconomy(s=>{s.Balance-=price;s.Owned[type]=1;});if(ok){JobNotice(_catalog[type].Label.ToUpperInvariant()+" / UNLOCKED");JobSound("hitch-lock",.45f);}RefreshEconomyUi();return ok;}
 public bool SellPart(int type)=>false; public bool UnlockTuning(int type){if(!_catalog.ContainsKey(type))return false;if(_economy.Tuning.Contains(type))return true;int price=TuningPrice(type);if(_credits<price){_message=$"Need ${price-_credits} more to unlock tuning.";return false;}bool ok=CommitEconomy(s=>{s.Balance-=price;s.Tuning.Add(type);});if(ok)RefreshTuning();return ok;}
 bool AwardContract(){string id=ContractId(CurrentContract);if(_economy.Rewards.Contains(id))return true;return CommitEconomy(s=>{s.Balance+=ContractReward(CurrentContract);s.Rewards.Add(id);});}
 void BindEconomyUi(){
  void Button(string name,string parent,Vector2 offset,Action click){if(!_ui.TryGetValue(parent,out var host))return;var obj=GameObject.Scene!.CreateGameObject(name);obj.SetParent(host,false);obj.AddComponent(new UiWidget{Kind=UiWidgetKind.Button,Offset=offset,Size=new(240,30),Label=name,FontSize=16,FontReference=new("Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf"),Color=new(.28f,.22f,.12f,1),HoverColor=new(.4f,.3f,.15f,1),OrderInLayer=40});_ui[name]=obj;Bind(name,click);}
  Button("Buy upgrade","Garage inspector",new(20,270),()=>BuyPart(_type));Button("Unlock tuning","Block tuning panel",new(20,200),()=>{if(_tuning>=0)UnlockTuning(_blocks.First(b=>b.Id==_tuning).Type);});RefreshEconomyUi();
 }
 void RefreshEconomyUi(){
  if(_catalog.Count==0)return;
  if(_ui.TryGetValue("Tune selection",out var tune)&&tune.GetComponent<UiWidget>() is {} tuneButton){tuneButton.Offset=new(tuneButton.Offset.X,(PartPrice(_type)>0?307:260)+(_inspectorJoined?48:0));tuneButton.Interactable=_selected>=0;}
  GarageActive("Sell unused upgrade",false);
  if(_ui.TryGetValue("Buy upgrade",out var obj)){int price=PartPrice(_type);bool unlocked=PartUnlocked(_type);obj.Active=Building&&_tuning<0&&price>0&&_garageOverlay.Length==0;var w=obj.GetComponent<UiWidget>()!;w.Label=unlocked?"PART UNLOCKED / UNLIMITED":"UNLOCK PART / $"+price;w.Interactable=!unlocked&&_credits>=price;}
 }
 void RefreshTuningLayout(){
  bool summary=_tuning<0;
  foreach(string name in new[]{"Summary divider","Summary title","Mass caption","Garage mass","Wheels caption","Garage wheels","Engine caption","Garage engines","Readiness divider","Garage readiness","Requirement seat","Requirement engine","Requirement wheel","Seat readiness","Engine readiness","Wheel readiness","Garage drive hint"})GarageActive(name,summary);
 }
}
