"""Slightly stronger sand tracks in the working project, with backups."""
import json,shutil,datetime
from pathlib import Path
root=Path('C:/Users/codex/Documents/DuneCompany')
backup=root/'.byteengine/backups'/('delivery-polish-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'))
backup.mkdir(parents=True)
for name in ['Workshop','WinchRescue']:
    path=root/'Scenes'/f'{name}.bytescene';shutil.copy2(path,backup/path.name)
    scene=json.loads(path.read_text(encoding='utf-8-sig'))
    for obj in scene['gameObjects']:
        for c in obj.get('components',[]):
            if c['type'].endswith('.DesertTerrain3D'):
                p=c['properties'];p['contactRutDepth']=round(p.get('contactRutDepth',.065)*1.15,5)
    path.write_text(json.dumps(scene,indent=2))
print('Sand track depth increased 15%; backups:',backup)
