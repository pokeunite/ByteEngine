using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
using Component=ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;
public enum MiningScreen{None,Drill,AmmoPress,Refinery,Company,Market,Research,AudioCredits,Account}
/// <summary>Reference-matched scalable presentation. Native button objects are bound by Event Sheets.</summary>
public sealed partial class MiningBusinessUi3D:Component
{
 public bool ShowEnergy{get;set;}
 [SceneObjectReference] public Guid OperationObjectId{get;set;}
 [SceneObjectReference] public Guid DrillInteractionObjectId{get;set;}
 [SceneObjectReference] public Guid WorkshopObjectId{get;set;}
 [SceneObjectReference] public Guid AmmoMachineObjectId{get;set;}
 [SceneObjectReference] public Guid RefineryMachineObjectId{get;set;}
 [SceneObjectReference] public Guid TerminalObjectId{get;set;}
 [SceneObjectReference] public Guid ModalBlockerObjectId{get;set;}
 [SceneObjectReference] public Guid CloseButtonObjectId{get;set;}
 [SceneObjectReference] public Guid CloseXButtonObjectId{get;set;}
 [SceneObjectReference] public Guid Row0ButtonObjectId{get;set;}
 [SceneObjectReference] public Guid Row1ButtonObjectId{get;set;}
 [SceneObjectReference] public Guid Row2ButtonObjectId{get;set;}
 [SceneObjectReference] public Guid PrimaryButtonObjectId{get;set;}
 [SceneObjectReference] public Guid SecondaryButtonObjectId{get;set;}
 [SceneObjectReference] public Guid MinusButtonObjectId{get;set;}
 [SceneObjectReference] public Guid PlusButtonObjectId{get;set;}
 [SceneObjectReference] public Guid QuantitySliderObjectId{get;set;}
 public List<Guid> ResearchButtonObjectIds{get;set;}=[];
 [Category("Bindings")] public string ResearchButtonBindings{get=>string.Join(";",ResearchButtonObjectIds);set=>ResearchButtonObjectIds=(value??"").Split(';',StringSplitOptions.RemoveEmptyEntries).Select(s=>Guid.TryParse(s,out var id)?id:Guid.Empty).Where(id=>id!=Guid.Empty).ToList();}
 [SceneObjectReference] public Guid CreditsButtonObjectId{get;set;}
 [SceneObjectReference] public Guid RawButtonObjectId{get;set;}
 [SceneObjectReference] public Guid RefinedButtonObjectId{get;set;}
 [Category("Typography")] public AssetReference HeadingFont{get;set;}=AssetReference.Empty;
 [Category("Typography")] public AssetReference BodyFont{get;set;}=AssetReference.Empty;
 [Category("Artwork")] public AssetReference MachineIllustration{get;set;}=AssetReference.Empty;
 [Category("Artwork")] public AssetReference RawOreIcon{get;set;}=AssetReference.Empty;
 [Category("Artwork")] public AssetReference RefinedOreIcon{get;set;}=AssetReference.Empty;
 [Category("Artwork")] public AssetReference OreIllustration{get;set;}=AssetReference.Empty;
 [Category("Artwork")] public AssetReference DrillIllustration{get;set;}=AssetReference.Empty;
 [Category("Artwork")] public AssetReference RefineryIllustration{get;set;}=AssetReference.Empty;
 [Category("Artwork")] public AssetReference TentIllustration{get;set;}=AssetReference.Empty;
 [Category("Palette")] public Vector4 Surface{get;set;}=new(.055f,.063f,.073f,.98f);
 [Category("Palette")] public Vector4 TextColor{get;set;}=new(.94f,.93f,.88f,1);
 [Category("Palette")] public Vector4 Accent{get;set;}=new(.91f,.71f,.15f,1);
 [Category("Palette")] public Vector4 PurchaseColor{get;set;}=new(.91f,.71f,.15f,1);
 [Category("Interaction")] public float InteractionDistance{get;set;}=4.5f;
 public MiningScreen Screen{get;private set;}
 public bool RefinedSelected{get;private set;}=true;
 public int Quantity{get;private set;}=20;
 public string Feedback{get;private set;}="";
 public MiningScreen NearestScreen{get;private set;}
 public string SelectedResearchId{get;private set;}="company";
 public float HoverSoundGain{get;set;}=1;
 Guid hoveredButton;
 MiningCampLayout3D? CampLayout=>GameObject.Scene?.FindGameObject(Op?.PlayerObjectId??Guid.Empty)?.GetComponent<MiningCampLayout3D>();
 bool CampEditing=>CampLayout?.IsEditing==true;
 public bool NearTerminal{get{if(Op is not {} op||GameObject.Scene?.FindGameObject(TerminalObjectId) is not {} terminal||GameObject.Scene.FindGameObject(op.PlayerObjectId) is not {} player)return false;var d=player.Transform.WorldPosition-terminal.Transform.WorldPosition;return d.X*d.X+d.Z*d.Z<=InteractionDistance*InteractionDistance;}}
 public override int? RenderOrder=>900;
 MiningOperation3D? Op=>GameObject.Scene?.FindGameObject(OperationObjectId)?.GetComponent<MiningOperation3D>();
 MiningTrade3D? Trade=>GameObject.Scene?.FindGameObject(WorkshopObjectId)?.GetComponent<MiningTrade3D>();
 bool restoreMovement=true,restoreThirdPerson,restoreDash;float feedbackClock;bool receiptInitialized;string? seenReceipt;
 public float PayoutRevealSeconds{get;private set;}
 public bool PayoutReceiptVisible=>PayoutRevealSeconds>0;
 UiWidget? W(Guid id)=>GameObject.Scene?.FindGameObject(id)?.GetComponent<UiWidget>();
 protected override void OnUpdate()
 {
  if(Op is not {} op)return;TickWeaponHud((float)Time.DeltaTime);feedbackClock-= (float)Time.DeltaTime;if(feedbackClock<=0)Feedback="";
  PayoutRevealSeconds=Math.Max(0,PayoutRevealSeconds-(float)Time.DeltaTime);if(!receiptInitialized){seenReceipt=op.LastMarketSale?.Id;receiptInitialized=true;}else if(op.LastMarketSale is {} receipt&&receipt.Id!=seenReceipt){seenReceipt=receipt.Id;PayoutRevealSeconds=6;if(Screen!=MiningScreen.None||!CombatHud){Notice($"PAID +{receipt.CampPayout:N0} CAMP / +{receipt.CompanyDeposit:N0} COMPANY");feedbackClock=6;}}
  NearestScreen=MiningScreen.None;float distance=InteractionDistance;
  var player=GameObject.Scene?.FindGameObject(op.PlayerObjectId);
  foreach(var pair in new[]{(op.DrillObjectId,MiningScreen.Drill),(AmmoMachineObjectId,MiningScreen.AmmoPress),(RefineryMachineObjectId,MiningScreen.Refinery),(TerminalObjectId,MiningScreen.Company)})
   if(player!=null&&GameObject.Scene?.FindGameObject(pair.Item1) is {} machine){float d=pair.Item2==MiningScreen.Company?Vector3.Distance(player.Transform.WorldPosition,machine.Transform.WorldPosition):MiningCampGeometry.SurfaceDistance(machine,player.Transform.WorldPosition);if(d<distance){distance=d;NearestScreen=pair.Item2;}}
  if(MiningCampInteraction.Nearest(GameObject.Scene!,op)?.GetComponent<MiningAuxiliaryRefinery3D>()!=null)NearestScreen=MiningScreen.None;
  if(Screen!=MiningScreen.None&&(op.Phase==MiningPhase.Drilling&&Screen is MiningScreen.Drill or MiningScreen.Research or MiningScreen.Account||op.Phase==MiningPhase.Failed||op.TargetWon||op.TargetExpired))Close();
  if(Screen is MiningScreen.Company or MiningScreen.Market or MiningScreen.Research or MiningScreen.AudioCredits or MiningScreen.Account&&!NearTerminal)Close();
  TickOperationAssist(op);TickCompanyAccountInput(op);bool modal=Screen!=MiningScreen.None;
  if(modal)Input.NotifyGameViewPointerAim();
  if(GameObject.Scene?.FindGameObject(ModalBlockerObjectId) is {} blocker)blocker.Active=modal;
  void Set(Guid id,bool visible,bool enabled=true){if(W(id) is {} w){w.Visible=visible;w.Interactable=enabled;}}
  bool ammo=Screen==MiningScreen.AmmoPress,market=Screen==MiningScreen.Market,refinery=Screen==MiningScreen.Refinery,research=Screen==MiningScreen.Research;
  Set(CloseButtonObjectId,modal&&!market);Set(CloseXButtonObjectId,modal);
  Set(Row0ButtonObjectId,ammo||refinery||market||Screen==MiningScreen.Drill,market?Trade?.SaleInProgress!=true:CanBuy(0));Set(Row1ButtonObjectId,ammo||refinery||market,market?Trade?.SaleInProgress!=true:CanBuy(1));Set(Row2ButtonObjectId,ammo||refinery||market&&!GuaranteedSelected,market?Trade?.SaleInProgress!=true:CanBuy(2));
  Set(PrimaryButtonObjectId,modal&&!ammo,market?Trade?.SaleInProgress!=true&&(GuaranteedSelected?Trade?.ActiveDeliveryContract is {} dc&&Trade.Stock(true)>=dc.Quantity:MarketQuantityLimit>=Quantity&&Quantity>0):research?Trade?.CanBuyResearch(SelectedResearchId)==true:ammo?(Trade?.CollectibleAmmo??0)>0:refinery?op.RefiningAmount==0&&op.RawOre>0:Screen!=MiningScreen.Drill||op.RefiningAmount==0||op.QueueRefineryBatches);
  for(int i=0;i<3;i++)Set(ContractButton(i),market&&GuaranteedSelected&&Trade?.SaleInProgress!=true&&Trade?.ActiveDeliveryContract==null,Screen==MiningScreen.Drill?op.PreviewContract(i)!=null:Trade is {} ct&&i<ct.DeliveryOrders.Count&&ct.DeliveryOrders[i].Quantity<=ct.DeliveryCapacity);
  if(Screen==MiningScreen.Drill)Set(PrimaryButtonObjectId,true,op.ExpeditionActive?op.Phase==MiningPhase.Preparation:op.SelectedContract is {} preview&&(op.ExpeditionsEnabled||op.RefiningAmount==0||op.QueueRefineryBatches)&&(long)op.CargoUsed+preview.Ore<=op.CargoCapacity);
  Set(CreditsButtonObjectId,research);if(Screen==MiningScreen.AudioCredits)Set(PrimaryButtonObjectId,false);
  Set(SecondaryButtonObjectId,market||research||refinery||Screen==MiningScreen.Company);
  Set(CloseButtonObjectId,modal);
  Set(MinusButtonObjectId,false);Set(PlusButtonObjectId,false);Set(RawButtonObjectId,market&&!GuaranteedSelected,Trade?.SaleInProgress!=true);Set(RefinedButtonObjectId,market&&!GuaranteedSelected,Trade?.SaleInProgress!=true);
  Set(QuantitySliderObjectId,market&&!GuaranteedSelected&&Trade?.SaleInProgress!=true,MarketQuantityLimit>0);
  if(market&&!GuaranteedSelected&&W(QuantitySliderObjectId) is {} slider){slider.Maximum=Math.Max(1,MarketQuantityLimit);Quantity=Math.Clamp((int)MathF.Round(slider.Value),0,MarketQuantityLimit);}
  for(int i=0;i<ResearchButtonObjectIds.Count;i++)Set(ResearchButtonObjectIds[i],research&&i<(Trade?.Research.Count??0));
  if(Screen==MiningScreen.Account){Set(Row0ButtonObjectId,false);Set(Row1ButtonObjectId,false);Set(Row2ButtonObjectId,false);Set(PrimaryButtonObjectId,false);Set(SecondaryButtonObjectId,false);}UpdateDefenceHud();UpdateMapInput();UpdateDrillMapInput();LayoutCompact();
  var hovered=modal?GameObject.Scene!.GameObjects.SelectMany(o=>o.Components.OfType<UiWidget>()).FirstOrDefault(w=>w.Visible&&w.Interactable&&w.IsHovered)?.GameObject.Id??Guid.Empty:Guid.Empty;
  if(hovered!=hoveredButton){if(hovered!=Guid.Empty)MiningAudio3D.Find(GameObject)?.PlayHoverCue(HoverSoundGain);hoveredButton=hovered;}
 }
 protected override void OnStop(){Input.RequestUiPointer(defencePointerOwner,false);Close();}
 protected override void OnDestroy(){Input.RequestUiPointer(defencePointerOwner,false);Input.ConsumeGameplayPointer();Input.RequestUiPointer(this,false);}
 public void OpenNearby(){if(DefencePlanner?.BuildMode==true)return;if(Op is {} interactionOp&&MiningCampInteraction.Nearest(GameObject.Scene!,interactionOp)?.GetComponent<MiningAuxiliaryRefinery3D>() is {} local){local.Open();return;}if(GameObject.Scene?.GetComponentsSnapshot<MiningAuxiliaryRefinery3D>().Any(r=>r.IsOpen)==true)return;if(GameObject.Scene?.GetComponentsSnapshot<MiningResearchStation3D>().Any(r=>r.IsOpen)==true)return;if(Op?.OverclockAvailable==true&&NearestScreen==MiningScreen.Drill)return;if(CampEditing||Op is {TargetWon:true} or {TargetExpired:true})return;if(Screen!=MiningScreen.None){Close();return;}if(Op?.Recovering==true)return;if(Op?.Phase==MiningPhase.Failed){Op.Retry();return;}Open(NearestScreen);}
 public void Open(MiningScreen screen)
 {
  if(CampEditing)return;

  if(screen==MiningScreen.None||Op?.CanUseIndustry!=true)return;if(Op.Phase==MiningPhase.Drilling&&screen is MiningScreen.Drill or MiningScreen.Research or MiningScreen.Account){Notice(screen==MiningScreen.Drill?"Drill controls are locked during a wave.":"Permanent research is available between waves.");return;}
  if(screen is MiningScreen.Company or MiningScreen.Market or MiningScreen.Research or MiningScreen.AudioCredits or MiningScreen.Account&&!NearTerminal){Notice("Approach the company tent to use its computer.");return;}
  if(Screen==MiningScreen.None&&GameObject.Scene?.FindGameObject(Op.PlayerObjectId) is {} player)
  {
   if(player.GetComponent<TopDownController3D>() is {} top){restoreMovement=top.InputEnabled;top.InputEnabled=false;}
   if(player.GetComponent<PlayerController3D>() is {} third){restoreThirdPerson=third.Enabled;third.Enabled=false;}
   if(player.GetComponent<DashAbility3D>() is {} dash){restoreDash=dash.Enabled;dash.Cancel();dash.Enabled=false;}
   player.GetComponent<CharacterController3D>()?.Move(Vector3.Zero);
  }
  Input.NotifyGameViewPointerAim();
  Input.RequestUiPointer(this,true);
  if(screen==MiningScreen.Market&&Trade?.Operation?.PendingMarketSale is {} accepted){GuaranteedSelected=accepted.Guaranteed;RefinedSelected=accepted.Refined;}
  if(screen==MiningScreen.Research&&Trade is {} research&&!research.Research.Any(n=>n.Id==SelectedResearchId))SelectedResearchId=research.Research.FirstOrDefault(n=>n.Id=="shotgun")?.Id??research.Research.FirstOrDefault()?.Id??"";
  if(Op.OverclockAvailable)Op.ClearOverclockOffer();Screen=screen;MiningAudio3D.Find(GameObject)?.PlayCue(screen==MiningScreen.Refinery?MiningSoundCue.RefineryOpen:screen==MiningScreen.AmmoPress?MiningSoundCue.PressOpen:screen is MiningScreen.Research or MiningScreen.Market or MiningScreen.AudioCredits?MiningSoundCue.TerminalOpen:MiningSoundCue.Confirm);Feedback="";if(screen==MiningScreen.Market)SetQuantity(Math.Min(20,Trade?.Stock(RefinedSelected)??0));UiNavigation.Focus(null);
 }
 public void Close()
 {
  if(Screen!=MiningScreen.None&&Input.IsKeyPressed(Key.Escape))MiningPauseMenu3D.IgnoreEscapeAt=Time.TotalTime;
  if(Screen!=MiningScreen.None)MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.PanelClose);
  if(Screen!=MiningScreen.None&&Op is {} op&&GameObject.Scene?.FindGameObject(op.PlayerObjectId) is {} player)
  {if(player.GetComponent<TopDownController3D>() is {} top)top.InputEnabled=restoreMovement;if(player.GetComponent<PlayerController3D>() is {} third)third.Enabled=restoreThirdPerson;if(player.GetComponent<DashAbility3D>() is {} dash)dash.Enabled=restoreDash;}
  Screen=MiningScreen.None;UiNavigation.Focus(null);
  Input.ConsumeGameplayPointer();Input.RequestUiPointer(this,false);
 }
 public void SelectResource(bool refined){MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.TerminalSelect);RefinedSelected=refined;SetQuantity(Math.Min(20,MarketQuantityLimit));}
 public void SetQuantity(int quantity){Quantity=Math.Clamp(quantity,0,MarketQuantityLimit);if(W(QuantitySliderObjectId) is {} slider){slider.Maximum=Math.Max(1,MarketQuantityLimit);slider.Value=Quantity;}}
 public void ChangeQuantity(int change)=>SetQuantity(Quantity+change);
 public void ConfirmSelection(){if(Screen==MiningScreen.Research&&!ReferenceEquals(UiNavigation.Focused,W(PrimaryButtonObjectId)))Primary();}
 public void SelectResearch(int index){if(Trade is {} trade&&index>=0&&index<trade.Research.Count){if(SelectedResearchId!=trade.Research[index].Id)MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.TerminalSelect);SelectedResearchId=trade.Research[index].Id;descriptionScroll=0;}}
 MiningUpgrade RowUpgrade(int row)=>Screen==MiningScreen.Drill?MiningUpgrade.Drill:row==1?MiningUpgrade.RefinerySpeed:row==2?MiningUpgrade.RefineryYield:MiningUpgrade.Refinery;
 public bool CanBuy(int row)
 {if(Op is not {} op||!op.CanUseIndustry||row<0||row>2)return false;if(Screen==MiningScreen.AmmoPress)return Trade is {} trade&&trade.Level(row)<trade.MaximumLevel&&op.Credits>=trade.Cost(row);if(Screen!=MiningScreen.Drill&&Screen!=MiningScreen.Refinery)return false;var kind=RowUpgrade(row);if(kind==MiningUpgrade.Refinery&&op.RefineryBatchSizeAt(op.RefineryLevel+1)<=op.RefineryBatchSizeAt(op.RefineryLevel))return false;return (Screen==MiningScreen.Refinery||row==0)&&!op.IsUpgradeMaxed(kind)&&op.Credits>=op.Cost(kind);}
 public void Purchase(int row)
 {if(Screen==MiningScreen.Market&&row==2){ToggleMarketBuying();return;}if(Screen==MiningScreen.Market&&row is 0 or 1){SelectMarketMethod(row==1);return;}if(!CanBuy(row)){MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.Error);Notice(Screen==MiningScreen.AmmoPress?(Trade?.Level(row)>=Trade?.MaximumLevel?"MAX LEVEL":$"Need {Trade?.Cost(row):N0} cr / camp funds {Op?.Credits??0:N0} cr"):Op?.UpgradeAvailability(RowUpgrade(row))??"Unavailable");return;}bool ok=Screen==MiningScreen.AmmoPress?Trade?.Buy(row)==true:Op?.Buy(RowUpgrade(row))==true;TransactionFeedback(ok,"UPGRADE PURCHASED","Purchase unavailable.");}
 void TransactionFeedback(bool ok,string success,string failure){MiningAudio3D.Find(GameObject)?.PlayCue(ok?(Screen==MiningScreen.Research?MiningSoundCue.TerminalPurchase:MiningSoundCue.Confirm):MiningSoundCue.Error);Notice(ok?success:failure);}
 void Notice(string text){Feedback=text;feedbackClock=3;}
 public void ShowFeedback(string text)=>Notice(text);
 public void Primary()
 {
  if(Op is not {} op)return;
  switch(Screen){case MiningScreen.Drill:if(op.StartOperation())Close();else TransactionFeedback(false,"",op.Message);break;case MiningScreen.AmmoPress:Notice($"COLLECTED {Trade?.Collect()??0} AMMO");break;case MiningScreen.Refinery:if(op.BeginRefining())Close();else TransactionFeedback(false,"",op.Message);break;case MiningScreen.Company:Open(MiningScreen.Research);break;case MiningScreen.Research:TransactionFeedback(Trade?.BuyResearch(SelectedResearchId)==true,"PERMANENT UPGRADE UNLOCKED","Check prerequisites and credits.");break;case MiningScreen.Market:StartSelectedMarketSale();break;}
 }
 public void Secondary(){if(Screen==MiningScreen.Refinery){Op?.ToggleAutoRefinery();return;}if(Screen==MiningScreen.Account)Open(MiningScreen.Research);else if(Screen==MiningScreen.Research)Open(MiningScreen.Market);else if(Screen==MiningScreen.Market)Open(MiningScreen.Research);}
 protected override void OnRender(RenderContext c)
 {
  if(Op is not {} op)return;float scale=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f);float width=c.TargetWidth/scale,height=c.TargetHeight/scale;
  bool externalMenu=GameObject.Scene?.GetComponentsSnapshot<MiningResearchStation3D>().Any(r=>r.IsOpen)==true||GameObject.Scene?.GetComponentsSnapshot<MiningAuxiliaryRefinery3D>().Any(r=>r.IsOpen)==true;
  if(externalMenu){RenderFieldStatus(c,width,height,scale,true);return;}
  if(CampEditing){RenderFieldStatus(c,width,height,scale,false);return;}
  if(Screen!=MiningScreen.None){RenderIndustrialBusiness(c,width,height,scale);RenderFieldStatus(c,width,height,scale,true);return;}
  if(DefencePlanner?.BuildMode!=true){op.DrawCampTutorial(c,BodyFont,true);DrawOperationAssist(c,op);}
  RenderFieldStatus(c,width,height,scale,false);
  RenderContextHud(c,width,height,scale);
 }
}
