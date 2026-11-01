"""Apply the reviewed Hazard Pay combat balance to the saved scene (editor closed)."""
import json,pathlib,shutil,datetime
p=pathlib.Path('C:/Users/codex/Documents/palclone/Scenes/Main.bytescene')
backup=p.parent.parent/'SourceArt/Combat-Market-Backups'/datetime.datetime.now().strftime('%Y%m%d-%H%M%S');backup.mkdir(parents=True,exist_ok=True);shutil.copy2(p,backup/p.name)
d=json.loads(p.read_text(encoding='utf-8-sig'))
for o in d['gameObjects']:
 for c in o['components']:
  v=c.get('properties',{});t=c['type']
  if t.endswith('WeaponDefinition3D'):
   if v.get('requiredResearchId')=='shotgun':v.update(damagePerPellet=9,hitImpulse=12,enemyKnockbackSeconds=.22,spreadDegrees=1.75,accuracyDegrees=.2,movingInaccuracyDegrees=.3)
   if v.get('requiredResearchId')=='ar':v.update(damagePerPellet=23,recoilPitchPerShot=.18,recoilYawPerShot=.06,maximumRecoilDegrees=1.6,recoilRecoveryDegreesPerSecond=16,recoilRecoveryDelay=.06)
  if t.endswith('MiningOperation3D'):v.update(raiderMoveSpeed=3.6,hunterMoveSpeed=6,refineryCostMultiplier=.8)
  if t.endswith('MiningTrade3D'):
   v['ammoPressCostMultiplier']=.8
   if v.get('researchDefinitionsJson'):
    catalog=json.loads(v['researchDefinitionsJson'])
    for n in catalog:
     if n.get('Id')=='pellet':n['Cost']=225;n['RankCosts']=[]
    v['researchDefinitionsJson']=json.dumps(catalog)
  if t=='AudioSource3D' and 'shot sound' in o['name'].lower():v['volume']=.68 if 'rifle' in o['name'].lower() else .95 if 'shotgun' in o['name'].lower() else .85
p.write_text(json.dumps(d,indent=2)+'\n',encoding='utf-8')
print('Saved combat tuning; backup:',backup)
