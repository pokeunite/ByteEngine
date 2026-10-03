from pathlib import Path
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.cs');s=p.read_text().replace('Speed=0; _yaw=0;','Speed=0; _motion.Reset(); _rollDistance=0;');p.write_text(s)
# Make the existing GPU diagnostic install the refined assembly without disturbing other scene objects.
p=Path('Tests/ByteEngine.Tests/GoblinVehicleDiagnostic.cs');s=p.read_text();s=s.replace('private readonly bool _prepare;','private readonly bool _prepare;\n    private readonly bool _refined;')
s=s.replace('public GoblinVehicleDiagnostic(string projectFile, bool prepare)', 'public GoblinVehicleDiagnostic(string projectFile, bool prepare, bool refined=false)')
s=s.replace('_projectFile=projectFile; _prepare=prepare;', '_projectFile=projectFile; _prepare=prepare; _refined=refined;')
needle='        var builder=scene.GameObjects.SelectMany(o=>o.Components).OfType<VehicleBuilder3D>().Single();'
s=s.replace(needle,'''        if (_refined)
        {
            var configured=scene.GameObjects.SelectMany(o=>o.Components).OfType<VehicleBuilder3D>().Single();
            configured.PartsDirectory="Assets/refinded parts"; configured.MaximumSpeed=16; configured.RoadGrip=12; configured.DriftGrip=.9f;
            var chassis=configured.GameObject.Children.FirstOrDefault(c=>c.Name=="Chassis");
            if(chassis!=null) scene.DestroyGameObject(chassis);
            VehicleBuilder3D.CreateModelVisual(scene,_project.Assets,configured.PartsDirectory+"/scrap_frame_2x1.glb",configured.GameObject,"Chassis");
            _project.Scenes.Save(scene,scenePath);
            scene=_project.Scenes.Load(scenePath);
        }
'''+needle)
s=s.replace('for(int i=8;i<VehicleBuildLayout.Mounts.Length;i++)','for(int i=8;i<19;i++)')
# Exercise every expansion part on an actual native renderer, then leave one comprehensible assembly.
needle='        Assert(builder.RemovePart(7) && !builder.BeginDriving(),'
pos=s.index(needle)
s=s[:pos]+'''        if (_refined)
        {
            foreach(string part in VehicleBuildLayout.PartFiles.Skip(14))
            {
                int mount=Array.FindIndex(VehicleBuildLayout.Mounts,m=>m.Parts.Contains(part) && !builder.Layout.Parts.ContainsKey(Array.IndexOf(VehicleBuildLayout.Mounts,m)));
                if(mount<0) { mount=Array.FindIndex(VehicleBuildLayout.Mounts,m=>m.Parts.Contains(part)); builder.RemovePart(mount); }
                Assert(builder.AttachPart(mount,part),"Expansion attachment failed: "+part);
                var visual=builder.GameObject.Children.First(o=>o.Name==VehicleBuildLayout.Mounts[mount].Name);
                var skeletal=visual.GetComponent<ByteEngine.Core.Graphics.ThreeD.SkeletalMeshRenderer>();
                if(skeletal!=null) Assert(skeletal.ResolvedModel?.Animations.Count>0 && skeletal.BoneNames.Count>1,"Expansion rig missing: "+part);
                builder.RemovePart(mount);
            }
            // Restore the optional mounts displaced by the expansion checks.
            for(int i=8;i<19;i++)
                if(!builder.Layout.Parts.ContainsKey(i)) builder.AttachPart(i,VehicleBuildLayout.Mounts[i].Parts[0]);
            builder.RemovePart(11); builder.AttachPart(11,"scrap_auger_drill");
            builder.AttachPart(21,"scrap_catapult_basket");
            builder.AttachPart(22,"scrap_battering_fist");
        }
'''+s[pos:]
s=s.replace('        Assert(!builder.RemovePart(0),"Vehicle edited during driving.");','''        Assert(!builder.RemovePart(0),"Vehicle edited during driving.");
        if (_refined)
        {
            Assert(builder.FireWeapons()==2,"Fist and catapult did not start their native clips.");
            builder.SetPoweredWeapons(true);
            var drill=builder.GameObject.Children.First(o=>o.Name=="Front weapon").GetComponent<ByteEngine.Core.Graphics.ThreeD.SkeletalMeshRenderer>()!;
            Assert(drill.IsPlaying && drill.CurrentAnimation.EndsWith("__Drill"),"Drill loop did not start.");
            for(int i=0;i<30;i++) TickInput(scene,new(.5f,.5f),[Key.F]);
            Assert(drill.PlaybackTime>0,"Native animation playback did not advance.");
            builder.SetPoweredWeapons(false); Assert(!drill.IsPlaying,"Powered weapon did not stop on release.");
            builder.ResetPosition();
        }''')
s=s.replace('builder.Building && builder.Layout.Parts.Count==19,"Returning to build lost the assembly."', 'builder.Building && builder.Layout.Parts.Count==(_refined ? 22 : 19),"Returning to build lost the assembly."')
p.write_text(s)
p=Path('Tests/ByteEngine.Tests/Program.cs');s=p.read_text().replace('new GoblinVehicleDiagnostic(args[1], args.Contains("--prepare"))','new GoblinVehicleDiagnostic(args[1], args.Contains("--prepare"), args.Contains("--refined"))');needle='    if (args.Contains("--construction"))';s=s.replace(needle,'''    if (args.Contains("--vehicle-drift"))
    {
        VehicleDriftTests.Run();
        return;
    }

'''+needle);p.write_text(s)
