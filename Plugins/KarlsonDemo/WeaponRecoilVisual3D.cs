using System.ComponentModel;
using System.Numerics;
using ByteEngine.Core;
using ByteEngine.Core.Scene;
using Component = ByteEngine.Core.Scene.Component;
namespace KarlsonDemo.Plugin;
/// <summary>Presentation-only kick on a selected weapon model child; never changes the grip socket.</summary>
public sealed class WeaponRecoilVisual3D : Component
{
    [SceneObjectReference,Category("Recoil")] public Guid VisualObjectId { get; set; }
    [Category("Recoil")] public float KickDistance { get; set; } = .025f;
    [Category("Recoil")] public float KickTiltDegrees { get; set; } = 2;
    [Category("Recoil")] public float ReturnSharpness { get; set; } = 25;
    private GameObject? _visual; private Vector3 _position; private Quaternion _rotation; private int _shots; private float _kick;
    protected override void OnStart()
    {
        _visual=GameObject.Scene?.FindGameObject(VisualObjectId);
        if(_visual==GameObject||_visual?.IsDescendantOf(GameObject)!=true){_visual=null;return;}
        _position=_visual.Transform.LocalPosition;_rotation=_visual.Transform.LocalRotation;_shots=GameObject.GetComponent<WeaponInstance3D>()?.ShotsFired??0;
    }
    protected override void OnLateUpdate()
    {
        if(_visual==null||GameObject.GetComponent<WeaponInstance3D>() is not {} weapon)return;
        _kick*=MathF.Exp(-WeaponDefinition3D.Bound(ReturnSharpness,25,1,100)*Math.Clamp((float)Time.DeltaTime,0,.1f));
        if(weapon.ShotsFired!=_shots){_shots=weapon.ShotsFired;_kick=Math.Min(1.5f,_kick+1);}
        var parent=_visual.Parent!;var actor=weapon.InventoryOwner?.GameObject;
        Vector3 backward=-(actor?.Transform.Forward??Transform.Forward);
        Vector3 local=Vector3.Transform(backward,Quaternion.Inverse(parent.Transform.WorldRotation));
        var scale=parent.Transform.WorldScale;
        local=new(local.X/Math.Max(Math.Abs(scale.X),.00001f),local.Y/Math.Max(Math.Abs(scale.Y),.00001f),local.Z/Math.Max(Math.Abs(scale.Z),.00001f));
        _visual.Transform.LocalPosition=_position+local*WeaponDefinition3D.Bound(KickDistance,.025f,0,.2f)*_kick;
        var axis=Vector3.Transform(actor?.Transform.Right??Transform.Right,Quaternion.Inverse(parent.Transform.WorldRotation));
        _visual.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis),WeaponDefinition3D.Bound(KickTiltDegrees,2,0,15)*_kick*MathF.PI/180)*_rotation;
    }
    protected override void OnStop(){if(_visual!=null){_visual.Transform.LocalPosition=_position;_visual.Transform.LocalRotation=_rotation;}_kick=0;}
}
