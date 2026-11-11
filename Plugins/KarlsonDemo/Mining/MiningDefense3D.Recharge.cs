using System.ComponentModel;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningDefense3D
{
 [Category("Mine recharge")] public bool RechargeableMine{get;set;}=true;
 [Category("Live state")] public float DisabledSeconds{get;private set;}
 public bool NeedsRecharge=>Kind==MiningDefenseKind.Mine&&RechargeableMine&&Spent&&!DestroyedByEnemy;
 public void DisableFor(float seconds){if(float.IsFinite(seconds)&&seconds>0)DisabledSeconds=Math.Max(DisabledSeconds,Math.Clamp(seconds,0,15));}
 public void RestoreDepleted(){if(Kind==MiningDefenseKind.Mine){Spent=true;mineFuse=-1;}}
 void DrawReadiness(ByteEngine.Core.Graphics.RenderContext c){if(ByteEngine.Core.Input.IsUiPointerRequested)return;if((!NeedsRecharge&&DisabledSeconds<=0)||!c.Has3DCamera||GameObject.Scene?.GetComponentsSnapshot<MiningBusinessUi3D>().Any(u=>u.Screen!=MiningScreen.None)==true)return;var p=System.Numerics.Vector4.Transform(new System.Numerics.Vector4(Transform.WorldPosition+System.Numerics.Vector3.UnitY*.65f,1),c.GetViewMatrix3D()*c.GetProjectionMatrix3D());if(p.W<=0)return;float x=(p.X/p.W+1)*c.TargetWidth/2,y=(1-p.Y/p.W)*c.TargetHeight/2;c.DrawUiQuad(new(x-10,y),new(20,5),NeedsRecharge?new(.45f,.45f,.45f,1):new(.05f,.8f,1,1));}
 public bool Recharge(){if(!NeedsRecharge||Op is not {} op||!op.CanUseIndustry)return false;var planner=op.DefencePlanner;if(!op.SpendRefinedOre(planner?.MineReplacementOreCost??10))return false;Spent=false;mineFuse=-1;PlayVisual(ArmClip);planner?.Save();return true;}
}
public sealed partial class MiningDefensePlanner3D
{
 public int DepletedMineCount=>LiveDefences.Count(d=>d.NeedsRecharge);
 public int RechargeAllMines(){int count=0;foreach(var mine in LiveDefences)if(mine.NeedsRecharge&&mine.Recharge())count++;Status=count>0?$"Recharged {count} mines in place.":"No depleted mines or insufficient refined ore.";return count;}
}
