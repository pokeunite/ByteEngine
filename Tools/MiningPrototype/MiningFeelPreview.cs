using System.Numerics;
using ByteEngine.Core.Gameplay;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
internal static class MiningFeelPreview
{
 public static void Apply(Scene scene,MiningOperation3D op)
 {
  void Set(object o,string p,object v)=>o.GetType().GetProperty(p)!.SetValue(o,v);
  var feel=op.GameObject.GetComponent<MiningGameFeel3D>()!;
  var bucket=scene.FindGameObject(feel.AmmoBucketObjectId)!.GetComponent<MiningAmmoBucket3D>()!;Set(bucket,"LastCollected",9);Set(bucket,"CollectionSequence",bucket.CollectionSequence+1);
  op.RefundResearchCredits(90);Set(op,"CompletedRounds",op.CompletedRounds+1);scene.FindGameObject(op.PlayerObjectId)!.GetComponent<HealthComponent>()!.Damage(12,Vector3.UnitX);feel.Tick(.01f);feel.ConfirmDamage(new(Guid.NewGuid(),Vector3.Zero,12,true,true));feel.AddCameraImpulse(.4f);
 }
}
