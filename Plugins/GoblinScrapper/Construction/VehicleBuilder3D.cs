using ByteEngine.Core;
using ByteEngine.Core.Construction;
using System.Numerics;

using ByteEngine.Core.Assets;

using ByteEngine.Core.Graphics;

using ByteEngine.Core.Graphics.ThreeD;

using ByteEngine.Core.Scene;



namespace GoblinScrapper.Construction;



/// <summary>Socket-based garage and arcade test driving. Parts stay rigidly parented to the frame.</summary>

public sealed partial class VehicleBuilder3D : Component

{

    internal AssetManager? Assets { get; set; }

    internal string ProjectRoot { get; set; } = "";

    public string PartsDirectory { get; set; } = "Assets/refinded parts";

    public float RoadGrip { get; set; } = 12;

    public float DriftGrip { get; set; } = .9f;

    public bool IsDrifting => _contraption!=null?_physicalDrift:_motion.IsDrifting;

    public float DriftAngle => _motion.DriftAngle;

    public Vector3 Velocity => _contraption?.Velocity(0)??_motion.Velocity;

    private static readonly Matrix4x4 ImportAlignment = Matrix4x4.CreateRotationY(MathF.PI);

    private readonly VehicleDriveMotion _motion = new();

    private readonly Dictionary<int, SkeletalMeshRenderer> _mechanisms = new();

    private readonly List<UiWidget> _palette = new();

    private readonly List<(GameObject Object, Material Material)> _skids = new();

    private readonly float[] _skidLife = new float[80];

    private int _palettePage, _skidIndex;

    private float _skidTimer, _rollDistance;

    public float MaximumSpeed { get; set; } = 12;

    public bool Building { get; private set; } = true;

    public float Speed { get; private set; }

    public VehicleBuildLayout Layout { get; private set; } = new();

    private readonly Dictionary<int, GameObject> _visuals = new();

    private readonly List<(UiWidget Button, Action Click)> _buttons = new();

    private readonly List<GameObject> _buildUi = new();

    private readonly List<(Vector2 Center, Vector2 Half)> _obstacles = new();

    private readonly List<GameObject> _owned = new();

    private GameObject? _ghost, _canvas;

    private GameObject[] _markers = [];

    private Camera3D? _camera;

    private UiText? _status, _hint, _selection;

    private UiWidget? _driveButton;

    private int _part, _mount;

    private float _orbit = .65f;

    private string _message = "Choose a part, then click a glowing mount.";

    private string SelectedPart => BuilderPartFiles[_part];

    private string SavePath => Path.Combine(ProjectRoot, "Saves", "vehicle-build.json");

    public override int UpdateOrder => 50;



    protected override void OnStart()

    {

        if (Assets == null || GameObject.Scene == null) return;

        Building = true;

        Speed = 0;

        Layout = new();

        Transform.WorldPosition = new Vector3(0, Layout.RideHeight, 0);

        Transform.WorldRotation = Quaternion.Identity;

        _motion.Reset();

        if (!FreeBuilding && !GameObject.Children.Any(c => c.Name == "Chassis"))

            CreateModelVisual(GameObject.Scene, Assets, PartsDirectory + "/scrap_frame_2x1.glb", GameObject, "Chassis");

        _camera = GameObject.Scene.ActiveCamera;

        if (_camera == null)

            _camera = Own("Garage camera").AddComponent(new Camera3D());

        if(AutomaticCamera){_camera.ActiveGameCamera = true;_camera.FieldOfView = 52;}

        CreateYard();

        CreateSkidPool();

        CreateWorkshopAtmosphere();

        if(FreeBuilding) StartAssembly();

        if(FreeBuilding) CreateFreeHud(); else CreateHud();

        _markers = (FreeBuilding ? Array.Empty<VehicleMount>() : Layout.ActiveMounts).Select((m, i) =>

        {

            var marker = Own("Mount: " + m.Name);

            marker.SetParent(GameObject, false);

            marker.Transform.LocalPosition = m.Position;

            marker.Transform.LocalScale = new Vector3(.10f);

            marker.AddComponent(new MeshRenderer { UsePrimitive = true, Primitive = PrimitiveMeshType.Sphere,

                Material = new Material { BaseColor = new Vector4(.2f,.95f,.65f,1) }, CastShadows = false });

            return marker;

        }).ToArray();

        RefreshGhost();

        if(AutomaticCamera)UpdateCamera(1);

        RefreshHud();

    }



    private GameObject Own(string name)

    {

        var obj = GameObject.Scene!.CreateGameObject(name);

        _owned.Add(obj);

        return obj;

    }



    public static GameObject CreateModelVisual(ByteEngine.Core.Scene.Scene scene, AssetManager assets, string path,

        GameObject parent, string name, bool ghost = false)

    {

        var reference = new AssetReference(path);

        var model = assets.LoadModel(reference);

        var root = scene.CreateGameObject(name);

        root.SetParent(parent, false);

        root.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI);

