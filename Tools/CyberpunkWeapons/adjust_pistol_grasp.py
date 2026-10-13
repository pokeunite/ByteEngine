from pathlib import Path
p=Path('Tools/CyberpunkWeapons/refine_rack_contact.py');s=p.read_text().replace("rotation=Matrix.Rotation(-math.pi/2,3,'Y');target=Vector((-.012,-.027,.065))-rotation@offset-wrist;roll=-90","rotation=Matrix.Rotation(-math.pi/2,3,'X');target=Vector((-.039,-.027,.097))-rotation@offset-wrist;roll=0")
s=s.replace('g.key(\'LeftWristControl\',f,pos,roll=r)','g.key(\'LeftWristControl\',f,pos,roll=r,pitch=-90 if g.name==\'Pistol\' and f in [140,150,155] else 0)');p.write_text(s)
