from pathlib import Path
p=Path('Tests/ByteEngine.Tests/GoblinVehicleDiagnostic.cs');s=p.read_text().replace('100f/1280,227f/720','728f/1280,622f/720').replace('100f/1280,134f/720','130f/1280,622f/720').replace('\\\"Version\\\":2','\\\"Version\\\":3')
needle='            foreach(string part in VehicleBuildLayout.PartFiles.Skip(14))';s=s.replace(needle,'''            var originalWheel=builder.GameObject.Children.First(o=>o.Name=="Front left wheel").Transform.LocalPosition;
            Assert(builder.SetChassis("scrap_frame_long"),"Long base selection failed.");
            Assert(builder.Layout.LongChassis && builder.GameObject.Children.Count(o=>o.Name=="Chassis")==1,"Long base was added instead of replaced.");
            Assert(builder.GameObject.Children.First(o=>o.Name=="Front left wheel").Transform.LocalPosition.Z<originalWheel.Z-.7f,"Wheels did not move to long base mounts.");
            Assert(builder.UndoBuild() && !builder.Layout.LongChassis,"Chassis undo failed.");
            Assert(builder.RedoBuild() && builder.Layout.LongChassis,"Chassis redo failed.");
            Screenshot(scene,Path.Combine(output,"long-base.png"));
            builder.SetChassis("scrap_frame_2x1");
'''+needle)
s=s.replace('''            {
                int mount=Array.FindIndex''','''            {
                if(part=="scrap_frame_long") continue;
                if(part=="scrap_track_pod")
                {
                    var running=builder.Layout.Parts.Where(p=>p.Key<=5).ToArray();
                    foreach(var p in running) builder.RemovePart(p.Key);
                    Assert(builder.AttachPart(19,part) && builder.AttachPart(20,part),"Track pair attachment failed.");
                    Assert(!builder.AttachPart(0,"scrap_wheel_large") && !builder.AttachPart(4,"scrap_axle_2m"),"Wheels or axles mixed with tracks.");
                    Screenshot(scene,Path.Combine(output,"tracked.png"));
                    builder.RemovePart(19);builder.RemovePart(20);
                    foreach(var p in running) builder.AttachPart(p.Key,p.Value);
                    continue;
                }
                int mount=Array.FindIndex''')
p.write_text(s)
p=Path('Tests/ByteEngine.Tests/VehicleDriftTests.cs');s=p.read_text().replace('VehicleBuildLayout.PartFiles.All(p=>VehicleBuildLayout.Mounts.Any(m=>m.Parts.Contains(p)))','VehicleBuildLayout.PartFiles.Where(p=>p!="scrap_frame_long").All(p=>VehicleBuildLayout.Mounts.Any(m=>m.Parts.Contains(p)))');p.write_text(s)
