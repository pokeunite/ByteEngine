using System.Numerics;
namespace ByteEngine.Core.Graphics.ThreeD;
/// <summary>Deterministic static-mesh vertex clustering. Preserves hard-normal buckets; unsuitable for skinned meshes and tiny silhouette-critical props.</summary>
public static class MeshSimplifier
{
    public static Mesh Simplify(Mesh source,float ratio)
    {
        ratio=Math.Clamp(float.IsFinite(ratio)?ratio:.5f,.01f,1);
        var vertices=source.VertexData.Span;var indices=source.IndexData.Span;
        if(ratio>=1||source.VertexCount<12)return new(vertices.ToArray(),indices.ToArray());
        Vector3 span=source.LocalBounds.Size;
        float cell=Math.Max(.00001f,Math.Max(span.X,Math.Max(span.Y,span.Z))/Math.Max(1,MathF.Sqrt(source.VertexCount*ratio)));
        var clusters=new Dictionary<(int,int,int,int,int,int,int,int),int>();var sums=new List<float[]>();var counts=new List<int>();var remap=new uint[source.VertexCount];
        for(int i=0;i<source.VertexCount;i++)
        {
            int offset=i*8;
            var key=((int)MathF.Floor(vertices[offset]/cell),(int)MathF.Floor(vertices[offset+1]/cell),(int)MathF.Floor(vertices[offset+2]/cell),
                (int)MathF.Round(vertices[offset+3]*4),(int)MathF.Round(vertices[offset+4]*4),(int)MathF.Round(vertices[offset+5]*4),(int)MathF.Floor(vertices[offset+6]*16),(int)MathF.Floor(vertices[offset+7]*16));
            if(!clusters.TryGetValue(key,out int index)){clusters[key]=index=sums.Count;sums.Add(new float[8]);counts.Add(0);}
            remap[i]=(uint)index;counts[index]++;for(int c=0;c<8;c++)sums[index][c]+=vertices[offset+c];
        }
        var output=new float[sums.Count*8];for(int i=0;i<sums.Count;i++)
        {
            for(int c=0;c<8;c++)output[i*8+c]=sums[i][c]/counts[i];
            Vector3 normal=new(output[i*8+3],output[i*8+4],output[i*8+5]);if(normal.LengthSquared()>.00001f)normal=Vector3.Normalize(normal);
            output[i*8+3]=normal.X;output[i*8+4]=normal.Y;output[i*8+5]=normal.Z;
        }
        var triangles=new List<uint>();var seen=new HashSet<(uint,uint,uint)>();
        for(int i=0;i+2<indices.Length;i+=3){uint a=remap[indices[i]],b=remap[indices[i+1]],c=remap[indices[i+2]];if(a==b||a==c||b==c||!seen.Add((a,b,c)))continue;triangles.Add(a);triangles.Add(b);triangles.Add(c);}
        return triangles.Count==0?new(vertices.ToArray(),indices.ToArray()):new(output,triangles.ToArray());
    }
}
