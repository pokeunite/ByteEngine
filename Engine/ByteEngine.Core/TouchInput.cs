using System.Numerics;
namespace ByteEngine.Core;

public enum TouchPhase { Started, Held, Ended, Cancelled }
public readonly record struct TouchPoint(int Id,Vector2 Position,TouchPhase Phase);
/// <summary>Host-fed stable touch IDs and frame-wide lifecycle snapshots.</summary>
public static class TouchInput
{
    private static readonly Dictionary<int,TouchPoint> Active=new();
    private static readonly List<TouchPoint> Pending=new();
    private static TouchPoint[] _frame=[];
    public static IReadOnlyList<TouchPoint> Points=>_frame;
    public static void Feed(int id,float x,float y,TouchPhase phase)
    {
        if(!float.IsFinite(x)||!float.IsFinite(y))return;
        lock(Pending){if(Pending.Count<4096)Pending.Add(new(id,new(Math.Clamp(x,0,1),Math.Clamp(y,0,1)),phase));}
    }
    public static void CancelAll(){lock(Pending){foreach(var point in Active.Values)Pending.Add(point with {Phase=TouchPhase.Cancelled});}}
    internal static void BeginFrame()
    {
        lock(Pending)
        {
            var events=new List<TouchPoint>();
            var started=new Dictionary<int,TouchPoint>();
            foreach(var point in Pending)
            {
                if(point.Phase is TouchPhase.Ended or TouchPhase.Cancelled){Active.Remove(point.Id);events.Add(point);}
                else {if(!Active.ContainsKey(point.Id))started[point.Id]=point with {Phase=TouchPhase.Started};Active[point.Id]=point with {Phase=TouchPhase.Held};}
            }
            events.AddRange(started.Values);
            foreach(var point in Active.Values)if(!started.ContainsKey(point.Id))events.Add(point);
            _frame=events.OrderBy(p=>p.Id).ToArray();
            foreach(var id in Active.Keys.ToArray())Active[id]=Active[id] with {Phase=TouchPhase.Held};
            Pending.Clear();
        }
    }
}
