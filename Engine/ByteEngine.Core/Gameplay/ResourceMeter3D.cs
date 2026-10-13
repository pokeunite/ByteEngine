using ByteEngine.Core.Scene;
namespace ByteEngine.Core.Gameplay;
/// <summary>Reusable bounded resource. Consumers decide spending/recharge policy and clock domain.</summary>
public sealed class ResourceMeter3D:Component
{
    float _capacity=100;
    public float Capacity{get=>_capacity;set{_capacity=float.IsFinite(value)?Math.Max(0,value):0;Set(Current);}}
    public float InitialAmount{get;set;}=100;
    public float Current{get;private set;}
    public float Fraction=>Capacity>0?Current/Capacity:0;
    public event Action<float>? Changed;
    protected override void OnStart()=>Reset();
    public void Reset()=>Set(InitialAmount);
    public bool TrySpend(float amount){if(!float.IsFinite(amount)||amount<0||Current<amount)return false;Set(Current-amount);return true;}
    public void Restore(float amount){if(float.IsFinite(amount)&&amount>0)Set(Current+amount);}
    void Set(float amount){float next=Math.Clamp(float.IsFinite(amount)?amount:0,0,float.IsFinite(Capacity)?Math.Max(0,Capacity):0);if(next!=Current){Current=next;Changed?.Invoke(next);}}
}
