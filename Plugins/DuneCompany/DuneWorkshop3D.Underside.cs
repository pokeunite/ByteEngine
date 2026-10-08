using System.Numerics;
using ByteEngine.Core.Scene;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Gameplay;
namespace DuneCompany;
public sealed partial class DuneWorkshop3D
{
 readonly Dictionary<Component,bool> _hiddenGround=[];
 void RestoreGroundVisibility(){foreach(var pair in _hiddenGround)pair.Key.Enabled=pair.Value;_hiddenGround.Clear();}
 void UpdateUndersideVisibility(Vector3 camera){RestoreGroundVisibility();if(_sand==null||!_sand.Sample(camera,out var ground,out _,out _,out _)||camera.Y>=ground.Y+.08f)return;foreach(var c in GameObject.Scene!.GameObjects.SelectMany(o=>o.Components).Where(c=>c is InteractiveSand3D||c.GetType().FullName=="DesertTerrain.DesertTerrain3D"||c is MeshRenderer&&c.GameObject.Name.Contains("Floor",StringComparison.OrdinalIgnoreCase))){_hiddenGround[c]=c.Enabled;c.Enabled=false;}}
}
