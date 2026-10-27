from pathlib import Path
import json,shutil,datetime
r=Path('C:/Users/codex/Documents/palclone');backup=r/'.byteengine/Backups'/('yellow-machine-menus-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True);p=r/'Scenes/Main.bytescene';shutil.copy2(p,backup/p.name);d=json.loads(p.read_text(encoding='utf-8-sig'));font='Assets/UI/Mining/Fonts/TypeLightSans.ttf';guid=json.loads((r/(font+'.meta')).read_text())['guid']
for o in d['gameObjects']:
 for c in o.get('components',[]):
  if c['type'].endswith('.MiningBusinessUi3D'):c['properties'].update(surface=[.055,.063,.073,.98],textColor=[.94,.93,.88,1],accent=[.91,.71,.15,1],purchaseColor=[.91,.71,.15,1],bodyFont={'guid':guid,'path':font},researchZoom=.65,researchPan=[0,0])
p.write_text(json.dumps(d,indent=2));print('Scene palette and typography updated; backup:',backup)
