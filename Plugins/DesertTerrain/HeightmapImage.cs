using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;

namespace DesertTerrain;

/// <summary>CPU-only PNG height data. Reads raw samples without gamma correction, preserving 16-bit precision.
/// Non-interlaced grayscale/RGB, with optional alpha, at 8 or 16 bits. RGB uses the red channel.</summary>
public sealed class HeightmapImage
{
    public int Width { get; }
    public int Height { get; }
    public int BitDepth { get; }
    public string ContentHash { get; }
    private readonly float[] _samples;
    private HeightmapImage(int width,int height,int bitDepth,string hash,float[] samples)
    {Width=width;Height=height;BitDepth=bitDepth;ContentHash=hash;_samples=samples;}
    public float Sample(float u,float v)
    {
        u=Math.Clamp(u,0,1);v=Math.Clamp(v,0,1);
        float px=u*(Width-1),pz=v*(Height-1);int x=Math.Min((int)px,Width-2),z=Math.Min((int)pz,Height-2);
        float tx=px-x,tz=pz-z;
        return (_samples[z*Width+x]*(1-tx)+_samples[z*Width+x+1]*tx)*(1-tz)+
               (_samples[(z+1)*Width+x]*(1-tx)+_samples[(z+1)*Width+x+1]*tx)*tz;
    }
    public static HeightmapImage Load(string file)
    {
        var info=new FileInfo(file);
        if(!info.Exists)throw new FileNotFoundException("Heightmap not found. Place a PNG in Assets/Terrain and set Heightmap Path.",file);
        if(info.Length>64*1024*1024)throw new InvalidDataException("Heightmap PNG exceeds 64 MB.");
        byte[] bytes=File.ReadAllBytes(file);
        ReadOnlySpan<byte> signature=[137,80,78,71,13,10,26,10];
        if(bytes.Length<33||!bytes.AsSpan(0,8).SequenceEqual(signature))throw new InvalidDataException("Heightmaps must be PNG files.");
        int width=0,height=0,depth=0,channels=0,offset=8;bool header=false,ended=false;using var compressed=new MemoryStream();
        while(offset+12<=bytes.Length)
        {
            uint size=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset,4));
            if(size>64*1024*1024||size>bytes.Length-offset-12)throw new InvalidDataException("Truncated PNG chunk.");
            int length=(int)size;var kind=bytes.AsSpan(offset+4,4);var data=bytes.AsSpan(offset+8,length);
            uint checksum=BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset+8+length,4));
            if(Crc(bytes.AsSpan(offset+4,length+4))!=checksum)throw new InvalidDataException("Heightmap PNG checksum failed.");
            string name=Encoding.ASCII.GetString(kind);
            if(!header&&name!="IHDR")throw new InvalidDataException("PNG header must be the first chunk.");
            if(name=="IHDR")
            {
                if(header||length!=13)throw new InvalidDataException("Invalid PNG header.");header=true;
                width=BinaryPrimitives.ReadInt32BigEndian(data[..4]);height=BinaryPrimitives.ReadInt32BigEndian(data.Slice(4,4));depth=data[8];
                channels=data[9] switch{0=>1,2=>3,4=>2,6=>4,_=>0};
                if(width<2||height<2||width>4096||height>4096)throw new InvalidDataException("Heightmap dimensions must be 2..4096 pixels on each axis.");
                if(channels==0||depth is not (8 or 16)||data[10]!=0||data[11]!=0||data[12]!=0)
                    throw new InvalidDataException("Use a non-interlaced 8-bit or 16-bit grayscale/RGB PNG; indexed images are unsupported.");
            }
            else if(name=="IDAT")compressed.Write(data);
            else if(name=="IEND"){if(length!=0)throw new InvalidDataException("Invalid PNG end chunk.");ended=true;break;}
            else if(name!="PLTE"&&(kind[0]&32)==0)throw new InvalidDataException("Unsupported critical PNG chunk: "+name);
            offset+=length+12;
        }
        if(!ended||compressed.Length==0)throw new InvalidDataException("PNG image data or end chunk is missing.");
        int bytesPerPixel=channels*(depth/8),stride=checked(width*bytesPerPixel);
        byte[] previous=new byte[stride],row=new byte[stride];float[] values=new float[checked(width*height)];
        compressed.Position=0;using var zlib=new ZLibStream(compressed,CompressionMode.Decompress);
        for(int y=0;y<height;y++)
        {
            int filter=zlib.ReadByte();if(filter<0||filter>4)throw new InvalidDataException("Invalid or missing PNG scanline filter.");
            try{zlib.ReadExactly(row);}catch(EndOfStreamException e){throw new InvalidDataException("Truncated heightmap scanline.",e);}
            for(int i=0;i<stride;i++)
            {
                int left=i>=bytesPerPixel?row[i-bytesPerPixel]:0,above=previous[i],corner=i>=bytesPerPixel?previous[i-bytesPerPixel]:0;
                int predictor=filter switch{0=>0,1=>left,2=>above,3=>(left+above)/2,4=>Paeth(left,above,corner),_=>0};
                row[i]=unchecked((byte)(row[i]+predictor));
            }
            for(int x=0;x<width;x++)values[y*width+x]=depth==16?BinaryPrimitives.ReadUInt16BigEndian(row.AsSpan(x*bytesPerPixel,2))/65535f:row[x*bytesPerPixel]/255f;
            (row,previous)=(previous,row);
        }
        if(zlib.ReadByte()!=-1)throw new InvalidDataException("Unexpected heightmap scanline data.");
        return new(width,height,depth,Convert.ToHexString(SHA256.HashData(bytes)),values);
    }
    private static int Paeth(int a,int b,int c){int p=a+b-c,pa=Math.Abs(p-a),pb=Math.Abs(p-b),pc=Math.Abs(p-c);return pa<=pb&&pa<=pc?a:pb<=pc?b:c;}
    private static readonly uint[] CrcTable=MakeCrcTable();
    private static uint[] MakeCrcTable(){var table=new uint[256];for(uint i=0;i<256;i++){uint c=i;for(int j=0;j<8;j++)c=(c>>1)^((c&1)!=0?0xedb88320u:0);table[i]=c;}return table;}
    private static uint Crc(ReadOnlySpan<byte> data){uint c=0xffffffff;foreach(byte b in data)c=CrcTable[(c^b)&255]^(c>>8);return ~c;}
}
