using System.ComponentModel;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningTrade3D
{
 [Category("Market / demand")]public float DemandPeriodSeconds{get;set;}=90;
 [Browsable(false)]public float DemandClock{get;private set;}
 int DemandIndex=>(int)(DemandClock/Math.Max(1,DemandPeriodSeconds))%3;
 float DemandTarget=>DemandIndex switch{1=>1.2f,2=>.8f,_=>1};
 public string DemandDescription=>DemandIndex switch{1=>"Strong demand",2=>"Quiet demand",_=>"Normal demand"};
 public string NextDemandDescription=>((DemandIndex+1)%3) switch{1=>"Strong demand",2=>"Quiet demand",_=>"Normal demand"};
 public float DemandSecondsRemaining=>Math.Max(0,Math.Max(1,DemandPeriodSeconds)-DemandClock%Math.Max(1,DemandPeriodSeconds));
 public bool HasDemandScanner=>Stat("DemandScanner")>0;
}
