from pathlib import Path
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.cs');s=p.read_text()
s=s.replace('public string PartsDirectory { get; set; } = "Assets/parts";', 'public string PartsDirectory { get; set; } = "Assets/refinded parts";\n    public float RoadGrip { get; set; } = 12;\n    public float DriftGrip { get; set; } = .9f;\n    public bool IsDrifting => _motion.IsDrifting;\n    public float DriftAngle => _motion.DriftAngle;\n    public Vector3 Velocity => _motion.Velocity;\n    private readonly VehicleDriveMotion _motion = new();\n    private readonly Dictionary<int, SkeletalMeshRenderer> _mechanisms = new();\n    private readonly List<UiWidget> _palette = new();\n    private readonly List<(GameObject Object, Material Material)> _skids = new();\n    private readonly float[] _skidLife = new float[80];\n    private int _palettePage, _skidIndex;\n    private float _skidTimer, _rollDistance;')
s=s.replace('private float _yaw, _orbit = .65f, _wheelAngle;', 'private float _orbit = .65f;')
s=s.replace('Transform.WorldPosition = new Vector3(0, .73f, 0);','Transform.WorldPosition = new Vector3(0, Layout.RideHeight, 0);')
s=s.replace('_yaw = 0;', '_motion.Reset();')
s=s.replace('        CreateYard();','        CreateYard();\n        CreateSkidPool();')
# Existing runtime code created static meshes for animated imports. Use the engine skeletal component instead.
needle='        var nodes = model.Nodes.ToDictionary'
pos=s.index(needle)
s=s[:pos]+'''        if (!ghost && model.Skeleton is { Bones.Count: > 0 } && model.Animations.Count > 0)
        {
            root.AddComponent(new SkeletalMeshRenderer { Model = reference, PlayOnStart = false, TransitionDuration = 0 });
            return root;
        }
'''+s[pos:]
start=s.index('        for (int i=0; i<VehicleBuildLayout.PartFiles.Length; i++)')
end=s.index('        Button("Attach [Enter]"',start)
s=s[:start]+'''        for (int i=0; i<14; i++)
        {
            int index = i;
            _palette.Add(Button("", new(28,120+i*31), new(201,28),
                () => SelectPart(_palettePage*14+index), true));
        }
        Button("< Parts [Z]", new(28,566), new(96,30), () => ChangePalette(-1), true);
        Button("Parts [X] >", new(129,566), new(100,30), () => ChangePalette(1), true);
        RefreshPalette();
'''+s[end:]
s=s.replace('        if (Input.IsKeyPressed(Key.R)) ResetPosition();','        if (Input.IsKeyPressed(Key.R)) ResetPosition();')
s=s.replace('            if (Input.IsKeyPressed(Key.Tab)) CycleMount();','            if (Input.IsKeyPressed(Key.Z)) ChangePalette(-1);\n            if (Input.IsKeyPressed(Key.X)) ChangePalette(1);\n            if (Input.IsKeyPressed(Key.Tab)) CycleMount();')
s=s.replace('        else StepDrive(((Input.IsKeyDown(Key.W)?1:0)-(Input.IsKeyDown(Key.S)?1:0)),\n            ((Input.IsKeyDown(Key.D)?1:0)-(Input.IsKeyDown(Key.A)?1:0)), Input.IsKeyDown(Key.Space), dt);','''        else
        {
            StepDrive(((Input.IsKeyDown(Key.W)?1:0)-(Input.IsKeyDown(Key.S)?1:0)),
                ((Input.IsKeyDown(Key.D)?1:0)-(Input.IsKeyDown(Key.A)?1:0)), Input.IsKeyDown(Key.Space), dt,
                Input.IsKeyDown(Key.LeftShift) || Input.IsKeyDown(Key.RightShift));
            bool fire = Input.IsKeyDown(Key.F) || (!clickedUi && Input.IsGameViewHovered && Input.IsMouseButtonDown(MouseButton.Left));
            if (Input.IsKeyPressed(Key.F) || (!clickedUi && Input.IsGameViewHovered && Input.IsMouseButtonPressed(MouseButton.Left))) FireWeapons();
            SetPoweredWeapons(fire);
            if (Input.IsKeyPressed(Key.G)) ActivateMechanisms();
        }
        UpdateSkidMarks(dt);
        FollowRoofPayload();''')
