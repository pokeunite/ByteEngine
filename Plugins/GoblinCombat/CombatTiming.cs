namespace GoblinCombat;

/// <summary>One buffered command, timed contact windows and explicit recovery.
/// Pure timing state is independent of rendering and can be checked without a window.</summary>
public sealed class CombatTiming
{
 public static readonly float[] Windup=[.13f,.12f,.30f],Contact=[.12f,.12f,.16f],Recovery=[.27f,.24f,.32f],ChainOpen=[.52f,.48f,.78f];
 public float BufferSeconds {get;set;}= .26f;
 public float Stamina {get;private set;}=100;
 public int Stage {get;private set;}=-1;
 public float Elapsed {get;private set;}
 public float DodgeElapsed {get;private set;}=-1;
 public int Swing {get;private set;}
 public int ContactStageThisTick {get;private set;}=-1;
 public int ContactSwingThisTick {get;private set;}=-1;
 public bool NextStrikeQueued=>queued>0;
 public bool Attacking=>Stage>=0;
 public bool Dodging=>DodgeElapsed>=0;
 public bool Invincible=>Dodging&&DodgeElapsed>=.04f&&DodgeElapsed<.22f;
 public bool Active=>Attacking&&Elapsed>=Windup[Stage]&&Elapsed<Windup[Stage]+Contact[Stage];
 public bool CanDodge=>!Dodging&&(!Attacking||Elapsed<.07f||Elapsed>=Windup[Stage]+Contact[Stage]+.09f);
 public string State=>Dodging?"DODGE":!Attacking?"READY":Elapsed<Windup[Stage]?"WINDUP":Active?"STRIKE":"RECOVERY";
 float queued,regenDelay;bool freshCommand;
 public bool PressAttack(){queued=Attacking&&Stage<2?Math.Max(BufferSeconds,ChainOpen[Stage]-Elapsed+.05f):BufferSeconds;freshCommand=true;if(!Attacking&&!Dodging)return Start(0);return false;}
 bool Start(int stage){if(Stamina<12)return false;Stage=stage;Elapsed=0;Swing++;Stamina-=12;regenDelay=.55f;queued=0;return true;}
 public bool PressDodge(){if(!CanDodge||Stamina<25)return false;Stage=-1;Elapsed=0;DodgeElapsed=0;Stamina-=25;regenDelay=.7f;queued=0;return true;}
 public void Interrupt(){Stage=-1;Elapsed=0;DodgeElapsed=-1;queued=0;}
 public void Tick(float dt){ContactStageThisTick=-1;ContactSwingThisTick=-1;dt=Math.Max(0,dt);if(!freshCommand)queued=Math.Max(0,queued-dt);freshCommand=false;regenDelay=Math.Max(0,regenDelay-dt);if(regenDelay<=0)Stamina=Math.Min(100,Stamina+30*dt);
  if(Dodging){DodgeElapsed+=dt;if(DodgeElapsed>=.34f){DodgeElapsed=-1;if(queued>0)Start(0);}return;}
  if(!Attacking){if(queued>0)Start(0);return;}
  float previous=Elapsed;Elapsed+=dt;if(previous<Windup[Stage]+Contact[Stage]&&Elapsed>=Windup[Stage]){ContactStageThisTick=Stage;ContactSwingThisTick=Swing;}
  if(Stage<2&&Elapsed>=ChainOpen[Stage]&&queued>0){Start(Stage+1);return;}
  if(Elapsed>=Windup[Stage]+Contact[Stage]+Recovery[Stage]){Stage=-1;if(queued>0)Start(0);}
 }
}





