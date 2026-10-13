from pathlib import Path
p=Path('Tools/KarlsonDemo/DemoApplication.cs');s=p.read_text();a=s.index('        _frame++;');b=s.index('\n    }\n    static void NativeInput',a)
s=s[:a]+'''        _frame++;var scene=Scenes.ActiveScene!;var player=scene.FindComponent<FpsGunLab3D>()!;var motor=player.GameObject.GetComponent<CharacterController3D>()!;
        var raw=new RawInputSnapshot();
        if(_frame is 60 or 270 or 750 || (_frame>=510&&_frame<=529))raw.MouseButtonsDown.Add(MouseButton.Left);
        if(_frame is 90 or 320 or 550)raw.KeysDown.Add(Key.R);
        if(_frame==220)raw.KeysDown.Add(Key.D2);if(_frame==460)raw.KeysDown.Add(Key.D3);if(_frame==710)raw.KeysDown.Add(Key.D4);
        if(_frame==810)raw.KeysDown.Add(Key.D1);
        if(_frame==840)raw.KeysDown.Add(Key.Space);if(_frame>=870&&_frame<890)raw.KeysDown.Add(Key.W);
        if(_frame==920)raw.MouseDelta=new(20,-10);
        NativeInput(raw);
        if(_frame==40){Check(motor.IsGrounded&&player.Transform.WorldPosition.Y>-.05f,"FPS character stays on solid floor");_start=player.Transform.WorldPosition;_capture="FPS_Pistol.png";Check(View(0).ModelLoaded,"Rebuilt pistol imports");View(0).TryGetBoneModelMatrix("Slide",out var bone);_slideRest=bone.Translation;}
        if(_frame==64){Check(player.ShotsFired==1&&player.Ammo==11,"Pistol consumes one round");Check(View(0).CurrentAnimation=="Fire","Pistol fire animation plays");View(0).TryGetBoneModelMatrix("Slide",out var bone);Check(Vector3.Distance(_slideRest,bone.Translation)>.005f,"Slide retracts");Check(scene.GameObjects.Any(o=>o.Name=="Weapon shot trace"),"Visible shot traces are created");_capture="FPS_Pistol_Fire.png";}
        if(_frame==130){Check(player.Reloading&&View(0).CurrentAnimation=="Reload","Reload action drives pistol profile");_capture="FPS_Pistol_Reload.png";}
        if(_frame==205){Check(player.Ammo==12&&player.ReserveAmmo==59&&!player.Reloading,"Magazine reload transfers finite reserve");Check(View(0).CurrentAnimation=="Idle","Profile returns to idle");}
        if(_frame==258){Check(player.SelectedSlot==1,"Parameterized equip event selects shotgun");View(1).TryGetBoneModelMatrix("Pump",out var bone);_pumpRest=bone.Translation;_capture="FPS_Shotgun.png";}
        if(_frame==285){Check(player.Ammo==5&&View(1).CurrentAnimation=="Fire","Shotgun consumes one shell for eight pellets");View(1).TryGetBoneModelMatrix("Pump",out var bone);Check(Vector3.Distance(_pumpRest,bone.Translation)>.02f,"Pump moves during cycling");_capture="FPS_Shotgun_Pump.png";}
        if(_frame==365){Check(player.Reloading,"Per-round loading state is active");_capture="FPS_Shotgun_Reload.png";}
        if(_frame==450)Check(player.Ammo==6&&player.ReserveAmmo==29&&!player.Reloading,"Per-round reload transfers one reserve shell");
        if(_frame==500){Check(player.SelectedSlot==2&&View(2).ModelLoaded,"Rifle uses same equipment controller");View(2).TryGetBoneModelMatrix("Magazine",out var bone);_magRest=bone.Translation;_capture="FPS_Rifle.png";}
        if(_frame==535)Check(player.Ammo<=21&&player.ShotsFired>=5,"Configured automatic trigger repeats shots");
        if(_frame==590){View(2).TryGetBoneModelMatrix("Magazine",out var bone);Check(player.Reloading&&Vector3.Distance(_magRest,bone.Translation)>.1f,"Rifle magazine clears the well");_capture="FPS_Rifle_Reload.png";}
        if(_frame==670)Check(player.Ammo==24&&!player.Reloading&&player.ReserveAmmo<60,"Rifle reload consumes reserve");
        if(_frame==705)Check(scene.GameObjects.Single(o=>o.Name=="Practice target 2").GetComponent<HealthComponent>()!.CurrentHealth<1000,"Hitscan damages target");
        if(_frame==755){Check(player.SelectedSlot==3&&player.Ammo==7,"Second pistol variant has independent eight-round magazine");Check(player.SelectedWeapon!.Definition!.ShotMode==WeaponShotMode.Projectile,"Variant uses projectile mode");Check(scene.GameObjects.Any(o=>o.GetComponent<Projectile3D>()!=null),"Physical projectile is spawned");_capture="FPS_Projectile_Pistol.png";}
        if(_frame==815)Check(player.SelectedSlot==0&&player.Ammo==12&&player.ReserveAmmo==59,"Switching preserves each weapon's ammunition");
        if(_frame==848)Check(player.Transform.WorldPosition.Y>.4f,"FPS jump works");
        if(_frame==910){Check(Vector3.Distance(_start,player.Transform.WorldPosition)>1,"FPS movement works");Check(motor.IsGrounded&&player.Transform.WorldPosition.Y>-.05f,"Floor remains solid after movement");}
        if(_frame==924)Check(Math.Abs(player.GameObject.GetComponent<PlayerController3D>()!.ControlYaw)>1,"FPS mouse look works");
        if(_frame==940){foreach(var renderer in scene.GameObjects.Select(o=>o.GetComponent<SkeletalMeshRenderer>()).OfType<SkeletalMeshRenderer>()){Check(renderer.TryGetCurrentModelBounds(out var bounds)&&bounds.IsValid,"Finite animated bounds: "+renderer.GameObject.Name);Check(new[]{"Idle","Fire","Reload","Equip"}.All(renderer.HasAnimation),"All clips imported: "+renderer.GameObject.Name);}Check(scene.GameObjects.Count(o=>o.Name=="Weapon shot trace")<64,"Debug traces expire and remain bounded");File.WriteAllText(Program.Root+"/Preview/gameplay-validation.json",JsonSerializer.Serialize(new{checks=_checks.Count,passed=_checks,nativeFrames=_frame,gpu=GL.GetString(StringName.Renderer)},new JsonSerializerOptions{WriteIndented=true}));Close();}''' +s[b:];p.write_text(s)
