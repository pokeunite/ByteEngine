namespace ByteEngine.Core.Vfx;

/// <summary>Small normalized lifetime curve. Evaluation is allocation-free.</summary>
public sealed class VfxCurve
{
    public List<VfxCurveKey> Keys { get; set; } = new() { new(0,1), new(1,1) };
    public float Evaluate(float time)
    {
        if(Keys.Count==0) return 1;
        time=VfxEffect.Safe(time,0,0,1);
        if(time<=Keys[0].Time) return Keys[0].Value;
        for(int i=1;i<Keys.Count;i++)
        {
            var a=Keys[i-1]; var b=Keys[i];
            if(time<=b.Time) return a.Value+(b.Value-a.Value)*(time-a.Time)/Math.Max(.00001f,b.Time-a.Time);
        }
        return Keys[^1].Value;
    }
    public void Validate(float maximum=4)
    {
        Keys??=new();
        Keys=Keys.Take(8).Select(k=>new VfxCurveKey(VfxEffect.Safe(k.Time,0,0,1),VfxEffect.Safe(k.Value,1,0,maximum)))
            .OrderBy(k=>k.Time).GroupBy(k=>k.Time).Select(g=>g.Last()).ToList();
        if(Keys.Count==0) Keys.Add(new(0,1));
    }
    public static VfxCurve Linear(float start,float end) => new() { Keys=new() { new(0,start),new(1,end) } };
}

public readonly record struct VfxCurveKey(float Time,float Value);
