from pathlib import Path
import json,uuid,shutil,datetime
r=Path('C:/Users/codex/Documents/palclone');backup=r/'.byteengine/Backups'/('hud-web-fixes-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True);p=r/'Scenes/Main.bytescene';shutil.copy2(p,backup/p.name);d=json.loads(p.read_text(encoding='utf-8-sig'));refs={}
for kind in ['raw','refined']:
 path='Assets/UI/Mining/Artwork/ore-'+kind+'.png';target=r/path;shutil.copy2('Plugins/KarlsonDemo/Samples/Hud/Art/ore-'+kind+'.png',target);meta=Path(str(target)+'.meta');guid=json.loads(meta.read_text())['guid'] if meta.exists() else str(uuid.uuid4());meta.write_text(json.dumps({'guid':guid,'type':'Texture2D','importer':{'filter':'Linear'}}));refs[kind]={'guid':guid,'path':path}
for o in d['gameObjects']:
 for c in o.get('components',[]):
  if c['type'].endswith('.MiningBusinessUi3D'):
   c['properties']['rawOreIcon']=refs['raw'];c['properties']['refinedOreIcon']=refs['refined'];c['properties'].pop('defencePointerKey',None)
p.write_text(json.dumps(d,indent=2));print('Backed up scene and installed original ore icons:',backup)
