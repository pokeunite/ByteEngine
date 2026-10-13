from pathlib import Path
p=Path('Tools/KarlsonDemoModeling/final_exports_v2.py');s=p.read_text().replace('export_active_scene=True','use_active_scene=True');a=s.index('frame=next');b=s.index('for rig in rigs:',a);s=s[:a]+s[b:];p.write_text(s)
p=Path('Tools/KarlsonDemoModeling/rebuild_v2.py');s=p.read_text().replace('use_selection=True,export_animations','use_selection=True,use_active_scene=True,export_animations').replace("[(-.10,-.014),(.145,-.014)","[(-.10,.005),(.145,.005)");p.write_text(s)
