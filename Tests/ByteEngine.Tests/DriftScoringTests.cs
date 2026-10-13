using System.Numerics;
using ByteEngine.Core.Gameplay;
namespace ByteEngine.Tests;
internal static class DriftScoringTests
{
 public static void Run(){
  var score=new DriftScore3D();void Feed(Vector3 v,bool ground=true,bool collision=false,int frames=180){for(int i=0;i<frames;i++)score.Sample(v,Quaternion.Identity,ground,collision,1f/60);}
  Feed(new(0,0,-12));Feed(new(0,0,12));Feed(new(10,0,-1));Feed(new(7,0,-10),false);if(score.ChainScore!=0||score.BankedScore!=0)throw new Exception("Straight, reverse, spins or airborne motion farms drift score");
  Feed(new(7,0,-10));if(score.ChainScore<1000||score.Multiplier<2||!score.Drifting)throw new Exception("A real grounded slide does not grow a chain and multiplier");
  float points=score.ChainScore;Feed(new(0,0,-12),frames:60);if(score.BankedScore!=points||score.ChainScore!=0||score.Multiplier!=1)throw new Exception("Clean exit does not bank chain");
  Feed(new(7,0,-10));Feed(new(7,0,-10),collision:true,frames:1);if(score.ChainScore!=0||score.BankedScore!=points)throw new Exception("Collision must discard the pending chain, preserving banked points");
  score.Sample(new(float.NaN),Quaternion.Identity,true,false,.1f);if(!float.IsFinite(score.BankedScore))throw new Exception("Nonfinite sample corrupts score");score.Reset();if(score.BankedScore!=0||score.Fill!=0)throw new Exception("Retry doesn't reset scoring");
  Console.WriteLine("PASS Grounded drift scoring, multiplier, clean banking, collision loss and anti-farming checks");
 }
}
