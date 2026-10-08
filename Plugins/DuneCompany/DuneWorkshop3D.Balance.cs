using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Scene;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 UiText? _balanceText;
 void LoadCampaignBalance(){try{var file=Path.Combine(SaveRoot,"recovery-payment.json");_credits=File.Exists(file)?Math.Max(0,JsonSerializer.Deserialize<int>(File.ReadAllText(file))):0;}catch(Exception error)when(error is IOException or JsonException){_credits=0;}}
 void EnsureBalanceDisplay()
 {
  var scene=GameObject.Scene!;var obj=scene.FindGameObject("Company balance");
  if(obj==null){obj=scene.CreateGameObject("Company balance");obj.SetParent(scene.GameObjects.First(o=>o.GetComponent<UiCanvas>()!=null),false);obj.AddComponent(new UiText{Text="BALANCE  $0",Offset=new(665,15),FontSize=20,FontReference=new AssetReference("Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf"),Color=new(.96f,.68f,.30f,1),ShadowColor=new(0,0,0,.8f),OrderInLayer=25});}
  _balanceText=obj.GetComponent<UiText>();RefreshBalanceDisplay();
 }
 void RefreshBalanceDisplay(){if(_balanceText!=null)_balanceText.Text=$"BALANCE  ${_credits:N0}";}
}
