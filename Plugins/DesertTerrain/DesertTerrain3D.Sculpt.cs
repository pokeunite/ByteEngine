using System.Numerics;
using System.IO.Compression;
using ByteEngine.Core.Characters;
namespace DesertTerrain;

public sealed partial class DesertTerrain3D : ITerrainSculptSurface
{
    private float[] _sculpt=[];
    private int _sculptCells;
    private float _sculptSpacing;
    private string _sculptSource="";
    private bool _hasSculpt;
    public HeightfieldCollider3D SculptCollider=>this;
    private string SculptSource=>HeightmapPath+"|"+HeightmapContentHash;
    private void ApplyAuthoredLayer()
    {
        string source=HeightmapPath+"|"+(_heightmap?.ContentHash??"");
        if(_sculpt.Length!=_base.Length||_sculptCells!=_cells||Math.Abs(_sculptSpacing-_spacing)>.00001f||_sculptSource!=source){_sculpt=new float[_base.Length];_sculptCells=_cells;_sculptSpacing=_spacing;_sculptSource=source;_hasSculpt=false;}
        if(!_hasSculpt)return;
        for(int i=0;i<_base.Length;i++){_base[i]+=_sculpt[i];_heights[i]=_base[i];_minimum=Math.Min(_minimum,_base[i]);_maximum=Math.Max(_maximum,_base[i]);}
    }
    public int Sculpt(Vector3 worldCenter,float worldRadius,float strength,float seconds,TerrainBrushMode mode,float worldFlattenHeight)
    {
        EnsureGenerated();if(!SupportedTransform||!float.IsFinite(worldRadius)||!float.IsFinite(strength)||!float.IsFinite(seconds)||!float.IsFinite(worldFlattenHeight)||!float.IsFinite(worldCenter.LengthSquared())||worldRadius<=0||strength<=0||seconds<=0||!Matrix4x4.Invert(SurfaceMatrix,out var inverse))return 0;
        worldRadius=Math.Clamp(worldRadius,.5f,150);strength=Math.Clamp(strength,0,50);seconds=Math.Min(seconds,.1f);
        Vector3 local=Vector3.Transform(worldCenter,inverse);float localRadius=worldRadius/Math.Min(Transform.WorldScale.X,Transform.WorldScale.Z);float half=_cells*_spacing*.5f;
        int x0=Math.Clamp((int)MathF.Floor((local.X-localRadius+half)/_spacing),0,_cells),x1=Math.Clamp((int)MathF.Ceiling((local.X+localRadius+half)/_spacing),0,_cells);
        int z0=Math.Clamp((int)MathF.Floor((local.Z-localRadius+half)/_spacing),0,_cells),z1=Math.Clamp((int)MathF.Ceiling((local.Z+localRadius+half)/_spacing),0,_cells),n=_cells+1;
        int sx=Math.Max(0,x0-1),sz=Math.Max(0,z0-1),ex=Math.Min(_cells,x1+1),ez=Math.Min(_cells,z1+1),width=ex-sx+1;
        float[]? previous=null;if(mode==TerrainBrushMode.Smooth){previous=new float[width*(ez-sz+1)];for(int z=sz;z<=ez;z++)Array.Copy(_base,z*n+sx,previous,(z-sz)*width,width);}
        float flatten=Vector3.Transform(new Vector3(worldCenter.X,worldFlattenHeight,worldCenter.Z),inverse).Y;int changed=0;
        for(int z=z0;z<=z1;z++)for(int x=x0;x<=x1;x++){
            var point=Vector3.Transform(new Vector3(x*_spacing-half,local.Y,z*_spacing-half),SurfaceMatrix);float dx=point.X-worldCenter.X,dz=point.Z-worldCenter.Z,q=(dx*dx+dz*dz)/(worldRadius*worldRadius);if(q>=1)continue;
            float weight=(1-q)*(1-q),old=_base[z*n+x],value=old;
            if(mode==TerrainBrushMode.Raise||mode==TerrainBrushMode.Lower)value+=strength*seconds*weight/Transform.WorldScale.Y*(mode==TerrainBrushMode.Raise?1:-1);
            else if(mode==TerrainBrushMode.Flatten)value+=(flatten-old)*(1-MathF.Exp(-strength*seconds*weight));
            else {float sum=0;int count=0;for(int dz2=-1;dz2<=1;dz2++)for(int dx2=-1;dx2<=1;dx2++){int xx=Math.Clamp(x+dx2,0,_cells),zz=Math.Clamp(z+dz2,0,_cells);sum+=previous![(zz-sz)*width+xx-sx];count++;}value+=(sum/count-old)*(1-MathF.Exp(-strength*seconds*weight));}
            value=Math.Clamp(value,-200,500);if(Math.Abs(value-old)<.000001f)continue;int i=z*n+x;float delta=value-old;_base[i]=value;_sculpt[i]+=delta;_heights[i]+=delta;_minimum=Math.Min(_minimum,_heights[i]);_maximum=Math.Max(_maximum,_heights[i]);changed++;
        }
        if(changed>0){_hasSculpt=true;DeformationRevision++;for(int i=0;i<_chunks.Count;i++){var c=_chunks[i];if(c.X<=x1+1&&c.X+c.Width>=x0-1&&c.Z<=z1+1&&c.Z+c.Depth>=z0-1)_dirty.Add(i);}}
        return changed;
    }
    public string ExportSculpt()
    {
        EnsureGenerated();if(!_hasSculpt)return "";using var output=new MemoryStream();using(var gzip=new GZipStream(output,CompressionLevel.Fastest,true))using(var w=new BinaryWriter(gzip)){w.Write(1);w.Write(_cells);w.Write(_spacing);w.Write(_sculptSource);foreach(float h in _sculpt)w.Write(h);}return Convert.ToBase64String(output.ToArray());
    }
    public void ImportSculpt(string data)
    {
        EnsureGenerated();if(string.IsNullOrEmpty(data))return;if(data.Length>8_000_000)throw new InvalidDataException("Sculpt layer exceeds maximum size");
        using var input=new MemoryStream(Convert.FromBase64String(data));using var gzip=new GZipStream(input,CompressionMode.Decompress);using var r=new BinaryReader(gzip);
        if(r.ReadInt32()!=1||r.ReadInt32()!=_cells||Math.Abs(r.ReadSingle()-_spacing)>.00001f||r.ReadString()!=SculptSource)throw new InvalidDataException("Sculpt layer belongs to another terrain source or grid");
        var values=new float[_base.Length];for(int i=0;i<values.Length;i++){values[i]=r.ReadSingle();if(!float.IsFinite(values[i])||Math.Abs(values[i])>1000)throw new InvalidDataException("Invalid authored terrain height");}if(gzip.ReadByte()!=-1)throw new InvalidDataException("Trailing sculpt data");
        for(int i=0;i<values.Length;i++){float delta=values[i]-_sculpt[i];_base[i]+=delta;_heights[i]+=delta;}_sculpt=values;_hasSculpt=true;_minimum=_heights.Min();_maximum=_heights.Max();for(int i=0;i<_chunks.Count;i++)_dirty.Add(i);DeformationRevision++;
    }
}
