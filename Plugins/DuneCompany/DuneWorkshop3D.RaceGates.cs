using System.Numerics;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using ByteEngine.Core.AI;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 readonly List<GameObject> _raceGates=[];Mesh? _raceRingMesh;
 readonly List<int> _raceGateIndices=[];
 void BuildRaceGates(RoutePath3D route)
 {
  var vertices=new List<float>();var indices=new List<uint>();const int segments=64,sides=6;const float radius=4.5f,tube=.09f;
  for(int i=0;i<=segments;i++){float a=MathF.Tau*i/segments;var radial=new Vector3(MathF.Sin(a),MathF.Cos(a),0);for(int j=0;j<sides;j++){float b=MathF.Tau*j/sides;var normal=radial*MathF.Cos(b)+Vector3.UnitZ*MathF.Sin(b);var p=radial*radius+normal*tube;vertices.AddRange([p.X,p.Y,p.Z,normal.X,normal.Y,normal.Z,(float)i/segments,(float)j/sides]);if(i<segments){uint x=(uint)(i*sides+j),y=(uint)(i*sides+(j+1)%sides);indices.AddRange([x,y,x+sides,y,y+sides,x+sides]);}}}
  _raceRingMesh=new(vertices.ToArray(),indices.ToArray());
  for(int i=0;i<route.Points.Count;i++){
   if(DragRace&&i==0)continue;
   for(int lane=0;lane<(DragRace?2:1);lane++){
   var gate=GameObject.Scene!.CreateGameObject("Sprint blue ring "+(i+1)+(DragRace?" lane "+lane:""));gate.Transform.LocalPosition=route.Points[i]+Vector3.UnitY*(DragRace?2.15f:4.4f)+(DragRace?Vector3.UnitX*(lane==0?-2:2):Vector3.Zero);if(DragRace)gate.Transform.LocalScale=new(.48f);
   var direction=route.Points[Math.Min(i+1,route.Points.Count-1)]-route.Points[Math.Max(0,i-1)];direction.Y=0;if(direction.LengthSquared()>.001f){direction=Vector3.Normalize(direction);float yaw=MathF.Atan2(direction.X,direction.Z);gate.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,yaw);}
   gate.AddComponent(new MeshRenderer{Mesh=_raceRingMesh,CastShadows=false,Material=new(){Shading=MaterialShadingMode.Unlit,BaseColor=new(.08f,.65f,1,1)}});_raceGates.Add(gate);_raceGateIndices.Add(i);
   }
  }
 }
 void RefreshRaceGates(){if(_race==null)return;int next=_race.Gate("Player");for(int i=0;i<_raceGates.Count;i++){int index=_raceGateIndices[i];var gate=_raceGates[i];gate.Active=_contractRun&&!Building&&_race.State!=RaceState.Finished&&index>=next&&index<=next+2;gate.GetComponent<MeshRenderer>()!.Material.BaseColor=index==next||DragRace?new(.1f,.75f,1,1):new(.04f,.22f,.52f,1);}}
 void DestroyRaceGates(){foreach(var gate in _raceGates)if(gate.Scene is {} scene)scene.DestroyGameObject(gate);_raceGates.Clear();_raceGateIndices.Clear();_raceRingMesh?.Dispose();_raceRingMesh=null;}
}
