using System.Runtime.InteropServices;
namespace ByteEngine.Core.Media;

/// <summary>Windows Media Foundation reader. Owned and used by one decoder thread.</summary>
public sealed class WindowsVideoReader : IDisposable
{
    private IntPtr _reader;
    private bool _started,_com;
    public int Width {get;private set;}
    public int Height {get;private set;}
    private int _stride;
    private double _seekTarget;
    private const int Video=unchecked((int)0xfffffffc);
    [DllImport("ole32.dll")] private static extern int CoInitializeEx(IntPtr reserved,uint mode);
    [DllImport("ole32.dll")] private static extern void CoUninitialize();
    [DllImport("mfplat.dll")] private static extern int MFStartup(int version,int flags);
    [DllImport("mfplat.dll")] private static extern int MFShutdown();
    [DllImport("mfplat.dll")] private static extern int MFCreateAttributes(out IntPtr attributes,int count);
    [DllImport("mfplat.dll")] private static extern int MFCreateMediaType(out IntPtr type);
    [DllImport("mfreadwrite.dll",CharSet=CharSet.Unicode)] private static extern int MFCreateSourceReaderFromURL(string url,IntPtr attributes,out IntPtr reader);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetU32(IntPtr self,ref Guid key,int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetGuid(IntPtr self,ref Guid key,ref Guid value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetU64(IntPtr self,ref Guid key,out ulong value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetU32(IntPtr self,ref Guid key,out int value);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetType(IntPtr self,int stream,IntPtr reserved,IntPtr type);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetMediaType(IntPtr self,int stream,out IntPtr type);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SelectStream(IntPtr self,int stream,int selected);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int Read(IntPtr self,int stream,int flags,out int actual,out int status,out long timestamp,out IntPtr sample);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int GetBuffer(IntPtr self,out IntPtr buffer);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int LockBuffer(IntPtr self,out IntPtr data,out int maximum,out int length);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int UnlockBuffer(IntPtr self);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)] private delegate int SetPosition(IntPtr self,ref Guid format,ref PropVariant value);
    [StructLayout(LayoutKind.Explicit,Size=24)] private struct PropVariant
    {[FieldOffset(0)] public ushort Type;[FieldOffset(8)] public long Value;}
    private static T Method<T>(IntPtr pointer,int index) where T:Delegate=>Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(pointer),index*IntPtr.Size));
    private static void Check(int hr)=>Marshal.ThrowExceptionForHR(hr);
    public WindowsVideoReader(string path)
    {
        if(!OperatingSystem.IsWindows())throw new PlatformNotSupportedException("Native video uses Windows Media Foundation.");
        IntPtr attributes=IntPtr.Zero,type=IntPtr.Zero,current=IntPtr.Zero;
        try
        {
            Check(CoInitializeEx(IntPtr.Zero,0));_com=true;Check(MFStartup(0x20070,0));_started=true;
            Check(MFCreateAttributes(out attributes,1));var processing=new Guid("fb394f3d-ccf1-42ee-bbb3-f9b845d5681d");Check(Method<SetU32>(attributes,21)(attributes,ref processing,1));
            Check(MFCreateSourceReaderFromURL(path,attributes,out _reader));
            Check(Method<SelectStream>(_reader,4)(_reader,unchecked((int)0xfffffffe),0));Check(Method<SelectStream>(_reader,4)(_reader,Video,1));
            Check(MFCreateMediaType(out type));var major=new Guid("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");var video=new Guid("73646976-0000-0010-8000-00aa00389b71");
            Check(Method<SetGuid>(type,24)(type,ref major,ref video));var subtype=new Guid("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");var rgb=new Guid("00000016-0000-0010-8000-00aa00389b71");Check(Method<SetGuid>(type,24)(type,ref subtype,ref rgb));
            Check(Method<SetType>(_reader,7)(_reader,Video,IntPtr.Zero,type));Check(Method<GetMediaType>(_reader,6)(_reader,Video,out current));
            var size=new Guid("1652c33d-d6b2-4012-b834-72030849a37d");Check(Method<GetU64>(current,8)(current,ref size,out ulong dimensions));Width=(int)(dimensions>>32);Height=(int)(dimensions&0xffffffff);
            if(Width<=0||Height<=0||(long)Width*Height>3840L*2160)throw new InvalidDataException("Video must be at most 3840 × 2160.");
            var stride=new Guid("644b4e48-1e02-4516-b0eb-c01ca9d49ac6");if(Method<GetU32>(current,7)(current,ref stride,out _stride)<0)_stride=Width*4;
        }
        catch {Dispose();throw;}
        finally {if(current!=IntPtr.Zero)Marshal.Release(current);if(type!=IntPtr.Zero)Marshal.Release(type);if(attributes!=IntPtr.Zero)Marshal.Release(attributes);}
    }
    public void Seek(double seconds)
    {if(!double.IsFinite(seconds)||seconds<0)throw new ArgumentOutOfRangeException(nameof(seconds));var format=Guid.Empty;var value=new PropVariant{Type=20,Value=(long)(seconds*10000000)};Check(Method<SetPosition>(_reader,8)(_reader,ref format,ref value));_seekTarget=seconds;}
    public (double Seconds,byte[] Pixels)? ReadFrame()
    {
        for(int attempt=0;attempt<10000;attempt++)
        {
            IntPtr sample=IntPtr.Zero,buffer=IntPtr.Zero;bool locked=false;
            try
            {
                Check(Method<Read>(_reader,9)(_reader,Video,0,out _,out int flags,out long timestamp,out sample));
                if((flags&2)!=0)return null;if(sample==IntPtr.Zero)continue;
                if(timestamp/10000000.0+0.001<_seekTarget)continue;
                Check(Method<GetBuffer>(sample,41)(sample,out buffer));Check(Method<LockBuffer>(buffer,3)(buffer,out var data,out _,out int length));locked=true;
                int stride=Math.Abs(_stride);if(length<(Height-1)*stride+Width*4)throw new InvalidDataException("Video frame is truncated.");
                var pixels=new byte[checked(Width*Height*4)];
                for(int y=0;y<Height;y++)
                {int row=_stride<0?Height-1-y:y;Marshal.Copy(data+row*stride,pixels,y*Width*4,Width*4);}
                for(int p=0;p<pixels.Length;p+=4){(pixels[p],pixels[p+2])=(pixels[p+2],pixels[p]);pixels[p+3]=255;}
                return(timestamp/10000000.0,pixels);
            }
            finally {if(locked)Method<UnlockBuffer>(buffer,4)(buffer);if(buffer!=IntPtr.Zero)Marshal.Release(buffer);if(sample!=IntPtr.Zero)Marshal.Release(sample);}
        }
        throw new InvalidDataException("No decodable video sample was produced.");
    }
    public void Dispose(){if(_reader!=IntPtr.Zero){Marshal.Release(_reader);_reader=IntPtr.Zero;}if(_started){MFShutdown();_started=false;}if(_com){CoUninitialize();_com=false;}}
}
