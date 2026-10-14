from pathlib import Path
p=Path('C:/Users/codex/ByteEngine/Plugins/DuneCompany/DuneWorkshop3D.Racing.cs');s=p.read_text()
old='var blocks=BuildStore.Read(File.ReadAllText(Path.Combine(ProjectRoot,profile.OpponentBlueprint)),_catalog);foreach(var b in blocks)'
assert old in s;s=s.replace(old,'var blocks=BuildStore.Read(DragRace?Snapshot():File.ReadAllText(Path.Combine(ProjectRoot,profile.OpponentBlueprint)),_catalog);if(!DragRace)foreach(var b in blocks)')
s=s.replace('if(DragRace){var engine=blocks.First','if(DragRace&&!blocks.Any(b=>b.Type==39&&GearboxMounts.Connected(b,blocks,_catalog))){var engine=blocks.First')
s=s.replace('workshop.Sprint?.OpponentSpeed??13);','workshop.DragRace?workshop._physics!.ConfiguredMaximumSpeed:workshop.Sprint?.OpponentSpeed??13);')
old='if(DragRace&&_race.State==RaceState.Countdown&&throttle>0&&!_jumpStart){_jumpStart=true;_launchPenalty=.75f;JobNotice("EARLY THROTTLE / LAUNCH DELAY");}'
assert old in s;s=s.replace(old,'')
old='if(racing&&_launchPenalty>0){_launchPenalty=Math.Max(0,_launchPenalty-dt);throttle=steer=0;brake=true;}'
assert old in s;s=s.replace(old,'')
s=s.replace('(_launchPenalty>0?"EARLY THROTTLE / LAUNCH DELAY":_race.State==RaceState.Countdown?"WAIT FOR GREEN / RELEASE THE THROTTLE":"400m straight / braking area beyond finish")','(_race.State==RaceState.Countdown?"HOLD THE ACCELERATOR / LAUNCH ON GREEN":"400m straight / braking area beyond finish")')
p.write_text(s)
p=Path('C:/Users/codex/ByteEngine/Plugins/DuneCompany/DuneWorkshop3D.MotorsportMenus.cs');s=p.read_text();p.write_text(s.replace('Wait for green, then stop in the long braking zone.','Hold the accelerator through the countdown. Launch on green, use all six gears, then stop in the long braking zone.'))
p=Path('C:/Users/codex/ByteEngine/Tests/BrowserSmoke/dune_spring_arm.py');s=p.read_text();a=s.index("        if os.environ.get('TEST_CONVOY')=='1':");b=s.index("        if os.environ.get('TEST_MOTORSPORT')=='1':",a);p.write_text(s[:a]+s[b:])
print('Six-gear drag logic / matched opponent build / staged throttle release')
