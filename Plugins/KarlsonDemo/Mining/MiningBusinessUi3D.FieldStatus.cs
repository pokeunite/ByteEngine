using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 public bool WaveCountdownVisible=>Op is {ExpeditionActive:true,Phase:MiningPhase.Preparation,TargetWon:false,TargetExpired:false};
 public bool ShipmentProgressVisible=>Op?.PendingMarketSale!=null;
 public string WaveCountdownText=>TimeText(Op?.PreparationRemaining??0);
 static string TimeText(float seconds){int n=(int)Math.Ceiling(Math.Max(0,seconds));return $"{n/60:00}:{n%60:00}";}
 void RenderFieldStatus(RenderContext c,float w,float h,float s,bool modal)
 {
  if(Op is not {} op||Screen==MiningScreen.Drill||IsCompanyScreen)return;
  if(CampEditing&&!modal&&op.ExpeditionActive){float x=w-284,y=16;bool urgent=WaveCountdownVisible&&op.PreparationRemaining<=5;var color=urgent?new Vector4(1,.57f,.08f,1):SupportUiArt.Gold;SupportUiArt.Panel(c,s,x,y,264,48);SupportUiArt.T(c,s,$"WAVE {Math.Min(op.WavesPerExpedition,op.ExpeditionWave+(WaveCountdownVisible?1:0))} / {op.WavesPerExpedition}",x+12,y+10,16,true);SupportUiArt.T(c,s,WaveCountdownVisible?WaveCountdownText:$"{op.AliveEnemies} active",x+252,y+10,20,true,color,UiAnchor.TopRight);if(WaveCountdownVisible){SupportUiArt.Q(c,s,x+12,y+39,240,3,new(.25f,.27f,.27f,1));SupportUiArt.Q(c,s,x+12,y+39,240*Math.Clamp(op.PreparationRemaining/Math.Max(1,op.PreparationSeconds),0,1),3,color);}}
  if(op.PendingMarketSale is {} sale){float x=20,y=h-198,width=Screen==MiningScreen.AudioCredits?230:modal?270:330;var split=op.PreviewSaleIncome(sale.Credits,sale.CompanyDepositPercent);SupportUiArt.Panel(c,s,x,y,width,82);SupportUiArt.T(c,s,sale.Guaranteed?"DELIVERY IN TRANSIT":"EXCHANGE IN TRANSIT",x+12,y+10,13,true);SupportUiArt.T(c,s,$"{(int)Math.Floor(sale.Quantity*Math.Clamp(sale.Age/sale.Seconds,0,1))} / {sale.Quantity} ore",x+12,y+32,17,true);SupportUiArt.T(c,s,TimeText(sale.Seconds-sale.Age),x+width-12,y+32,17,true,SupportUiArt.Gold,UiAnchor.TopRight);SupportUiArt.Q(c,s,x+12,y+59,width-24,4,new(.25f,.27f,.27f,1));SupportUiArt.Q(c,s,x+12,y+59,(width-24)*Math.Clamp(sale.Age/sale.Seconds,0,1),4,SupportUiArt.Gold);SupportUiArt.T(c,s,$"+{split.Camp:N0} camp credits on arrival",x+12,y+66,12,false,SupportUiArt.Muted);}
 }
}
