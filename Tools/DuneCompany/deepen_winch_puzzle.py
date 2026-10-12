"""Deepen only the existing winch site; preserve garage, art and save progress."""
import json,shutil,datetime,importlib.util
from pathlib import Path
root=Path('C:/Users/codex/Documents/DuneCompany')
spec=importlib.util.spec_from_file_location('levels',Path(__file__).with_name('author_starter_levels.py'))
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
backup=root/'.byteengine/backups'/('deep-winch-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True)
folder=root/'Assets/ContractLevels';path=root/'Scenes/WinchRescue.bytescene'
for src in [path,folder/'extraction-height.png',folder/'extraction-packed.png']:shutil.copy2(src,backup/src.name)
m.png(folder/'extraction-height.png',True);m.png(folder/'extraction-packed.png',True,True)
scene=json.loads(path.read_text(encoding='utf-8-sig'))
for obj in scene['gameObjects']:
    pos=obj['transform']['localPosition'];scale=obj['transform']['localScale']
    if obj['name'].startswith(('Contract landscape /','Desert dressing /')):
        pos['y']=m.height(pos['x'],pos['z'],True)+scale['y']*.42
    for c in obj.get('components',[]):
        if c['type'].endswith('.DesertTerrain3D'):
            c['properties'].update(heightmapHash='',sandState='',sculptState='',autoLoadSavedSand=False)
        if c['type'].endswith('.DuneRecoveryContract3D'):
            c['properties']['depressionDepth']=0
path.write_text(json.dumps(scene,indent=2))
print('Pit floor',m.height(12,70,True),'rim',m.height(18.5,70,True),'depth 5m; south ramp remains open. Backup:',backup)
