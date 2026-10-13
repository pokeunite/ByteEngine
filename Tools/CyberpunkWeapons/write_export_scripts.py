from pathlib import Path
s=Path('Tools/CyberpunkWeapons/export_pistol.py').read_text()
for i,name in [(1,'shotgun'),(2,'rifle')]:Path('Tools/CyberpunkWeapons/export_'+name+'.py').write_text(s.replace("g=C['guns'][0]",f"g=C['guns'][{i}]"))