        root.AddComponent(new ModelHierarchyInstance { Model = reference });

        if (!ghost && model.Skeleton is { Bones.Count: > 0 } && model.Animations.Count > 0)

        {

            root.AddComponent(new SkeletalMeshRenderer { Model = reference, PlayOnStart = false, TransitionDuration = 0 });

            return root;

        }

        var nodes = model.Nodes.ToDictionary(n => n.Key, n => scene.CreateGameObject(n.Name));

        foreach (var node in model.Nodes)

        {

            var obj = nodes[node.Key];

            obj.SetParent(node.ParentKey != null && nodes.TryGetValue(node.ParentKey, out var p) ? p : root, false);

            if (Matrix4x4.Decompose(node.LocalTransform, out var scale, out var rotation, out var position))

            {

                obj.Transform.LocalPosition = position;

                obj.Transform.LocalRotation = rotation;

                obj.Transform.LocalScale = scale;

            }

            foreach (var key in node.MeshKeys)

            {

                var mesh = model.Meshes.First(m => m.Key == key);

                var child = scene.CreateGameObject(mesh.Name);

                child.SetParent(obj, false);

                var renderer = new MeshRenderer

                {

                    Mesh = assets.GetModelMesh(reference, key),

                    MeshReference = new ModelMeshReference(reference, key),

                    CastShadows = !ghost

                };

                if (ghost) renderer.Material = new Material { BaseColor = new Vector4(.2f,1,.65f,.4f), BlendMode = BlendMode3D.AlphaBlend };

                else if (mesh.MaterialKey != null)

                {

                    renderer.MaterialReference = new ModelMaterialReference(reference, mesh.MaterialKey);

                    renderer.Material = assets.GetModelMaterial(reference, mesh.MaterialKey);

                }

                child.AddComponent(renderer);

            }

        }

