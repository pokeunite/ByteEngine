
using System.Numerics;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;
using ByteEngine.Core.Characters;
namespace DuneCompany;

public readonly record struct PartBounds(Vector3 Low,Vector3 High){public Vector3 Center=>(Low+High)*.5f;public Vector3 Size=>Vector3.Max(High-Low,new Vector3(.035f));}
public sealed record VehiclePartPhysics(PartBounds Fixed,PartBounds Moving,Vector3 Pivot,Vector3 Axis,bool Articulated);

/// <summary>One rigid body per rigid assembly island, plus constrained mechanism outputs and rolling tyres.
/// All motion comes from gravity, contacts and bounded joint motors. No terrain snapping or imposed chassis yaw.</summary>
public sealed partial class LandVehiclePhysics:IDisposable
{
 const int TerrainChunkCells=16;
 readonly float FixedStep;
 public int SimulationHz {get;}
 public int MaximumCatchUpSteps {get;}
 public double DroppedSimulationSeconds {get;private set;}
 readonly BufferPool _pool=new();readonly Simulation _simulation;readonly ThreadDispatcher? _dispatcher;
 Part[] _wheels=[];PlacedBlock[] _engines=[];float _axleMid,_mass;
 readonly List<Group> _groups=[];readonly Dictionary<int,Part> _parts=[];
 readonly Dictionary<int,float> _friction=[];readonly ContactCache _contacts=new();
 readonly HashSet<ulong> _ignored=[];readonly Dictionary<int,StaticHandle> _obstacles=[];
 readonly IReadOnlyDictionary<int,DunePart> _catalog;readonly List<PlacedBlock> _blocks;
 HeightfieldCollider3D? _sandPatch;Vector2? _patchMinimum;readonly Dictionary<(int x,int z),(StaticHandle body,TypedIndex shape)> _fineChunks=[];readonly HashSet<(int x,int z)> _fineDirty=[];readonly Dictionary<(int x,int z),float> _collisionHeights=[];readonly HashSet<(int x,int z)> _terrainHoles=[];
 HeightfieldCollider3D? _terrain;readonly Dictionary<(int x,int z),(StaticHandle body,TypedIndex shape)> _terrainChunks=[];readonly HashSet<(int x,int z)> _dirtyTerrain=[];float _terrainTimer;
 readonly Vector3 _origin;readonly Quaternion _rotation;float _accumulator,_servo,_piston,_elapsed,_driftBlend;bool _disposed;
 sealed class Group {public readonly List<(TypedIndex shape,RigidPose pose,float mass,BodyInertia inertia)> Shapes=[];public BodyHandle Body;public Vector3 Center;public float Mass;}
 sealed class Part {public required PlacedBlock Block;public required VehiclePartPhysics Definition;public required Group Root;public Group? Output;public ConstraintHandle? Motor,Servo,Slider;public Vector3 Axis,OffsetA,OffsetB;public Quaternion RestRelative;public float MotorTarget;}
 readonly Dictionary<int,RigidPose> _previousPoses=[];
 public double TerrainMilliseconds {get;private set;}
 public double ControlsMilliseconds {get;private set;}
 public double SolverMilliseconds {get;private set;}
 public double CollisionMilliseconds {get;private set;}
 public double ConstraintMilliseconds {get;private set;}
 long _phaseStart;
 // Collision callbacks may run on desktop workers. Retain storage between ticks;
 // ConcurrentDictionary.Clear replaces its buckets/locks and contact writes allocate nodes.
 sealed class ContactCache {
  readonly Dictionary<int,(Vector3 Normal,float Depth)> _values=new(32);
  readonly object _gate=new();
  public int Count=>_values.Count;
  public bool ContainsKey(int key)=>_values.ContainsKey(key);
  public bool TryGetValue(int key,out (Vector3 Normal,float Depth) value)=>_values.TryGetValue(key,out value);
  public (Vector3 Normal,float Depth) this[int key]{set{lock(_gate)_values[key]=value;}}
  public void Clear()=>_values.Clear();
 }

