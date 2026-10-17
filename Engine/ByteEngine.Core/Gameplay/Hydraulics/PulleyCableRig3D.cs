using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics.ThreeD;

namespace ByteEngine.Core.Gameplay.Hydraulics;

/// <summary>Visible two-to-one cable reeving, following one buoyant float and its own gate.</summary>
public sealed class PulleyCableRig3D : Component
{
 public string FloatName {get;set;}="";
 public string GateName {get;set;}="";
 public float FloorHeight {get;set;}
 public float UpperHeight {get;set;}=6.6f;
 public float GateUpperHeight {get;set;}=6.35f;
 public float GateAttachmentHeight {get;set;}=2.58f;
 public float MovingPulleyOffset {get;set;}=-.7f;
 public float Radius {get;set;}=.025f;
 public float ConnectionError {get;private set;}
 GameObject? buoy,gate;readonly List<CableRenderer3D> cables=[];
 protected override void OnStart(){var s=GameObject.Scene!;buoy=s.FindGameObject(FloatName);gate=s.FindGameObject(GateName);for(int i=0;i<7;i++){var o=s.CreateGameObject(GameObject.Name+" / rope "+i);o.SetParent(GameObject,false);cables.Add(o.AddComponent(new CableRenderer3D{Length=0,Radius=Radius,DrawEndHook=false}));}Apply();}
 protected override void OnLateUpdate()=>Apply();
 void Apply(){if(buoy==null||gate==null||cables.Count!=7)return;var f=buoy.Transform.WorldPosition;var g=gate.Transform.WorldPosition;float y=f.Y+MovingPulleyOffset,z=f.Z,x=f.X;
  void Segment(int i,Vector3 a,Vector3 b){cables[i].Start=a;cables[i].End=b;}
  Segment(0,new(x-.2f,FloorHeight+.2f,z),new(x-.2f,y,z));
  Segment(1,new(x+.2f,y,z),new(x+.2f,FloorHeight+.3f,z));
  Segment(2,new(x+.6f,FloorHeight+.3f,z),new(x+.6f,UpperHeight,z));
  Segment(3,new(x+.6f,UpperHeight,z),new(g.X,GateUpperHeight,g.Z));
  Segment(4,new(g.X,GateUpperHeight,g.Z),g+Vector3.UnitY*GateAttachmentHeight);
  Segment(5,new(x-.2f,y,z),new(x+.2f,y,z));cables[5].UseControlPoint=true;cables[5].ControlPoint=new(x,y+.4f,z);
  Segment(6,new(x+.2f,FloorHeight+.3f,z),new(x+.6f,FloorHeight+.3f,z));cables[6].UseControlPoint=true;cables[6].ControlPoint=new(x+.4f,FloorHeight-.1f,z);
  ConnectionError=Vector3.Distance(cables[4].End,g+Vector3.UnitY*GateAttachmentHeight);
 }
}
