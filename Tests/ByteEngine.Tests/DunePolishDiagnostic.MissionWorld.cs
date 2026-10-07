using System.Numerics;
using System.Text.Json;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;
using DuneCompany;
using DesertTerrain;
namespace ByteEngine.Tests;
internal sealed partial class DunePolishDiagnostic
{
 void AuthorMissionWorld(Scene scene,DesertTerrain3D terrain,string root,string path){
  bool wide=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/MissionWorld/world-layout.json"))).RootElement.GetProperty("worldSize").GetInt32()>1024;bool relocate=wide&&(scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneMinimap3D>().FirstOrDefault()?.WorldSize??1024)<2000;
  terrain.HeightmapPath="Assets/MissionWorld/world-heightmap.png";terrain.PackedSurfaceMaskPath="Assets/MissionWorld/world-packing.png";terrain.HeightmapHeight=wide?32:80;terrain.Cells=512;terrain.Spacing=wide?8:2;terrain.Regenerate();terrain.ResetSand();
  foreach(var obj in scene.GameObjects.Where(o=>o.Name.StartsWith("Blockout - ")).ToArray())scene.DestroyGameObject(obj);
  using var layout=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"Assets/MissionWorld/world-layout.json")));
  Vector3 Ground(float x,float z){terrain.TryGetSand(new(x,0,z),out var s);return s.Position;}
  GameObject Box(string name,float x,float z,Vector3 size,Vector4 color,float elevation=0){if(wide){if(name.StartsWith("Fort")){x-=465;z-=435;}else if(name.StartsWith("Quarry")){x+=502.5f;z-=195;}else if(name.StartsWith("Convoy")||name.StartsWith("Escort")){x-=427.5f;z+=330;}}var o=scene.CreateGameObject("Blockout - "+name);o.Transform.WorldPosition=Ground(x,z)+Vector3.UnitY*(size.Y*.5f+elevation);o.Transform.LocalScale=size;o.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Cube,CastShadows=true,Material=new(){BaseColor=color,Roughness=.94f}});o.AddComponent(new DuneWorldObstacle3D{Size=Vector3.One});return o;}
  var fort=new Vector4(.45f,.36f,.25f,1);Box("Fort north wall",-310,-325,new(70,7,3),fort);Box("Fort west wall",-345,-290,new(3,7,70),fort);Box("Fort east wall",-275,-290,new(3,7,70),fort);Box("Fort entrance west",-333,-255,new(24,7,3),fort);Box("Fort entrance east",-287,-255,new(24,7,3),fort);foreach(float x in new[]{-345f,-275f})foreach(float z in new[]{-325f,-255f})Box("Fort tower",x,z,new(8,11,8),fort);Box("Fort keep",-310,-308,new(18,10,14),new(.35f,.28f,.20f,1));
  for(int i=0;i<6;i++)Box("Quarry bounty cover "+i,315+i%3*18,-142+i/3*22,new(8,3+i%2,5),new(.5f,.4f,.25f,1));Box("Quarry crane mast",358,-160,new(3,16,3),new(.3f,.31f,.28f,1));Box("Quarry crane boom",345,-160,new(30,2,2),new(.3f,.31f,.28f,1),14);
  for(int i=0;i<4;i++)Box("Convoy shelter "+i,-325+i*23,240,new(12,6,10),new(.34f,.40f,.35f,1));Box("Escort loading dock",-285,207,new(28,1,9),new(.45f,.43f,.36f,1));
  var race=layout.RootElement.GetProperty("routes").EnumerateArray().Last().GetProperty("points").EnumerateArray().Select(p=>new Vector2(p[0].GetSingle(),p[1].GetSingle())).ToArray();for(int i=0;i<12;i++){var p=race[i*(race.Length-1)/12];var forward=Vector2.Normalize(race[(i*(race.Length-1)/12+1)%race.Length]-p);var side=new Vector2(-forward.Y,forward.X);foreach(float sign in new[]{-1f,1f})Box("Race gate "+(i+1),p.X+side.X*sign*9,p.Y+side.Y*sign*9,new(1.2f,5,1.2f),new(.85f,.50f,.14f,1));}
  foreach(var route in layout.RootElement.GetProperty("routes").EnumerateArray().Skip(1)){var road=scene.CreateGameObject("Blockout - "+route.GetProperty("name").GetString());road.AddComponent(new DesertRoad3D{Width=18,EndTaperLength=3,Points=string.Join(";",route.GetProperty("points").EnumerateArray().Where(p=>MathF.Sqrt(p[0].GetSingle()*p[0].GetSingle()+p[1].GetSingle()*p[1].GetSingle())>25).Select(p=>p[0].GetSingle().ToString(System.Globalization.CultureInfo.InvariantCulture)+","+p[1].GetSingle().ToString(System.Globalization.CultureInfo.InvariantCulture)))});}
  foreach(var obj in scene.GameObjects.Where(o=>o.Parent==null&&o.Name.StartsWith("Slice - ")&&o.Name is not ("Slice - Distant sand sea" or "Slice - First salvage route"))){if(obj.Children.Count>0){var position=obj.Transform.WorldPosition;if(relocate){if(obj.Name.Contains("Road shoulder marker")){position.X*=2.5f;position.Z*=2.5f;}else if(Vector2.Distance(new(position.X,position.Z),new(160,-360))<150){position.X+=240;position.Z-=540;}}obj.Transform.WorldPosition=Ground(position.X,position.Z);}}
  if(!scene.GameObjects.Any(o=>o.GetComponent<DuneMinimap3D>()!=null))scene.CreateGameObject("Blockout - World minimap").AddComponent(new DuneMinimap3D());
  var minimap=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneMinimap3D>().Single();minimap.WorldSize=wide?4096:1024;
  if(wide){
   scene.ActiveCamera!.FarClip=6000;
   foreach(var sky in scene.GameObjects.SelectMany(o=>o.Components).OfType<SkyEnvironment>()){sky.FogDensity=.00015f;sky.FogMaxOpacity=.45f;sky.Warmth=.06f;}
   var backdrop=scene.FindGameObject("Slice - Distant sand sea");if(backdrop!=null)backdrop.Active=false;
   var route=scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneSalvageRoute3D>().Single();route.SalvageYard=new(400,0,-900);
   scene.FindGameObject("Slice - Haul road - editable points")!.GetComponent<DesertRoad3D>()!.Points=string.Join(";",layout.RootElement.GetProperty("routes")[0].GetProperty("points").EnumerateArray().Select(p=>p[0].GetSingle().ToString(System.Globalization.CultureInfo.InvariantCulture)+","+p[1].GetSingle().ToString(System.Globalization.CultureInfo.InvariantCulture)));
   scene.FindGameObject("Slice - Salvage compacted apron")!.GetComponent<DesertRoad3D>()!.Points="400,-860;400,-940";
   foreach(var target in scene.GameObjects.SelectMany(o=>o.Components).OfType<DuneBountyTarget3D>()){var pos=target.Transform.WorldPosition;if(relocate){pos.X+=240;pos.Z-=540;}target.Transform.WorldPosition=Ground(pos.X,pos.Z);}
  }
  _project!.Scenes.Save(scene,path);Console.WriteLine("PASS Authored whole-map districts, roads, collision proxies and minimap");
 }
}
