using ByteEngine.Core.Diagnostics;
using ByteEngine.Core;
using ByteEngine.Core.Construction;
using System.Numerics;

using System.Text.Json;

using ByteEngine.Core.Assets;

using ByteEngine.Core.Assets.Importers;

using ByteEngine.Core.Audio;

using ByteEngine.Core.Graphics;

using ByteEngine.Core.Graphics.ThreeD;

using ByteEngine.Core.Scene;

namespace GoblinScrapper.Construction;

public sealed partial class VehicleBuilder3D

{

    private AudioSource3D? _selectSound,_placeSound;

    public bool FreeBuilding {get;set;}=true;

    public VehicleAssembly? Assembly {get;private set;}

    private VehiclePartCatalog? _catalog;

    private float _freeSteering;

    private ContraptionPhysicsWorld? _contraption;

    private bool _powered,_physicalDrift;

    private ImpactParticles3D? _flameEffect,_waterEffect,_vacuumEffect;

    private readonly Dictionary<int,GameObject> _projectileVisuals=new();

    private bool _eraseMode;

    private GameObject? _eraseHighlight;

    public void SetEraseTool(bool enabled)

    {if(!Building)return;CancelMove();_eraseMode=enabled;_message=enabled?"ERASE: click any placed part. Ctrl+Z restores it.":"PLACE: choose a part and click a free connector.";}



    private readonly Dictionary<int,GameObject> _moveGhosts=new();

    private Vector3 _simulationOrigin;

    private Quaternion _simulationRotation;

    private readonly List<GameObject> _socketMarkers=new();

    private readonly List<(int Block,string Socket)> _socketMarkerKeys=new();

    private Vector3 _workshopPan;

    private int _movingBlock=-1;

    private int _hoveredBlock=-1,_selectedBlock,_ownSocket,_twist;

    private Vector3 _candidatePosition;

    private Quaternion _candidateRotation=Quaternion.Identity;

    private string _candidateConnector="",_candidateBone="Root",_placementIssue="Point at the machine to connect a part";

    private bool _candidateVisible;

    private readonly Dictionary<int,GameObject> _assemblyVisuals=new();

    private readonly Dictionary<int,SkeletalMeshRenderer> _assemblyRigs=new();

    private int _buildTraceGeneration=-1; private string _previousBuildTrace="";
    private readonly Stack<string> _assemblyUndo=new(),_assemblyRedo=new();

    public string FreePlacementIssue=>_placementIssue;

    private void StartAssembly()

    {

        _catalog=VehiclePartCatalog.Load(Path.Combine(ProjectRoot,"Assets","GarageUI","parts-catalog.json"));

        if(!_catalog.Standard)RegisterSteeringClips();StartWorkshopSounds();

        Assembly=new(_catalog);

        if(_catalog.Standard)

        {

            _flameEffect=Own("Contraption flame particles").AddComponent(new ImpactParticles3D{Color=new(1,.4f,.04f,.9f)});

            _waterEffect=Own("Contraption water particles").AddComponent(new ImpactParticles3D{Color=new(.4f,.75f,1,.7f)});

            _vacuumEffect=Own("Contraption suction particles").AddComponent(new ImpactParticles3D{Color=new(.8f,.75f,.56f,.65f)});

        }

        foreach(var old in GameObject.Children.Where(o=>o.Name=="Chassis").ToArray())GameObject.Scene!.DestroyGameObject(old);

        _assemblyVisuals[0]=CreateMasterBlock();_part=_catalog.Standard?Categories[0][0]:11;_category=0;

        RefreshSocketMarkers();

        foreach(var bridge in _axleBridges)bridge.Active=false;

        if(!UseAuthoredScene)Transform.WorldPosition=new(0,1.25f,0);_message="Start with beams. Click a glowing connector to extend your machine.";

    }

    private void RememberAssembly() {_assemblyUndo.Push(Assembly!.ToJson());_assemblyRedo.Clear();if(_assemblyUndo.Count>128)_assemblyUndo.Clear();}

    public int AddAssemblyPart(string file,int parent,Vector3 position,Quaternion rotation,string parentBone="Root",string parentConnector="",string ownConnector="")

    {

        if(!Building||Assembly==null)return -1;
        string placementIssue=Assembly.PlacementIssue(file,parent,position,rotation,parentConnector);
        if(placementIssue=="")placementIssue=Assembly.ConnectionIssue(file,parent,position,rotation,parentBone,parentConnector,ownConnector);
        if(placementIssue!=""){if(ConstructionDiagnostics.Enabled)ConstructionDiagnostics.Record("PLACE REJECT",$"file={file} parent={parent} target={parentConnector} source={ownConnector} position={position} reason={placementIssue}");return -1;}

        Assets!.LoadModel(new AssetReference(PartsDirectory+"/"+file+".glb"));RememberAssembly();

        int id=Assembly.Add(file,parent,position,rotation,parentBone,parentConnector,ownConnector);
        if(file==_copiedTuningFile&&_copiedTuning!=null)Assembly.Parts[id]=Assembly.Parts[id] with {Tuning=new(_copiedTuning)};

        var obj=CreateModelVisual(GameObject.Scene!,Assets!,PartsDirectory+"/"+file+".glb",GameObject,VehicleAssembly.DisplayName(file)+" #"+id);

        _assemblyVisuals.Add(id,obj);if(obj.GetComponent<SkeletalMeshRenderer>() is {} rig)_assemblyRigs[id]=rig;

        ApplyAssemblyPose();RefreshSocketMarkers();_placeSound?.Play();_selectedBlock=id;_message="Connected "+VehicleAssembly.DisplayName(file);return id;

    }

    public bool RemoveAssemblyPart(int id)

