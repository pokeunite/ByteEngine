from pathlib import Path
import json,shutil,datetime
root=Path('C:/Users/codex/Documents/palclone')
files=[root/'Scenes/Main.bytescene',root/'Assets/Blueprints/Mining/Research/SecurityDrone.byteblueprint']
backup=root/'.byteengine/Backups'/('support-ux-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'))
for path in files:
 target=backup/path.relative_to(root);target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,target)
 data=json.loads(path.read_text(encoding='utf-8-sig'))
 for obj in data.get('gameObjects',[data.get('root',{})]):
  for c in obj.get('components',[]):
   p=c.get('properties',{})
   if c['type'].endswith('MiningResearchStation3D'):p.update(barricadeOreCost=10,mineOreCost=15,maximumDrones=3,droneBatterySeconds=45,droneCost=108)
   if c['type'].endswith('MiningSecurityDrone3D'):p['batterySeconds']=45
 path.write_text(json.dumps(data,indent=2),encoding='utf-8')
print('Applied 10-ore barricades, three drones and 45-second batteries. Backup:',backup)
