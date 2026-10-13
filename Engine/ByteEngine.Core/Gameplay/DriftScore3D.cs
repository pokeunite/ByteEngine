using System.Numerics;
using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Gameplay;

/// <summary>Feed a vehicle's physical velocity and pose; visuals and game rewards remain external.</summary>
public sealed class DriftScore3D : Component
{
    public float MinimumSpeed { get; set; } = 3.5f;
    public float MinimumAngle { get; set; } = 12;
    public float MaximumAngle { get; set; } = 75;
    public float BankDelay { get; set; } = .85f;
    public float MultiplierSeconds { get; set; } = 2.5f;
    public int MaximumMultiplier { get; set; } = 5;
    public Vector3 LocalForward { get; set; } = -Vector3.UnitZ;
    public float BankedScore { get; private set; }
    public float ChainScore { get; private set; }
    public float Fill { get; private set; }
    public int Multiplier { get; private set; } = 1;
    public float SlipAngle { get; private set; }
    public bool Drifting { get; private set; }
    public event Action<float>? Banked;
    public event Action? ChainBroken;
    float _quiet;
    public void Reset(){BankedScore=ChainScore=Fill=SlipAngle=_quiet=0;Multiplier=1;Drifting=false;}
    public void BreakChain(){bool lost=ChainScore>0;ChainScore=Fill=_quiet=0;Multiplier=1;Drifting=false;if(lost)ChainBroken?.Invoke();}
    public void Bank(){if(ChainScore>0){float score=ChainScore;BankedScore+=score;ChainScore=0;Banked?.Invoke(score);}Fill=_quiet=0;Multiplier=1;}
    public void Sample(Vector3 velocity,Quaternion rotation,bool grounded,bool collision,float delta){
        if(!float.IsFinite(delta)||delta<=0||!float.IsFinite(velocity.LengthSquared())||!float.IsFinite(rotation.LengthSquared()))return;
        float dt=Math.Min(delta,.1f);if(collision){BreakChain();return;}
        var forward=Vector3.Transform(LocalForward,rotation);forward.Y=0;velocity.Y=0;float speed=velocity.Length();
        if(forward.LengthSquared()<.0001f){Drifting=false;return;}forward=Vector3.Normalize(forward);
        float longitudinal=Vector3.Dot(velocity,forward);SlipAngle=speed>.01f?MathF.Acos(Math.Clamp(longitudinal/speed,-1,1))*180/MathF.PI:0;
        Drifting=grounded&&speed>=Math.Max(.1f,MinimumSpeed)&&longitudinal>1&&SlipAngle>=Math.Clamp(MinimumAngle,1,70)&&SlipAngle<=Math.Clamp(MaximumAngle,Math.Clamp(MinimumAngle,1,70)+1,89);
        if(Drifting){
            _quiet=0;Fill+=dt*(.6f+Math.Clamp(SlipAngle/45,0,1))/Math.Max(.5f,MultiplierSeconds);
            if(Fill>=1&&Multiplier<Math.Clamp(MaximumMultiplier,1,10)){Multiplier++;Fill-=1;}
            if(Multiplier>=MaximumMultiplier)Fill=1;
            ChainScore+=dt*speed*Math.Clamp(SlipAngle/20,.5f,3)*12*Multiplier;
        }else if(ChainScore>0){_quiet+=dt;if(grounded&&_quiet>=Math.Max(.2f,BankDelay))Bank();}
    }
}
