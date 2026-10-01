using System.Numerics;
using ByteEngine.Core.Assets;
using ByteEngine.Core.Graphics;
using ByteEngine.Core.Graphics.ThreeD;
using ByteEngine.Core.Scene;

namespace ByteEngine.Core.Construction;

/// <summary>Socket-based garage and arcade test driving. Parts stay rigidly parented to the frame.</summary>
public sealed class VehicleBuilder3D : Component
{
    internal AssetManager? Assets { get; set; }
    internal string ProjectRoot { get; set; } = "";
    public string PartsDirectory { get; set; } = "Assets/parts";
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
    private float _yaw, _orbit = .65f, _wheelAngle;
    private string _message = "Choose a part, then click a glowing mount.";
    private string SelectedPart => VehicleBuildLayout.PartFiles[_part];
    private string SavePath => Path.Combine(ProjectRoot, "Saves", "vehicle-build.json");
    public override int UpdateOrder => 50;

    protected override void OnStart()
    {
        if (Assets == null || GameObject.Scene == null) return;
        Building = true;
        Speed = 0;
        Layout = new();
        Transform.WorldPosition = new Vector3(0, .73f, 0);
        Transform.WorldRotation = Quaternion.Identity;
        _yaw = 0;
        if (!GameObject.Children.Any(c => c.Name == "Chassis"))
            CreateModelVisual(GameObject.Scene, Assets, PartsDirectory + "/scrap_frame_2x1.glb", GameObject, "Chassis");
        _camera = GameObject.Scene.ActiveCamera;
        if (_camera == null)
            _camera = Own("Garage camera").AddComponent(new Camera3D());
        _camera.ActiveGameCamera = true;
        _camera.FieldOfView = 52;
        CreateYard();
        CreateHud();
        _markers = VehicleBuildLayout.Mounts.Select((m, i) =>
        {
            var marker = Own("Mount: " + m.Name);
            marker.SetParent(GameObject, false);
            marker.Transform.LocalPosition = m.Position;
            marker.Transform.LocalScale = new Vector3(.17f);
            marker.AddComponent(new MeshRenderer { UsePrimitive = true, Primitive = PrimitiveMeshType.Sphere,
                Material = new Material { BaseColor = new Vector4(.2f,.95f,.65f,1) }, CastShadows = false });
            return marker;
        }).ToArray();
        RefreshGhost();
        UpdateCamera(1);
        RefreshHud();
    }

    private GameObject Own(string name)
    {
        var obj = GameObject.Scene!.CreateGameObject(name);
        _owned.Add(obj);
        return obj;
    }

    public static GameObject CreateModelVisual(Scene.Scene scene, AssetManager assets, string path,
        GameObject parent, string name, bool ghost = false)
    {
        var reference = new AssetReference(path);
        var model = assets.LoadModel(reference);
        var root = scene.CreateGameObject(name);
        root.SetParent(parent, false);
        root.AddComponent(new ModelHierarchyInstance { Model = reference });
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
        Box("Test yard floor", new(0,-.18f,0), new(64,.3f,64), new(.23f,.26f,.23f,1));
        for (int i=-6; i<=6; i++)
        {
            Box("Yard grid", new(i*5,-.022f,0), new(.035f,.012f,60), new(.37f,.4f,.34f,1));
            Box("Yard grid", new(0,-.022f,i*5), new(60,.012f,.035f), new(.37f,.4f,.34f,1));
        }
        Box("Build pad", new(0,-.008f,0), new(5,.02f,5), new(.35f,.37f,.26f,1));
        for (int side=0; side<4; side++)
        {
            Vector3 pos = side < 2 ? new(side==0 ? -31:31,.4f,0) : new(0,.4f,side==2 ? -31:31);
            Box("Yard barrier", pos, side<2 ? new(.45f,.8f,62) : new(62,.8f,.45f), new(.55f,.41f,.2f,1));
        }
        foreach (var p in new[] { new Vector2(8,-8), new Vector2(-9,-13), new Vector2(14,8), new Vector2(-14,10) })
        {
            Box("Scrap crate", new(p.X,.65f,p.Y), new(2,1.3f,2), new(.44f,.31f,.2f,1));
            _obstacles.Add((p, new Vector2(1)));
        }
        for(int i=0;i<6;i++) Box("Slalom marker", new(6,.25f,-3-i*3), new(.45f,.5f,.45f), new(.9f,.65f,.18f,1));
    }

    private void Box(string name, Vector3 position, Vector3 scale, Vector4 color)
    {
        var obj = Own(name);
        obj.Transform.WorldPosition = position;
        obj.Transform.LocalScale = scale;
        obj.AddComponent(new MeshRenderer { UsePrimitive = true, Material = new Material { BaseColor=color, Roughness=.85f } });
    }

