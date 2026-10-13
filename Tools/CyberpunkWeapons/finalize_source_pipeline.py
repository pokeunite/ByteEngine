from pathlib import Path
for name in ['pistol','shotgun','rifle']:
 p=Path('Tools/CyberpunkWeapons/export_'+name+'.py');s=p.read_text().replace('g.rest();bpy.ops.object.select_all','g.rest();g.rig.location=(0,0,0);bpy.ops.object.select_all');p.write_text(s)
p=Path('Tools/CyberpunkWeapons/render_review.py');s=p.read_text().replace('  g.rest()','  g.rest();g.rig.location=(0,0,0)');p.write_text(s)
