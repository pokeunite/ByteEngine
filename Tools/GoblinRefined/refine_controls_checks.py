from pathlib import Path
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.cs');s=p.read_text()
s=s.replace('else if (part=="scrap_cab_shell") SampleCycle(mechanism,"SteerWheel",Math.Abs(steering)*.5f);', 'else if (part=="scrap_cab_shell" && !(mechanism.IsPlaying && mechanism.CurrentAnimation.EndsWith("__ShiftLever"))) SampleCycle(mechanism,"SteerWheel",Math.Abs(steering)*.5f);')
s=s.replace('"scrap_weapon_mount" or "scrap_swivel_turret"=>"AimYaw", "scrap_lift_mast"=>"Lift",', '"scrap_weapon_mount"=>"AimYaw", "scrap_swivel_turret"=>pair.Value.CurrentAnimation.EndsWith("__AimYaw") ? "AimPitch" : "AimYaw",\n                "scrap_cab_shell"=>"ShiftLever", "scrap_catapult_basket"=>"WindUp", "scrap_lift_mast"=>"Lift",')
# The roof payload is mounted to the rotating rig, with the same calculation in previews and drive mode.
s=s.replace('            ApplyPlacement(_ghost,_mount,SelectedPart);','            ApplyPlacement(_ghost,_mount,SelectedPart);\n            if (_mount==21) PositionRoofPayload(_ghost,SelectedPart);')
s=s.replace('''        ApplyPlacement(payload,21,part);
        if (!_visuals.TryGetValue(12''', '''        ApplyPlacement(payload,21,part);
        PositionRoofPayload(payload,part);
    }
    private void PositionRoofPayload(GameObject payload,string part)
    {
        if (!_visuals.TryGetValue(12''')
# Preview status makes page and actuation obvious.
s=s.replace('VehicleBuildLayout.PartNames[_part]+"  >  "', '"PARTS "+(_palettePage+1)+"/2  |  "+VehicleBuildLayout.PartNames[_part]+"  >  "')
p.write_text(s)
p=Path('Tests/ByteEngine.Tests/GoblinVehicleDiagnostic.cs');s=p.read_text().replace('_refined ? 22 : 19','_refined ? 21 : 19')
needle='        Screenshot(scene,Path.Combine(output,"assembled.png"));'
s=s.replace(needle,needle+'''
        if (_refined)
        {
            var roof=builder.GameObject.Children.First(o=>o.Name=="Roof payload");
            Vector3 roofBefore=roof.Transform.LocalPosition;
            Assert(builder.ActivateMechanisms()>0,"Mount controls did not activate.");
            for(int i=0;i<20;i++) TickInput(scene,new(.5f,.5f),[]);
            Assert(Vector3.Distance(roofBefore,roof.Transform.LocalPosition)>.01f,"Roof payload did not follow rotating mount.");
            builder.ResetPosition();
            for(int i=0;i<60;i++) TickInput(scene,new(.5f,.5f),[Key.W]);
            for(int i=0;i<30;i++) TickInput(scene,new(.5f,.5f),[Key.W,Key.D,Key.LeftShift]);
            Assert(builder.IsDrifting && builder.DriftAngle>15,"Drift input did not produce actual lateral velocity.");
            Assert(scene.GameObjects.Count(o=>o.Name=="Tire skid" && o.Active)>0,"Drift produced no tire marks.");
            Screenshot(scene,Path.Combine(output,"drifting.png"));
            for(int i=0;i<60;i++) TickInput(scene,new(.5f,.5f),[Key.W]);
            Assert(!builder.IsDrifting && builder.DriftAngle<2,"Releasing Shift did not restore traction.");
            builder.ResetPosition();
        }''')
s=s.replace('Console.WriteLine("PASS: palette clicks;', 'Console.WriteLine((_refined ? "REFINED: 29 imported assets; every expansion attachment; native weapon playback; roof payload follows its rig; Shift drift; tire marks; traction recovery. " : "")+"PASS: palette clicks;')
p.write_text(s)
