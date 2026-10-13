from pathlib import Path
import json
root=Path('C:/Users/codex/ByteEngine')
def change(name,a,b):
 p=root/name;s=p.read_text(encoding='utf-8-sig');assert a in s,name;p.write_text(s.replace(a,b),encoding='utf-8')
change('Plugins/DuneCompany/LandVehiclePhysics.Assemblies.cs','Where(b=>b.Type==39).ToArray()','Where(b=>b.Type==39&&GearboxMounts.Connected(b,parts.Select(p=>p.Block).ToArray(),_catalog)).ToArray()')
change('Plugins/DuneCompany/LandVehiclePhysics.cs','power*=finalDrive;maximumSpeed/=finalDrive;','power*=finalDrive;maximumSpeed/=finalDrive;float transmissionFactor=1;\n  if(rig.Gearboxes.Length>0){var body=_simulation.Bodies[rig.Parts[0].Root.Body];float speed=new Vector2(body.Velocity.Linear.X,body.Velocity.Linear.Z).Length();rig.Transmission.Update(dt,speed,maximumSpeed*finalDrive,finalDrive);if(!ReferenceEquals(rig,_playerRig)&&rig.Transmission.ShiftReady)rig.Transmission.Shift(1,speed,maximumSpeed*finalDrive,finalDrive);if(throttle>=0){maximumSpeed/=rig.Transmission.Ratio;power*=rig.Transmission.Ratio;transmissionFactor=rig.Transmission.DriveFactor;}else maximumSpeed=Math.Min(maximumSpeed,8);}')
change('Plugins/DuneCompany/LandVehiclePhysics.cs',')*b.Power;',')*b.Power*transmissionFactor;')
change('Plugins/DuneCompany/DuneWorkshop3D.Polish.cs','("Test drive",Key.B)]','("Test drive",Key.B),("Shift up",Key.X),("Shift down",Key.Z)]')
change('Plugins/DuneCompany/DuneWorkshop3D.Polish.cs','new(28,108+i*43),new(449,36)','new(28,108+i*35),new(449,30)')
change('Plugins/DuneCompany/DuneWorkshop3D.cs','case "Toggle drive":ToggleDrive();break;','case "Shift up":ShiftPlayerGear(1);break;case "Shift down":ShiftPlayerGear(-1);break;case "Toggle drive":ToggleDrive();break;')
change('Plugins/DuneCompany/DuneCompanyPlugin.cs','"Toggle drive","Save session"','"Shift up","Shift down","Toggle drive","Save session"')
change('Plugins/DuneCompany/DuneWorkshop3D.cs','else{if(DrivePressed("Recover"))','else{if(DrivePressed("Shift up"))ShiftPlayerGear(1);else if(DrivePressed("Shift down"))ShiftPlayerGear(-1);if(DrivePressed("Recover"))')
change('Plugins/DuneCompany/DuneWorkshop3D.cs','RefreshSpeedometer();RefreshPolish(dt);','RefreshSpeedometer();RefreshGearboxHud();RefreshPolish(dt);')
change('Plugins/DuneCompany/DuneWorkshop3D.cs','if(!CanPlacePaid(type))return false;','if(!CanPlacePaid(type))return false;\n  if(type==39){if(!TryGearboxMount(parent,out position,out rotation)){_message="Attach the gearbox directly to the rear of an engine.";return false;}movingMount=false;}')
change('Plugins/DuneCompany/DuneWorkshop3D.cs','_candidateQ=q;_valid=!Overlap','_candidateQ=q;if(_type==39&&GearboxMounts.IsEngine(target.Type)&&n.Z>.5f){_candidate=GearboxMounts.Position(target,def);_candidateQ=q=target.Q;incoming=-Vector3.UnitZ;}\n   _valid=!Overlap')
change('Plugins/DuneCompany/DuneWorkshop3D.cs','_valid?"": "Part intersects another block"','_valid?"": "Part intersects another block";if(_type==39&&(!GearboxMounts.IsEngine(target.Type)||n.Z<=.5f)){_valid=false;placementError="Gearbox must attach to the rear face of an engine";}')
# Fix the additional semicolon from replacing a declaration fragment.
change('Plugins/DuneCompany/DuneWorkshop3D.cs','engine";};if(_valid','engine";}if(_valid') if 'engine";};if(_valid' in (root/'Plugins/DuneCompany/DuneWorkshop3D.cs').read_text() else None
change('Plugins/DuneCompany/DuneWorkshop3D.cs','foreach(var b in _blocks.Where(b=>b.Id!=exclude))foreach(var existing','foreach(var b in _blocks.Where(b=>b.Id!=exclude&&!(type==39&&GearboxMounts.IsEngine(b.Type)&&Vector3.Distance(p,GearboxMounts.Position(b,_catalog[b.Type]))<.015f)))foreach(var existing')
change('Plugins/DuneCompany/DuneWorkshop3D.cs','foreach(var b in _blocks.Where(b=>_catalog[b.Type].Power>0))','foreach(var b in _blocks.Where(b=>b.Type==39))_visuals[b.Id].AddComponent(new DuneGearboxAnimation3D());\n  foreach(var b in _blocks.Where(b=>_catalog[b.Type].Power>0))')
change('Plugins/DuneCompany/DuneWorkshop3D.VehicleFeedback.cs','float gearRev=Math.Clamp((_feedbackGroundSpeed-(_audioGear-1)*8.5f)/10,0,1);','float gearRev=_physics.Transmission is {} transmission?Math.Clamp((transmission.Rpm-900)/6100,0,1):Math.Clamp((_feedbackGroundSpeed-(_audioGear-1)*8.5f)/10,0,1);')
change('Plugins/DuneCompany/DuneWorkshop3D.VehicleFeedback.cs','float rev=.86f+gearRev*.30f+_engineLoad*.12f;','float rev=.78f+gearRev*.95f+_engineLoad*.12f;')
change('Plugins/DuneCompany/DuneCompanyPlugin.cs','c.RegisterSimpleComponent<DuneEngineAnimation3D>','c.RegisterSimpleComponent<DuneGearboxAnimation3D>("DuneGearboxAnimation3D",new("Dune Gearbox Animation","Rendering","Sequential up/down lever motion without moving the physical housing.","gearbox lever shift"));\n  c.RegisterSimpleComponent<DuneEngineAnimation3D>')
manifest=root/'Plugins/DuneCompany/plugin.json';data=json.loads(manifest.read_text());data['version']='0.25.0';manifest.write_text(json.dumps(data,indent=2)+'\n')
print('Manual transmission integration authored')
