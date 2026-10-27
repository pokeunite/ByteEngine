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
public enum MiningScreen{None,Drill,AmmoPress,Refinery,Company,Market,Research,AudioCredits}
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
 MiningCampLayout3D? CampLayout=>GameObject.Scene?.FindGameObject(Op?.PlayerObjectId??Guid.Empty)?.GetComponent<MiningCampLayout3D>();
 bool CampEditing=>CampLayout?.IsEditing==true;
 public bool NearTerminal{get{if(Op is not {} op||GameObject.Scene?.FindGameObject(TerminalObjectId) is not {} terminal||GameObject.Scene.FindGameObject(op.PlayerObjectId) is not {} player)return false;var d=player.Transform.WorldPosition-terminal.Transform.WorldPosition;return d.X*d.X+d.Z*d.Z<=InteractionDistance*InteractionDistance;}}
 public override int? RenderOrder=>900;
 MiningOperation3D? Op=>GameObject.Scene?.FindGameObject(OperationObjectId)?.GetComponent<MiningOperation3D>();
 MiningTrade3D? Trade=>GameObject.Scene?.FindGameObject(WorkshopObjectId)?.GetComponent<MiningTrade3D>();
 bool restoreMovement=true,restoreThirdPerson,restoreDash;float feedbackClock;bool shippingWasActive;
 UiWidget? W(Guid id)=>GameObject.Scene?.FindGameObject(id)?.GetComponent<UiWidget>();
 protected override void OnUpdate()
 {
  if(Op is not {} op)return;feedbackClock-= (float)Time.DeltaTime;if(feedbackClock<=0)Feedback="";
  if(shippingWasActive&&Trade?.SaleInProgress==false)Notice($"SHIPMENT COMPLETE: {Trade.LastSaleTotal} cr");shippingWasActive=Trade?.SaleInProgress==true;
  NearestScreen=MiningScreen.None;float distance=InteractionDistance;
  var player=GameObject.Scene?.FindGameObject(op.PlayerObjectId);
  foreach(var pair in new[]{(op.DrillObjectId,MiningScreen.Drill),(AmmoMachineObjectId,MiningScreen.AmmoPress),(RefineryMachineObjectId,MiningScreen.Refinery),(TerminalObjectId,MiningScreen.Company)})
   if(player!=null&&GameObject.Scene?.FindGameObject(pair.Item1) is {} machine){float d=pair.Item2==MiningScreen.Company?Vector3.Distance(player.Transform.WorldPosition,machine.Transform.WorldPosition):MiningCampGeometry.SurfaceDistance(machine,player.Transform.WorldPosition);if(d<distance){distance=d;NearestScreen=pair.Item2;}}
  if(Screen!=MiningScreen.None&&op.Phase==MiningPhase.Drilling)Close();
  if(Screen is MiningScreen.Company or MiningScreen.Market or MiningScreen.Research or MiningScreen.AudioCredits&&!NearTerminal)Close();
  bool modal=Screen!=MiningScreen.None;
  if(modal)Input.NotifyGameViewPointerAim();
  if(GameObject.Scene?.FindGameObject(ModalBlockerObjectId) is {} blocker)blocker.Active=modal;
  void Set(Guid id,bool visible,bool enabled=true){if(W(id) is {} w){w.Visible=visible;w.Interactable=enabled;}}
  bool ammo=Screen==MiningScreen.AmmoPress,market=Screen==MiningScreen.Market,refinery=Screen==MiningScreen.Refinery,research=Screen==MiningScreen.Research;
  Set(CloseButtonObjectId,modal&&!market);Set(CloseXButtonObjectId,modal);
  Set(Row0ButtonObjectId,ammo||refinery||Screen==MiningScreen.Drill,CanBuy(0));Set(Row1ButtonObjectId,ammo||refinery,CanBuy(1));Set(Row2ButtonObjectId,ammo||refinery,CanBuy(2));
  Set(PrimaryButtonObjectId,modal&&!ammo,market?Trade?.SaleInProgress!=true&&Trade?.Stock(RefinedSelected)>=Quantity&&Quantity>0:research?Trade?.CanBuyResearch(SelectedResearchId)==true:ammo?(Trade?.CollectibleAmmo??0)>0:refinery?op.RefiningAmount==0&&op.RawOre>0:Screen!=MiningScreen.Drill||op.RefiningAmount==0);
  for(int i=0;i<3;i++)Set(ContractButton(i),Screen==MiningScreen.Drill,op.PreviewContract(i)!=null);
  if(Screen==MiningScreen.Drill)Set(PrimaryButtonObjectId,true,op.SelectedContract is {} preview&&op.RefiningAmount==0&&(long)op.RawOre+op.RefinedOre+preview.Ore<=op.CargoCapacity);
  Set(CreditsButtonObjectId,research);if(Screen==MiningScreen.AudioCredits)Set(PrimaryButtonObjectId,false);
  Set(SecondaryButtonObjectId,market||research||Screen==MiningScreen.Company);
  Set(CloseButtonObjectId,modal);
  Set(MinusButtonObjectId,false);Set(PlusButtonObjectId,false);Set(RawButtonObjectId,market);Set(RefinedButtonObjectId,market);
  Set(QuantitySliderObjectId,market,(Trade?.Stock(RefinedSelected)??0)>0);
  if(market&&W(QuantitySliderObjectId) is {} slider){slider.Maximum=Math.Max(1,Trade?.Stock(RefinedSelected)??0);Quantity=Math.Clamp((int)MathF.Round(slider.Value),0,Trade?.Stock(RefinedSelected)??0);}
  for(int i=0;i<ResearchButtonObjectIds.Count;i++)Set(ResearchButtonObjectIds[i],research&&i<(Trade?.Research.Count??0));
  UpdateDefenceHud();UpdateMapInput();LayoutCompact();
 }
 protected override void OnStop(){Input.RequestUiPointer(defencePointerOwner,false);Close();}
 protected override void OnDestroy(){Input.RequestUiPointer(defencePointerOwner,false);Input.RequestUiPointer(this,false);}
 public void OpenNearby(){if(CampEditing)return;if(Screen!=MiningScreen.None){Close();return;}if(Op?.Phase==MiningPhase.Failed){Op.Retry();return;}Open(NearestScreen);}
 public void Open(MiningScreen screen)
 {
  if(CampEditing)return;
  if(screen==MiningScreen.Company)screen=MiningScreen.Research;
  if(screen==MiningScreen.None||Op?.Phase!=MiningPhase.Preparation)return;
  if(screen is MiningScreen.Company or MiningScreen.Market or MiningScreen.Research or MiningScreen.AudioCredits&&!NearTerminal){Notice("Approach the company tent to use its computer.");return;}
  if(Screen==MiningScreen.None&&GameObject.Scene?.FindGameObject(Op.PlayerObjectId) is {} player)
  {
   if(player.GetComponent<TopDownController3D>() is {} top){restoreMovement=top.InputEnabled;top.InputEnabled=false;}
   if(player.GetComponent<PlayerController3D>() is {} third){restoreThirdPerson=third.Enabled;third.Enabled=false;}
   if(player.GetComponent<DashAbility3D>() is {} dash){restoreDash=dash.Enabled;dash.Cancel();dash.Enabled=false;}
   player.GetComponent<CharacterController3D>()?.Move(Vector3.Zero);
  }
  Input.NotifyGameViewPointerAim();
  Input.RequestUiPointer(this,true);
  Screen=screen;MiningAudio3D.Find(GameObject)?.PlayCue(screen==MiningScreen.Refinery?MiningSoundCue.RefineryOpen:screen==MiningScreen.AmmoPress?MiningSoundCue.PressOpen:screen is MiningScreen.Research or MiningScreen.Market or MiningScreen.AudioCredits?MiningSoundCue.TerminalOpen:MiningSoundCue.Confirm);Feedback="";if(screen==MiningScreen.Market)SetQuantity(Math.Min(20,Trade?.Stock(RefinedSelected)??0));UiNavigation.Focus(null);
 }
 public void Close()
 {
  if(Screen!=MiningScreen.None&&Input.IsKeyPressed(Key.Escape))MiningPauseMenu3D.IgnoreEscapeAt=Time.TotalTime;
  if(Screen!=MiningScreen.None)MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.PanelClose);
  if(Screen!=MiningScreen.None&&Op is {} op&&GameObject.Scene?.FindGameObject(op.PlayerObjectId) is {} player)
  {if(player.GetComponent<TopDownController3D>() is {} top)top.InputEnabled=restoreMovement;if(player.GetComponent<PlayerController3D>() is {} third)third.Enabled=restoreThirdPerson;if(player.GetComponent<DashAbility3D>() is {} dash)dash.Enabled=restoreDash;}
  Screen=MiningScreen.None;UiNavigation.Focus(null);
  Input.RequestUiPointer(this,false);
 }
 public void SelectResource(bool refined){MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.TerminalSelect);RefinedSelected=refined;SetQuantity(Math.Min(20,Trade?.Stock(refined)??0));}
 public void SetQuantity(int quantity){Quantity=Math.Clamp(quantity,0,Trade?.Stock(RefinedSelected)??0);if(W(QuantitySliderObjectId) is {} slider){slider.Maximum=Math.Max(1,Trade?.Stock(RefinedSelected)??0);slider.Value=Quantity;}}
 public void ChangeQuantity(int change)=>SetQuantity(Quantity+change);
 public void ConfirmSelection(){if(Screen==MiningScreen.Research&&!ReferenceEquals(UiNavigation.Focused,W(PrimaryButtonObjectId)))Primary();}
 public void SelectResearch(int index){if(Trade is {} trade&&index>=0&&index<trade.Research.Count){if(SelectedResearchId!=trade.Research[index].Id)MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.TerminalSelect);SelectedResearchId=trade.Research[index].Id;}}
 MiningUpgrade RowUpgrade(int row)=>Screen==MiningScreen.Drill?MiningUpgrade.Drill:row==1?MiningUpgrade.RefinerySpeed:row==2?MiningUpgrade.RefineryYield:MiningUpgrade.Refinery;
 public bool CanBuy(int row)
 {if(Op is not {} op||op.Phase!=MiningPhase.Preparation||row<0||row>2)return false;if(Screen==MiningScreen.AmmoPress)return Trade is {} trade&&trade.Level(row)<trade.MaximumLevel&&op.Credits>=trade.Cost(row);if(Screen!=MiningScreen.Drill&&Screen!=MiningScreen.Refinery)return false;var kind=RowUpgrade(row);return (Screen==MiningScreen.Refinery||row==0)&&op.Level(kind)<op.MaximumUpgradeLevel&&op.Credits>=op.Cost(kind);}
 public void Purchase(int row)
 {if(!CanBuy(row)){MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.Error);Notice("Insufficient credits or maximum level.");return;}bool ok=Screen==MiningScreen.AmmoPress?Trade?.Buy(row)==true:Op?.Buy(RowUpgrade(row))==true;TransactionFeedback(ok,"UPGRADE PURCHASED","Purchase unavailable.");}
 void TransactionFeedback(bool ok,string success,string failure){MiningAudio3D.Find(GameObject)?.PlayCue(ok?(Screen==MiningScreen.Research?MiningSoundCue.TerminalPurchase:MiningSoundCue.Confirm):MiningSoundCue.Error);Notice(ok?success:failure);}
 void Notice(string text){Feedback=text;feedbackClock=3;}
 public void Primary()
 {
  if(Op is not {} op)return;
  switch(Screen){case MiningScreen.Drill:if(op.StartOperation())Close();else TransactionFeedback(false,"",op.Message);break;case MiningScreen.AmmoPress:Notice($"COLLECTED {Trade?.Collect()??0} AMMO");break;case MiningScreen.Refinery:if(op.BeginRefining())Close();else TransactionFeedback(false,"",op.Message);break;case MiningScreen.Company:Open(MiningScreen.Research);break;case MiningScreen.Research:TransactionFeedback(Trade?.BuyResearch(SelectedResearchId)==true,"PERMANENT UPGRADE UNLOCKED","Check prerequisites and credits.");break;case MiningScreen.Market:int q=Quantity;float price=Trade?.Quote(RefinedSelected)??0;if(Trade?.BeginSale(RefinedSelected,q)==true){MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.TerminalPurchase);Notice($"SHIPPING {q} FOR {MiningOperation3D.SaleTotal(q,price):N0} cr");SetQuantity(Math.Min(20,Trade.Stock(RefinedSelected)));}else TransactionFeedback(false,"","No available cargo selected.");break;}
 }
 public void Secondary(){if(Screen==MiningScreen.Research)Open(MiningScreen.Market);else if(Screen==MiningScreen.Market)Open(MiningScreen.Research);}
 protected override void OnRender(RenderContext c)
 {
  if(CampEditing||Op is not {} op)return;var trade=Trade;float scale=Math.Min(c.TargetWidth/1280f,c.TargetHeight/720f);float width=c.TargetWidth/scale,height=c.TargetHeight/scale;
  if(Screen!=MiningScreen.None){RenderIndustrialBusiness(c,width,height,scale);return;}
  var muted=new Vector4(.60f,.62f,.53f,1);var edge=new Vector4(.30f,.33f,.27f,1);var green=new Vector4(.57f,.73f,.45f,1);
  void Q(float x,float y,float w,float h,Vector4 color)=>c.DrawUiQuad(new(x*scale,y*scale),new(w*scale,h*scale),color);
  void L(float x,float y,float x2,float y2,Vector4 color,float thick=1)=>c.DrawUiLine(new(x*scale,y*scale),new(x2*scale,y2*scale),thick*scale,color);
  void T(string text,float x,float y,int size=24,Vector4? color=null,bool bold=false,UiAnchor anchor=UiAnchor.TopLeft)=>c.DrawUiText(text,bold?HeadingFont:BodyFont,Math.Max(8,(int)(Math.Max(18,size*1.16f)*scale)),new(x*scale,y*scale),color??TextColor,anchor);
  bool Image(AssetReference asset,float x,float y,float w,float h){if(asset.IsEmpty||!AnimationRuntimeAssets.TryGet(out var assets)||assets==null)return false;c.DrawUiImage(assets.LoadTexture(asset),new(x*scale,y*scale),new(w*scale,h*scale),Vector4.One);return true;}
  void Card(float x,float y,float w,float h,Vector4? color=null)
  {var fill=color??Surface;Q(x+8,y,w-16,h,fill);Q(x,y+8,8,h-16,fill);Q(x+w-8,y+8,8,h-16,fill);L(x+8,y,x+w-8,y,edge);L(x+w-8,y,x+w,y+8,edge);L(x+w,y+8,x+w,y+h-8,edge);L(x+w,y+h-8,x+w-8,y+h,edge);L(x+w-8,y+h,x+8,y+h,edge);L(x+8,y+h,x,y+h-8,edge);L(x,y+h-8,x,y+8,edge);L(x,y+8,x+8,y,edge);}
  void Bar(float x,float y,float w,float h,float fraction,Vector4 color){Q(x,y,w,h,new(.21f,.23f,.19f,1));Q(x,y,w*Math.Clamp(fraction,0,1),h,color);}
  void Icon(string icon,float x,float y,float size,Vector4 color)
  {
   float u=size/24;
   void R(float a,float b,float w,float h)=>Q(x+a*u,y+b*u,w*u,h*u,color);
   if(icon=="credit"){R(3,1,18,22);R(0,5,3,14);Q(x+8*u,y+7*u,9*u,10*u,Surface);R(12,10,7,4);}
   else if(icon=="health"){R(8,0,8,24);R(0,8,24,8);}
   else if(icon=="energy"){for(int i=0;i<12;i++){R(11-i*.5f,i,7,1);R(6+(12-i)*.5f,12+i,5,1);}}
   else if(icon=="chart"){R(0,15,5,9);R(7,9,5,15);R(14,1,5,23);}
   else if(icon=="box"){R(1,5,22,17);L(x,y+5*u,x+12*u,y,color);L(x+12*u,y,x+24*u,y+5*u,color);L(x+12*u,y+7*u,x+12*u,y+23*u,Surface,2);L(x+3*u,y+7*u,x+12*u,y+11*u,Surface,2);L(x+12*u,y+11*u,x+21*u,y+7*u,Surface,2);}
   else if(icon=="gun"){R(1,4,22,6);R(2,3,2,1);R(18,2,2,2);R(2,10,11,3);R(8,12,5,2);R(12,10,2,3);for(int n=0;n<9;n++)R(3-n*.25f,12+n,5,1);Q(x+3*u,y+5*u,14*u,u,Surface);Q(x+10*u,y+13*u,2*u,2*u,Surface);for(int n=0;n<3;n++)Q(x+(3+n*2)*u,y+7*u,u,2*u,Surface);}
   else if(icon=="ore"){for(int i=0;i<5;i++){float a=i*1.25f;R(8+MathF.Cos(a)*6,8+MathF.Sin(a)*6,8,8);}}
   else{R(7,1,10,22);R(1,7,22,10);R(4,4,16,16);Q(x+8*u,y+8*u,8*u,8*u,Surface);}
  }
  Card(20,20,210,48);Icon("credit",32,33,22,Accent);T($"{op.Credits:N0} cr",64,29,26,bold:true);
  Card(20,76,210,64);Icon("ore",32,89,22,Accent);T($"{op.RefinedOre} REFINED",64,82,22,bold:true);T($"{op.RawOre} RAW",64,111,18,muted);
  var cargoColor=op.CargoFull?new Vector4(.94f,.35f,.23f,1):((float)op.CargoUsed+op.CargoReservedSpace)/op.CargoCapacity>=.8f?Accent:green;
  Card(20,150,210,78);T($"CARGO {op.CargoUsed:N0} / {op.CargoCapacity:N0}",32,157,18,cargoColor,bold:true);
  Q(32,188,186,9,new(.18f,.22f,.16f,1));Q(32,188,186*op.CargoFillFraction,9,cargoColor);float reserved=Math.Min(1-op.CargoFillFraction,(float)op.CargoReservedSpace/op.CargoCapacity);if(reserved>0)Q(32+186*op.CargoFillFraction,188,186*reserved,9,Accent);
  T(op.CargoReservedSpace>0?$"{op.CargoReservedSpace} RESERVED":op.CargoFull?"HOLD FULL":$"{Math.Max(0,op.CargoCapacity-op.CargoUsed)} SPACE FREE",32,203,15,op.CargoFull?cargoColor:muted);
  float activityY=238;
  if(op.RefiningAmount>0){Card(20,activityY,280,52);T($"REFINING  {op.RefiningProgress:P0}",32,activityY+4,20,bold:true);Bar(32,activityY+34,256,7,op.RefiningProgress,green);activityY+=60;}
  if(trade?.SaleInProgress==true){Card(20,activityY,280,52);T($"SHIPPING  {trade.SaleProcessed}/{trade.SaleQuantity}",32,activityY+4,20,bold:true);Bar(32,activityY+34,256,7,trade.SaleProgress,Accent);}
  string wave=op.Phase==MiningPhase.Drilling?$"WAVE {op.CompletedRounds+1:00}  |  {Math.Max(0,op.EnemiesScheduled-op.EnemiesSpawned)+op.AliveEnemies} LEFT":op.Phase==MiningPhase.Failed?"OPERATION FAILED":$"OPERATION {op.CompletedRounds+1:00}  |  READY";
  Card(width/2-200,20,400,52);T(wave,width/2,29,27,bold:true,anchor:UiAnchor.TopCenter);
  Card(width-240,20,220,50);T("DRILL HEALTH",width-224,25,16,bold:true);Bar(width-224,52,188,8,op.DrillHealthFraction,green);
  var player=GameObject.Scene?.FindGameObject(op.PlayerObjectId);var hp=player?.GetComponent<HealthComponent>();var energy=player?.GetComponent<ResourceMeter3D>();
  float vitalsY=height-(ShowEnergy?100:66);
  Card(20,vitalsY,325,ShowEnergy?80:46);Icon("health",34,vitalsY+15,18,green);T("HP",62,vitalsY+12,18,bold:true);Bar(115,vitalsY+18,138,13,hp?.HealthPercent??0,green);T($"{hp?.CurrentHealth??0:0} / {hp?.MaxHealth??100:0}",266,vitalsY+11,21,bold:true);
  if(ShowEnergy){Icon("energy",34,height-54,18,Accent);T("ENERGY",62,height-57,18,bold:true);Bar(115,height-51,138,13,energy?.Fraction??0,Accent);T($"{energy?.Current??0:0} / {energy?.Capacity??100:0}",266,height-58,21,bold:true);}
  var weapon=player?.GetComponent<FpsGunLab3D>()?.SelectedWeapon;Card(width-285,height-100,265,80);if(weapon?.Definition is {} iconDef&&!Image(iconDef.HudIcon,width-274,height-85,86,43))Icon("gun",width-260,height-79,36,TextColor);T(weapon==null?"UNARMED":$"{weapon.CurrentAmmo}",width-172,height-94,48,bold:true);if(weapon!=null)T($"/ {weapon.ReserveAmmo}",width-100,height-77,30,bold:true);
  if(Screen==MiningScreen.None)
  {DrawDefenceHud(c,width,height,scale);if(Feedback.Length>0)T(Feedback,width/2,height-87,21,Accent,anchor:UiAnchor.TopCenter);string prompt=op.Phase==MiningPhase.Failed?$"[{MiningPauseMenu3D.KeyLabel(Key.E)}] Recover site":NearestScreen==MiningScreen.None?"":$"[{MiningPauseMenu3D.KeyLabel(Key.E)}] {NearestScreen switch{MiningScreen.AmmoPress=>"Ammo press",MiningScreen.Refinery=>"Refinery",MiningScreen.Drill=>"Drill",_=>"Company terminal"}}";if(prompt.Length>0)Card(width/2-180,height-65,360,49);T(prompt,width/2,height-57,23,anchor:UiAnchor.TopCenter);return;}
  RenderIndustrialBusiness(c,width,height,scale);
 }
}
