from pathlib import Path
import json,datetime,shutil
root=Path('C:/Users/codex/Documents/palclone');backup=root/'.byteengine/Backups'/('spider-menu-repair-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True)
paths=list((root/'Assets/AI/Mining').glob('*.byteai'))+list((root/'Assets/Blueprints/Mining').glob('*.byteblueprint'))
def fix(v):
 changed=0
 if isinstance(v,dict):
  path=v.get('path') or v.get('ProjectPath') or v.get('CachedProjectPath') or v.get('cachedProjectPath')
  key='Guid' if 'Guid' in v else 'guid'
  if path and str(path).endswith('.byteai') and key in v:
   meta=Path(str(root/path)+'.meta')
   if meta.exists():
    guid=json.loads(meta.read_text(encoding='utf-8-sig'))['guid']
    if v[key]!=guid:v[key]=guid;changed+=1
  for value in v.values():changed+=fix(value)
 elif isinstance(v,list):
  for value in v:changed+=fix(value)
 return changed
for p in paths:
 d=json.loads(p.read_text(encoding='utf-8-sig'));n=fix(d)
 if n:shutil.copy2(p,backup/p.name);p.write_text(json.dumps(d,indent=2));print(p.name,n,'references repaired')
p=root/'Scenes/Main.bytescene';shutil.copy2(p,backup/p.name);d=json.loads(p.read_text(encoding='utf-8-sig'))
for o in d['gameObjects']:
 for c in o.get('components',[]):
  if c['type']=='PlayerController3D':c['properties']['allowJump']=False
  if c['type'].endswith('.MiningPauseMenu3D'):c['properties']['companyTitle']=''
p.write_text(json.dumps(d,indent=2))
p=root/'palclone.byteproject';shutil.copy2(p,backup/p.name);d=json.loads(p.read_text(encoding='utf-8-sig'));d.setdefault('window',{})['startFullscreen']=True;p.write_text(json.dumps(d,indent=2));print('Backup:',backup)