 public long StepAllocatedBytes {get;private set;}
 public int TerrainRebuilds {get;private set;}
 public int FineRebuilds {get;private set;}
 public int SolverIterations=>_simulation.Solver.VelocityIterationCount;
 public int SolverSubsteps=>_simulation.Solver.SubstepCount;
 public void SetSolverProfile(int iterations,int substeps){if(iterations<1||iterations>16||substeps<1||substeps>8)throw new ArgumentOutOfRangeException();_simulation.Solver.VelocityIterationCount=iterations;_simulation.Solver.SubstepCount=substeps;}
 public int FineTriangleCount=>_fineTriangleCounts.Values.Sum();
 readonly Dictionary<(int x,int z),int> _fineTriangleCounts=[];
 public int StaticColliderCount=>_terrainChunks.Count+_fineChunks.Count+_obstacles.Count;
 public int LastStepCount {get;private set;}
 RigidPose RenderPose(Group g){var current=_simulation.Bodies[g.Body].Pose;if(!_previousPoses.TryGetValue(g.Body.Value,out var previous))return current;float alpha=Math.Clamp(_accumulator/FixedStep,0,1);return new(Vector3.Lerp(previous.Position,current.Position,alpha),Quaternion.Slerp(previous.Orientation,current.Orientation,alpha));}
 public (Vector3 Position,Quaternion Rotation) RenderFrame {get{var g=_parts[0].Root;var p=RenderPose(g);return(p.Position-Vector3.Transform(g.Center,p.Orientation),p.Orientation);}}
 public (Vector3 Position,Quaternion Rotation) RenderPartPose(int id,bool output=false){var p=_parts[id];var g=output&&p.Output!=null?p.Output:p.Root;var body=RenderPose(g);return(body.Position+Vector3.Transform(p.Block.P-g.Center,body.Orientation),Quaternion.Normalize(body.Orientation*p.Block.Q));}
 public Matrix4x4 RenderOutputDeformation(int id){if(_parts[id].Output==null)return Matrix4x4.Identity;var a=RenderPartPose(id);var b=RenderPartPose(id,true);Matrix4x4.Invert(Matrix4x4.CreateFromQuaternion(a.Rotation)*Matrix4x4.CreateTranslation(a.Position),out var inverse);return Matrix4x4.CreateFromQuaternion(b.Rotation)*Matrix4x4.CreateTranslation(b.Position)*inverse;}
 public float DriftBlend=>_driftBlend;
 public float WheelGrip(int id)=>_parts[id].Output is {} output&&_friction.TryGetValue(output.Body.Value,out var grip)?grip:0;
 public int BodyCount=>_groups.Count;public int ContactCount=>_contacts.Count;public float Elapsed=>_elapsed;
 public Vector3 Velocity=>_simulation.Bodies[_parts[0].Root.Body].Velocity.Linear;
 public (Vector3 Position,Quaternion Rotation) Frame {get{var g=_parts[0].Root;var p=_simulation.Bodies[g.Body].Pose;return(p.Position-Vector3.Transform(g.Center,p.Orientation),p.Orientation);}}
 public LandVehiclePhysics(IReadOnlyList<PlacedBlock> blocks,IReadOnlyDictionary<int,DunePart> catalog,IReadOnlyDictionary<int,VehiclePartPhysics> definitions,Vector3 origin,Quaternion rotation,HeightfieldCollider3D? terrain=null,int simulationHz=0,int maximumCatchUpSteps=0)
 {
  SimulationHz=simulationHz==0?60:simulationHz;
  MaximumCatchUpSteps=maximumCatchUpSteps==0?(OperatingSystem.IsBrowser()?2:15):Math.Clamp(maximumCatchUpSteps,1,15);
  if(SimulationHz is not (40 or 60))throw new ArgumentOutOfRangeException(nameof(simulationHz));
  FixedStep=1f/SimulationHz;
  int workers=OperatingSystem.IsBrowser()?1:Environment.GetEnvironmentVariable("DUNE_PHYSICS_THREADS")=="1"?1:Math.Min(2,Environment.ProcessorCount);if(workers>1)_dispatcher=new ThreadDispatcher(workers);
  _blocks=blocks.ToList();_catalog=catalog;_origin=origin;_rotation=rotation;
  int iterations=int.TryParse(Environment.GetEnvironmentVariable("DUNE_SOLVER_ITERATIONS"),out var iterationOverride)?iterationOverride:(OperatingSystem.IsBrowser()?6:8);
  int substeps=int.TryParse(Environment.GetEnvironmentVariable("DUNE_SOLVER_SUBSTEPS"),out var substepOverride)?substepOverride:(OperatingSystem.IsBrowser()?2:240/SimulationHz);
  _simulation=Simulation.Create(_pool,new Contacts(_ignored,_friction,_contacts),new Gravity(),new SolveDescription(Math.Clamp(iterations,1,16),Math.Clamp(substeps,1,8)));
  var timestepper=(DefaultTimestepper)_simulation.Timestepper;
  timestepper.BeforeCollisionDetection+=(dt,dispatcher)=>_phaseStart=System.Diagnostics.Stopwatch.GetTimestamp();
  timestepper.CollisionsDetected+=(dt,dispatcher)=>{CollisionMilliseconds+=System.Diagnostics.Stopwatch.GetElapsedTime(_phaseStart).TotalMilliseconds;_phaseStart=System.Diagnostics.Stopwatch.GetTimestamp();};
  timestepper.ConstraintsSolved+=(dt,dispatcher)=>ConstraintMilliseconds+=System.Diagnostics.Stopwatch.GetElapsedTime(_phaseStart).TotalMilliseconds;
  if(terrain!=null)AddTerrain(terrain);
  AddAssembly(_blocks,definitions,origin,rotation);
  _wheels=_parts.Values.Where(p=>_catalog[p.Block.Type].Wheel).ToArray();_engines=_blocks.Where(b=>b.Type is 23 or 24).ToArray();_mass=_groups.Sum(g=>g.Mass);_axleMid=_wheels.Length>0?(_wheels.Min(p=>p.Block.P.Z)+_wheels.Max(p=>p.Block.P.Z))*.5f:0;
 }
 void AddAssembly(IReadOnlyList<PlacedBlock> blocks,IReadOnlyDictionary<int,VehiclePartPhysics> definitions,Vector3 origin,Quaternion rotation)
 {
  int firstGroup=_groups.Count;
  foreach(var b in blocks){var definition=definitions[b.Type];Group root;
   if(b.Parent<0){root=new();_groups.Add(root);}else{var parent=_parts[b.Parent];root=b.MovingMount&&parent.Output!=null?parent.Output:parent.Root;}
   var p=new Part{Block=b,Definition=definition,Root=root};_parts[b.Id]=p;
   bool split=definition.Articulated;AddBox(root,definition.Fixed,b.P,b.Q,_catalog[b.Type].Mass*(split?.3f:1));
   if(split){var output=new Group();_groups.Add(output);p.Output=output;
    if(_catalog[b.Type].Wheel){var d=_catalog[b.Type];var shape=new Cylinder(d.Radius,Math.Max(.12f,definition.Moving.Size.Z));var align=Align(Vector3.UnitY,Vector3.UnitZ);AddShape(output,_simulation.Shapes.Add(shape),new(b.P+Vector3.Transform(new(0,0,d.WheelCenterZ),b.Q),Quaternion.Normalize(b.Q*align)),shape.ComputeInertia(d.Mass*.7f),d.Mass*.7f);}
    else AddBox(output,definition.Moving,b.P,b.Q,_catalog[b.Type].Mass*.7f);
   }
  }
  foreach(var g in _groups.Skip(firstGroup)){using var builder=new CompoundBuilder(_pool,_simulation.Shapes,g.Shapes.Count);foreach(var s in g.Shapes)builder.Add(s.shape,s.pose,s.inertia);builder.BuildDynamicCompound(out var children,out var inertia,out var center);g.Center=center;g.Body=_simulation.Bodies.Add(BodyDescription.CreateDynamic(new(origin+Vector3.Transform(center,rotation),rotation),inertia,_simulation.Shapes.Add(new Compound(children)),new BodyActivityDescription(.005f)));}
  foreach(var p in blocks.Select(b=>_parts[b.Id]).Where(p=>p.Output!=null))AddJoint(p);
 }
 static Quaternion Align(Vector3 a,Vector3 b){float dot=Vector3.Dot(a,b);return dot>.9999f?Quaternion.Identity:dot<-.9999f?Quaternion.CreateFromAxisAngle(Vector3.UnitX,MathF.PI):Quaternion.Normalize(new(Vector3.Cross(a,b),1+dot));}
 void AddShape(Group g,TypedIndex shape,RigidPose pose,BodyInertia inertia,float mass){g.Shapes.Add((shape,pose,mass,inertia));g.Mass+=mass;}
 void AddBox(Group g,PartBounds bounds,Vector3 position,Quaternion rotation,float mass){var size=bounds.Size;var shape=new Box(size.X,size.Y,size.Z);mass=Math.Max(.5f,mass);AddShape(g,_simulation.Shapes.Add(shape),new(position+Vector3.Transform(bounds.Center,rotation),rotation),shape.ComputeInertia(mass),mass);}
 void AddJoint(Part p)
 {
  var a=p.Root;var b=p.Output!;var part=p.Block;var def=p.Definition;var pivot=part.P+Vector3.Transform(def.Pivot,part.Q);
  p.OffsetA=pivot-a.Center;p.OffsetB=pivot-b.Center;p.Axis=Vector3.Normalize(Vector3.Transform(def.Axis,part.Q));p.RestRelative=Quaternion.Identity;_ignored.Add(Pair(a.Body,b.Body));
  if(_catalog[part.Type].Wheel||part.Type is 18 or 19 or 20){
   _simulation.Solver.Add(a.Body,b.Body,new Hinge{LocalOffsetA=p.OffsetA,LocalOffsetB=p.OffsetB,LocalHingeAxisA=p.Axis,LocalHingeAxisB=p.Axis,SpringSettings=new(_catalog[part.Type].Wheel?90:60,1)});
   if(_catalog[part.Type].Wheel){p.Motor=_simulation.Solver.Add(a.Body,b.Body,new AngularAxisMotor{LocalAxisA=p.Axis,Settings=new(0,.0001f)});_friction[b.Body.Value]=part.Grip;}
   else if(part.Type==20)_simulation.Solver.Add(a.Body,b.Body,new AngularAxisMotor{LocalAxisA=p.Axis,TargetVelocity=0,Settings=new(120,.002f)});
   else if(part.Type is 18 or 19)p.Servo=_simulation.Solver.Add(a.Body,b.Body,new AngularServo{TargetRelativeRotationLocalA=Quaternion.Identity,SpringSettings=new(30,1),ServoSettings=new(3,0,6000)});
  }else if(part.Type is 16 or 17 or 21 or 32){
   _simulation.Solver.Add(a.Body,b.Body,new PointOnLineServo{LocalOffsetA=p.OffsetA,LocalOffsetB=p.OffsetB,LocalDirection=p.Axis,SpringSettings=new(120,1),ServoSettings=new(30,0,120000)});
   _simulation.Solver.Add(a.Body,b.Body,new AngularServo{TargetRelativeRotationLocalA=Quaternion.Identity,SpringSettings=new(120,1),ServoSettings=new(12,0,120000)});
   float travel=part.Type==21?part.Stroke:part.Travel;
   _simulation.Solver.Add(a.Body,b.Body,new LinearAxisLimit{LocalOffsetA=p.OffsetA,LocalOffsetB=p.OffsetB,LocalAxis=p.Axis,MinimumOffset=part.Type==21?0:-travel,MaximumOffset=part.Type==21?travel:travel*.15f,SpringSettings=new(40,1)});
   p.Slider=_simulation.Solver.Add(a.Body,b.Body,new LinearAxisServo{LocalOffsetA=p.OffsetA,LocalOffsetB=p.OffsetB,LocalPlaneNormal=p.Axis,TargetOffset=0,SpringSettings=new(part.Type==21?20:part.SpringRate,part.Type==21?1:part.Damping),ServoSettings=new(part.Type==21?1:4,0,part.Type==21?8000:20000)});
  }else _simulation.Solver.Add(a.Body,b.Body,new Weld{LocalOffset=b.Center-a.Center,LocalOrientation=Quaternion.Identity,SpringSettings=new(60,1)});
 }
 public void AddTerrain(HeightfieldCollider3D terrain)
 {
  _terrain=terrain;for(int z=0;z<terrain.Rows-1;z+=TerrainChunkCells)for(int x=0;x<terrain.Columns-1;x+=TerrainChunkCells)BuildTerrainChunk(x,z);
 }
 void BuildTerrainChunk(int ox,int oz)
 {
  TerrainRebuilds++;
  var terrain=_terrain!;var key=(ox,oz);if(_terrainChunks.Remove(key,out var previous)){_simulation.Statics.Remove(previous.body);_simulation.Shapes.RemoveAndDispose(previous.shape,_pool);}
  int width=Math.Min(TerrainChunkCells,terrain.Columns-1-ox),depth=Math.Min(TerrainChunkCells,terrain.Rows-1-oz);_pool.Take<Triangle>(width*depth*2,out var triangles);int n=0;
  var surface=terrain.SurfaceMatrix;var grid=terrain.GridMinimum;float spacing=terrain.CellSpacing;var vertices=new Vector3[(width+1)*(depth+1)];for(int z=0;z<=depth;z++)for(int x=0;x<=width;x++)vertices[z*(width+1)+x]=Vector3.Transform(new(grid.X+(ox+x)*spacing,terrain.HeightAt(ox+x,oz+z),grid.Y+(oz+z)*spacing),surface);
  Matrix4x4 patchInverse=Matrix4x4.Identity;if(_sandPatch!=null)Matrix4x4.Invert(_sandPatch.SurfaceMatrix,out patchInverse);
  Vector3 Point(int x,int z)=>vertices[(z-oz)*(width+1)+x-ox];
  for(int z=oz;z<oz+depth;z++)for(int x=ox;x<ox+width;x++){var a=Point(x,z);if(_sandPatch!=null){var mid=(Point(x,z)+Point(x+1,z+1))*.5f;var local=Vector3.Transform(mid,patchInverse);var min=_sandPatch.GridMinimum;float size=(_sandPatch.Columns-1)*_sandPatch.CellSpacing;if(local.X>min.X&&local.X<min.X+size&&local.Z>min.Y&&local.Z<min.Y+size)continue;}var b=Point(x+1,z);var c=Point(x,z+1);var d=Point(x+1,z+1);// Bepu mesh contacts require clockwise winding when viewed from above in right-handed space.
   triangles[n++]=new(){A=a,B=b,C=c};triangles[n++]=new(){A=b,B=d,C=c};}
  if(n==0){_pool.Return(ref triangles);_terrainHoles.Add(key);return;}_terrainHoles.Remove(key);_pool.Take<Triangle>(n,out var exact);for(int i=0;i<n;i++)exact[i]=triangles[i];_pool.Return(ref triangles);triangles=exact;var mesh=new BepuPhysics.Collidables.Mesh(triangles,Vector3.One,_pool);var shape=_simulation.Shapes.Add(mesh);var body=_simulation.Statics.Add(new StaticDescription(Vector3.Zero,shape));_terrainChunks[key]=(body,shape);
 }
 public void SetSandPatch(HeightfieldCollider3D? patch){_sandPatch=patch;SyncSandPatch();}
 float PatchWidth=>(_sandPatch!.Columns-1)*_sandPatch.CellSpacing;
 float FineTileSize=>16*(_sandPatch?.CellSpacing??.25f);
 void SyncSandPatch(){if(_sandPatch==null||_terrain==null||_patchMinimum==_sandPatch.GridMinimum)return;var old=_patchMinimum;_patchMinimum=_sandPatch.GridMinimum;
  foreach(var key in _fineChunks.Keys.ToArray()){float x=key.x*FineTileSize,z=key.z*FineTileSize;if(x<_patchMinimum.Value.X||x>=_patchMinimum.Value.X+PatchWidth||z<_patchMinimum.Value.Y||z>=_patchMinimum.Value.Y+PatchWidth){var chunk=_fineChunks[key];_simulation.Statics.Remove(chunk.body);_simulation.Shapes.RemoveAndDispose(chunk.shape,_pool);_fineChunks.Remove(key);_fineTriangleCounts.Remove(key);}}
  for(int z=0;z<_terrain.Rows-1;z+=TerrainChunkCells)for(int x=0;x<_terrain.Columns-1;x+=TerrainChunkCells){float px=_terrain.GridMinimum.X+x*_terrain.CellSpacing,pz=_terrain.GridMinimum.Y+z*_terrain.CellSpacing,w=TerrainChunkCells*_terrain.CellSpacing;bool Overlap(Vector2 min)=>px<min.X+PatchWidth&&px+w>min.X&&pz<min.Y+PatchWidth&&pz+w>min.Y;if(Overlap(_patchMinimum.Value)||(old.HasValue&&Overlap(old.Value)))BuildTerrainChunk(x,z);}
  for(int z=(int)(_patchMinimum.Value.Y/FineTileSize);z<(int)(_patchMinimum.Value.Y/FineTileSize)+(int)(PatchWidth/FineTileSize);z++)for(int x=(int)(_patchMinimum.Value.X/FineTileSize);x<(int)(_patchMinimum.Value.X/FineTileSize)+(int)(PatchWidth/FineTileSize);x++)if(!_fineChunks.ContainsKey((x,z)))BuildFineChunk(x,z);
 }
 void BuildFineChunk(int tx,int tz){if(_sandPatch==null)return;FineRebuilds++;var key=(tx,tz);if(_fineChunks.Remove(key,out var previous)){_simulation.Statics.Remove(previous.body);_simulation.Shapes.RemoveAndDispose(previous.shape,_pool);}var patch=_sandPatch;int ox=(int)MathF.Round((tx*FineTileSize-patch.GridMinimum.X)/patch.CellSpacing),oz=(int)MathF.Round((tz*FineTileSize-patch.GridMinimum.Y)/patch.CellSpacing);int cells=16,stride=cells+1;_pool.Take<Triangle>(cells*cells*2,out var triangles);int n=0;var surface=patch.SurfaceMatrix;var grid=patch.GridMinimum;float spacing=patch.CellSpacing;var vertices=new Vector3[stride*stride];for(int z=0;z<=cells;z++)for(int x=0;x<=cells;x++)vertices[z*stride+x]=Vector3.Transform(new(grid.X+(ox+x)*spacing,patch.HeightAt(ox+x,oz+z),grid.Y+(oz+z)*spacing),surface);Vector3 Point(int x,int z)=>vertices[(z-oz)*stride+x-ox];// Merge only regions whose sampled surface stays within one millimetre
 // of the original triangulation. Ruts and slopes subdivide automatically.
 void Tile(int x,int z,int size){
  var a=Point(x,z);var b=Point(x+size,z);var c=Point(x,z+size);var d=Point(x+size,z+size);bool flat=true;
  if(size>1)for(int dz=0;dz<=size&&flat;dz++)for(int dx=0;dx<=size;dx++){
   float u=(float)dx/size,v=(float)dz/size;
   var expected=u+v<=1?a+u*(b-a)+v*(c-a):d+(1-u)*(c-d)+(1-v)*(b-d);
   if(Vector3.DistanceSquared(Point(x+dx,z+dz),expected)>1e-6f){flat=false;break;}
  }
  if(flat||size==1){triangles[n++]=new(){A=a,B=b,C=c};triangles[n++]=new(){A=b,B=d,C=c};}
  else{int half=size/2;Tile(x,z,half);Tile(x+half,z,half);Tile(x,z+half,half);Tile(x+half,z+half,half);}
 }
 Tile(ox,oz,cells);_fineTriangleCounts[key]=n;_pool.Take<Triangle>(n,out var exact);for(int i=0;i<n;i++)exact[i]=triangles[i];_pool.Return(ref triangles);triangles=exact;var mesh=new BepuPhysics.Collidables.Mesh(triangles,Vector3.One,_pool);var shape=_simulation.Shapes.Add(mesh);_fineChunks[key]=(_simulation.Statics.Add(new StaticDescription(Vector3.Zero,shape)),shape);}
 public void MarkTerrainChanged(Vector3 position)
 {
  if(_terrain==null)return;if(_sandPatch!=null){Matrix4x4.Invert(_sandPatch.SurfaceMatrix,out var patchInverse);var point=Vector3.Transform(position,patchInverse);int tx=(int)MathF.Floor(point.X/FineTileSize),tz=(int)MathF.Floor(point.Z/FineTileSize);int gx=(int)MathF.Round((point.X-_sandPatch.GridMinimum.X)/_sandPatch.CellSpacing),gz=(int)MathF.Round((point.Z-_sandPatch.GridMinimum.Y)/_sandPatch.CellSpacing);var sampleKey=((int)MathF.Round(point.X/_sandPatch.CellSpacing),(int)MathF.Round(point.Z/_sandPatch.CellSpacing));float height=_sandPatch.HeightAt(Math.Clamp(gx,0,_sandPatch.Columns-1),Math.Clamp(gz,0,_sandPatch.Rows-1));if(_collisionHeights.TryGetValue(sampleKey,out float previousHeight)&&Math.Abs(height-previousHeight)<.012f)return;_collisionHeights[sampleKey]=height;
  for(int fz=(int)MathF.Floor((point.Z-1)/FineTileSize);fz<=(int)MathF.Floor((point.Z+1)/FineTileSize);fz++)for(int fx=(int)MathF.Floor((point.X-1)/FineTileSize);fx<=(int)MathF.Floor((point.X+1)/FineTileSize);fx++)if(_fineChunks.ContainsKey((fx,fz)))_fineDirty.Add((fx,fz));return;}Matrix4x4.Invert(_terrain.SurfaceMatrix,out var inverse);var local=Vector3.Transform(position,inverse);float x=(local.X-_terrain.GridMinimum.X)/_terrain.CellSpacing,z=(local.Z-_terrain.GridMinimum.Y)/_terrain.CellSpacing;
  for(int dz=-2;dz<=2;dz+=2)for(int dx=-2;dx<=2;dx+=2){int cx=(int)MathF.Floor((x+dx)/TerrainChunkCells)*TerrainChunkCells,cz=(int)MathF.Floor((z+dz)/TerrainChunkCells)*TerrainChunkCells;if(cx>=0&&cz>=0&&cx<_terrain.Columns-1&&cz<_terrain.Rows-1)_dirtyTerrain.Add((cx,cz));}
 }
 public void MarkAllTerrainChanged(){foreach(var key in _terrainChunks.Keys.Concat(_terrainHoles))_dirtyTerrain.Add(key);foreach(var key in _fineChunks.Keys)_fineDirty.Add(key);}
 public void AddFlatGround(float height=0)=>AddObstacle(-1,new(0,height-.5f,0),new(1000,1,1000),Quaternion.Identity);
 public void AddObstacle(int id,Vector3 center,Vector3 size,Quaternion rotation){size=Vector3.Max(size,new(.05f));_obstacles[id]=_simulation.Statics.Add(new StaticDescription(new RigidPose(center,rotation),_simulation.Shapes.Add(new Box(size.X,size.Y,size.Z))));}
 public void MoveObstacle(int id,Vector3 position,Quaternion rotation){if(!_obstacles.TryGetValue(id,out var handle))return;var current=_simulation.Statics[handle];if(Vector3.DistanceSquared(current.Pose.Position,position)<1e-10f&&Math.Abs(Quaternion.Dot(current.Pose.Orientation,rotation))>.999999f)return;_simulation.Statics.ApplyDescription(handle,new StaticDescription(new RigidPose(position,rotation),current.Shape));}
 public void RemoveObstacle(int id){if(_obstacles.Remove(id,out var handle))_simulation.Statics.Remove(handle);}
 public (Vector3 Position,Quaternion Rotation) PartPose(int id){var p=_parts[id];var body=_simulation.Bodies[p.Root.Body].Pose;return(body.Position+Vector3.Transform(p.Block.P-p.Root.Center,body.Orientation),Quaternion.Normalize(body.Orientation*p.Block.Q));}
 public (Vector3 Position,Quaternion Rotation) OutputPose(int id){var p=_parts[id];if(p.Output==null)return PartPose(id);var body=_simulation.Bodies[p.Output.Body].Pose;return(body.Position+Vector3.Transform(p.Block.P-p.Output.Center,body.Orientation),Quaternion.Normalize(body.Orientation*p.Block.Q));}
 public Matrix4x4 OutputDeformation(int id){var p=_parts[id];if(p.Output==null)return Matrix4x4.Identity;var body=_simulation.Bodies[p.Output.Body].Pose;var pos=body.Position+Vector3.Transform(p.Block.P-p.Output.Center,body.Orientation);var q=Quaternion.Normalize(body.Orientation*p.Block.Q);var root=PartPose(id);Matrix4x4.Invert(Matrix4x4.CreateFromQuaternion(root.Rotation)*Matrix4x4.CreateTranslation(root.Position),out var inverse);return Matrix4x4.CreateFromQuaternion(q)*Matrix4x4.CreateTranslation(pos)*inverse;}
 public readonly record struct TyreFeedback(Vector3 Center,Vector3 Contact,Vector3 Normal,Vector3 Velocity,bool Grounded,float Speed,float Slip,float Rpm);
 public TyreFeedback WheelFeedback(int id)
 {
  var p=_parts[id];if(p.Output==null)return default;
  var output=_simulation.Bodies[p.Output.Body];var root=_simulation.Bodies[p.Root.Body];var pose=OutputPose(id);var definition=_catalog[p.Block.Type];
  // Use the cylinder's physical centre, not the asymmetrical visual bounds (hub included).
  var center=pose.Position+Vector3.Transform(new Vector3(0,0,definition.WheelCenterZ),pose.Rotation);
  var axle=Vector3.Normalize(Vector3.Transform(p.Axis,root.Pose.Orientation));
  bool grounded=_contacts.TryGetValue(p.Output.Body.Value,out var c)&&Math.Abs(c.Normal.Y)>.35f;
  var normal=grounded?Vector3.Normalize(c.Normal):Vector3.UnitY;if(normal.Y<0)normal=-normal;
  var contact=center-normal*definition.Radius;
  // Sleeping bodies and speculative contacts do not necessarily report a penetrating manifold.
  // Confirm support against the same collision surface; this affects feedback only, never chassis pose.
  var surface=_sandPatch??_terrain;
  if(surface!=null&&surface.TrySampleWorld(center,out var ground,out var terrainNormal))
  {
   float gap=Vector3.Dot(center-ground,terrainNormal)-definition.Radius;
   if(gap>=-.12f&&gap<=.07f&&terrainNormal.Y>.5f){grounded=true;normal=terrainNormal;contact=ground;}
   else if(gap>.12f)grounded=false;
  }
  float side=Math.Abs(Vector3.Dot(output.Velocity.Linear,axle));float linear=new Vector2(output.Velocity.Linear.X,output.Velocity.Linear.Z).Length();
  float rolling=Math.Abs(Vector3.Dot(output.Velocity.Angular-root.Velocity.Angular,axle))*definition.Radius;
  return new(center,contact,normal,output.Velocity.Linear,grounded,linear,Math.Max(side,Math.Abs(rolling-linear)),WheelRpm(id));
 }

