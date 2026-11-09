using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Audio;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 bool accountDragging;
 void TickCompanyAccountInput(MiningOperation3D op)
 {
  if(Screen is not (MiningScreen.Research or MiningScreen.Market or MiningScreen.Account)){accountDragging=false;return;}
  var size=Input.GameViewSize;float scale=Math.Max(.01f,Math.Min(size.X/1280,size.Y/720));var b=CompactBounds(size.X/scale,size.Y/scale);var mouse=Input.GameViewMousePosition/scale-new Vector2(b.X,b.Y);
  bool pressed=Input.IsMouseButtonPressedForUi(MouseButton.Left),down=Input.IsMouseButtonDownForUi(MouseButton.Left);
  if(pressed&&mouse.Y>=80&&mouse.Y<=116){
   if(mouse.X>=372&&mouse.X<=532)Open(MiningScreen.Account);
   else if(Screen==MiningScreen.Account&&mouse.X>=28&&mouse.X<=190)Open(MiningScreen.Research);
   else if(Screen==MiningScreen.Account&&mouse.X>=200&&mouse.X<=362)Open(MiningScreen.Market);
  }
  if(Screen!=MiningScreen.Account)return;
  if(pressed&&mouse.X>=28&&mouse.X<=532&&mouse.Y>=455&&mouse.Y<=505){bool ok=op.UpgradeCargo();Notice(ok?$"CARGO EXPANDED TO {op.CargoCapacity}":"Not enough camp credits for cargo expansion.");}
  if(pressed&&mouse.X>=28&&mouse.X<=532&&mouse.Y>=288&&mouse.Y<=330)accountDragging=true;
  if(accountDragging&&down)op.SetCompanyDepositPercent((int)MathF.Round(Math.Clamp((mouse.X-40)/480,0,1)*20)*5,false);
  if(accountDragging&&!down){accountDragging=false;op.SaveProgress();MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.Confirm);}
  if(Input.IsKeyPressed(Key.Left))op.SetCompanyDepositPercent(op.CompanyDepositPercent-5);
  if(Input.IsKeyPressed(Key.Right))op.SetCompanyDepositPercent(op.CompanyDepositPercent+5);
 }
 void DrawCompanyAccount(BusinessPainter p,MiningOperation3D op)
 {
  DrawTerminalTabs(p,false,560);
  p.T("COMPANY ACCOUNT",28,151,23,Accent);
  p.T($"{op.CompanyAccountCredits:N0} / {op.TargetCredits:N0} cr",28,188,29);
  p.Bar(28,230,504,10,op.TargetProgress);
  p.T($"DEPOSIT {op.CompanyDepositPercent}% OF ORE SALES",28,264,19);
  float fraction=op.CompanyDepositPercent/100f;p.Bar(40,306,480,6,fraction);p.Q(40+480*fraction-7,296,14,26,Accent);
  p.T("CAMP",28,338,17,MenuMuted);p.T("COMPANY",532,338,17,MenuMuted,UiAnchor.TopRight);
  int example=1000,deposit=example*op.CompanyDepositPercent/100;
  p.T("EXAMPLE: 1,000 cr SHIPMENT",28,376,18);
  p.T($"{example-deposit:N0} cr to spend",28,412,21);p.T($"{deposit:N0} cr secured",532,412,21,Accent,UiAnchor.TopRight);
  p.Q(28,455,504,50,MenuInset);p.T($"CARGO {op.CargoCapacity} > {Math.Min(1000000,op.CargoCapacity+op.CargoCapacityPerUpgrade)}",40,463,18);p.T($"UPGRADE {op.CargoUpgradeCost} cr",520,466,17,Accent,UiAnchor.TopRight);
  p.T("Cargo upgrades use camp credits. Company deposits stay protected.",28,518,14,MenuMuted);
 }
}
