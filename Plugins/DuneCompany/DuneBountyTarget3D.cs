using ByteEngine.Core.Scene;
namespace DuneCompany;
public sealed class DuneBountyTarget3D:Component
{
 public float MaximumHealth{get;set;}=100;public float Radius{get;set;}=1.3f;public float Health{get;private set;}=100;public bool Defeated=>Health<=0;
 protected override void OnStart(){Health=MaximumHealth;}
 public void Damage(float value){if(Defeated||!float.IsFinite(value))return;Health=Math.Max(0,Health-Math.Max(0,value));if(Defeated)foreach(var c in GameObject.Children)c.Active=false;}
 public void ResetTarget(){Health=MaximumHealth;foreach(var c in GameObject.Children)c.Active=true;}
}
