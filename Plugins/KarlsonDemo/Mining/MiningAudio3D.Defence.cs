using System.ComponentModel;
using ByteEngine.Core.Assets;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningAudio3D
{
 [Category("Defence sounds")] public AssetReference TurretShot{get;set;}=AssetReference.Empty;
 [Category("Defence sounds")] public AssetReference RepeaterShot{get;set;}=AssetReference.Empty;
 [Category("Defence sounds")] public float DefenceVolume{get;set;}=.95f;
 [Category("Defence sounds")] public float ExplosionVolume{get;set;}=1;
 [Category("Defence sounds")] public float MortarLaunchVolume{get;set;}=.9f;
 [Category("Defence sounds")] public float DefenceHearingDistance{get;set;}=12;
 [Category("Company terminal")] public AssetReference TerminalOpen{get;set;}=AssetReference.Empty;
 [Category("Company terminal")] public AssetReference TerminalSelect{get;set;}=AssetReference.Empty;
 [Category("Company terminal")] public AssetReference TerminalPurchase{get;set;}=AssetReference.Empty;
}
