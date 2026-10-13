using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using KarlsonDemo.Plugin;
namespace KarlsonDemoTool;
internal static class SocketCheck
{
    internal static void Run(Scene scene)
    {
        var checks=new List<string>();
        void Check(bool ok,string message){if(!ok)throw new Exception(message);checks.Add(message);Console.WriteLine("PASS "+message);}
        foreach(var name in Program.Names)
        {
            var obj=scene.GameObjects.Single(o=>o.Name==name.ToUpperInvariant()+" view model");
            var renderer=obj.GetComponent<SkeletalMeshRenderer>()!;var weapon=obj.GetComponent<WeaponInstance3D>()!;var definition=weapon.Definition!;
            Check(renderer.TryGetBoneModelMatrix("Frame",out var frame)&&renderer.TryGetBoneModelMatrix("MuzzleSocket",out _),"Source pose available: "+name);
            renderer.TryGetBoneModelMatrix("MuzzleSocket",out var muzzle);
            Matrix4x4.Decompose(frame,out _,out var rotation,out _);
            var authored=new SkeletalSocketDefinition{Name="__WeaponSocketFixture",BoneName="Frame",PositionOffset=Vector3.Transform(muzzle.Translation-frame.Translation,Quaternion.Inverse(rotation))};
            var model=renderer.ResolvedModel!;var original=model.Sockets.Select(s=>s.Clone()).ToList();model.ReplaceSockets([..original,authored]);
            Vector3 expected=Vector3.Transform(muzzle.Translation,obj.Transform.WorldMatrix);
            definition.MuzzleSource=WeaponMuzzleSource.AnimationSocket;definition.MuzzleSocket=authored.Name;definition.MuzzleBone="NoSuchMuzzleBone";
            Check(weapon.TryGetMuzzleWorldPosition(out var position)&&Vector3.Distance(position,expected)<.0001f,"Authored socket works without matching muzzle bone: "+name);
            definition.MuzzleSource=WeaponMuzzleSource.Automatic;
            Check(weapon.TryGetMuzzleWorldPosition(out position)&&Vector3.Distance(position,expected)<.0001f,"Automatic source prefers authored socket: "+name);
            var home=obj.Transform.LocalPosition;obj.Transform.LocalPosition+=new Vector3(.4f,.2f,.1f);
            Check(weapon.TryGetMuzzleWorldPosition(out var moved)&&Vector3.Distance(moved,position)>.4f,"Socket follows transformed weapon: "+name);obj.Transform.LocalPosition=home;
            definition.MuzzleSource=WeaponMuzzleSource.ModelBone;Check(!weapon.TryGetMuzzleWorldPosition(out _),"Explicit missing bone is reported as unresolved: "+name);
            definition.MuzzleSource=WeaponMuzzleSource.LocalOffset;definition.MuzzleLocalOffset=new(.1f,.2f,.3f);
            Check(weapon.TryGetMuzzleWorldPosition(out position)&&Vector3.Distance(position,Vector3.Transform(definition.MuzzleLocalOffset,obj.Transform.WorldMatrix))<.0001f,"Local offset supports models without a muzzle bone: "+name);
            definition.MuzzleSource=WeaponMuzzleSource.Automatic;definition.MuzzleSocket="WeaponMuzzle";definition.MuzzleBone="MuzzleSocket";definition.MuzzleLocalOffset=Vector3.Zero;
            if(!original.Any(s=>s.Name.Equals("WeaponMuzzle",StringComparison.OrdinalIgnoreCase))) { var saved=authored.Clone(false);saved.Name="WeaponMuzzle";original.Add(saved); }
            ModelSocketMetadataStore.Save(Program.Root,model.Guid,original);model.ReplaceSockets(original);
        }
        File.WriteAllText(Program.Root+"/Preview/socket-validation.json",JsonSerializer.Serialize(checks,new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("SOCKET PASS "+checks.Count);
    }
}
