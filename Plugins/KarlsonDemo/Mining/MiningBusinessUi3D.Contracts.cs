using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Graphics;
namespace KarlsonDemo.Plugin;
public sealed partial class MiningBusinessUi3D
{
 [ByteEngine.Core.Scene.SceneObjectReference] public Guid Contract0ButtonObjectId{get;set;}
 [ByteEngine.Core.Scene.SceneObjectReference] public Guid Contract1ButtonObjectId{get;set;}
 [ByteEngine.Core.Scene.SceneObjectReference] public Guid Contract2ButtonObjectId{get;set;}
 Guid ContractButton(int i)=>i==0?Contract0ButtonObjectId:i==1?Contract1ButtonObjectId:Contract2ButtonObjectId;
 public void SelectContract(int index){if(Screen==MiningScreen.Market&&GuaranteedSelected){if(Trade is {} t&&index>=0&&index<t.DeliveryOrders.Count){selectedDelivery=index;MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.TerminalSelect);}return;}if(Screen==MiningScreen.Drill&&Op?.SelectContract(index)==true){MiningAudio3D.Find(GameObject)?.PlayCue(MiningSoundCue.Confirm);Feedback="";}}
}
