using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Physics;
namespace ByteEngine.Core.Gameplay.Hydraulics;

/// <summary>Shallow-channel streamline driven by measured water transfer. Applies no body transforms.</summary>
public sealed class WaterCurrentPath3D : Component
{
    public string BasinName { get; set; } = "";
    public string FlowGate { get; set; } = "";
    public string EnableGate { get; set; } = "";
    public Vector3 Start { get; set; }
    public Vector3 Bend { get; set; }
    public Vector3 End { get; set; } = new(10,0,0);
    public bool HasBend { get; set; }
    public float FlowSign { get; set; } = 1;
    public float VelocityScale { get; set; } = 1;
    public float MaximumSpeed { get; set; } = 1.4f;
    public float Centering { get; set; } = .6f;
    internal Vector3 VelocityAt(Vector3 point, WaterNetwork3D network, WaterBasin3D basin)
    {
        if(EnableGate.Length>0 && network.Gates.FirstOrDefault(g=>g.GameObject.Name==EnableGate) is not {Opening:>.1f})return Vector3.Zero;
        var gate=network.Gates.FirstOrDefault(g=>g.GameObject.Name==FlowGate);
        float speed=Math.Clamp((gate?.Flow??0)*FlowSign*VelocityScale/Math.Max(.5f,basin.Depth*(basin.Level-basin.Floor)),-MaximumSpeed,MaximumSpeed);
        if(Math.Abs(speed)<.00001f)return Vector3.Zero;
        point.Y=0;Vector3 a=Start,b=HasBend?Bend:End;a.Y=b.Y=0;
        Vector3 Closest(Vector3 p,Vector3 x,Vector3 y)=>x+(y-x)*Math.Clamp(Vector3.Dot(p-x,y-x)/Math.Max(.001f,(y-x).LengthSquared()),0,1);
        Vector3 near=Closest(point,a,b);
        if(HasBend){var e=End;e.Y=0;var n=Closest(point,b,e);if(Vector3.DistanceSquared(point,n)<Vector3.DistanceSquared(point,near)){a=b;b=e;near=n;}}
        Vector3 tangent=Vector3.Normalize(b-a);var correction=near-point;
        correction-=tangent*Vector3.Dot(correction,tangent);
        return tangent*speed+correction*Math.Min(Centering,Math.Abs(speed));
    }
}

/// <summary>Ideal cable linkage with the lifted mass reflected as a force on its buoyant input.</summary>
public sealed class BuoyantPulley3D : Component
{
    public string FloatName { get; set; } = "";
    public string LoadName { get; set; } = "";
    public float RestHeight { get; set; }
    public float Ratio { get; set; } = 1;
    public float LoadMass { get; set; } = 300;
    public float ClosedHeight { get; set; } = 2.5f;
    public float MaximumTravel { get; set; } = 5;
    public float Travel { get; private set; }
    protected override void OnFixedUpdate()
    {
        var scene=GameObject.Scene!;var input=scene.FindGameObject(FloatName);var load=scene.FindGameObject(LoadName);
        if(input?.GetComponent<Rigidbody3D>() is not {} body||load==null)return;
        body.AddForce(new(0,-Math.Max(0,LoadMass)*9.81f*Math.Abs(Ratio),0));
        Travel=Math.Clamp((input.Transform.WorldPosition.Y-RestHeight)*Ratio,0,MaximumTravel);
        var p=load.Transform.WorldPosition;p.Y=ClosedHeight+Travel;load.Transform.WorldPosition=p;
    }
}
