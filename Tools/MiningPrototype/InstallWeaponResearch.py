from pathlib import Path
import json,shutil,datetime
W=Path(__file__).resolve().parents[2];P=Path('C:/Users/codex/Documents/palclone');scene=P/'Scenes/Main.bytescene'
d=json.loads(scene.read_text(encoding='utf-8-sig'));catalog=json.loads((W/'Plugins/KarlsonDemo/Mining/CompanyResearchCatalog.json').read_text());weapon=[n for n in catalog if n['Id'].startswith(('pistol-','rifle-','shotgun-'))];ids={n['Id'] for n in weapon};count=0
for o in d['gameObjects']:
 for c in o.get('components',[]):
  if c['type'].endswith('MiningTrade3D'):
   old=json.loads(c['properties'].get('researchDefinitionsJson','[]'));old=[n for n in old if n['Id'] not in {'pistol-handling','rifle-handling','shotgun-handling'} and n['Id'] not in ids]
   for n in old:
    n['Prerequisites']=[new for prior in n.get('Prerequisites',[]) for new in ([prior.removesuffix('-handling')+'-'+kind for kind in ('reload','magazine','accuracy')] if prior in {'pistol-handling','rifle-handling','shotgun-handling'} else [prior])]
    if n['Id'] in ('mine','sentry'):n['Description']=next(x['Description'] for x in catalog if x['Id']==n['Id'])
   c['properties']['researchDefinitionsJson']=json.dumps(old+weapon,separators=(',',':'));count+=1
assert count==1
backup=P/'Backups'/('WeaponResearch-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True,exist_ok=True);shutil.copy2(scene,backup/scene.name)
tmp=scene.with_suffix('.bytescene.tmp');tmp.write_text(json.dumps(d,indent=2),encoding='utf-8');tmp.replace(scene)
print('Installed separate weapon upgrades; preserved other project research. Backup:',backup)
