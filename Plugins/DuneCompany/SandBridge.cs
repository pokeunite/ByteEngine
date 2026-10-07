using System.Numerics;
using System.Reflection;
using ByteEngine.Core.Scene;
namespace DuneCompany;
// Optional project-plugin bridge: no terrain assembly is bundled or duplicated.
internal sealed class SandBridge
{
 public ByteEngine.Core.Characters.HeightfieldCollider3D Heightfield=>(ByteEngine.Core.Characters.HeightfieldCollider3D)_terrain;
 readonly Component _terrain;readonly MethodInfo _sample,_stamp;readonly MethodInfo? _wheel;readonly PropertyInfo _position,_normal,_grip,_resistance;
 public SandBridge(Component terrain){_terrain=terrain;var t=terrain.GetType();_sample=t.GetMethod("TryGetSand")!;_stamp=t.GetMethod("StampTrack")!;_wheel=t.GetMethod("StampWheelContact");var s=_sample.GetParameters()[1].ParameterType.GetElementType()!;_position=s.GetProperty("Position")!;_normal=s.GetProperty("Normal")!;_grip=s.GetProperty("Grip")!;_resistance=s.GetProperty("RollingResistance")!;}
 public bool Sample(Vector3 p,out Vector3 ground,out Vector3 normal,out float grip,out float resistance){object?[] args=[p,null];bool found=(bool)_sample.Invoke(_terrain,args)!;if(found){object s=args[1]!;ground=(Vector3)_position.GetValue(s)!;normal=(Vector3)_normal.GetValue(s)!;grip=(float)_grip.GetValue(s)!;resistance=(float)_resistance.GetValue(s)!;}else{ground=p;normal=Vector3.UnitY;grip=1;resistance=.3f;}return found;}
 public bool Wheel(Vector3 a,Vector3 b,float width,float load,float slip,float dt){if(_wheel!=null)return (int)_wheel.Invoke(_terrain,[a,b,width,load,slip,dt])!>0;Track(a,b,width);return true;}
 public ByteEngine.Core.Characters.HeightfieldCollider3D? EnablePatch(Vector3 focus)=>_terrain.GetType().GetMethod("EnableSimulationPatch")?.Invoke(_terrain,[focus]) as ByteEngine.Core.Characters.HeightfieldCollider3D;
 public void FocusPatch(Vector3 focus)=>_terrain.GetType().GetMethod("FocusSimulationPatch")?.Invoke(_terrain,[focus]);
 public void Track(Vector3 a,Vector3 b,float w)=>_stamp.Invoke(_terrain,[a,b,w,.035f]);
}