s=s.replace('        _part=index;','        if (index < 0 || index >= VehicleBuildLayout.PartFiles.Length) return;\n        _part=index;\n        _palettePage=index/14; RefreshPalette();')
s=s.replace('        visual.Transform.LocalPosition=VehicleBuildLayout.Mounts[mount].Position;\n        visual.Transform.LocalRotation=MountRotation(mount);','        ApplyPlacement(visual,mount,part);\n        if (visual.GetComponent<SkeletalMeshRenderer>() is { } mechanism) _mechanisms[mount]=mechanism;')
s=s.replace('        _visuals.Add(mount,visual);','        _visuals.Add(mount,visual);\n        if (mount==12) FollowRoofPayload();')
s=s.replace('        if (_visuals.Remove(mount,out var obj))', '        _mechanisms.Remove(mount);\n        if (_visuals.Remove(mount,out var obj))')
start=s.index('    private static Quaternion MountRotation(');end=s.index('    private void RefreshGhost()',start)
s=s[:start]+'''    public static (Vector3 Position, Quaternion Rotation) PartPlacement(int mount, string part)
    {
        Vector3 target=VehicleBuildLayout.Mounts[mount].Position, anchor=Vector3.Zero;
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
            case "scrap_track_pod": if (mount==19) rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI); break;
            case "scrap_outrigger_arm":
                anchor=new(-.61f,.16f,0);
                if (mount==25) rotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.PI);
                break;
        }
        return (target-Vector3.Transform(anchor,rotation),rotation);
    }
    private static void ApplyPlacement(GameObject visual,int mount,string part)
    {
        var placement=PartPlacement(mount,part);
        visual.Transform.LocalPosition=placement.Position; visual.Transform.LocalRotation=placement.Rotation;
    }
'''+s[end:]
s=s.replace('            _ghost.Transform.LocalPosition=VehicleBuildLayout.Mounts[_mount].Position;\n            _ghost.Transform.LocalRotation=MountRotation(_mount);','            ApplyPlacement(_ghost,_mount,SelectedPart);')
s=s.replace('        Building=false; Speed=0;', '        Building=false; Speed=0; _motion.Reset(_motion.Yaw);\n        if (_mechanisms.TryGetValue(6,out var engine)) PlayClip(engine,"EngineIdle",true);')
s=s.replace('_message="W/S accelerate and reverse. A/D steer. Space brakes. B returns to build.";', '_message="Hold Shift while steering to drift. F or left click uses weapons; G cycles mount mechanisms.";')
s=s.replace('            Building=true; Speed=0;', '            Building=true; Speed=0; _motion.Stop();\n            foreach(var mechanism in _mechanisms.Values) mechanism.Stop();\n            foreach(var pair in _visuals) ApplyPlacement(pair.Value,pair.Key,Layout.Parts[pair.Key]);')
start=s.index('    public void StepDrive(');end=s.index('    public void ResetPosition()',start)
s=s[:start]+'''    public void StepDrive(float throttle, float steering, bool brake, float dt, bool drift=false)
    {
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
                var placement=PartPlacement(pair.Key,part);
                _visuals[pair.Key].Transform.LocalRotation=placement.Rotation*Quaternion.CreateFromAxisAngle(Vector3.UnitY,pair.Key<2 ? -steering*.35f : 0);
                float radius=part=="scrap_wheel_large" ? .64f : .4125f;
                SampleCycle(mechanism,"Roll",_rollDistance/(MathF.Tau*radius)*(pair.Key%2==0 ? -1 : 1));
            }
            else if (part=="scrap_axle_2m") SampleCycle(mechanism,"AxleSpin",-_rollDistance/(MathF.Tau*.64f));
            else if (part=="scrap_track_pod") SampleCycle(mechanism,"TrackDrive",_rollDistance/3.8f*(pair.Key==19 ? -1 : 1));
            else if (part=="scrap_engine_block") mechanism.Speed=.7f+Math.Abs(Speed)*.10f;
            else if (part=="scrap_cab_shell") SampleCycle(mechanism,"SteerWheel",Math.Abs(steering)*.5f);
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
        if (Building) return 0;
        int fired=0;
        foreach(var pair in _mechanisms)
        {
            string? action=Layout.Parts[pair.Key] switch
            {
                "scrap_battering_fist"=>"Punch", "scrap_hammer_arm"=>"Smash", "scrap_grabber_jaws"=>"Grip",
                "scrap_catapult_basket"=>"Launch", _=>null
            };
            if (action!=null && !pair.Value.IsPlaying && PlayClip(pair.Value,action,false))
            { pair.Value.Seek(0); fired++; }
        }
        return fired;
    }
    public void SetPoweredWeapons(bool powered)
    {
        if (Building) return;
        foreach(var pair in _mechanisms)
        {
            string? action=Layout.Parts[pair.Key] switch
            {
                "scrap_auger_drill"=>"Drill", "scrap_crushing_drum"=>"Crush",
                "scrap_saw_disc" or "scrap_saw_module"=>"Cut", _=>null
            };
            if (action==null) continue;
            if (powered) PlayClip(pair.Value,action,true); else pair.Value.Pause();
        }
    }
    public int ActivateMechanisms()
    {
        if (Building) return 0;
        int count=0;
        foreach(var pair in _mechanisms)
        {
            string? action=Layout.Parts[pair.Key] switch
            {
                "scrap_weapon_mount" or "scrap_swivel_turret"=>"AimYaw", "scrap_lift_mast"=>"Lift",
                "scrap_outrigger_arm"=>"Adjust", _=>null
            };
            if (action!=null && !pair.Value.IsPlaying && PlayClip(pair.Value,action,false)) { pair.Value.Seek(0); count++; }
        }
        return count;
    }
    private void FollowRoofPayload()
    {
        if (!_visuals.TryGetValue(21,out var payload) || !Layout.Parts.TryGetValue(21,out string? part)) return;
        ApplyPlacement(payload,21,part);
        if (!_visuals.TryGetValue(12,out var mount) || !_mechanisms.TryGetValue(12,out var rig)) return;
        string basePart=Layout.Parts[12];
        string bone=basePart=="scrap_lift_mast" ? "Carriage" : basePart=="scrap_swivel_turret" ? "Pitch" : "Yaw";
        Vector3 socket=basePart=="scrap_lift_mast" ? new(0,.30f,-.40f) : basePart=="scrap_swivel_turret" ? new(0,.61f,0) : new(0,.48f,0);
        if (!rig.TryGetBoneModelMatrix(bone,out Matrix4x4 current)) return;
        var bind=rig.ResolvedModel?.Skeleton?.Bones.FirstOrDefault(b=>b.Name==bone);
        if (bind==null) return;
        Matrix4x4 delta=bind.BindPose*current;
        Vector3 anchor=Vector3.Transform(socket,delta*mount.Transform.LocalMatrix);
        Matrix4x4.Decompose(delta,out _,out Quaternion rotation,out _);
        if (part=="scrap_catapult_basket") payload.Transform.LocalPosition=anchor+Vector3.Transform(new Vector3(0,.29f,0),rotation*mount.Transform.LocalRotation);
        else
        {
            var placement=PartPlacement(21,part);
            Vector3 source=VehicleBuildLayout.Mounts[21].Position-placement.Position;
            payload.Transform.LocalPosition=anchor-Vector3.Transform(source,rotation*mount.Transform.LocalRotation);
        }
        payload.Transform.LocalRotation=rotation*mount.Transform.LocalRotation;
    }
    private void ChangePalette(int direction)
    {
        _palettePage=(_palettePage+direction+2)%2; RefreshPalette(); SelectPart(_palettePage*14);
    }
    private void RefreshPalette()
    {
        for(int i=0;i<_palette.Count;i++)
        {
            int part=_palettePage*14+i;
            _palette[i].Label=VehicleBuildLayout.PartNames[part];
            _palette[i].Color=part==_part ? new(.34f,.43f,.18f,1) : new(.16f,.23f,.13f,1);
        }
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
        if (Building || !IsDrifting || _skidTimer>0 || Layout.HasTracks) return;
        _skidTimer=.055f;
        for(int side=-1;side<=1;side+=2)
        {
            int i=_skidIndex++%_skids.Count; var mark=_skids[i];
            Vector3 position=Transform.WorldPosition+Transform.Right*(side*1.02f)-Transform.Forward*.65f;
            mark.Object.Transform.WorldPosition=new(position.X,.012f,position.Z);
            mark.Object.Transform.WorldRotation=Quaternion.CreateFromAxisAngle(Vector3.UnitY,MathF.Atan2(-Velocity.X,-Velocity.Z));
            mark.Object.Active=true; _skidLife[i]=6;
        }
    }
'''+s[end:]
s=s.replace('        Speed=0; _motion.Reset();','        Speed=0; _motion.Reset(); _rollDistance=0;')
s=s.replace('"TEST DRIVE  |  "+Math.Abs(Speed*3.6f).ToString("0")+" km/h";', '"TEST DRIVE  |  "+(Velocity.Length()*3.6f).ToString("0")+" km/h"+(IsDrifting ? "  |  DRIFT "+DriftAngle.ToString("0")+" degrees" : "");')
s=s.replace('"Click part + glowing mount  |  Tab next mount  |  Q/E orbit  |  B drive"', '"Click part + mount  |  Z/X parts pages  |  Tab mount  |  Q/E orbit  |  B drive"')
s=s.replace('"W/S throttle  |  A/D steer  |  Space brake  |  B build  |  R recover"', '"W/S throttle  |  A/D steer  |  Shift drift  |  Space brake  |  F weapon  |  G mechanisms  |  B build"')
s=s.replace('_owned.Clear(); _visuals.Clear();', '_owned.Clear(); _mechanisms.Clear(); _palette.Clear(); _skids.Clear(); _motion.Reset(); Array.Clear(_skidLife); _visuals.Clear();')
p.write_text(s)
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3DCodec.cs');s=p.read_text().replace('["maximumSpeed"]=builder.MaximumSpeed','["maximumSpeed"]=builder.MaximumSpeed, ["roadGrip"]=builder.RoadGrip, ["driftGrip"]=builder.DriftGrip').replace('?? "Assets/parts"','?? "Assets/refinded parts"').replace('MaximumSpeed=data.Properties["maximumSpeed"]?.GetValue<float>() ?? 12','MaximumSpeed=data.Properties["maximumSpeed"]?.GetValue<float>() ?? 12,\n        RoadGrip=data.Properties["roadGrip"]?.GetValue<float>() ?? 12,\n        DriftGrip=data.Properties["driftGrip"]?.GetValue<float>() ?? .9f');p.write_text(s)
