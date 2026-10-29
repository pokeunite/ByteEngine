from pathlib import Path
import json, shutil, datetime
root=Path('C:/Users/codex/Documents/palclone')
backup=root/'.byteengine/Backups'/('hazard-pay-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'))
backup.mkdir(parents=True)
for relative in ['palclone.byteproject','Scenes/Main.bytescene']:
    file=root/relative
    shutil.copy2(file,backup/file.name)
    data=json.loads(file.read_text(encoding='utf-8-sig'))
    if file.suffix=='.byteproject':
        data['name']='Hazard Pay'
    else:
        for obj in data['gameObjects']:
            for component in obj.get('components',[]):
                if component['type'].endswith('.MiningPauseMenu3D'):
                    component['properties']['companyTitle']='Hazard Pay'
    file.write_text(json.dumps(data,indent=2),encoding='utf-8')
print('Updated display name and main-menu title; preserved project ID, paths and saves. Backup:',backup)
