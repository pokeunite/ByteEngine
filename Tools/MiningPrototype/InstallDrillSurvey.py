from pathlib import Path
import json,shutil,uuid,datetime
workspace=Path(__file__).resolve().parents[2]
project=Path('C:/Users/codex/Documents/palclone')
scene=project/'Scenes/Main.bytescene'
backup=project/'Backups'/('Drill-Site-Map-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'))
backup.mkdir(parents=True,exist_ok=True);shutil.copy2(scene,backup/scene.name)
data=json.loads(scene.read_text(encoding='utf-8-sig'))
catalog=json.loads((workspace/'Plugins/KarlsonDemo/Mining/DrillSites.json').read_text())
count=0
for obj in data['gameObjects']:
 for component in obj.get('components',[]):
  if component['type']=='bytebard.karlsondemo.MiningOperation3D':
   props=component['properties'];props['contractsJson']=json.dumps(catalog,separators=(',',':'));props['maximumEnemiesPerRound']=300;props['maximumAlive']=24
   if props.get('selectedContractId') not in {site['Id'] for site in catalog}:props['selectedContractId']='survey'
   count+=1
assert count==1
scene.write_text(json.dumps(data,indent=2),encoding='utf-8')
source=project/'SourceData';source.mkdir(exist_ok=True);shutil.copy2(workspace/'Plugins/KarlsonDemo/Mining/DrillSites.json',source/'DrillSites.json')
kit=workspace/'artifacts/hud-field-kit';runtime=project/'Assets/UI/Mining/FieldHud';art=project/'SourceArt/Gameplay HUD'
for asset in kit.iterdir():
 if not asset.is_file():continue
 target=(runtime if asset.suffix=='.png' else art)/asset.name;shutil.copy2(asset,target)
 if asset.suffix=='.png' and not Path(str(target)+'.meta').exists():Path(str(target)+'.meta').write_text(json.dumps({'guid':str(uuid.uuid5(uuid.NAMESPACE_URL,'hazardpay/fieldhud/'+asset.name)),'type':'Texture2D'},indent=2))
print(f'Installed {len(catalog)} editable drill sites. Backed up scene at {backup}. Concurrent cap 24; operation limit 300. Saves unchanged.')