    private void CreateHud()
    {
        _canvas = Own("Goblin Scraper garage HUD");
        _canvas.AddComponent(new UiCanvas { ReferenceResolution = new(1280,720) });
        var panel = HudObject("Parts panel", true);
        panel.AddComponent(new UiWidget { Offset=new(16,76), Size=new(225,590), Color=new(.055f,.075f,.055f,.94f) });
        Text("Title", "GOBLIN SCRAPER  /  GARAGE", new(24,20), 24);
        _status = Text("Vehicle status", "", new(274,78), 21);
        _selection = Text("Selected mount", "", new(274,112), 17);
        _hint = Text("Garage controls", "", new(274,619), 17);
        _hint.WrapWidth = 960;
        Text("Parts heading", "SCRAP PARTS", new(30,88), 18, true);
        for (int i=0; i<VehicleBuildLayout.PartFiles.Length; i++)
        {
            int index = i;
            Button(VehicleBuildLayout.PartNames[i], new(28,120+i*31), new(201,28), () => SelectPart(index), true);
        }
        Button("Attach [Enter]", new(274,160), new(160,38), PlaceSelected, true);
        Button("Remove [Del]", new(444,160), new(160,38), RemoveSelected, true);
        Button("Next mount [Tab]", new(614,160), new(190,38), CycleMount, true);
        _driveButton = Button("Test drive [B]", new(1044,24), new(210,44), ToggleDrive, false);
        Button("Save [F5]", new(1000,632), new(118,36), SaveBuild, false);
        Button("Load [F9]", new(1130,632), new(118,36), LoadBuild, false);
        Button("Reset [R]", new(1130,676), new(118,30), ResetPosition, false);
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
            Color=new(.91f,.94f,.82f,1), ShadowColor=new(0,0,0,.85f), OrderInLayer=10 });
    private UiWidget Button(string label, Vector2 offset, Vector2 size, Action click, bool buildOnly)
    {
        var button = HudObject(label, buildOnly).AddComponent(new UiWidget { Kind=UiWidgetKind.Button, Label=label,
            FontSize=16, Offset=offset, Size=size, Color=new(.16f,.23f,.13f,1), HoverColor=new(.33f,.43f,.19f,1), OrderInLayer=5 });
        _buttons.Add((button,click));
        return button;
    }

    protected override void OnUpdate()
    {
        if (_camera == null) return;
        float dt = Math.Clamp((float)Time.DeltaTime,0,.05f);
        Input.NotifyGameViewPointerAim();
        bool clickedUi=false;
        foreach (var item in _buttons.ToArray())
            if (item.Button.GameObject.ActiveInHierarchy && item.Button.IsHovered && Input.IsMouseButtonPressedForUi(MouseButton.Left))
            { item.Click(); clickedUi=true; break; }
        if (Input.IsKeyPressed(Key.B)) ToggleDrive();
        if (Input.IsKeyPressed(Key.F5)) SaveBuild();
        if (Input.IsKeyPressed(Key.F9)) LoadBuild();
        if (Input.IsKeyPressed(Key.R)) ResetPosition();
        if (Building)
        {
            if (Input.IsKeyPressed(Key.Tab)) CycleMount();
            if (Input.IsKeyPressed(Key.PageUp)) SelectPart((_part+1)%VehicleBuildLayout.PartFiles.Length);
            if (Input.IsKeyPressed(Key.PageDown)) SelectPart((_part+VehicleBuildLayout.PartFiles.Length-1)%VehicleBuildLayout.PartFiles.Length);
            if (Input.IsKeyPressed(Key.Delete) || Input.IsMouseButtonPressed(MouseButton.Right)) RemoveSelected();
            if (Input.IsKeyPressed(Key.Enter) && !clickedUi) PlaceSelected();
            _orbit += ((Input.IsKeyDown(Key.E)?1:0)-(Input.IsKeyDown(Key.Q)?1:0))*dt*1.5f;
            if (!clickedUi && Input.IsGameViewHovered && Input.IsMouseButtonPressed(MouseButton.Left) &&
                !_buttons.Any(b => b.Button.GameObject.ActiveInHierarchy && b.Button.IsHovered)) PickMount();
        }
        else StepDrive(((Input.IsKeyDown(Key.W)?1:0)-(Input.IsKeyDown(Key.S)?1:0)),
            ((Input.IsKeyDown(Key.D)?1:0)-(Input.IsKeyDown(Key.A)?1:0)), Input.IsKeyDown(Key.Space), dt);
        RefreshHud();
    }

    private void SelectPart(int index)
    {
        if (!Building) return;
        _part=index;
        int available=Array.FindIndex(VehicleBuildLayout.Mounts, m => m.Parts.Contains(SelectedPart) &&
            !Layout.Parts.ContainsKey(Array.IndexOf(VehicleBuildLayout.Mounts,m)));
        _mount=available>=0 ? available : Array.FindIndex(VehicleBuildLayout.Mounts,m=>m.Parts.Contains(SelectedPart));
        RefreshGhost();
    }
    private void CycleMount()
    {
        if (!Building) return;
        for (int i=1;i<=VehicleBuildLayout.Mounts.Length;i++)
        {
            int next=(_mount+i)%VehicleBuildLayout.Mounts.Length;
            if (VehicleBuildLayout.Mounts[next].Parts.Contains(SelectedPart)) { _mount=next; break; }
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
        if (Layout.CanPlace(_mount,SelectedPart)) PlaceSelected();
        else _message="Mount occupied. Remove its part first.";
    }
    public bool AttachPart(int mount, string part)
    {
        if (!Building || Assets==null || !Layout.CanPlace(mount,part)) return false;
        GameObject visual=CreateModelVisual(GameObject.Scene!,Assets,PartsDirectory+"/"+part+".glb",GameObject,VehicleBuildLayout.Mounts[mount].Name);
        visual.Transform.LocalPosition=VehicleBuildLayout.Mounts[mount].Position;
        visual.Transform.LocalRotation=MountRotation(mount);
        Layout.Place(mount,part);
        _visuals.Add(mount,visual);
        Transform.WorldPosition=new(Transform.WorldPosition.X,Layout.RideHeight,Transform.WorldPosition.Z);
        return true;
    }
    private void PlaceSelected()
    {
        if (AttachPart(_mount,SelectedPart))
        { _message="Attached " + VehicleBuildLayout.PartNames[_part]+". "+Layout.DriveRequirement+"."; SelectPart(_part); }
        else _message="Choose an empty compatible mount.";
    }
    public bool RemovePart(int mount)
    {
        if (!Building || !Layout.Remove(mount)) return false;
        if (_visuals.Remove(mount,out var obj)) GameObject.Scene!.DestroyGameObject(obj);
        Transform.WorldPosition=new(Transform.WorldPosition.X,Layout.RideHeight,Transform.WorldPosition.Z);
        return true;
    }
    private void RemoveSelected()
    {
        if (RemovePart(_mount)) _message="Removed part. "+Layout.DriveRequirement+".";
        RefreshGhost();
    }
    private static Quaternion MountRotation(int mount) => mount==8 ? Quaternion.CreateFromAxisAngle(Vector3.UnitZ,MathF.PI/2) :
        mount==9 ? Quaternion.CreateFromAxisAngle(Vector3.UnitZ,-MathF.PI/2) : Quaternion.Identity;
    private void RefreshGhost()
    {
        if (Assets==null || !Building) return;
        if (_ghost!=null) GameObject.Scene!.DestroyGameObject(_ghost);
        _ghost=null;
        if (Layout.CanPlace(_mount,SelectedPart))
        {
            _ghost=CreateModelVisual(GameObject.Scene!,Assets,PartsDirectory+"/"+SelectedPart+".glb",GameObject,"Placement preview",true);
            _ghost.Transform.LocalPosition=VehicleBuildLayout.Mounts[_mount].Position;
            _ghost.Transform.LocalRotation=MountRotation(_mount);
        }
        for(int i=0;i<_markers.Length;i++)
        {
            _markers[i].Active=VehicleBuildLayout.Mounts[i].Parts.Contains(SelectedPart);
            _markers[i].GetComponent<MeshRenderer>()!.Material.BaseColor=Layout.Parts.ContainsKey(i) ? new(1,.32f,.12f,1) :
                i==_mount ? new(1,.87f,.25f,1) : new(.2f,.95f,.65f,1);
        }
    }
    public bool BeginDriving()
    {
        if (!Layout.CanDrive) return false;
        Building=false; Speed=0;
        if (_ghost!=null) { GameObject.Scene!.DestroyGameObject(_ghost); _ghost=null; }
        foreach(var marker in _markers) marker.Active=false;
        foreach(var ui in _buildUi) ui.Active=false;
        _message="W/S accelerate and reverse. A/D steer. Space brakes. B returns to build.";
        return true;
    }
    private void ToggleDrive()
    {
        if (Building)
        { if (!BeginDriving()) _message=Layout.DriveRequirement+" before driving."; }
        else
        {
            Building=true; Speed=0;
            foreach(var ui in _buildUi) ui.Active=true;
            RefreshGhost();
            _message="Back in the garage. Your assembled parts are kept.";
        }
    }
    public void StepDrive(float throttle, float steering, bool brake, float dt)
    {
        if (Building || dt<=0 || !float.IsFinite(dt)) return;
        dt=Math.Min(dt,.05f);
        throttle=Math.Clamp(throttle,-1,1); steering=Math.Clamp(steering,-1,1);
        float cap=Math.Clamp(MaximumSpeed,1,30)/(1+Math.Max(0,Layout.Parts.Count-8)*.025f);
        float target=brake ? 0 : throttle>=0 ? throttle*cap : throttle*cap*.45f;
        float change=(brake ? 24 : throttle==0 ? 3.5f : 7)*dt;
        Speed += Math.Clamp(target-Speed,-change,change);
        _yaw -= steering*Speed*dt*.16f;
        Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,_yaw);
        Vector3 next=Transform.WorldPosition+Transform.Forward*Speed*dt;
        if (Math.Abs(next.X)>28.5f || Math.Abs(next.Z)>28.5f || _obstacles.Any(o =>
            Math.Abs(next.X-o.Center.X)<o.Half.X+1.3f && Math.Abs(next.Z-o.Center.Y)<o.Half.Y+1.7f)) Speed=0;
        else Transform.WorldPosition=next;
        _wheelAngle-=Speed*dt/.55f;
        foreach(var pair in _visuals)
            if (pair.Key<4) pair.Value.Transform.LocalRotation=Quaternion.CreateFromYawPitchRoll(
                pair.Key<2 ? -steering*.35f : 0, _wheelAngle,0);
            else if (pair.Key==11) pair.Value.Transform.LocalRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitX,(float)Time.TotalTime*10);
    }
    public void ResetPosition()
    {
        Speed=0; _yaw=0;
        Transform.WorldPosition=new(0,Layout.RideHeight,0);
        Transform.WorldRotation=Quaternion.Identity;
        _message="Vehicle returned to the build pad.";
    }
    protected override void OnLateUpdate() => UpdateCamera(Math.Clamp((float)Time.DeltaTime*6,0,1));
    private void UpdateCamera(float blend)
    {
        if (_camera==null) return;
        Vector3 target=Transform.WorldPosition+Vector3.UnitY*.5f;
        Vector3 offset=Building ? new(MathF.Sin(_orbit)*7,4.6f,MathF.Cos(_orbit)*7) :
            -Transform.Forward*6.8f+Vector3.UnitY*3.4f;
        _camera.Transform.WorldPosition=Vector3.Lerp(_camera.Transform.WorldPosition,target+offset,blend);
        Matrix4x4.Invert(Matrix4x4.CreateLookAt(_camera.Transform.WorldPosition,target,Vector3.UnitY),out var world);
        _camera.Transform.WorldRotation=Quaternion.CreateFromRotationMatrix(world);
    }
    public void SaveBuild()
    {
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
        if (!Building) { _message="Press B to return to build mode before loading."; return; }
        try
        {
            var loaded=VehicleBuildLayout.FromJson(File.ReadAllText(SavePath));
            // Resolve every model before changing the current assembly.
            foreach(var p in loaded.Parts.Values) Assets!.LoadModel(new AssetReference(PartsDirectory+"/"+p+".glb"));
            foreach(int mount in Layout.Parts.Keys.ToArray()) RemovePart(mount);
            foreach(var p in loaded.Parts) AttachPart(p.Key,p.Value);
            ResetPosition(); RefreshGhost(); _message="Saved vehicle restored. "+Layout.DriveRequirement+".";
        }
        catch(Exception e) when(e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException or ArgumentException)
        { _message="Could not load build: "+e.Message; }
    }
    private void RefreshHud()
    {
        if (_status==null) return;
        _status.Text=Building ? "BUILD MODE  |  "+Layout.Parts.Count+" parts  |  "+Layout.DriveRequirement :
            "TEST DRIVE  |  "+Math.Abs(Speed*3.6f).ToString("0")+" km/h";
        _selection!.Text=Building ? VehicleBuildLayout.PartNames[_part]+"  >  "+VehicleBuildLayout.Mounts[_mount].Name+
            (Layout.Parts.ContainsKey(_mount) ? " (occupied)" : " (empty)") : "";
        _hint!.Text=_message+"\n"+(Building ? "Click part + glowing mount  |  Tab next mount  |  Q/E orbit  |  B drive" :
            "W/S throttle  |  A/D steer  |  Space brake  |  B build  |  R recover");
        _driveButton!.Label=Building ? "Test drive [B]" : "Return to build [B]";
        _driveButton.Interactable=!Building || Layout.CanDrive;
    }
    protected override void OnStop()
    {
        foreach(var obj in _visuals.Values.ToArray()) GameObject.Scene?.DestroyGameObject(obj);
        if (_ghost!=null) GameObject.Scene?.DestroyGameObject(_ghost);
        foreach(var obj in _owned.ToArray()) if (obj.Scene!=null) obj.Scene.DestroyGameObject(obj);
        _owned.Clear(); _visuals.Clear(); _buttons.Clear(); _buildUi.Clear(); _obstacles.Clear(); _markers=[];
        _ghost=null; _camera=null;
    }
}


