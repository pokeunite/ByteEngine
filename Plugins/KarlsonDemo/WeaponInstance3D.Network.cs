using System.Numerics;
namespace KarlsonDemo.Plugin;
public sealed partial class WeaponInstance3D
{
 public void PresentNetworkShot(Vector3 direction){ShotsFired++;FiredThisFrame=true;Flash(TryGetMuzzleWorldPosition(out var p)?p:Transform.WorldPosition,direction);Fired?.Invoke(this);}
 public void PresentNetworkReload(bool active){if(active&&!Reloading){_reloadDuration=_reloadRemaining=Definition?.ReloadSeconds??1;ReloadStartedThisFrame=true;ReloadStartSequence++;}else if(!active&&Reloading){_reloadRemaining=0;ReloadCompletedThisFrame=true;ReloadCompleteSequence++;}}
}