    {

        if(id < -1)return DeleteBrace(id);
        if(!Building||Assembly==null||id==0||!Assembly.Parts.ContainsKey(id))return false;

        RememberAssembly();if(_assemblyVisuals.Remove(id,out var obj))GameObject.Scene!.DestroyGameObject(obj);_assemblyRigs.Remove(id);

        _braceStart=null;if(_bracePreview!=null)_bracePreview.Active=false;Assembly.Remove(id);SyncBraceVisuals();RefreshSocketMarkers();_selectedBlock=0;_message="Part erased. Detached pieces stay in place. Ctrl+Z restores the connection.";return true;

    }

    internal void RestoreAssembly(string json)

    {

        var restored=VehicleAssembly.FromJson(_catalog!,json);

        foreach(var p in restored.Parts.Values.Where(p=>p.Id!=0))Assets!.LoadModel(new AssetReference(PartsDirectory+"/"+p.File+".glb"));

        foreach(var obj in _assemblyVisuals.Values)GameObject.Scene!.DestroyGameObject(obj);_assemblyVisuals.Clear();_assemblyRigs.Clear();Assembly=restored;

        foreach(var p in restored.Parts.Values)

        {

            var obj=p.Id==0?CreateMasterBlock():CreateModelVisual(GameObject.Scene!,Assets!,PartsDirectory+"/"+p.File+".glb",GameObject,VehicleAssembly.DisplayName(p.File)+" #"+p.Id);

            _assemblyVisuals[p.Id]=obj;if(obj.GetComponent<SkeletalMeshRenderer>() is {} rig)_assemblyRigs[p.Id]=rig;

        }

        CancelMove();_braceStart=null;_selectedBlock=0;ApplyAssemblyPose();RefreshSocketMarkers();RefreshFreeGhost();

    }

    private bool UndoAssembly(bool redo)

    {

        var source=redo?_assemblyRedo:_assemblyUndo;var destination=redo?_assemblyUndo:_assemblyRedo;

        if(!Building||!source.TryPeek(out var json))return false;
        if(ConstructionDiagnostics.Enabled)ConstructionDiagnostics.Record(redo?"REDO":"UNDO",$"undo={_assemblyUndo.Count} redo={_assemblyRedo.Count}");string current=Assembly!.ToJson();RestoreAssembly(json);source.Pop();destination.Push(current);_message=redo?"Change restored":"Change undone";return true;

    }

    private bool ReplaceAssemblyRoot(string file) {_message="Build your frame from beams; the master block stays.";return false;}

    private GameObject CreateMasterBlock()

    {

        if(_catalog?.Standard==true)return CreateModelVisual(GameObject.Scene!,Assets!,PartsDirectory+"/goblin_starting_block.glb",GameObject,"Master block");

        var root=GameObject.Scene!.CreateGameObject("Master block");root.SetParent(GameObject,false);

        var reference=new AssetReference(PartsDirectory+"/scrap_beam_1m.glb");var model=Assets!.LoadModel(reference);

        var green=model.Materials.FirstOrDefault(m=>m.Name.Contains("green",StringComparison.OrdinalIgnoreCase))??model.Materials.First();

        var material=Assets.GetModelMaterial(reference,green.Key);

        void Cube(string name,Vector3 position,Vector3 scale,Material mat)

        {var child=GameObject.Scene.CreateGameObject(name);child.SetParent(root,false);child.Transform.LocalPosition=position;child.Transform.LocalScale=scale;child.AddComponent(new MeshRenderer{UsePrimitive=true,Material=mat});}

        Cube("Scrap steel core",Vector3.Zero,new(.5f),material);

        var steel=new Material{BaseColor=new(.14f,.16f,.14f,1),Metallic=.85f,Roughness=.48f};

        foreach(var axis in new[]{Vector3.UnitX,Vector3.UnitY,Vector3.UnitZ})foreach(int sign in new[]{-1,1})

        {

            var scale=new Vector3(.36f);if(axis.X!=0)scale.X=.02f;if(axis.Y!=0)scale.Y=.02f;if(axis.Z!=0)scale.Z=.02f;

            Cube("Master socket plate",axis*sign*.252f,scale,steel);

        }

        return root;

    }

    private HashSet<(int Block,string Socket)> _occupiedSocketKeys=new();
    private void RefreshSocketMarkers()

    {

        foreach(var obj in _socketMarkers)GameObject.Scene!.DestroyGameObject(obj);_socketMarkers.Clear();_socketMarkerKeys.Clear();

        _occupiedSocketKeys=Assembly!.OccupiedSockets();
        foreach(var part in Assembly!.Parts.Values)foreach(var socket in _catalog![part.File].Sockets)

        {

            if(_occupiedSocketKeys.Contains((part.Id,socket.Name)))continue;

            var obj=GameObject.Scene!.CreateGameObject("Connector "+part.Id+" "+socket.Name);obj.SetParent(GameObject,false);

            obj.Transform.LocalPosition=part.Position+Vector3.Transform(socket.Position+socket.Normal*.028f,part.Rotation);obj.Transform.LocalScale=new(.055f);

            obj.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Sphere,CastShadows=false,Material=new Material{BaseColor=new(.55f,.78f,.46f,.85f),BlendMode=BlendMode3D.AlphaBlend}});

