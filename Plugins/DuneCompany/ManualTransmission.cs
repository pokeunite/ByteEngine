namespace DuneCompany;
/// <summary>Speed-coupled manual gearbox; timing consequences emerge from engine torque and gear ratios.</summary>
public sealed class ManualTransmission
{
 static readonly float[] Ratios=[3.2f,2.15f,1.55f,1.18f,1,.78f];
 public int GearCount=>Ratios.Length;
 public bool TopGear=>Gear==GearCount;
 public int Gear{get;private set;}=1;
 public float Rpm{get;private set;}=900;
 public float ShiftRemaining{get;private set;}
 public int ShiftDirection{get;private set;}
 public string LastShift{get;private set;}="";
 public bool Limiter=>Rpm>=7000;
 public bool ShiftReady=>!TopGear&&Rpm>=5900&&Rpm<6800&&ShiftRemaining<=0;
 public float Ratio=>Ratios[Gear-1];
 public static float TorqueBand(float rpm)=>rpm<2200?.38f+(Math.Clamp(rpm,900,2200)-900)/1300*.24f:rpm<4800?.62f+(rpm-2200)/2600*.38f:rpm<6200?1:rpm<7000?1-(rpm-6200)/800*.35f:0;
 public float GearSpeed(float topSpeed,float finalDrive)=>Math.Max(1,topSpeed)/(Ratio*finalDrive);
 public void Update(float dt,float speed,float topSpeed,float finalDrive)
 {
  ShiftRemaining=Math.Max(0,ShiftRemaining-Math.Clamp(dt,0,.1f));
  float target=900+Math.Abs(speed)/GearSpeed(topSpeed,finalDrive)*6100;
  Rpm+=(Math.Clamp(target,900,7400)-Rpm)*Math.Clamp(dt*24,0,1);
 }
 public bool Shift(int direction,float speed,float topSpeed,float finalDrive)
 {
  if(ShiftRemaining>0||direction is not (1 or -1))return false;int next=Gear+direction;if(next<1||next>GearCount)return false;
  float nextRpm=900+Math.Abs(speed)/(Math.Max(1,topSpeed)/(Ratios[next-1]*finalDrive))*6100;
  if(direction<0&&nextRpm>7000){LastShift="DOWNSHIFT BLOCKED / TOO FAST";return false;}
  LastShift=direction<0?"DOWNSHIFT":Rpm<5900?"EARLY SHIFT / LOW RPM":Rpm>=6800?"LATE SHIFT / REV LIMIT":"GOOD SHIFT";
  Gear=next;ShiftDirection=direction;ShiftRemaining=.22f;return true;
 }
 public float DriveFactor=>ShiftRemaining>0?0:TorqueBand(Rpm);
}
