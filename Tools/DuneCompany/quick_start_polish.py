"""Apply track visibility and anchor spacing without regenerating authored levels."""
import json, shutil, datetime
from pathlib import Path
root=Path('C:/Users/codex/Documents/DuneCompany')
backup=root/'.byteengine/backups'/('quick-start-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'))
backup.mkdir(parents=True)
for name in ['Workshop','WinchRescue','AnchorRescue']:
    path=root/'Scenes'/f'{name}.bytescene'
    shutil.copy2(path,backup/path.name)
    scene=json.loads(path.read_text(encoding='utf-8-sig'))
    for obj in scene['gameObjects']:
        for c in obj.get('components',[]):
            if c['type'].endswith('.DesertTerrain3D'):
                c['properties']['maximumRutDepth']=.18
        if name=='AnchorRescue' and obj['name'] in ['Anchor rock / old steel eye','Anchor eye','Contract landscape / Anchor eye'] and obj.get('parentId') is None:
            pos=obj['transform']['localPosition']
            pos['z']=40 if obj['name']=='Anchor rock / old steel eye' else 41.5
    path.write_text(json.dumps(scene,indent=2))
print('Updated tracks and anchor spacing; backups:',backup)
