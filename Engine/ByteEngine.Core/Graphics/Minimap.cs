using System.Numerics;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Graphics;

/// <summary>Maps an X/Z world rectangle onto an authored UI marker. Works with any game or camera.</summary>
public sealed class Minimap : Component
{
    public string TargetObject { get; set; } = "";
    public Vector2 WorldCenter { get; set; }
    public Vector2 WorldSize { get; set; } = new(1024);
    public Vector2 MapOffset { get; set; }
    public Vector2 MapSize { get; set; } = new(240, 216);
    public bool ClampToMap { get; set; } = true;
    public bool InvertZ { get; set; }
    public Vector2 WorldToMap(Vector3 position)
    {
        var uv = new Vector2((position.X - WorldCenter.X) / Math.Max(.001f, WorldSize.X) + .5f, (position.Z - WorldCenter.Y) / Math.Max(.001f, WorldSize.Y) + .5f);
        if (InvertZ) uv.Y = 1 - uv.Y;
        if (ClampToMap) uv = Vector2.Clamp(uv, Vector2.Zero, Vector2.One);
        return MapOffset + uv * MapSize;
    }
    public void SetPosition(Vector3 position)
    {
        if (GameObject.GetComponent<UiWidget>() is {} marker) marker.Offset = WorldToMap(position) - marker.Size * .5f;
    }
    public override int UpdateOrder => 95;
    protected override void OnUpdate()
    {
        if (TargetObject.Length > 0 && GameObject.Scene?.FindGameObject(TargetObject) is {} target) SetPosition(target.Transform.WorldPosition);
    }
}
