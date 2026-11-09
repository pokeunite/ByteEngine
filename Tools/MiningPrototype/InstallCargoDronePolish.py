from pathlib import Path
import json,shutil,uuid,datetime
W=Path(__file__).resolve().parents[2];P=Path('C:/Users/codex/Documents/palclone');scene=P/'Scenes/Main.bytescene';backup=P/'SourceArt/Cargo Drone Backups'/datetime.datetime.now().strftime('%Y%m%d-%H%M%S');backup.mkdir(parents=True);shutil.copy2(scene,backup/scene.name);d=json.loads(scene.read_text(encoding='utf-8-sig'))
audio=P/'Assets/Audio/Mining/Creatures/Spider shell hit.wav';audio.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(Path('C:/Users/codex/Downloads/511194__pablobd__headshot.wav'),audio);meta=audio.with_name(audio.name+'.meta')
gid=json.loads(meta.read_text())['guid'] if meta.exists() else str(uuid.uuid5(uuid.NAMESPACE_URL,'hazardpay/selected-spider-hit-pablobd'));meta.write_text(json.dumps({'guid':gid,'type':'AudioClip'},indent=2))
for o in d['gameObjects']:
 for c in o.get('components',[]):
  if c['type'].endswith('MiningAudio3D'):c['properties']['spiderHitSound']={'guid':gid,'path':audio.relative_to(P).as_posix()}
  if c['type'].endswith('MiningOperation3D'):c['properties'].update(cargoCapacityPerUpgrade=100,cargoUpgradeBaseCost=80)
scene.write_text(json.dumps(d,indent=2)+'\n')
for name in ('MortarRound.glb','MortarRound.png','HazardPay-Mortar-Round.blend'):
 source=W/'artifacts/research-models'/name;target=P/('Assets/Models/Mining/Research' if name.endswith('.glb') else 'SourceArt/Research Support')/name;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
 if name.endswith('.glb') and not target.with_name(target.name+'.meta').exists():target.with_name(target.name+'.meta').write_text(json.dumps({'guid':str(uuid.uuid5(uuid.NAMESPACE_URL,'hazardpay/mortar-round')),'type':'Model3D'},indent=2))
(audio.parent/'Spider shell hit source.txt').write_text('User-selected source file: 511194__pablobd__headshot.wav\nCreator indicated by filename: pablobd\nhttps://freesound.org/people/pablobd/sounds/511194/\nInstalled unchanged at user request.\n')
(audio.parent/'Spider shell hit source.txt.meta').write_text(json.dumps({'guid':str(uuid.uuid5(uuid.NAMESPACE_URL,'hazardpay/pablobd-source-note')),'type':'Unknown'},indent=2))
print(json.dumps({'backup':str(backup),'sound':str(audio),'cargo_upgrade':'100 capacity / 80 starting credits','mortar_model_installed':True}))
