using System.Numerics;
using ByteEngine.Core.Variables;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Navigation;

namespace ByteEngine.Core.VisualLogic;
public sealed partial class VisualLogicRegistry
{
    private static GameObject? Other(VisualInstruction i,EventExecutionContext c,string key="other")
        =>ResolveObjectArgument(i,key,c);
    private static void RegisterCreatorWorld(VisualLogicRegistry r)
    {
        Add(r,"renderer.setOpacity","Renderer","Set renderer opacity",(i,c)=>
        {var obj=ResolveObjectTarget(i,c);if(obj==null)return;float alpha=(float)N(i,c,"opacity",1);if(!float.IsFinite(alpha))throw new ArgumentOutOfRangeException("opacity");alpha=Math.Clamp(alpha,0,1);if(obj.GetComponent<SpriteRenderer>() is {} sprite){var tint=sprite.Tint;tint.W=alpha;sprite.Tint=tint;}if(obj.GetComponent<MeshRenderer>() is {} mesh)mesh.MaterialOverrides.Opacity=alpha;if(obj.GetComponent<SkeletalMeshRenderer>() is {} skin)skin.GetMaterialOverrides(Math.Max(0,(int)N(i,c,"slot"))).Opacity=alpha;},ObjectArg(),NumberArg("opacity","Opacity (0 to 1)",1),NumberArg("slot","Skeletal material slot"));
        var target=ObjectArg();var other=ObjectArg("other","Other object");
        Add(r,"object.copyDirection","Object / Spatial","Copy normalized direction to other object",(i,c)=>{if(ResolveObjectTarget(i,c) is {} a&&Other(i,c) is {} b){var delta=b.Transform.WorldPosition-a.Transform.WorldPosition;Output(i,c,VariableValue.FromVector3(delta.LengthSquared()>1e-12f?Vector3.Normalize(delta):Vector3.Zero));}},target,other,TextArg("scope","Variable scope","Self"),TextArg("output","Result variable","Direction"));
        var set=TextArg("set","Selection set","Selection");var tag=TextArg("tag","Tag name or ID (empty = all active objects)");
        IEnumerable<GameObject> Candidates(VisualInstruction i,EventExecutionContext c)
        {
            string token=S(i,c,"tag");
            var objects=c.Scene.GameObjects.Where(o=>o.ActiveInHierarchy);
            if(token.Length==0)return objects;
            var match=c.Scene.Classification.Tags.FirstOrDefault(t=>t.Name.Equals(token,StringComparison.OrdinalIgnoreCase)||t.Id.ToString().Equals(token,StringComparison.OrdinalIgnoreCase));
            return match==null?Enumerable.Empty<GameObject>():objects.Where(o=>o.HasTag(match.Id));
        }
        bool Pick(VisualInstruction i,EventExecutionContext c,string mode)
        {
            var list=Candidates(i,c).OrderBy(o=>o.Id).ToList();
            if(mode=="nearest") list=list.OrderBy(o=>Vector3.DistanceSquared(o.Transform.WorldPosition,(Other(i,c)??c.Self).Transform.WorldPosition)).Take(1).ToList();
            if(mode=="random"&&list.Count>0)list=[list[Random.Shared.Next(list.Count)]];
            c.PickedSets[S(i,c,"set","Selection")]=list.Select(o=>o.Id).ToList();return list.Count>0;
        }
        foreach(var mode in new[]{"all","nearest","random"})
        {
            string copy=mode;
            Test(r,"pick."+mode,"Object / Selection","Pick "+mode+" matching objects",(i,c)=>Pick(i,c,copy),set,tag,other);
            Add(r,"pick."+mode,"Object / Selection","Pick "+mode+" matching objects",(i,c)=>Pick(i,c,copy),set,tag,other);
        }
        Add(r,"pick.count","Object / Selection","Copy selected object count",(i,c)=>Output(i,c,VariableValue.FromNumber(c.PickedSets.TryGetValue(S(i,c,"set","Selection"),out var ids)?ids.Count:0)),set,TextArg("scope","Variable scope","Self"),TextArg("output","Result variable","PickedCount"));
        Add(r,"camera.activate","Camera","Use camera",(i,c)=>c.Scene.SetActiveCamera(ResolveObjectTarget(i,c)?.GetComponent<Camera3D>()),target);
        Add(r,"camera.setFov","Camera","Set camera field of view",(i,c)=>{var camera=ResolveObjectTarget(i,c)?.GetComponent<Camera3D>()??c.Scene.ActiveCamera;if(camera!=null)camera.FieldOfView=(float)Math.Clamp(N(i,c,"degrees",60),1,179);},target,NumberArg("degrees","Field of view degrees",60));
        Add(r,"camera.setZoom","Camera","Set orthographic camera width",(i,c)=>{var camera=ResolveObjectTarget(i,c)?.GetComponent<Camera3D>()??c.Scene.ActiveCamera;if(camera!=null){camera.Orthographic=true;camera.OrthographicWidth=(float)Math.Max(.01,N(i,c,"width",20));}},target,NumberArg("width","Visible world width",20));
        Add(r,"camera.centerOn","Camera","Center camera on object (preserve offset)",(i,c)=>{var camera=ResolveObjectTarget(i,c)?.GetComponent<Camera3D>()??c.Scene.ActiveCamera;var follow=Other(i,c);if(camera!=null&&follow!=null)camera.Transform.WorldPosition=follow.Transform.WorldPosition+EventValueResolver.GetVector3(i,"offset",c,new(0,5,10));},target,other,new("offset","Camera offset",VariableValue.FromVector3(new(0,5,10))));
        Add(r,"camera.clamp","Camera","Clamp camera position to bounds",(i,c)=>{var camera=ResolveObjectTarget(i,c)?.GetComponent<Camera3D>()??c.Scene.ActiveCamera;var min=EventValueResolver.GetVector3(i,"minimum",c);var max=EventValueResolver.GetVector3(i,"maximum",c);if(camera!=null)camera.Transform.WorldPosition=Vector3.Clamp(camera.Transform.WorldPosition,Vector3.Min(min,max),Vector3.Max(min,max));},target,new("minimum","Minimum position",VariableValue.FromVector3(new(-10))),new("maximum","Maximum position",VariableValue.FromVector3(new(10))));
        Test(r,"camera.fovBelow","Camera","Camera field of view is below",(i,c)=>(ResolveObjectTarget(i,c)?.GetComponent<Camera3D>()??c.Scene.ActiveCamera)?.FieldOfView<N(i,c,"degrees",60),target,NumberArg("degrees","Degrees",60));
        Add(r,"navigation.moveTo","Navigation","Move navigation agent to position",(i,c)=>ResolveObjectTarget(i,c)?.GetComponent<NavigationAgent3D>()?.SetDestination(EventValueResolver.GetVector3(i,"position",c)),target,new("position","Destination",VariableValue.FromVector3(default)));
        Add(r,"navigation.stop","Navigation","Stop navigation agent",(i,c)=>ResolveObjectTarget(i,c)?.GetComponent<NavigationAgent3D>()?.Stop(),target);
        foreach(var status in Enum.GetValues<NavigationAgentStatus>())
        {var copy=status;Test(r,"navigation.is"+status,"Navigation","Navigation status is "+status,(i,c)=>ResolveObjectTarget(i,c)?.GetComponent<NavigationAgent3D>()?.Status==copy,target);}
        Test(r,"object.distanceBelow","Object / Spatial","Distance to other object is below",(i,c)=>ResolveObjectTarget(i,c) is {} a&&Other(i,c) is {} b&&Vector3.Distance(a.Transform.WorldPosition,b.Transform.WorldPosition)<N(i,c,"distance",5),target,other,NumberArg("distance","Distance",5));
        Add(r,"object.copyDistance","Object / Spatial","Copy distance to other object",(i,c)=>{if(ResolveObjectTarget(i,c) is {} a&&Other(i,c) is {} b)Output(i,c,VariableValue.FromNumber(Vector3.Distance(a.Transform.WorldPosition,b.Transform.WorldPosition)));},target,other,TextArg("scope","Variable scope","Self"),TextArg("output","Result variable","Distance"));
        Add(r,"renderer.setVisible","Object / Appearance","Show / hide object renderers",(i,c)=>{var obj=ResolveObjectTarget(i,c);bool visible=B(i,c,"visible",true);if(obj==null)return;foreach(var component in obj.Components){if(component is MeshRenderer mesh)mesh.Visible=visible;if(component is SkeletalMeshRenderer skin)skin.Visible=visible;if(component is SpriteRenderer sprite)sprite.Visible=visible;}},target,BoolArg("visible","Visible",true));
        Test(r,"renderer.isVisible","Object / Appearance","Object has a visible renderer",(i,c)=>ResolveObjectTarget(i,c)?.Components.Any(component=>component is MeshRenderer{Visible:true} or SkeletalMeshRenderer{Visible:true} or SpriteRenderer{Visible:true})==true,target);
    }
}
