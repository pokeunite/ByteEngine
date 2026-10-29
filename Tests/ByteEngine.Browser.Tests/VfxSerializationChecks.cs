using System.Numerics;
using ByteEngine.Core.Vfx;

internal static class VfxSerializationChecks
{
    public static void Run()
    {
        var effect=new VfxEffect {Name="Portable particles",Loop=true,Layers=[new() {
            StartColor=new(.17f,.43f,.81f,.67f),EndColor=new(.05f,.11f,.23f,.31f),
            Offset=new(1.2f,3.4f,-5.6f),Gravity=new(0,-2.7f,.8f),Direction=new(0,0,-1),
            Burst=7,Rate=13,StartSize=.37f,Additive=false,
            SizeOverLife=VfxCurve.Linear(.2f,.9f)
        }]};
        var copy=VfxEffectSerializer.Clone(effect);
        var a=effect.Layers[0];var b=copy.Layers[0];
        if(copy.Name!=effect.Name||!copy.Loop||b.StartColor!=a.StartColor||b.EndColor!=a.EndColor||
            b.Offset!=a.Offset||b.Gravity!=a.Gravity||b.Direction!=a.Direction||b.Burst!=7||b.Rate!=13||
            b.StartSize!=a.StartSize||b.Additive||Math.Abs(b.SizeOverLife!.Evaluate(.5f)-.55f)>.001f)
            throw new Exception("Portable VFX fields did not survive serialization.");
        Console.WriteLine("PASS: typed VFX serialization preserves color alpha, vectors, curves and emitter settings");
    }
}
