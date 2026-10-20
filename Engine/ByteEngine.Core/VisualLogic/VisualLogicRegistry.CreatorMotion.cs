using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Variables;

namespace ByteEngine.Core.VisualLogic;
public sealed partial class VisualLogicRegistry
{
    private static void RegisterCreatorMotion(VisualLogicRegistry r)
    {
        var target=ObjectArg();var name=TextArg("name","Tween name","Move");var seconds=NumberArg("seconds","Duration seconds",1);
        var easing=TextArg("easing","Easing (Linear, Smoothstep, EaseIn, EaseOut, EaseInOut)","Smoothstep");
        var end=new VisualArgumentDefinition("end","Destination",VariableValue.FromVector3(default));
        Add(r,"tween.opacity","Tween / Appearance","Tween renderer opacity",(i,c)=>
        {
            var obj=ResolveObjectTarget(i,c);if(obj==null)return;
            var sprite=obj.GetComponent<ByteEngine.Core.Graphics.SpriteRenderer>();var mesh=obj.GetComponent<ByteEngine.Core.Graphics.ThreeD.MeshRenderer>();var skin=obj.GetComponent<ByteEngine.Core.Graphics.ThreeD.SkeletalMeshRenderer>();
            int slot=Math.Clamp((int)N(i,c,"slot"),0,4095);var state=skin?.GetMaterialOverrides(slot);float start=sprite?.Tint.W??mesh?.MaterialOverrides.Opacity??mesh?.Material.BaseColor.W??state?.Opacity??1;
            double finish=N(i,c,"end",1);if(!double.IsFinite(finish))throw new ArgumentOutOfRangeException("end");finish=Math.Clamp(finish,0,1);
            CreatorSceneServices.For(c.Scene).StartTween(Key(i,c),N(i,c,"seconds",1),S(i,c,"easing","Smoothstep"),t=>{if(c.Scene.FindGameObject(obj.Id)==null)return;float alpha=start+(float)(finish-start)*(float)t;if(sprite!=null){var tint=sprite.Tint;tint.W=alpha;sprite.Tint=tint;}if(mesh!=null)mesh.MaterialOverrides.Opacity=alpha;if(state!=null)state.Opacity=alpha;});
        },target,name,NumberArg("end","Destination opacity (0 to 1)",1),NumberArg("slot","Skeletal material slot"),seconds,easing);
        string Key(VisualInstruction i,EventExecutionContext c)=>(ResolveObjectTarget(i,c)?.Id??c.Self.Id).ToString("N")+"/"+S(i,c,"name","Move");
        foreach(string property in new[]{"position","scale","rotation"})
        {
            string kind=property;
            Add(r,"tween."+kind,"Tween / Transform","Tween object "+kind,(i,c)=>
            {
                var obj=ResolveObjectTarget(i,c);if(obj==null)return;
                var destination=EventValueResolver.GetVector3(i,"end",c);var start=kind=="position"?obj.Transform.WorldPosition:kind=="scale"?obj.Transform.LocalScale:obj.Transform.EulerAngles;
                var fromRotation=obj.Transform.LocalRotation;var toRotation=Quaternion.CreateFromYawPitchRoll(destination.Y*MathF.PI/180,destination.X*MathF.PI/180,destination.Z*MathF.PI/180);
                CreatorSceneServices.For(c.Scene).StartTween(Key(i,c),N(i,c,"seconds",1),S(i,c,"easing","Smoothstep"),t=>
                {if(c.Scene.FindGameObject(obj.Id)==null)return;var v=Vector3.Lerp(start,destination,(float)t);if(kind=="position")obj.Transform.WorldPosition=v;else if(kind=="scale")obj.Transform.LocalScale=v;else obj.Transform.LocalRotation=Quaternion.Slerp(fromRotation,toRotation,(float)t);});
            },target,name,end,seconds,easing);
        }
        Add(r,"tween.variable","Tween / Variables","Tween number variable",(i,c)=>
        {
            var store=Store(i,c);string variable=S(i,c,"variable","Value");double start=store.TryGet(variable,out var value)?value!.Number:0;double finish=N(i,c,"end");
            CreatorSceneServices.For(c.Scene).StartTween(Key(i,c),N(i,c,"seconds",1),S(i,c,"easing","Smoothstep"),t=>store.Set(variable,VariableValue.FromNumber(start+(finish-start)*t)));
        },target,name,TextArg("scope","Variable scope","Self"),TextArg("variable","Variable name","Value"),NumberArg("end","Destination value",1),seconds,easing);
        Add(r,"tween.cameraFov","Tween / Camera","Tween camera field of view",(i,c)=>
        {var camera=ResolveObjectTarget(i,c)?.GetComponent<Camera3D>()??c.Scene.ActiveCamera;if(camera==null)return;float start=camera.FieldOfView;float finish=(float)Math.Clamp(N(i,c,"end",60),1,179);CreatorSceneServices.For(c.Scene).StartTween(Key(i,c),N(i,c,"seconds",1),S(i,c,"easing","Smoothstep"),t=>camera.FieldOfView=start+(finish-start)*(float)t);},target,name,NumberArg("end","Field of view degrees",60),seconds,easing);
        Add(r,"tween.pause","Tween","Pause tween",(i,c)=>CreatorSceneServices.For(c.Scene).PauseTween(Key(i,c),true),target,name);
        Add(r,"tween.resume","Tween","Resume tween",(i,c)=>CreatorSceneServices.For(c.Scene).PauseTween(Key(i,c),false),target,name);
        Add(r,"tween.remove","Tween","Stop / remove tween",(i,c)=>CreatorSceneServices.For(c.Scene).RemoveTween(Key(i,c)),target,name);
        Test(r,"tween.running","Tween","Tween is running",(i,c)=>CreatorSceneServices.For(c.Scene).GetTween(Key(i,c)) is {Completed:false,Paused:false},target,name);
        Test(r,"tween.paused","Tween","Tween is paused",(i,c)=>CreatorSceneServices.For(c.Scene).GetTween(Key(i,c)) is {Paused:true},target,name);
        Test(r,"tween.finished","Tween","Tween finished this update",(i,c)=>CreatorSceneServices.For(c.Scene).GetTween(Key(i,c)) is {FinishedThisUpdate:true},target,name);
        Test(r,"tween.completed","Tween","Tween is complete",(i,c)=>CreatorSceneServices.For(c.Scene).GetTween(Key(i,c)) is {Completed:true},target,name);
        foreach(var trigger in new[]{false,true})foreach(var phase in new[]{"enter","stay","exit"})
        {
            bool sensor=trigger;string mode=phase;
            Test(r,(sensor?"trigger.":"collision.")+phase,"Physics / Contacts",(sensor?"Trigger ":"Collision ")+phase,(i,c)=>
            {
                var obj=ResolveObjectTarget(i,c);if(obj==null)return false;var other=B(i,c,"anyOther",true)?null:Other(i,c);
                if(!B(i,c,"anyOther",true)&&other==null)return false;
                var hit=CreatorSceneServices.For(c.Scene).Contact(obj.Id,other?.Id,mode,sensor);
                if(hit is not {} contact)return false;
                var partner=contact.A==obj?contact.B:contact.A;
                c.Self.Variables.Set("ContactObjectId",VariableValue.FromString(partner.Id.ToString()));
                c.Self.Variables.Set("ContactPoint",VariableValue.FromVector3(contact.Point));
                c.Self.Variables.Set("ContactNormal",VariableValue.FromVector3(contact.A==obj?-contact.Normal:contact.Normal));return true;
            },target,BoolArg("anyOther","Any other object",true),ObjectArg("other","Other object"));
        }
    }
}