 public float WheelAxisError(int id){var p=_parts[id];if(p.Output==null)return 0;var a=Vector3.Normalize(Vector3.Transform(p.Axis,_simulation.Bodies[p.Root.Body].Pose.Orientation));var b=Vector3.Normalize(Vector3.Transform(p.Axis,_simulation.Bodies[p.Output.Body].Pose.Orientation));return MathF.Acos(Math.Clamp(Vector3.Dot(a,b),-1,1))*180/MathF.PI;}
 public float WheelRpm(int id){var p=_parts[id];if(p.Output==null)return 0;var root=_simulation.Bodies[p.Root.Body];return Vector3.Dot(_simulation.Bodies[p.Output.Body].Velocity.Angular-root.Velocity.Angular,Vector3.Transform(p.Axis,root.Pose.Orientation))*60/MathF.Tau;}
 public float SuspensionLateralError(int id){var p=_parts[id];if(p.Output==null)return 0;var a=_simulation.Bodies[p.Root.Body].Pose;var b=_simulation.Bodies[p.Output.Body].Pose;var delta=b.Position+Vector3.Transform(p.OffsetB,b.Orientation)-a.Position-Vector3.Transform(p.OffsetA,a.Orientation);var axis=Vector3.Transform(p.Axis,a.Orientation);return (delta-axis*Vector3.Dot(delta,axis)).Length();}
 public float SuspensionOffset(int id){var p=_parts[id];if(p.Output==null)return 0;var a=_simulation.Bodies[p.Root.Body].Pose;var b=_simulation.Bodies[p.Output.Body].Pose;return Vector3.Dot(b.Position+Vector3.Transform(p.OffsetB,b.Orientation)-a.Position-Vector3.Transform(p.OffsetA,a.Orientation),Vector3.Transform(p.Axis,a.Orientation));}
 public void Impulse(Vector3 impulse){_simulation.Bodies[_parts[0].Root.Body].ApplyLinearImpulse(impulse);}
 public void Step(float dt,float throttle,float steering,bool brake,float maximumSpeed,float servo=0,float piston=0,Func<Vector3,float>? sandGrip=null,bool handbrake=false,Func<Vector3,float>? sandResistance=null)
 {
  if(_disposed||!float.IsFinite(dt)||dt<=0)return;_accumulator=Math.Min(_accumulator+dt,.25f);
  TerrainMilliseconds=ControlsMilliseconds=SolverMilliseconds=CollisionMilliseconds=ConstraintMilliseconds=0;TerrainRebuilds=FineRebuilds=0;long allocatedBefore=GC.GetAllocatedBytesForCurrentThread();
  LastStepCount=0;while(_accumulator+1e-7f>=FixedStep&&LastStepCount<MaximumCatchUpSteps){foreach(var g in _groups)_previousPoses[g.Body.Value]=_simulation.Bodies[g.Body].Pose;LastStepCount++;StepFixed(FixedStep,throttle,steering,brake,maximumSpeed,servo,piston,sandGrip,handbrake,sandResistance);_accumulator=Math.Max(0,_accumulator-FixedStep);}
  // Drop whole overdue ticks after a stall, retaining only the interpolation remainder.
  // This prevents a slow frame from turning every later frame into a catch-up spike.
  if(_accumulator+1e-7f>=FixedStep){int overdue=(int)MathF.Floor((_accumulator+1e-7f)/FixedStep);float dropped=overdue*FixedStep;DroppedSimulationSeconds+=dropped;_accumulator=Math.Max(0,_accumulator-dropped);}
  StepAllocatedBytes=GC.GetAllocatedBytesForCurrentThread()-allocatedBefore;
 }
 void StepFixed(float dt,float throttle,float steering,bool brake,float maximumSpeed,float servo,float piston,Func<Vector3,float>? sandGrip,bool handbrake,Func<Vector3,float>? sandResistance)
 {
  long terrainStart=System.Diagnostics.Stopwatch.GetTimestamp();
  _elapsed+=dt;SyncSandPatch();_terrainTimer+=dt;if(_terrainTimer>=.1f){_terrainTimer=0;if(_sandPatch!=null)foreach(var wheel in _wheels)MarkTerrainChanged(OutputPose(wheel.Block.Id).Position);foreach(var chunk in _dirtyTerrain)BuildTerrainChunk(chunk.x,chunk.z);_dirtyTerrain.Clear();if(_fineDirty.Count>0){var key=_fineDirty.First();_fineDirty.Remove(key);if(_fineChunks.ContainsKey(key))BuildFineChunk(key.x,key.z);}}
  TerrainMilliseconds+=System.Diagnostics.Stopwatch.GetElapsedTime(terrainStart).TotalMilliseconds;long controlsStart=System.Diagnostics.Stopwatch.GetTimestamp();
  _servo=Math.Clamp(_servo+servo*dt,-1,1);_piston=Math.Clamp(_piston+(piston>0?1:-1)*dt*2,0,1);
  _driftBlend+=( (handbrake?1f:0f)-_driftBlend)*(1-MathF.Exp(-dt*(handbrake?10:3)));
  var wheels=_wheels;float axleMid=_axleMid;float power=_blocks.Where(b=>b.Type is 23 or 24).Sum(b=>_catalog[b.Type].Power*b.Power);float mass=_mass;var engines=_engines;if(engines.Length>0)maximumSpeed=Math.Min(maximumSpeed,engines.Max(b=>b.SpeedLimit));
  foreach(var p in _parts.Values){var b=p.Block;if(b.Id>=RecoveryRootId)continue;
   if(p.Motor is {} motor){var def=_catalog[b.Type];float side=Vector3.Dot(p.Axis,Vector3.UnitX)<0?-1:1;bool rear=p.Block.P.Z>axleMid+.05f;bool wheelBrake=brake||(handbrake&&rear);float target=wheelBrake?0:throttle*maximumSpeed/def.Radius*side;float torque=wheelBrake?mass*9.81f/wheels.Length*def.Radius*1.5f*b.BrakeStrength:Math.Abs(throttle)*Math.Min(power/Math.Max(1,wheels.Length)*def.Radius,mass*9.81f/Math.Max(1,wheels.Length)*def.Radius*.7f)*b.Power;
    // Unloaded tyres spin with low torque; full engine torque needs a ground contact.
      if(!wheelBrake&&!_contacts.ContainsKey(p.Output!.Body.Value))torque*=.04f;
      var wheelBody=_simulation.Bodies[p.Output!.Body];var rootBody=_simulation.Bodies[p.Root.Body];float rolling=Math.Abs(Vector3.Dot(wheelBody.Velocity.Angular-rootBody.Velocity.Angular,Vector3.Transform(p.Axis,rootBody.Pose.Orientation)))*def.Radius;float translation=new Vector2(wheelBody.Velocity.Linear.X,wheelBody.Velocity.Linear.Z).Length();if(!wheelBrake&&_contacts.ContainsKey(p.Output.Body.Value))torque*=1-Math.Clamp((rolling-translation-1)/3,0,.9f);
    p.MotorTarget+=(target-p.MotorTarget)*Math.Clamp(dt*8,0,1);_simulation.Solver.ApplyDescription(motor,new AngularAxisMotor{LocalAxisA=p.Axis,TargetVelocity=p.MotorTarget,Settings=new(torque,.0001f)});
    var wheel=_simulation.Bodies[p.Output!.Body];_friction[p.Output.Body.Value]=Math.Clamp(b.Grip*(sandGrip?.Invoke(wheel.Pose.Position)??.8f)*(rear?1-_driftBlend*.65f:1),.05f,3);
    if(sandResistance!=null&&_contacts.TryGetValue(p.Output.Body.Value,out var groundContact)){var n=Vector3.Normalize(groundContact.Normal);var planar=wheel.Velocity.Linear-n*Vector3.Dot(wheel.Velocity.Linear,n);float speed=planar.Length();if(speed>.01f){float resistance=Math.Clamp(sandResistance(wheel.Pose.Position),0,2);float force=Math.Min(mass*9.81f/Math.Max(1,wheels.Length)*resistance,mass/Math.Max(1,wheels.Length)*speed/dt);wheel.ApplyLinearImpulse(-planar/speed*force*dt);}}if(Math.Abs(throttle)>.001f||wheelBrake)_simulation.Awakener.AwakenBody(p.Output.Body);
   }
   if(p.Servo is {} angular){float orientation=Vector3.Dot(p.Axis,Vector3.UnitY)>=0?1:-1;float angle=(b.Type==18?-steering*orientation:_servo)*b.Angle*MathF.PI/180;_simulation.Solver.ApplyDescription(angular,new AngularServo{TargetRelativeRotationLocalA=Quaternion.CreateFromAxisAngle(p.Axis,angle),SpringSettings=new(30,1),ServoSettings=new(2.5f,0,6000)});if(Math.Abs(steering)>.001f||Math.Abs(servo)>.001f){_simulation.Awakener.AwakenBody(p.Root.Body);_simulation.Awakener.AwakenBody(p.Output!.Body);}}
   if(p.Slider is {} linear){_simulation.Solver.GetDescription(linear,out LinearAxisServo desc);desc.TargetOffset=b.Type==21?_piston*b.Stroke:Math.Clamp(b.Preload,-b.Travel,b.Travel*.15f);if(b.Type!=21)desc.SpringSettings=new(b.SpringRate,b.Damping);_simulation.Solver.ApplyDescription(linear,desc);if(b.Type==21)_simulation.Awakener.AwakenBody(p.Output!.Body);}
  }
  ApplyWinch(dt);ApplyRecoveryWheelContacts(sandGrip,sandResistance,dt);
  foreach(var g in _groups){var body=_simulation.Bodies[g.Body];var v=body.Velocity;v.Linear*=MathF.Exp(-.035f*dt);v.Angular*=MathF.Exp(-.06f*dt);body.Velocity=v;}
  ControlsMilliseconds+=System.Diagnostics.Stopwatch.GetElapsedTime(controlsStart).TotalMilliseconds;long solverStart=System.Diagnostics.Stopwatch.GetTimestamp();
  _contacts.Clear();_simulation.Timestep(dt,_dispatcher);SolverMilliseconds+=System.Diagnostics.Stopwatch.GetElapsedTime(solverStart).TotalMilliseconds;
 }
 public string DebugSnapshot(){var text=new System.Text.StringBuilder($"Bepu land physics / time={_elapsed:0.00} / rigid islands={BodyCount} / contacts={ContactCount} / velocity={Velocity}\n");foreach(var p in _parts.Values){var pose=PartPose(p.Block.Id);text.AppendLine($"block={p.Block.Id} type={p.Block.Type} parent={p.Block.Parent} moving={p.Block.MovingMount} body={p.Root.Body.Value} output={p.Output?.Body.Value} position={pose.Position} rpm={WheelRpm(p.Block.Id):0.0} travel={SuspensionOffset(p.Block.Id):0.000}");}return text.ToString();}
 public void Dispose(){if(_disposed)return;_simulation.Dispose();_dispatcher?.Dispose();_pool.Clear();_disposed=true;}
 static ulong Pair(BodyHandle a,BodyHandle b)=>((ulong)(uint)Math.Min(a.Value,b.Value)<<32)|(uint)Math.Max(a.Value,b.Value);
 struct Contacts:INarrowPhaseCallbacks
 {
  readonly HashSet<ulong> _ignored;readonly Dictionary<int,float> _friction;readonly ContactCache _contacts;
  public Contacts(HashSet<ulong> ignored,Dictionary<int,float> friction,ContactCache contacts){_ignored=ignored;_friction=friction;_contacts=contacts;}
  public void Initialize(Simulation simulation){}
  public bool AllowContactGeneration(int workerIndex,CollidableReference a,CollidableReference b,ref float margin)=> (a.Mobility==CollidableMobility.Dynamic||b.Mobility==CollidableMobility.Dynamic)&&(a.Mobility==CollidableMobility.Static||b.Mobility==CollidableMobility.Static||!_ignored.Contains(Pair(a.BodyHandle,b.BodyHandle)));
  public bool AllowContactGeneration(int workerIndex,CollidablePair pair,int childA,int childB)=>true;
  public bool ConfigureContactManifold<T>(int workerIndex,CollidablePair pair,ref T manifold,out PairMaterialProperties material) where T:unmanaged,IContactManifold<T>
  {
   bool tireA=pair.A.Mobility!=CollidableMobility.Static&&_friction.ContainsKey(pair.A.BodyHandle.Value),tireB=pair.B.Mobility!=CollidableMobility.Static&&_friction.ContainsKey(pair.B.BodyHandle.Value);
   float friction=tireA?_friction[pair.A.BodyHandle.Value]:tireB?_friction[pair.B.BodyHandle.Value]:.6f;
   if(pair.A.Mobility==CollidableMobility.Static||pair.B.Mobility==CollidableMobility.Static){var body=pair.A.Mobility==CollidableMobility.Static?pair.B:pair.A;for(int i=0;i<manifold.Count;i++){manifold.GetContact(i,out _,out var normal,out float depth,out _);if(depth>=-.04f&&Math.Abs(normal.Y)>.35f)_contacts[body.BodyHandle.Value]=(normal,depth);}}
   material=new(){FrictionCoefficient=friction,MaximumRecoveryVelocity=1,SpringSettings=new(35,1)};return true;
  }
  public bool ConfigureContactManifold(int workerIndex,CollidablePair pair,int childA,int childB,ref ConvexContactManifold manifold)=>true;public void Dispose(){}
 }
 struct Gravity:IPoseIntegratorCallbacks
 {
  Vector3Wide _gravity;public AngularIntegrationMode AngularIntegrationMode=>AngularIntegrationMode.Nonconserving;public bool AllowSubstepsForUnconstrainedBodies=>false;public bool IntegrateVelocityForKinematics=>false;public void Initialize(Simulation simulation){}public void PrepareForIntegration(float dt)=>_gravity=Vector3Wide.Broadcast(new(0,-9.81f*dt,0));
  public void IntegrateVelocity(Vector<int> indices,Vector3Wide position,QuaternionWide orientation,BodyInertiaWide inertia,Vector<int> mask,int worker,Vector<float> dt,ref BodyVelocityWide velocity)=>velocity.Linear+=_gravity;
 }
}