        return root;

    }



    private void CreateYard()

    {

        Box("Test yard floor", new(0,-.18f,0), new(64,.3f,64), new(.13f,.125f,.105f,1));

        for (int i=-4; i<=4; i++)

        {

            Box("Yard grid", new(i,-.022f,0), new(.012f,.006f,8), new(.19f,.18f,.155f,1));

            Box("Yard grid", new(0,-.022f,i), new(8,.006f,.012f), new(.19f,.18f,.155f,1));

        }

        Box("Build pad", new(0,-.008f,0), new(7,.02f,7), new(.17f,.165f,.14f,1));

        for (int side=0; side<4; side++)

        {

            Vector3 pos = side < 2 ? new(side==0 ? -31:31,.4f,0) : new(0,.4f,side==2 ? -31:31);

            Box("Yard barrier", pos, side<2 ? new(.45f,.8f,62) : new(62,.8f,.45f), new(.22f,.28f,.25f,1));

        }

        foreach (var p in new[] { new Vector2(8,-8), new Vector2(-9,-13), new Vector2(14,8), new Vector2(-14,10) })

        {

            Box("Scrap crate", new(p.X,.65f,p.Y), new(2,1.3f,2), new(.31f,.26f,.18f,1));

            _obstacles.Add((p, new Vector2(1)));

        }

        for(int i=0;i<6;i++) Box("Slalom marker", new(6,.25f,-3-i*3), new(.45f,.5f,.45f), new(.9f,.65f,.18f,1));

    }



    private void Box(string name, Vector3 position, Vector3 scale, Vector4 color)

    {

        var obj = Own(name);

        obj.Transform.WorldPosition = position;

        obj.Transform.LocalScale = scale;

        obj.AddComponent(new MeshRenderer { UsePrimitive = true, CastShadows=!(name.Contains("floor") || name.Contains("grid") || name=="Build pad"), Material = new Material { BaseColor=color, Roughness=.85f } });

    }



    private GameObject HudObject(string name, bool buildOnly)

    {

        var obj = Own(name);

        obj.SetParent(_canvas, false);

        if (buildOnly) _buildUi.Add(obj);

        return obj;

    }

    private UiText Text(string name, string text, Vector2 offset, int size, bool buildOnly=false) =>

        HudObject(name, buildOnly).AddComponent(new UiText { Text=text, Offset=offset, FontSize=size,

            Color=new(.91f,.90f,.86f,1), FontReference=new AssetReference("Assets/GarageUI/fonts/Barlow-Regular.ttf"), ShadowColor=new(0,0,0,.20f), ShadowOffset=new(0,1), OrderInLayer=10 });

    private UiWidget Button(string label, Vector2 offset, Vector2 size, Action click, bool buildOnly)

    {

        var button = HudObject(label, buildOnly).AddComponent(new UiWidget { Kind=UiWidgetKind.Button, Label=label,

            FontSize=16, FontReference=new AssetReference("Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf"), Offset=offset, Size=size, Color=new(.16f,.23f,.13f,1), HoverColor=new(.33f,.43f,.19f,1), OrderInLayer=5 });

        _buttons.Add((button,click));

        return button;

    }



    protected override void OnUpdate()

    {

        TraceBuildState();
        if (_camera == null) return;

        float dt = Math.Clamp((float)Time.DeltaTime,0,.05f);

        Input.NotifyGameViewPointerAim();

        AdaptHudViewport(Input.GameViewSize);

        if(_canvas!=null)_canvas.Active=ShowWorkshopHud;
        bool clickedUi=false;

        foreach (var item in _buttons.ToArray())

            if (UseBuiltInPointerControls && item.Button.GameObject.ActiveInHierarchy && item.Button.IsHovered && Input.IsMouseButtonPressedForUi(MouseButton.Left))

            { if(FreeBuilding)_selectSound?.Play();item.Click(); clickedUi=true; break; }

        if (ControlKeyPressed(Key.B)||(FreeBuilding&&ControlKeyPressed(Key.Space)&&Building)) ToggleDrive();

        if (ControlKeyPressed(Key.F5)) SaveBuild();

        if (ControlKeyPressed(Key.F9)) LoadBuild();

        if (ControlKeyPressed(Key.R) && (!Building || !FreeBuilding)) ResetPosition();

        if (Building && FreeBuilding) UpdateFreeBuilding(clickedUi);

        else if (Building)

        {

            bool control=ControlKeyDown(Key.LeftControl) || ControlKeyDown(Key.RightControl);

            if(control && ControlKeyPressed(Key.Z)) UndoBuild();

            else if (ControlKeyPressed(Key.Z)) ChangePalette(-1);

            if(control && ControlKeyPressed(Key.Y)) RedoBuild();

            if (ControlKeyPressed(Key.X)) ChangePalette(1);

            if (ControlKeyPressed(Key.Tab)) CycleMount();

            if (ControlKeyPressed(Key.PageUp)) SelectPart((_part+1)%BuilderPartFiles.Length);

            if (ControlKeyPressed(Key.PageDown)) SelectPart((_part+BuilderPartFiles.Length-1)%BuilderPartFiles.Length);

            if (ControlKeyPressed(Key.Delete) || Input.IsMouseButtonPressed(MouseButton.Right)) RemoveSelected();

            if (ControlKeyPressed(Key.Enter) && !clickedUi) PlaceSelected();

            if (!PointerOnHud() && Input.IsGameViewHovered)

            {

                if(Input.IsMouseButtonDown(MouseButton.Middle))

                { var delta=Input.GameViewMouseDelta; _orbit-=delta.X*.008f; _elevation=Math.Clamp(_elevation+delta.Y*.006f,.22f,1.1f); }

                _zoom=Math.Clamp(_zoom-Input.Snapshot.MouseWheel*.6f,4,15);

                HoverMount();

            }

            _orbit += ((ControlKeyDown(Key.E)?1:0)-(ControlKeyDown(Key.Q)?1:0))*dt*1.5f;

            if (!clickedUi && Input.IsGameViewHovered && Input.IsMouseButtonPressed(MouseButton.Left) &&

                !PointerOnHud() && !_buttons.Any(b => b.Button.GameObject.ActiveInHierarchy && b.Button.IsHovered)) PickMount();

        }

        else

        {

            if(UseBuiltInControls)
            {
            StepDrive(((ControlKeyDown(Key.W)?1:0)-(ControlKeyDown(Key.S)?1:0)),

                ((ControlKeyDown(Key.D)?1:0)-(ControlKeyDown(Key.A)?1:0)), ControlKeyDown(Key.Space), dt,

                ControlKeyDown(Key.LeftShift) || ControlKeyDown(Key.RightShift));

            bool fire = ControlKeyDown(Key.F) || (!clickedUi && Input.IsGameViewHovered && Input.IsMouseButtonDown(MouseButton.Left));

            if (ControlKeyPressed(Key.F) || (!clickedUi && Input.IsGameViewHovered && Input.IsMouseButtonPressed(MouseButton.Left))) FireWeapons();

            SetPoweredWeapons(fire);

            if (ControlKeyPressed(Key.G)) ActivateMechanisms();
            }
            else StepDrive(_eventThrottle,_eventSteering,_eventBrake,dt,_eventDrift);

        }

        UpdateSkidMarks(dt);

        if(!FreeBuilding) FollowRoofPayload();

        else if(!Building&&_contraption==null) ApplyAssemblyPose(_freeSteering);

        RefreshHud();

    }



    private void SelectPart(int index)

    {

        if (!Building) return;

        if (index < 0 || index >= BuilderPartFiles.Length) return;

        if (!FreeBuilding && BuilderPartFiles[index]=="scrap_frame_long") { SetChassis("scrap_frame_long"); return; }

        if(FreeBuilding&&_catalog?.Standard!=true&&index==15)return;

        if(FreeBuilding){CancelMove();_eraseMode=false;}

        _part=index;

        _category=Array.FindIndex(Categories,g=>g.Contains(index));if(_category<0)return;

        _palettePage=Array.IndexOf(Categories[_category],index)/6; RefreshPalette();

        if(FreeBuilding) {RefreshFreeGhost(); return;}

        int available=Array.FindIndex(Layout.ActiveMounts, m => m.Parts.Contains(SelectedPart) &&

            !Layout.Parts.ContainsKey(Array.IndexOf(Layout.ActiveMounts,m)));

        _mount=available>=0 ? available : Array.FindIndex(Layout.ActiveMounts,m=>m.Parts.Contains(SelectedPart));

        RefreshGhost();

    }

    private void CycleMount()

    {

        if (!Building) return;

        for (int i=1;i<=Layout.ActiveMounts.Length;i++)

        {

            int next=(_mount+i)%Layout.ActiveMounts.Length;

            if (Layout.ActiveMounts[next].Parts.Contains(SelectedPart)) { _mount=next; break; }

        }

        RefreshGhost();

    }

    private void PickMount()

    {

        var ray=_camera!.ScreenPointToRay(Input.GameViewPointerNormalized, Input.GameViewSize.X / Math.Max(1,Input.GameViewSize.Y));

        float nearest=float.MaxValue; int selected=-1;

        for (int i=0;i<_markers.Length;i++)

        {

            if (!_markers[i].Active) continue;

            Vector3 delta=_markers[i].Transform.WorldPosition-ray.Origin;

            float distance=Vector3.Dot(delta,ray.Direction);

            if (distance>0 && distance<nearest && (delta-ray.Direction*distance).Length()<.24f)

            { nearest=distance; selected=i; }

        }

        if (selected<0) return;

        _mount=selected;

        RefreshGhost();

        PlaceSelected();

    }

    public bool AttachPart(int mount, string part)

    {

        if (!Building || Assets==null || !Layout.CanPlace(mount,part)) return false;

        RecordEdit();

        GameObject visual=CreateModelVisual(GameObject.Scene!,Assets,PartsDirectory+"/"+part+".glb",GameObject,Layout.ActiveMounts[mount].Name);

        ApplyPlacement(visual,mount,part);

        if (visual.GetComponent<SkeletalMeshRenderer>() is { } mechanism) _mechanisms[mount]=mechanism;

        Layout.Place(mount,part);

        _visuals.Add(mount,visual);

        if (mount==12) FollowRoofPayload();

        Transform.WorldPosition=new(Transform.WorldPosition.X,Layout.RideHeight,Transform.WorldPosition.Z);

        return true;

    }

    private void PlaceSelected()

    {

        if(FreeBuilding){PlaceFreePreview();return;}

        if (PlaceAtSelected())

        { _message="Attached " + BuilderPartNames[_part]+". "+Layout.DriveRequirement+"."; SelectPart(_part); }

        else _message=Layout.PlacementIssue(_mount,SelectedPart);

    }

    public bool RemovePart(int mount)

    {

        if (!Building || !Layout.Parts.ContainsKey(mount)) return false;

        RecordEdit(); Layout.Remove(mount);

        _mechanisms.Remove(mount);

        if (_visuals.Remove(mount,out var obj)) GameObject.Scene!.DestroyGameObject(obj);

        Transform.WorldPosition=new(Transform.WorldPosition.X,Layout.RideHeight,Transform.WorldPosition.Z);

        return true;

    }

    private void RemoveSelected()

    {

        if(FreeBuilding){RemoveAssemblyPart(_selectedBlock);return;}

        if (RemovePart(_mount)) _message="Removed part. "+Layout.DriveRequirement+".";

        RefreshGhost();

    }

    public static (Vector3 Position, Quaternion Rotation) PartPlacement(int mount, string part, string chassis="scrap_frame_2x1")

    {

        Vector3 target=VehicleBuildLayout.GetMounts(chassis)[mount].Position, anchor=Vector3.Zero;

        Quaternion rotation=Quaternion.Identity;

        switch (part)

        {

            case "scrap_wheel_large": case "scrap_wheel_small":

                if (mount%2==1) rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI);

                break;

            case "scrap_axle_2m": anchor=new(0,.31f,0); break;

            case "scrap_engine_block": anchor=new(0,-.44f,0); break;

            case "scrap_cab_shell": anchor=new(0,-.53f,0); break;

            case "scrap_armor_plate": rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitZ,mount==8 ? MathF.PI/2 : -MathF.PI/2); break;

            case "scrap_ram_wedge": anchor=new(0,.06f,.58f); break;

            case "scrap_forked_ram": anchor=new(0,0,.47f); break;

            case "scrap_crushing_drum": anchor=new(0,0,.44f); break;

            case "scrap_battering_fist": anchor=new(0,0,.66f); break;

            case "scrap_auger_drill": anchor=new(0,0,.52f); break;

            case "scrap_saw_module": anchor=new(.29f,-.02f,.61f); break;

            case "scrap_hammer_arm": anchor=new(0,0,.29f); break;

            case "scrap_grabber_jaws": anchor=new(0,0,.47f); break;

            case "scrap_weapon_mount": anchor=new(0,-.10f,0); break;

            case "scrap_swivel_turret": anchor=new(0,-.23f,0); break;

            case "scrap_lift_mast": anchor=new(0,-.24f,0); break;

            case "scrap_catapult_basket": anchor=new(0,-.29f,0); break;

            case "scrap_frame_long": anchor=new(0,0,-1.9f); break;

            case "scrap_track_pod": anchor=new(-.34f,.36f,0); if (mount==19) rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI); break;

            case "scrap_outrigger_arm":

                anchor=new(-.61f,.16f,0);

                if (mount==25) rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI);

                break;

        }

        Vector3 position=target-Vector3.Transform(anchor,rotation);

        // Refined sources already face -Z; cancel the importer correction before applying the mounting rotation.

        rotation=Quaternion.CreateFromRotationMatrix(ImportAlignment*Matrix4x4.CreateFromQuaternion(rotation));

        return (position,rotation);

    }

    private void ApplyPlacement(GameObject visual,int mount,string part)

    {

        var placement=PartPlacement(mount,part,Layout.Chassis);

        visual.Transform.LocalPosition=placement.Position; visual.Transform.LocalRotation=placement.Rotation;

    }

    private void RefreshGhost()

    {

        if(FreeBuilding){RefreshFreeGhost();return;}

        if (Assets==null || !Building) return;

        if (_ghost!=null) GameObject.Scene!.DestroyGameObject(_ghost);

        _ghost=null;

        if (Layout.ActiveMounts[_mount].Parts.Contains(SelectedPart))

        {

            _ghost=CreateModelVisual(GameObject.Scene!,Assets,PartsDirectory+"/"+SelectedPart+".glb",GameObject,"Placement preview",true);

            ApplyPlacement(_ghost,_mount,SelectedPart);

            if (_mount==21) PositionRoofPayload(_ghost,SelectedPart);

            TintGhost(Layout.PlacementIssue(_mount,SelectedPart));

        }

        for(int i=0;i<_markers.Length;i++)

        {

            _markers[i].Transform.LocalPosition=Layout.ActiveMounts[i].Position;

            _markers[i].Active=Layout.ActiveMounts[i].Parts.Contains(SelectedPart);

            _markers[i].GetComponent<MeshRenderer>()!.Material.BaseColor=Layout.Parts.ContainsKey(i) ? new(1,.32f,.12f,1) :

                i==_mount ? new(1,.87f,.25f,1) : new(.2f,.95f,.65f,1);

        }

    }

    public bool BeginDriving()

    {

        if(FreeBuilding)return BeginAssemblyDriving();

        if (!Layout.CanDrive) return false;

        Building=false; Speed=0; _motion.Reset(_motion.Yaw);

        if (_mechanisms.TryGetValue(6,out var engine)) PlayClip(engine,"EngineIdle",true);

        if (_ghost!=null) { GameObject.Scene!.DestroyGameObject(_ghost); _ghost=null; }

        foreach(var marker in _markers) marker.Active=false;

        foreach(var ui in _buildUi) ui.Active=false;

        _message="Hold Shift while steering to drift. F or left click uses weapons; G cycles mount mechanisms.";

        return true;

    }

    private void ToggleDrive()

    {

        if (Building)

        { if (!BeginDriving()) _message=(FreeBuilding?Assembly!.DriveRequirement:Layout.DriveRequirement)+" before driving."; }

        else

        {

            foreach(var obj in _projectileVisuals.Values)if(obj.Scene!=null)obj.Scene.DestroyGameObject(obj);_projectileVisuals.Clear();

        _contraption?.Dispose();_contraption=null;foreach(var rig in _assemblyRigs.Values)rig.ClearPhysicalBoneDeformations();

            Building=true; Speed=0; _motion.Stop();

            foreach(var mechanism in _mechanisms.Values) mechanism.Stop();

            if(FreeBuilding) {foreach(var rig in _assemblyRigs.Values)rig.Stop();foreach(var visual in _assemblyVisuals.Values)visual.SetParent(GameObject,false);ApplyAssemblyPose();Transform.WorldPosition=_simulationOrigin;Transform.WorldRotation=_simulationRotation;RefreshSocketMarkers();}

            foreach(var pair in _visuals) ApplyPlacement(pair.Value,pair.Key,Layout.Parts[pair.Key]);

            foreach(var ui in _buildUi) ui.Active=true;

            RefreshPalette(); RefreshGhost();

            _message="Back in the garage. Your assembled parts are kept.";

        }

    }

    public void StepDrive(float throttle, float steering, bool brake, float dt, bool drift=false)

    {

        if(FreeBuilding){_freeSteering=steering;StepAssemblyDrive(throttle,steering,brake,dt,drift);return;}

        if (Building || dt<=0 || !float.IsFinite(dt)) return;

        float cap=Math.Clamp(MaximumSpeed,1,30)/(1+Math.Max(0,Layout.Parts.Count-8)*.025f);

        Vector3 movement=_motion.Step(throttle,steering,brake,drift,dt,cap,RoadGrip,DriftGrip);

        Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,_motion.Yaw);

        Vector3 next=Transform.WorldPosition+movement;

        if (Math.Abs(next.X)>28.5f || Math.Abs(next.Z)>28.5f || _obstacles.Any(o =>

            Math.Abs(next.X-o.Center.X)<o.Half.X+1.3f && Math.Abs(next.Z-o.Center.Y)<o.Half.Y+1.7f)) _motion.Stop();

        else Transform.WorldPosition=next;

        Speed=_motion.ForwardSpeed;

        _rollDistance+=Speed*Math.Min(dt,.05f);

        foreach(var pair in _mechanisms)

        {

            string part=Layout.Parts[pair.Key]; var mechanism=pair.Value;

            if (pair.Key<4)

            {

                var placement=PartPlacement(pair.Key,part,Layout.Chassis);

                _visuals[pair.Key].Transform.LocalRotation=placement.Rotation*Quaternion.CreateFromAxisAngle(Vector3.UnitY,pair.Key<2 ? -steering*.35f : 0);

                float radius=part=="scrap_wheel_large" ? .64f : .4125f;

                SampleCycle(mechanism,"Roll",_rollDistance/(MathF.Tau*radius)*(pair.Key%2==0 ? -1 : 1));

            }

            else if (part=="scrap_axle_2m") SampleCycle(mechanism,"AxleSpin",-_rollDistance/(MathF.Tau*.64f));

            else if (part=="scrap_track_pod") SampleCycle(mechanism,"TrackDrive",_rollDistance/3.8f*(pair.Key==19 ? -1 : 1));

            else if (part=="scrap_engine_block") mechanism.Speed=.7f+Math.Abs(Speed)*.10f;

            else if (part=="scrap_cab_shell" && !(mechanism.IsPlaying && mechanism.CurrentAnimation.EndsWith("__ShiftLever"))) SampleCycle(mechanism,"SteerWheel",Math.Abs(steering)*.5f);

            else if (part=="scrap_suspension_piston") SampleCycle(mechanism,"Compress",Math.Min(.45f,Math.Abs(_motion.LateralSpeed)*.06f));

        }

        FollowRoofPayload();

    }

    private static bool PlayClip(SkeletalMeshRenderer mechanism,string suffix,bool loop)

    {

        string? name=mechanism.ResolvedModel?.Animations.FirstOrDefault(a=>a.Name.EndsWith("__"+suffix,StringComparison.Ordinal))?.Name;

        return name!=null && mechanism.Play(name,loop,0);

    }

    private static void SampleCycle(SkeletalMeshRenderer mechanism,string suffix,float phase)

    {

        if (PlayClip(mechanism,suffix,false)) { mechanism.Seek((phase-MathF.Floor(phase))*mechanism.Duration); mechanism.Pause(); }

    }

    public int FireWeapons()

    {

        if(_contraption!=null)

        {

            int activated=_contraption.Fire();foreach(var pair in _assemblyRigs)if(_catalog![Assembly!.Parts[pair.Key].File].ReferenceId is 11 or 53 or 61)PlayClip(pair.Value,"Extend",false);return activated;

        }

        if (Building) return 0;

        int fired=0;

        foreach(var pair in ActiveRigs())

        {

            string? action=pair.File switch

            {

                "scrap_battering_fist"=>"Punch", "scrap_hammer_arm"=>"Smash", "scrap_grabber_jaws"=>"Grip",

                "scrap_catapult_basket"=>"Launch", _=>null

            };

            if (action!=null && !pair.Rig.IsPlaying && PlayClip(pair.Rig,action,false))

            { pair.Rig.Seek(0); fired++; }

        }

        return fired;

    }

    public void SetPoweredWeapons(bool powered)

    {

        _powered=powered;

        if (Building) return;

        foreach(var pair in ActiveRigs())

        {

            string? action=pair.File switch

            {

                "scrap_auger_drill"=>"Drill", "scrap_crushing_drum"=>"Crush",

                "scrap_saw_disc" or "scrap_saw_module"=>"Cut", _=>null

            };

            if (action==null) continue;

            if (powered) PlayClip(pair.Rig,action,true); else pair.Rig.Pause();

        }

    }

    public int ActivateMechanisms()

    {

        if(_contraption!=null)

        {

            _contraption.Activate();foreach(var pair in _assemblyRigs)

            {

                var def=_catalog![Assembly!.Parts[pair.Key].File];

                if(def.ReferenceId==97)SampleCycle(pair.Value,"Deploy",_contraption.MechanismsActive?.999f:0);

                if(def.ReferenceId==30)PlayClip(pair.Value,"Extend",false);

            }

            return 1;

        }

        if (Building) return 0;

        int count=0;

        foreach(var pair in ActiveRigs())

        {

            string? action=pair.File switch

            {

                "scrap_weapon_mount"=>"AimYaw", "scrap_swivel_turret"=>pair.Rig.CurrentAnimation.EndsWith("__AimYaw") ? "AimPitch" : "AimYaw",

                "scrap_cab_shell"=>"ShiftLever", "scrap_catapult_basket"=>"WindUp", "scrap_lift_mast"=>"Lift",

                "scrap_outrigger_arm"=>"Adjust", _=>null

            };

            if (action!=null && !pair.Rig.IsPlaying && PlayClip(pair.Rig,action,false)) { pair.Rig.Seek(0); count++; }

        }

        return count;

    }

    private void FollowRoofPayload()

    {

        if (!_visuals.TryGetValue(21,out var payload) || !Layout.Parts.TryGetValue(21,out string? part)) return;

        ApplyPlacement(payload,21,part);

        PositionRoofPayload(payload,part);

    }

    private void PositionRoofPayload(GameObject payload,string part)

    {

        if (!_visuals.TryGetValue(12,out var mount) || !_mechanisms.TryGetValue(12,out var rig)) return;

        string basePart=Layout.Parts[12];

        string bone=basePart=="scrap_lift_mast" ? "Carriage" : basePart=="scrap_swivel_turret" ? "Pitch" : "Yaw";

        Vector3 socket=basePart=="scrap_lift_mast" ? new(0,.30f,-.40f) : basePart=="scrap_swivel_turret" ? new(0,.61f,0) : new(0,.48f,0);

        if (!rig.TryGetBoneModelMatrix(bone,out Matrix4x4 current)) return;

        var bind=rig.ResolvedModel?.Skeleton?.Bones.FirstOrDefault(b=>b.Name==bone);

        if (bind==null) return;

        Matrix4x4 delta=bind.BindPose*current;

        Vector3 anchor=Vector3.Transform(socket,delta*mount.Transform.LocalMatrix);

        Matrix4x4.Decompose(delta*mount.Transform.LocalMatrix,out _,out Quaternion rotation,out _);

        if (part=="scrap_catapult_basket") payload.Transform.LocalPosition=anchor+Vector3.Transform(new Vector3(0,.29f,0),rotation);

        else

        {

            var placement=PartPlacement(21,part,Layout.Chassis);

            Vector3 source=Layout.ActiveMounts[21].Position-placement.Position;

            payload.Transform.LocalPosition=anchor-Vector3.Transform(source,rotation);

        }

        payload.Transform.LocalRotation=Quaternion.CreateFromRotationMatrix(ImportAlignment*Matrix4x4.CreateFromQuaternion(rotation));

    }

    private void CreateSkidPool()

    {

        for(int i=0;i<_skidLife.Length;i++)

        {

            var obj=Own("Tire skid"); obj.Active=false; obj.Transform.LocalScale=new(.13f,.006f,.32f);

            var material=new Material { BaseColor=new(.045f,.045f,.035f,.7f), BlendMode=BlendMode3D.AlphaBlend };

            obj.AddComponent(new MeshRenderer { UsePrimitive=true,Material=material,CastShadows=false });

            _skids.Add((obj,material));

        }

    }

    private void UpdateSkidMarks(float dt)

    {

        for(int i=0;i<_skids.Count;i++)

        {

            if (_skidLife[i]<=0) continue;

            _skidLife[i]=Math.Max(0,_skidLife[i]-dt);

            _skids[i].Object.Active=_skidLife[i]>0;

            _skids[i].Material.BaseColor=new(.045f,.045f,.035f,.65f*Math.Min(1,_skidLife[i]/2));

        }

        _skidTimer-=dt;

        if (Building || !IsDrifting || _skidTimer>0 || (FreeBuilding ? Assembly!.HasTracks : Layout.HasTracks)) return;

        _skidTimer=.055f;

        Vector3[] contacts=FreeBuilding ? Assembly!.Parts.Values.Where(p=>p.File.StartsWith("scrap_wheel_")).Select(p=>

            _assemblyVisuals[p.Id].Transform.WorldPosition-Vector3.UnitY*(p.File=="scrap_wheel_large"?.64f:.4125f)).Where(p=>p.Y<.2f).ToArray() :

            new[]{-1,1}.Select(side=>Transform.WorldPosition+Transform.Right*(side*1.02f)-Transform.Forward*.65f).ToArray();

        foreach(var position in contacts)

        {

            int i=_skidIndex++%_skids.Count; var mark=_skids[i];

            mark.Object.Transform.WorldPosition=new(position.X,.012f,position.Z);

            mark.Object.Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.Atan2(-Velocity.X,-Velocity.Z));

            mark.Object.Transform.LocalScale=new(.13f,.006f,Math.Max(.24f,Velocity.Length()*.085f));

            mark.Object.Active=true; _skidLife[i]=6;

        }

    }

    public void ResetPosition()

    {

        if(_contraption!=null){ToggleDrive();ResetPosition();BeginAssemblyDriving();return;}

        Speed=0; _motion.Reset(); _rollDistance=0;_workshopPan=Vector3.Zero;

        Transform.WorldPosition=new(0,FreeBuilding && Assembly!=null ? (Building?Math.Max(1.25f,Assembly.RideHeight):Assembly.RideHeight) : Layout.RideHeight,0);

        Transform.WorldRotation=Quaternion.Identity;

        _message="Vehicle returned to the build pad.";

    }

    protected override void OnLateUpdate(){if(AutomaticCamera)UpdateCamera(Math.Clamp((float)Time.DeltaTime*6,0,1));}

    private void UpdateCamera(float blend)

    {

        if (_camera==null) return;

        Vector3 target=Transform.WorldPosition+Vector3.UnitY*.5f+(FreeBuilding && Building?_workshopPan:Vector3.Zero);

        Vector3 offset=Building ? new(MathF.Sin(_orbit)*_zoom*MathF.Cos(_elevation),_zoom*MathF.Sin(_elevation),MathF.Cos(_orbit)*_zoom*MathF.Cos(_elevation)) :

            -Transform.Forward*6.8f+Vector3.UnitY*3.4f;

        _camera.Transform.WorldPosition=Vector3.Lerp(_camera.Transform.WorldPosition,target+offset,blend);

        Matrix4x4.Invert(Matrix4x4.CreateLookAt(_camera.Transform.WorldPosition,target,Vector3.UnitY),out var world);

        _camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(world);

    }

    public void SaveBuild()

    {

        if(FreeBuilding){SaveAssembly();return;}

        try

        {

            Directory.CreateDirectory(Path.GetDirectoryName(SavePath)!);

            File.WriteAllText(SavePath+".tmp",Layout.ToJson());

            File.Move(SavePath+".tmp",SavePath,true);

            _message="Vehicle saved. F9 restores it after restarting Play.";

        }

        catch(Exception e) when(e is IOException or UnauthorizedAccessException) { _message="Save failed: "+e.Message; }

    }

    public void LoadBuild()

    {

        if(FreeBuilding){LoadAssembly();return;}

        if (!Building) { _message="Press B to return to build mode before loading."; return; }

        try

        {

            var loaded=VehicleBuildLayout.FromJson(File.ReadAllText(SavePath));

            // Resolve every model before changing the current assembly.

            foreach(var p in loaded.Parts.Values) Assets!.LoadModel(new AssetReference(PartsDirectory+"/"+p+".glb"));

            RecordEdit(); RestoreLayout(loaded);

            ResetPosition(); RefreshGhost(); _message="Saved vehicle restored. "+Layout.DriveRequirement+".";

        }

        catch(Exception e) when(e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)

        { _message="Could not load build: "+e.Message; }

    }

    protected override void OnStop()

    {

        foreach(var obj in _projectileVisuals.Values)if(obj.Scene!=null)obj.Scene.DestroyGameObject(obj);_projectileVisuals.Clear();

        _contraption?.Dispose();_contraption=null;

        CancelMove();

        foreach(var obj in _assemblyVisuals.Values.ToArray())if(obj.Scene!=null)obj.Scene.DestroyGameObject(obj);

        foreach(var marker in _socketMarkers.ToArray())if(marker.Scene!=null)marker.Scene.DestroyGameObject(marker);_socketMarkers.Clear();_socketMarkerKeys.Clear();

        _assemblyVisuals.Clear();_assemblyRigs.Clear();_assemblyUndo.Clear();_assemblyRedo.Clear();_freeCardBorders.Clear();Assembly=null;

        foreach(var obj in _visuals.Values.ToArray()) GameObject.Scene?.DestroyGameObject(obj);

        if (_ghost!=null) GameObject.Scene?.DestroyGameObject(_ghost);

        foreach(var obj in _owned.ToArray()) if (obj.Scene!=null) obj.Scene.DestroyGameObject(obj);

        _owned.Clear(); _mechanisms.Clear(); _palette.Clear(); _skids.Clear(); _motion.Reset(); Array.Clear(_skidLife); _visuals.Clear(); _buttons.Clear(); _buildUi.Clear(); _obstacles.Clear(); _markers=[];

        _ghost=null; _camera=null;_workshopPan=Vector3.Zero; _undo.Clear(); _redo.Clear(); _cardImages.Clear(); _cardLabels.Clear(); _categoryButtons.Clear(); _axleBridges.Clear(); _part=0; _category=0; _palettePage=0;

    }

}
