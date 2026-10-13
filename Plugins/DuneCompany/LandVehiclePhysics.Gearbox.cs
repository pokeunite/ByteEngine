namespace DuneCompany;
public sealed partial class LandVehiclePhysics
{
 public ManualTransmission? Transmission=>_playerRig.Gearboxes.Length>0?_playerRig.Transmission:null;
 public bool ShiftGear(int direction)=>Transmission?.Shift(direction,new System.Numerics.Vector2(Velocity.X,Velocity.Z).Length(),ConfiguredMaximumSpeed,FinalDriveRatio(_playerRig.Gearboxes))==true;
 // Average multiple gearboxes instead of multiplying bonuses. Legacy rigs stay 1:1.
 public static float FinalDriveRatio(IReadOnlyList<PlacedBlock> gearboxes)
 {
  float sum=0;for(int i=0;i<gearboxes.Count;i++)sum+=Math.Clamp(gearboxes[i].Power,.5f,2f);
  return gearboxes.Count==0?1:sum/gearboxes.Count;
 }
}