            obj.Active=false;_socketMarkers.Add(obj);_socketMarkerKeys.Add((part.Id,socket.Name));

        }

    }

    private void UpdateFreeBuilding(bool clickedUi)

    {

        bool control=ControlKeyDown(Key.LeftControl)||ControlKeyDown(Key.RightControl);

        if(control&&ControlKeyPressed(Key.Z))UndoAssembly(false);

        else if(ControlKeyPressed(Key.Z))ChangePalette(-1);

        if(control&&ControlKeyPressed(Key.Y))UndoAssembly(true);

        if(ControlKeyPressed(Key.PageUp))ChangePalette(-1);

        if(ControlKeyPressed(Key.PageDown))ChangePalette(1);



        if(ControlKeyPressed(Key.F)){_twist=(_twist+180)%360;UpdateFreePreview();}

        if(ControlKeyPressed(Key.Tab)){_ownSocket=(_ownSocket+1)%Math.Max(1,_catalog![SelectedPart].Sockets.Length);UpdateFreePreview();}

        if(ControlKeyPressed(Key.R))RotatePreview(ControlKeyDown(Key.LeftAlt)?15:90);
        if(ControlKeyPressed(Key.T))CyclePreviewMountFace();

        if(ControlKeyPressed(Key.Escape)){CloseTuning();CancelMove();SetEraseTool(false);}

        if(ControlKeyPressed(Key.M)&&_hoveredBlock>0)

        {int id=_hoveredBlock;CancelMove();int index=Array.IndexOf(BuilderPartFiles,Assembly!.Parts[id].File);SelectPart(index);_movingBlock=id;

            foreach(int child in Assembly.Descendants(id))

            {_assemblyVisuals[child].Active=false;if(child!=id)_moveGhosts[child]=CreateModelVisual(GameObject.Scene!,Assets!,PartsDirectory+"/"+Assembly.Parts[child].File+".glb",GameObject,"Moving branch preview",true);}

            _message="Move branch: choose another connector. Escape cancels.";}

        if(ControlKeyPressed(Key.C)&&_hoveredBlock>0){CancelMove();CopyHoveredPart();}

        if(ControlKeyPressed(Key.Delete))RemoveAssemblyPart(_selectedBlock);

        UpdateFreePreview();
        if(UseBuiltInPointerControls&&!PointerOnHud()&&Input.IsGameViewHovered)

        {

            if(Input.IsMouseButtonDown(MouseButton.Right)||Input.IsMouseButtonDown(MouseButton.Middle))

            {

                var delta=Input.GameViewMouseDelta;

                if(Input.IsMouseButtonDown(MouseButton.Middle))_workshopPan+=(-_camera!.Transform.Right*delta.X+_camera.Transform.Up*delta.Y)*(_zoom*.0014f);

                else OrbitBuildCamera(-delta.X*.008f,delta.Y*.006f);

            }

            _zoom=Math.Clamp(_zoom-Input.Snapshot.MouseWheel*.6f,3,24);
            if(Input.Snapshot.MouseWheel!=0||Input.IsMouseButtonDown(MouseButton.Right)||Input.IsMouseButtonDown(MouseButton.Middle))UpdateFreePreview();



            if(ControlKeyPressed(Key.X))RemoveAssemblyPart(_hoveredBlock);

            if(Input.IsMouseButtonPressed(MouseButton.Left)&&!clickedUi)

            {if(_tuneMode)TuneBlock(_hoveredBlock);
                else if(_eraseMode)RemoveAssemblyPart(_hoveredBlock);
                else if(_copyPick){CopyHoveredPart();_copyPick=false;}
                else if(_movePick){MoveHoveredBranch();_movePick=false;}
                else PlaceFreePreview();}

        }

        if(ControlKeyPressed(Key.Enter)&&!clickedUi&&!_tuneMode)PlaceFreePreview();

        for(int i=0;i<_socketMarkers.Count;i++)_socketMarkers[i].Active=Building&&!_eraseMode&&_socketMarkerKeys[i].Block==_hoveredBlock;

        _orbit+=((ControlKeyDown(Key.E)?1:0)-(ControlKeyDown(Key.Q)?1:0))*Math.Clamp((float)Time.DeltaTime,0,.05f)*1.5f;

    }

    private void CancelMove() {_movePick=_copyPick=false;_pendingMountTurns=0;_braceStart=null;if(_bracePreview!=null)_bracePreview.Active=false;_movingBlock=-1;foreach(var ghost in _moveGhosts.Values)GameObject.Scene!.DestroyGameObject(ghost);_moveGhosts.Clear();foreach(var visual in _assemblyVisuals.Values)visual.Active=true;}

    private void PlaceFreePreview()

    {
        if(BraceSelected){PlaceBracePreview();return;}

        if(!_candidateVisible||_placementIssue!=""){_message=_placementIssue;return;}

        var own=_catalog![SelectedPart].Sockets.ElementAtOrDefault(_ownSocket);

        if(_movingBlock>=0)

        {

            RememberAssembly();string issue=Assembly!.MoveAtSocket(_movingBlock,_hoveredBlock,_candidateConnector,own!.Name,_twist);

            if(issue==""){_message="Branch reconnected";CancelMove();ApplyAssemblyPose();RefreshSocketMarkers();_placeSound?.Play();UpdateFreePreview();}else{_assemblyUndo.Pop();_message=issue;}

            return;

        }

        if(AddAssemblyPart(SelectedPart,_hoveredBlock,_candidatePosition,_candidateRotation,_candidateBone,_candidateConnector,own?.Name??"")>=0)UpdateFreePreview();

    }

    private void RefreshFreeGhost()

    {

        if(Assets==null||!Building)return;

        if(_ghost!=null)GameObject.Scene!.DestroyGameObject(_ghost);

        _ghost=CreateModelVisual(GameObject.Scene!,Assets,PartsDirectory+"/"+SelectedPart+".glb",GameObject,"Free placement preview",true);

        _ghost.Active=false;_candidateVisible=false;_ownSocket=DefaultSocket(SelectedPart);_twist=0;

    }

    private int DefaultSocket(string file)

    {

        var sockets=_catalog![file].Sockets;

        int preferred=file.StartsWith("scrap_beam_")?Array.FindIndex(sockets,s=>s.Name.Contains("End_Rear")):Array.FindIndex(sockets,s=>s.Name.Contains("Chassis",StringComparison.OrdinalIgnoreCase)||s.Name.Contains("Base",StringComparison.OrdinalIgnoreCase)||s.Name.Contains("Mount",StringComparison.OrdinalIgnoreCase));return Math.Max(0,preferred);

    }

    private static Quaternion AlignNormals(Vector3 from,Vector3 to)

    {

        from=Vector3.Normalize(from);to=Vector3.Normalize(to);float dot=Vector3.Dot(from,to);

        if(dot>.9999f)return Quaternion.Identity;

        if(dot<-.9999f)return Quaternion.CreateFromAxisAngle(Math.Abs(from.Y)<.9f?Vector3.UnitY:Vector3.UnitX,MathF.PI);

        return Quaternion.Normalize(new Quaternion(Vector3.Cross(from,to),1+dot));

    }

    private void UpdateFreePreview()

    {

        if(Assembly==null||_ghost==null||_camera==null)return;

        var ray=_camera.ScreenPointToRay(Input.GameViewPointerNormalized,Input.GameViewSize.X/Math.Max(1,Input.GameViewSize.Y));

        Matrix4x4.Invert(Transform.WorldMatrix,out var inverse);Vector3 origin=Vector3.Transform(ray.Origin,inverse),direction=Vector3.Normalize(Vector3.TransformNormal(ray.Direction,inverse));

        var movingBranch=_movingBlock>=0?Assembly.Descendants(_movingBlock).ToHashSet():null;
        float nearest=float.MaxValue;Vector3 point=default,normal=Vector3.UnitY;_hoveredBlock=-1;_candidateConnector="";_candidateBone="Root";

        foreach(var p in Assembly.Parts.Values)

        {

            if(movingBranch?.Contains(p.Id)==true)continue;

            Quaternion inverseRotation=Quaternion.Inverse(p.Rotation);var localOrigin=Vector3.Transform(origin-p.Position,inverseRotation);var localDirection=Vector3.Transform(direction,inverseRotation);

            foreach(var box in _catalog![p.File].Clearance)

                if(RayBox(localOrigin,localDirection,box,out float distance,out Vector3 face)&&distance<nearest)

                {nearest=distance;_hoveredBlock=p.Id;point=origin+direction*distance;normal=Vector3.Transform(face,p.Rotation);}

        }

        if(_eraseMode||_movePick||_copyPick||_tuneMode)

        {

            if(!_tuneMode)PickBraceForErase(origin,direction,ref nearest);
            _ghost.Active=false;_candidateVisible=false;

            if(_eraseHighlight==null)

            {_eraseHighlight=Own("Erase selection highlight");_eraseHighlight.SetParent(GameObject,false);_eraseHighlight.AddComponent(new MeshRenderer{UsePrimitive=true,CastShadows=false,Material=new Material{BaseColor=new(.9f,.18f,.08f,.20f),BlendMode=BlendMode3D.AlphaBlend}});}

            _eraseHighlight.Active=_hoveredBlock>0||_hoveredBlock < -1;

            if(_hoveredBlock>0){var p=Assembly.Parts[_hoveredBlock];var b=_catalog![p.File].Bounds;_eraseHighlight.Transform.LocalPosition=p.Position+Vector3.Transform((b.Min+b.Max)*.5f,p.Rotation);_eraseHighlight.Transform.LocalRotation=p.Rotation;_eraseHighlight.Transform.LocalScale=b.Max-b.Min+new Vector3(.04f);_message="Click to erase "+VehicleAssembly.DisplayName(p.File)+" #"+p.Id+". Ctrl+Z restores it.";}

            else if(_hoveredBlock < -1){var brace=Assembly.Braces[-_hoveredBlock-1];var a=Assembly.BracePoint(brace.A);var b=Assembly.BracePoint(brace.B);_eraseHighlight.Transform.LocalPosition=(a+b)*.5f;_eraseHighlight.Transform.LocalRotation=AlignNormals(Vector3.UnitZ,Vector3.Normalize(b-a));_eraseHighlight.Transform.LocalScale=new(.14f,.14f,Vector3.Distance(a,b));_message="Click to erase brace. Ctrl+Z restores it";}
            else _message=_hoveredBlock < -1?"Click to erase brace. Ctrl+Z restores it":_hoveredBlock==0?"The master block is protected":"ERASE: point at a placed part, then click";

            if(_tuneMode)_message=_hoveredBlock>0?"Click to tune "+VehicleAssembly.DisplayName(Assembly.Parts[_hoveredBlock].File):"TUNE: click a placed block";
            _placementIssue=_message;return;

        }

        if(_eraseHighlight!=null)_eraseHighlight.Active=false;

        // Select only free, outward-facing authored attachment points, never an arbitrary surface.

        int hitBlock=_hoveredBlock;Vector3 hitNormal=normal;

        float best=float.MaxValue;int targetBlock=-1;

        foreach(var p in Assembly.Parts.Values)foreach(var socket in _catalog![p.File].Sockets)

        {

            if((!BraceSelected&&(_movingBlock>=0?Assembly.IsSocketOccupied(p.Id,socket.Name,_movingBlock):_occupiedSocketKeys.Contains((p.Id,socket.Name))))||(movingBranch?.Contains(p.Id)==true))continue;

            Vector3 pos=p.Position+Vector3.Transform(socket.Position,p.Rotation),n=Vector3.Transform(socket.Normal,p.Rotation),delta=pos-origin;

            float distance=Vector3.Dot(delta,direction),miss=(delta-direction*distance).Length();

            if(Vector3.Dot(n,direction)>.10f)continue;

            float radius=Math.Clamp(distance*.022f,.10f,.24f);

            float score=miss/radius+(p.Id==hitBlock?0:.15f);

            if(distance>0&&(distance<nearest+.24f||p.Id==hitBlock)&&miss<radius&&score<best)

            {best=score;targetBlock=p.Id;point=pos;normal=n;_candidateConnector=socket.Name;_candidateBone=socket.Bone;}

        }

        _candidateVisible=targetBlock>=0;_ghost.Active=_candidateVisible;

        for(int i=0;i<_socketMarkers.Count;i++)

        {

            var key=_socketMarkerKeys[i];bool active=key.Block==targetBlock&&key.Socket==_candidateConnector;

            _socketMarkers[i].Transform.LocalScale=new(active?.095f:.055f);

        }

        if(!_candidateVisible){if(_bracePreview!=null)_bracePreview.Active=false;foreach(var ghost in _moveGhosts.Values)ghost.Active=false;_placementIssue=hitBlock>=0?"Point at a free connector on this part":"Point at a glowing connector";return;}

        _hoveredBlock=targetBlock;
        if(BraceSelected){UpdateBracePreview(point);return;}

        var definition=_catalog![SelectedPart];var own=definition.Sockets.ElementAtOrDefault(_ownSocket);

        Vector3 ownNormal=own?.Normal??-Vector3.UnitY,ownPosition=own?.Position??new(0,definition.Bounds.Min.Y,0);

        _candidateRotation=Quaternion.Normalize(Quaternion.CreateFromAxisAngle(normal,_twist*MathF.PI/180)*AlignNormals(ownNormal,-normal));

        if(_pendingMountTurns>0){
            if(definition.Kind=="beam"){var next=RotateBeamMount(definition.Sockets,_ownSocket,_candidateRotation,normal,90*_pendingMountTurns);_ownSocket=next.Socket;_twist=next.Twist;own=definition.Sockets[_ownSocket];ownPosition=own.Position;ownNormal=own.Normal;}
            else _twist=(_twist+90*_pendingMountTurns)%360;
            _pendingMountTurns=0;_candidateRotation=Quaternion.Normalize(Quaternion.CreateFromAxisAngle(normal,_twist*MathF.PI/180)*AlignNormals(ownNormal,-normal));
        }
        _candidatePosition=point-Vector3.Transform(ownPosition,_candidateRotation);

        _placementIssue=_movingBlock>=0?Assembly.MoveAtSocket(_movingBlock,_hoveredBlock,_candidateConnector,own!.Name,_twist,true):Assembly.PlacementIssue(SelectedPart,_hoveredBlock,_candidatePosition,_candidateRotation,_candidateConnector);

        foreach(var ghost in _moveGhosts.Values)ghost.Active=_candidateVisible;

        if(_movingBlock>=0)

        {

            var old=Assembly.Parts[_movingBlock];Matrix4x4.Invert(Matrix4x4.CreateFromQuaternion(old.Rotation)*Matrix4x4.CreateTranslation(old.Position),out var inverseOld);

            var delta=inverseOld*Matrix4x4.CreateFromQuaternion(_candidateRotation)*Matrix4x4.CreateTranslation(_candidatePosition);

            foreach(var pair in _moveGhosts){var p=Assembly.Parts[pair.Key];var pose=ImportAlignment*Matrix4x4.CreateFromQuaternion(p.Rotation)*Matrix4x4.CreateTranslation(p.Position)*delta;Matrix4x4.Decompose(pose,out _,out var r,out var pos);pair.Value.Transform.LocalPosition=pos;pair.Value.Transform.LocalRotation=r;}

        }

        _ghost.Transform.LocalPosition=_candidatePosition;_ghost.Transform.LocalRotation=Quaternion.CreateFromRotationMatrix(ImportAlignment*Matrix4x4.CreateFromQuaternion(_candidateRotation));TintGhost(_placementIssue);

    }

    private static bool RayBox(Vector3 origin,Vector3 direction,AssemblyBox box,out float distance,out Vector3 normal)

    {

        float near=0,far=float.MaxValue;normal=Vector3.UnitY;

        for(int axis=0;axis<3;axis++)

        {

            float o=axis==0?origin.X:axis==1?origin.Y:origin.Z,d=axis==0?direction.X:axis==1?direction.Y:direction.Z,min=axis==0?box.Min.X:axis==1?box.Min.Y:box.Min.Z,max=axis==0?box.Max.X:axis==1?box.Max.Y:box.Max.Z;

            if(Math.Abs(d)<.00001f){if(o<min||o>max){distance=0;return false;}continue;}

            float a=(min-o)/d,b=(max-o)/d;Vector3 face=(axis==0?Vector3.UnitX:axis==1?Vector3.UnitY:Vector3.UnitZ)*(d>0?-1:1);

            if(a>b)(a,b)=(b,a);if(a>near){near=a;normal=face;}far=Math.Min(far,b);if(near>far){distance=0;return false;}

        }

        distance=near;return near>0;

    }

    private IEnumerable<AssemblyPart> PoseOrder()

    {

        var done=new HashSet<int>();var pending=Assembly!.Parts.Values.ToList();

        while(pending.Count>0){var ready=pending.Where(p=>p.Parent<0||done.Contains(p.Parent)).ToArray();if(ready.Length==0)throw new InvalidOperationException("Cyclic machine");foreach(var p in ready){done.Add(p.Id);pending.Remove(p);yield return p;}}

    }

    private Vector3 _buildFocus=new(0,.5f,0);
    private Dictionary<int,float> _cogPhases=new();
    private void ApplyAssemblyPose(float steering=0)

    {
        SyncBraceVisuals();

        // During joint simulation, body poses own the visual transforms.
        if(Assembly==null||(!Building&&_contraption!=null))return;

        if(Building){
            var bounds=Assembly.Parts.Values.SelectMany(p=>_catalog![p.File].Clearance.Select(b=>b.Transform(p.Position,p.Rotation))).ToArray();
            var min=new Vector3(bounds.Min(b=>b.Min.X),bounds.Min(b=>b.Min.Y),bounds.Min(b=>b.Min.Z));var max=new Vector3(bounds.Max(b=>b.Max.X),bounds.Max(b=>b.Max.Y),bounds.Max(b=>b.Max.Z));_buildFocus=(min+max)*.5f;
            var position=Transform.WorldPosition;position.Y=(UseAuthoredScene?_workshopStart.Y-1.25f:0)+Math.Max(1.25f,Assembly.RideHeight+.08f);Transform.WorldPosition=position;}
        _cogPhases=CogGeometry.Phases(Assembly);
        foreach(var pair in _cogPhases)if(_assemblyRigs.TryGetValue(pair.Key,out var gearRig)){var d=_catalog![Assembly.Parts[pair.Key].File];gearRig.SetPhysicalBoneDeformation("Moving",ImportAlignment*CogGeometry.PhaseTransform(d,pair.Value)*ImportAlignment);}
        var matrices=new Dictionary<int,Matrix4x4>();

        foreach(var p in PoseOrder())

        {

            if(!Building&&!Assembly.IsConnected(p.Id))continue;

            Matrix4x4 pose=Matrix4x4.CreateFromQuaternion(p.Rotation)*Matrix4x4.CreateTranslation(p.Position);

            if(p.Parent>=0&&Assembly.Parts.TryGetValue(p.Parent,out var parent))

            {

                Matrix4x4 bind=Matrix4x4.CreateFromQuaternion(parent.Rotation)*Matrix4x4.CreateTranslation(parent.Position);Matrix4x4.Invert(bind,out var inverseBind);

                Matrix4x4 boneDelta=Matrix4x4.Identity;

                if(!(Building&&_catalog?.Standard==true)&&p.ParentBone!="Root"&&_assemblyRigs.TryGetValue(parent.Id,out var rig)&&rig.TryGetBoneModelMatrix(p.ParentBone,out var current))

                {

                    var bone=rig.ResolvedModel?.Skeleton?.Bones.FirstOrDefault(b=>b.Name==p.ParentBone);

                    if(bone!=null)boneDelta=ImportAlignment*(bone.BindPose*current)*ImportAlignment;

                }

                pose=pose*inverseBind*boneDelta*matrices[parent.Id];

            }

            matrices[p.Id]=pose;

            Matrix4x4.Decompose(ImportAlignment*pose,out _,out var rotation,out var position);

            var visual=_assemblyVisuals[p.Id];visual.Transform.LocalPosition=position;visual.Transform.LocalRotation=rotation;

        }

    }

    private bool BeginAssemblyDriving()

    {

        if(_braceStart!=null){_message="Finish the brace or press Escape";return false;}
        if(_bracePreview!=null)_bracePreview.Active=false;
        if(_movingBlock>=0){_message="Finish moving the branch or press Escape";return false;}

        if(!Assembly!.CanDrive){_message=Assembly.DriveRequirement;return false;}

        if(ConstructionDiagnostics.Enabled)ConstructionDiagnostics.Record("DRIVE START",$"parts={Assembly.Parts.Count} rideHeight={Assembly.RideHeight} origin={Transform.WorldPosition}");
        _simulationOrigin=Transform.WorldPosition;_simulationRotation=Transform.WorldRotation;

        if(_catalog?.Standard==true)

        {

            Transform.WorldPosition=new(Transform.WorldPosition.X,Assembly.RideHeight+(UseAuthoredScene?_workshopStart.Y-1.25f:0),Transform.WorldPosition.Z);

            _contraption=new(Assembly,Transform.WorldPosition,Transform.WorldRotation,!UseAuthoredScene);

            AddAuthoredObstacles();
            foreach(var o in _obstacles)_contraption.AddObstacle(new(o.Center.X,o.Half.Y,o.Center.Y),new(o.Half.X*2,o.Half.Y*2,o.Half.Y*2));

            foreach(var visual in _assemblyVisuals.Values)visual.SetParent(null,true);

            DeployBattlefield();
            Building=false;foreach(var marker in _socketMarkers)marker.Active=false;

            if(_ghost!=null){GameObject.Scene!.DestroyGameObject(_ghost);_ghost=null;}

            foreach(var ui in _buildUi)ui.Active=false;return true;

        }

        foreach(var p in Assembly.Parts.Values.Where(p=>!Assembly.IsConnected(p.Id)))_assemblyVisuals[p.Id].SetParent(null,true);

        if(_eraseHighlight!=null)_eraseHighlight.Active=false;

        Building=false;foreach(var marker in _socketMarkers)marker.Active=false;_motion.Reset(_motion.Yaw);Speed=0;Transform.WorldPosition=new(Transform.WorldPosition.X,Assembly.RideHeight+(UseAuthoredScene?_workshopStart.Y-1.25f:0),Transform.WorldPosition.Z);

        foreach(var p in Assembly.Parts.Values)if(p.File=="scrap_engine_block"&&_assemblyRigs.TryGetValue(p.Id,out var rig))PlayClip(rig,"EngineIdle",true);

        if(_ghost!=null){GameObject.Scene!.DestroyGameObject(_ghost);_ghost=null;}foreach(var ui in _buildUi)ui.Active=false;return true;

    }

    private void StepAssemblyDrive(float throttle,float steering,bool brake,float dt,bool drift)

    {

        if(Building||dt<=0||!float.IsFinite(dt))return;

        if(_contraption!=null)

        {

            _physicalDrift=drift&&Speed>1;_contraption.Step(dt,throttle,steering,brake,_powered,drift);

            foreach(var p in Assembly!.Parts.Values)

            {

                var pose=_contraption.Pose(p.Id);var visual=_assemblyVisuals[p.Id];

                visual.Transform.WorldPosition=pose.Position;

                visual.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(ImportAlignment*Matrix4x4.CreateFromQuaternion(pose.Rotation));

                if(_assemblyRigs.TryGetValue(p.Id,out var rig)&&_contraption.HasPhysicalOutput(p.Id))

                {

                    var deformation=_contraption.OutputDeformation(p.Id);var def=_catalog![p.File];

                    if(def.ReferenceId is 9 or 16)SampleCycle(rig,"Extend",Math.Clamp(deformation.Translation.Length()/.3f,0,1)*.5f);

                    rig.SetPhysicalBoneDeformation("Moving",ImportAlignment*(_cogPhases.TryGetValue(p.Id,out var phase)?CogGeometry.PhaseTransform(def,phase)*deformation:deformation)*ImportAlignment);

                }

            }

            SyncBraceVisuals();
            if(_powered)foreach(var p in Assembly.Parts.Values)

            {

                var def=_catalog![p.File];if(def.ReferenceId is not (21 or 56 or 62))continue;var pose=_contraption.Pose(p.Id);var forward=Vector3.Transform(-Vector3.UnitZ,pose.Rotation);

                if(def.ReferenceId==21)_flameEffect?.EmitStream(pose.Position+forward*.8f,forward,5);

                if(def.ReferenceId==56)_waterEffect?.EmitStream(pose.Position+forward*.8f,forward,5,14);

                if(def.ReferenceId==62)_vacuumEffect?.EmitStream(pose.Position+forward*3,-forward,3,7);

            }

            var active=_contraption.Projectiles.ToArray();foreach(var item in active)

            {

                if(!_projectileVisuals.TryGetValue(item.Id,out var obj)){obj=GameObject.Scene!.CreateGameObject("Contraption projectile");obj.Transform.LocalScale=new(.15f);obj.AddComponent(new MeshRenderer{UsePrimitive=true,Primitive=PrimitiveMeshType.Sphere,Material=new Material{BaseColor=new(.15f,.13f,.1f,1),Metallic=.8f,Roughness=.3f}});_projectileVisuals[item.Id]=obj;}

                obj.Transform.WorldPosition=item.Position;

            }

            foreach(var id in _projectileVisuals.Keys.Where(id=>!active.Any(p=>p.Id==id)).ToArray()){GameObject.Scene!.DestroyGameObject(_projectileVisuals[id]);_projectileVisuals.Remove(id);}

            var root=_contraption.Pose(0);Transform.WorldPosition=root.Position;Transform.WorldRotation=root.Rotation;Speed=_contraption.Velocity(0).Length();return;

        }

        // Number of engines and carried mass affect power; lateral grip follows the chosen running gear.

        int engines=Assembly!.Parts.Values.Count(p=>p.File=="scrap_engine_block"&&Assembly.IsConnected(p.Id));

        float cap=Math.Clamp(MaximumSpeed*Math.Clamp(450f*engines/Math.Max(1,Assembly.TotalMass),.45f,1.5f),1,30);

        var movement=_motion.Step(throttle,steering,brake,drift&&!Assembly.HasTracks,dt,cap,Assembly.HasTracks?RoadGrip*1.6f:RoadGrip,DriftGrip);

        Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,_motion.Yaw);var next=Transform.WorldPosition+movement;

        var envelope=Assembly.Parts.Values.Where(p=>Assembly.IsConnected(p.Id)).Select(p=>_catalog![p.File].Bounds.Transform(p.Position,p.Rotation)).ToArray();

        float halfX=Math.Max(1,envelope.Max(b=>Math.Max(Math.Abs(b.Min.X),Math.Abs(b.Max.X)))),halfZ=Math.Max(1,envelope.Max(b=>Math.Max(Math.Abs(b.Min.Z),Math.Abs(b.Max.Z))));

        float radius=Math.Max(halfX,halfZ);

        if(Math.Abs(next.X)+radius>31||Math.Abs(next.Z)+radius>31||_obstacles.Any(o=>Math.Abs(next.X-o.Center.X)<o.Half.X+radius&&Math.Abs(next.Z-o.Center.Y)<o.Half.Y+radius))_motion.Stop();else Transform.WorldPosition=next;

        Speed=_motion.ForwardSpeed;_rollDistance+=Speed*Math.Min(dt,.05f);

        foreach(var pair in _assemblyRigs)

        {

            if(!Assembly.IsConnected(pair.Key))continue;

            string file=Assembly.Parts[pair.Key].File;var rig=pair.Value;

            if(file.StartsWith("scrap_wheel_"))

            {

                float sign=Vector3.Dot(Vector3.Transform(Vector3.UnitX,Assembly.Parts[pair.Key].Rotation),Vector3.UnitX)<0?-1:1;

                SampleCycle(rig,"Roll",-sign*_rollDistance/(MathF.Tau*(file=="scrap_wheel_large"?.64f:.4125f)));

            }

            else if(file=="scrap_axle_2m")SampleCycle(rig,"AxleSpin",-_rollDistance/(MathF.Tau*.64f));

            else if(file=="scrap_track_pod")SampleCycle(rig,"TrackDrive",_rollDistance/3.8f);

            else if(file=="scrap_engine_block")rig.Speed=.7f+Math.Abs(Speed)*.1f;

            else if(file=="scrap_cab_shell")SampleCycle(rig,"SteerWheel",Math.Abs(steering)*.5f);

            else if(file=="scrap_steering_pivot")SampleCycle(rig,steering>=0?"SteerRight":"SteerLeft",Math.Abs(steering)*.5f);

            else if(file=="scrap_suspension_piston")SampleCycle(rig,"Compress",Math.Min(.45f,Math.Abs(_motion.LateralSpeed)*.06f));

        }

        ApplyAssemblyPose(steering);

    }

    private IEnumerable<(string File,SkeletalMeshRenderer Rig)> ActiveRigs()=>FreeBuilding? _assemblyRigs.Where(p=>Assembly!.IsConnected(p.Key)).Select(p=>(Assembly!.Parts[p.Key].File,p.Value)):_mechanisms.Select(p=>(Layout.Parts[p.Key],p.Value));

    private void StartWorkshopSounds()

    {

        AudioSource3D Sound(string name)

        {

            var sound=Own("Workshop "+name+" sound").AddComponent(new AudioSource3D {Spatial=false,Volume=.22f,PlayOnStart=false,ClipReference=new("Assets/GarageUI/sounds/"+name+".wav")});

            if(AudioClip.TryLoadWave(Path.Combine(ProjectRoot,"Assets","GarageUI","sounds",name+".wav"),out var clip))sound.SetClip(clip);

            return sound;

        }

        _selectSound=Sound("select");_placeSound=Sound("place");

    }

    private void RegisterSteeringClips()

    {

        var model=Assets!.LoadModel(new AssetReference(PartsDirectory+"/scrap_steering_pivot.glb"));

        var source=model.Animations.First(a=>a.Name.EndsWith("__Steer"));

        foreach(bool mirrored in new[]{false,true})

        {

            string suffix=mirrored?"SteerRight":"SteerLeft";

            var channels=source.Channels.Select(c=>

            {

                ImportedQuaternionTrack? track=c.Rotation;

                if(mirrored&&track is {Keys.Count:>0})

                {

                    Quaternion rest=track.Keys[0].Value;

                    track=new ImportedQuaternionTrack {Interpolation=track.Interpolation,Keys=track.Keys.Select(k=>k with {Value=Quaternion.Normalize(rest*Quaternion.Inverse(Quaternion.Inverse(rest)*k.Value))}).ToList()};

                }

                return new ImportedAnimationChannel {NodeName=c.NodeName,Translation=c.Translation,Rotation=track,Scale=c.Scale};

            }).ToList();

            model.RegisterRuntimeAnimation(new ImportedAnimation {Key="goblin-workshop-"+suffix,Name="scrap_steering_pivot__"+suffix,Duration=source.Duration,Channels=channels});

        }

    }

    private void SaveAssembly()

    {

        try {Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);File.WriteAllText(SavePath+".tmp",Assembly!.ToJson());File.Move(SavePath+".tmp",SavePath,true);_message="Machine saved";if(ConstructionDiagnostics.Enabled)ConstructionDiagnostics.Record("SAVE",$"path={SavePath} parts={Assembly.Parts.Count}");}

        catch(Exception e)when(e is IOException or UnauthorizedAccessException){_message="Save failed: "+e.Message;if(ConstructionDiagnostics.Enabled)ConstructionDiagnostics.Record("SAVE FAILED",e.ToString());}

    }

    private void LoadAssembly()

    {

        if(!Building){_message="Return to Build before loading";return;}

        try

        {

            string json=File.ReadAllText(SavePath);using var doc=JsonDocument.Parse(json);

            if(doc.RootElement.GetProperty("Version").GetInt32() is not (4 or 5 or 6 or 7))throw new JsonException("Older chassis save preserved. Start a new machine from the master block.");

            var validated=VehicleAssembly.FromJson(_catalog!,json);foreach(var p in validated.Parts.Values.Where(p=>p.Id!=0))Assets!.LoadModel(new AssetReference(PartsDirectory+"/"+p.File+".glb"));

            RememberAssembly();RestoreAssembly(json);ResetPosition();_message="Machine restored";if(ConstructionDiagnostics.Enabled)ConstructionDiagnostics.Record("LOAD",$"path={SavePath} parts={Assembly!.Parts.Count}");

        }

        catch(Exception e)when(e is IOException or UnauthorizedAccessException or JsonException or ArgumentException or InvalidOperationException or KeyNotFoundException){_message="Could not load: "+e.Message;if(ConstructionDiagnostics.Enabled)ConstructionDiagnostics.Record("LOAD FAILED",e.ToString());}

    }

    private string MigrateLegacyAssembly(VehicleBuildLayout old)

    {

        var assembly=new VehicleAssembly(_catalog!,old.Chassis);

        // Legacy hardpoint saves are retained separately; explicit graph migration does not invent steering joints.

        foreach(var pair in old.Parts.OrderBy(p=>p.Key))

        {

            var pose=PartPlacement(pair.Key,pair.Value,old.Chassis);var rotation=Quaternion.CreateFromRotationMatrix(ImportAlignment*Matrix4x4.CreateFromQuaternion(pose.Rotation));

            int parent=pair.Key<4?assembly.Parts.Values.FirstOrDefault(p=>p.File=="scrap_axle_2m")?.Id??0:0;

            if(pair.Key<4)continue;

            int id=assembly.Add(pair.Value,parent,pose.Position,rotation);if(id<0)throw new JsonException("Legacy machine needs manual rebuilding for spatial connections; original save is unchanged");

        }

        foreach(var pair in old.Parts.Where(p=>p.Key<4))

        {var pose=PartPlacement(pair.Key,pair.Value,old.Chassis);var rotation=Quaternion.CreateFromRotationMatrix(ImportAlignment*Matrix4x4.CreateFromQuaternion(pose.Rotation));var axle=assembly.Parts.Values.Where(p=>p.File=="scrap_axle_2m").OrderBy(p=>Vector3.Distance(p.Position,pose.Position)).FirstOrDefault();if(axle==null||assembly.Add(pair.Value,axle.Id,pose.Position,rotation)<0)throw new JsonException("Legacy wheel connections need rebuilding; original save is unchanged");}

        return assembly.ToJson();

    }

}
