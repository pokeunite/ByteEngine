using System.Numerics;
using ByteEngine.Core.Animation;
using ByteEngine.Core.Graphics.ThreeD;
using System.Runtime.CompilerServices;

namespace ByteEngine.Core.Scene;

public enum AttachmentTransformRule { KeepRelative, KeepWorld, SnapToTarget }

public static class SkeletalAttachmentService
{
    private static readonly ConditionalWeakTable<GameObject, Binding> Bindings = new();
    public static bool AttachToSocket(GameObject child, GameObject parent, string socketName,
        AttachmentTransformRule locationRule = AttachmentTransformRule.SnapToTarget,
        AttachmentTransformRule rotationRule = AttachmentTransformRule.SnapToTarget,
        AttachmentTransformRule scaleRule = AttachmentTransformRule.KeepRelative)
    {
        ArgumentNullException.ThrowIfNull(child); ArgumentNullException.ThrowIfNull(parent);
        if (string.IsNullOrWhiteSpace(socketName) || !SkeletalSocketResolver.TryGetSocketWorldTransform(parent, socketName, out SkeletalSocketTransform socket)) return false;
        Vector3 worldPosition=child.Transform.WorldPosition, worldScale=child.Transform.WorldScale;
        Quaternion worldRotation=child.Transform.WorldRotation;
        Vector3 previousRelativePosition=child.IsAttached?child.AttachmentPosition:child.Transform.LocalPosition;
        Quaternion previousRelativeRotation=child.IsAttached?child.AttachmentRotation:child.Transform.LocalRotation;
        Vector3 previousRelativeScale=child.IsAttached?child.AttachmentScale:child.Transform.LocalScale;
        if (!child.SetParent(parent, true)) return false;
        Invalidate(child);
        child.ParentSocket = socketName.Trim(); child.AttachmentLocationRule=locationRule; child.AttachmentRotationRule=rotationRule; child.AttachmentScaleRule=scaleRule;
        child.AttachmentPosition = locationRule switch { AttachmentTransformRule.SnapToTarget=>Vector3.Zero, AttachmentTransformRule.KeepWorld=>Vector3.Transform(worldPosition-socket.Position,Quaternion.Inverse(socket.Rotation))/socket.Scale, _=>previousRelativePosition };
        child.AttachmentRotation = rotationRule switch { AttachmentTransformRule.SnapToTarget=>Quaternion.Identity, AttachmentTransformRule.KeepWorld=>Quaternion.Normalize(worldRotation*Quaternion.Inverse(socket.Rotation)), _=>previousRelativeRotation };
        child.AttachmentScale = scaleRule switch { AttachmentTransformRule.SnapToTarget=>Vector3.One, AttachmentTransformRule.KeepWorld=>worldScale/socket.Scale, _=>previousRelativeScale };
        Apply(child); return true;
    }

    public static bool Detach(GameObject child, bool keepWorldTransform=true)
    {
        ArgumentNullException.ThrowIfNull(child); if (!child.IsAttached) return false;
        Vector3 p=child.Transform.WorldPosition,s=child.Transform.WorldScale; Quaternion r=child.Transform.WorldRotation;
        Invalidate(child); child.ParentSocket=string.Empty; child.SetParent(null, false);
        if(keepWorldTransform){child.Transform.WorldPosition=p;child.Transform.WorldRotation=r;child.Transform.WorldScale=s;}
        return true;
    }

    public static bool IsAttachedToSocket(GameObject child, GameObject? parent=null, string? socketName=null) =>
        child.IsAttached && (parent==null||ReferenceEquals(child.Parent,parent)) && (string.IsNullOrWhiteSpace(socketName)||string.Equals(child.ParentSocket,socketName,StringComparison.OrdinalIgnoreCase));

    internal static void UpdateScene(Scene scene)
    {
        foreach(GameObject item in scene.GameObjects) if(item.IsAttached) Apply(item);
    }

    public static void Invalidate(GameObject child) => Bindings.Remove(child);
    internal static bool HasCachedBinding(GameObject child) => Bindings.TryGetValue(child, out _);

    private static bool TryResolveBinding(GameObject child, out Binding? binding)
    {
        GameObject parent=child.Parent!;
        if(Bindings.TryGetValue(child,out binding))
        {
            if(ReferenceEquals(binding.Parent,parent)&&string.Equals(binding.SocketName,child.ParentSocket,StringComparison.OrdinalIgnoreCase))
            {
                var model=binding.Renderer.ResolvedModel;
                if(model!=null&&model.Guid==binding.ModelGuid&&model.FindSocket(binding.SocketId)!=null)return true;
            }
            Bindings.Remove(child);
        }
        if(!SkeletalSocketResolver.TryResolveRenderer(parent,child.ParentSocket,out SkeletalMeshRenderer? renderer)||renderer?.ResolvedModel is not { } resolved)return false;
        SkeletalSocketDefinition? socket=resolved.FindSocket(child.ParentSocket); if(socket==null)return false;
        binding=new Binding(parent,child.ParentSocket,renderer,resolved.Guid,socket.Id); Bindings.Add(child,binding); return true;
    }

    private sealed record Binding(GameObject Parent,string SocketName,SkeletalMeshRenderer Renderer,Guid ModelGuid,Guid SocketId);
    public static bool Apply(GameObject child)
    {
        if(child.Parent==null||string.IsNullOrWhiteSpace(child.ParentSocket)||!TryResolveBinding(child,out Binding? binding)||!SkeletalSocketResolver.TryGetSocketWorldTransform(binding!.Renderer,child.ParentSocket,out SkeletalSocketTransform socket)) return false;
        Vector3 position=socket.Position+Vector3.Transform(child.AttachmentPosition*socket.Scale,socket.Rotation);
        Quaternion rotation=Quaternion.Normalize(child.AttachmentRotation*socket.Rotation);
        Vector3 scale=child.AttachmentScale*socket.Scale;
        child.Transform.WorldPosition=position; child.Transform.WorldRotation=rotation; child.Transform.WorldScale=scale; return true;
    }
}