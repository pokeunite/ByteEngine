using System.Numerics;
namespace ByteEngine.Core.Graphics.ThreeD;

/// <summary>Host-negotiated skinning capability. Bone/socket evaluation remains in Core;
/// only vertex deformation moves to the host. Desktop hosts default to CPU skinning.</summary>
internal static class GpuSkinningCapabilities {internal static int MaximumBones;}
internal sealed class GpuSkinningData
{
    internal readonly float[] Influences;
    internal Matrix4x4[] Bones=Array.Empty<Matrix4x4>();
    private readonly (int Bone,BoundingBox3D Bounds)[] _bounds;
    private readonly BoundingBox3D? _unweighted;
    private GpuSkinningData(GpuSkinningData template)
    {Influences=template.Influences;_bounds=template._bounds;_unweighted=template._unweighted;}
    // Binding bounds are immutable. Only the pose palette belongs to an actor.
    internal GpuSkinningData CreatePoseInstance()=>new(this);
    internal GpuSkinningData(float[] vertices,float[] influences)
    {
        Influences=influences;
        var bounds=new Dictionary<int,BoundingBox3D>();BoundingBox3D? unweighted=null;
        for(int v=0;v<vertices.Length/8;v++){
            var point=new Vector3(vertices[v*8],vertices[v*8+1],vertices[v*8+2]);bool weighted=false;
            for(int j=0;j<4;j++)if(influences[v*8+4+j]>.000001f&&influences[v*8+j]>=0){
                int bone=(int)influences[v*8+j];bounds[bone]=bounds.TryGetValue(bone,out var old)?new(Vector3.Min(old.Minimum,point),Vector3.Max(old.Maximum,point)):new(point,point);weighted=true;
            }
            if(!weighted)unweighted=unweighted is {} old?new(Vector3.Min(old.Minimum,point),Vector3.Max(old.Maximum,point)):new(point,point);
        }
        _bounds=bounds.Select(x=>(x.Key,x.Value)).ToArray();_unweighted=unweighted;
    }
    internal BoundingBox3D Bounds()
    {
        var min=new Vector3(float.PositiveInfinity);var max=new Vector3(float.NegativeInfinity);bool any=false;
        void Include(BoundingBox3D bounds){if(!bounds.IsValid)return;min=Vector3.Min(min,bounds.Minimum);max=Vector3.Max(max,bounds.Maximum);any=true;}
        if(_unweighted is {} raw)Include(raw);
        // Non-negative normalized LBS lies in the convex hull of its bone
        // transforms, so this union is conservative without visiting every vertex.
        foreach(var part in _bounds)Include(part.Bone<Bones.Length?part.Bounds.Transform(Bones[part.Bone]):part.Bounds);
        return any?new(min,max):BoundingBox3D.Empty;
    }
}
