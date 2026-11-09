using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Characters;
using ByteEngine.Core.Gameplay;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningAuxiliaryRefinery3D
{
 public int BaseBatchSize{get;set;}=3;
 public float BaseBatchSeconds{get;set;}=3;
 public float MinimumBatchSeconds{get;set;}=.5f;
 public int BatchUpgradeCost{get;set;}=104;
 public int SpeedUpgradeCost{get;set;}=128;
 public int YieldUpgradeCost{get;set;}=192;
 [Browsable(false)]public int BatchLevel{get;private set;}
 [Browsable(false)]public int SpeedLevel{get;private set;}
 [Browsable(false)]public int YieldLevel{get;private set;}
 [Browsable(false)]public bool IsOpen{get;private set;}
 public int BatchSize=>Math.Clamp(BaseBatchSize+BatchLevel*2,1,Math.Clamp(LoadCapacity,1,50));
 public float BatchDuration=>Math.Max(MinimumBatchSeconds,BaseBatchSeconds-SpeedLevel*.25f);
 public float YieldMultiplier=>1+YieldLevel*.1f;
 public override int? RenderOrder=>952;
 public override int UpdateOrder=>-1300;
 public float InteractionDistance{get;set;}=2.8f;
 public int Cost(int row)=>(int)Math.Clamp(Math.Ceiling((row==0?BatchUpgradeCost:row==1?SpeedUpgradeCost:YieldUpgradeCost)*Math.Pow(1.85,row==0?BatchLevel:row==1?SpeedLevel:YieldLevel)),1,1000000000);
 public bool BuyUpgrade(int row){if(row<0||row>2||Op is not {} op||op.Phase!=MiningPhase.Preparation||(row==0?BatchLevel:row==1?SpeedLevel:YieldLevel)>=5||!op.SpendCredits(Cost(row)))return false;if(row==0)BatchLevel++;else if(row==1)SpeedLevel++;else YieldLevel++;op.GameObject.GetComponent<MiningResearchStation3D>()?.PersistSupport();return true;}
 public void RestoreSettings(int batchLevel,int speedLevel,int yieldLevel,float remaining,double fraction){BatchLevel=Math.Clamp(batchLevel,0,5);SpeedLevel=Math.Clamp(speedLevel,0,5);YieldLevel=Math.Clamp(yieldLevel,0,5);remainder=double.IsFinite(fraction)?Math.Clamp(fraction,0,.999999):0;if(LoadedRaw>0&&Op is {} op){StartBatch(op);if(float.IsFinite(remaining)&&remaining>0)Remaining=Math.Min(total,remaining);}}
 bool restoreControl;
 public void Open(){if(IsOpen)return;if(Op?.Phase!=MiningPhase.Preparation)return;IsOpen=true;if(GameObject.Scene?.FindGameObject(Op.PlayerObjectId)?.GetComponent<PlayerController3D>() is {} control){restoreControl=control.Enabled;control.Enabled=false;}Input.ConsumeGameplayPointer();Input.RequestUiPointer(this,true);}
 public void Close(){if(!IsOpen)return;if(GameObject.Scene?.FindGameObject(Op?.PlayerObjectId??Guid.Empty)?.GetComponent<PlayerController3D>() is {} control)control.Enabled=restoreControl;IsOpen=false;Input.ConsumeGameplayPointer();Input.RequestUiPointer(this,false);}
 public float SavedRemaining=>Remaining;
 public double SavedRemainder=>remainder;
 void TickMenuInput(){if(Input.IsKeyPressed(Key.Escape)){MiningPauseMenu3D.IgnoreEscapeAt=Time.TotalTime;Close();return;}if(!Input.IsMouseButtonPressedForUi(MouseButton.Left))return;float s=Math.Max(.01f,Math.Min(Input.GameViewSize.X/1280,Input.GameViewSize.Y/720));var p=Input.GameViewMousePosition/s-new Vector2(Input.GameViewSize.X/s-548,(Input.GameViewSize.Y/s-640)/2);if((p.X>=468&&p.X<=504&&p.Y>=17&&p.Y<=53)||(p.X>=388&&p.X<=492&&p.Y>=592&&p.Y<=640)){Close();return;}for(int i=0;i<3;i++)if(p.X>=384&&p.X<=492&&p.Y>=312+i*74&&p.Y<=354+i*74){BuyUpgrade(i);return;}if(p.X>=28&&p.X<=492&&p.Y>=552&&p.Y<=596&&BeginLoad())Close();}
 void DrawMenu(RenderContext c)=>GameObject.Scene?.GetComponentsSnapshot<MiningBusinessUi3D>().FirstOrDefault()?.RenderIndependentRefinery(c,this);
}
