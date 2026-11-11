from pathlib import Path
import json,shutil,datetime
W=Path(__file__).resolve().parents[2];P=Path('C:/Users/codex/Documents/palclone')
scene=P/'Scenes/Main.bytescene';drone=P/'Assets/Blueprints/Mining/Research/SecurityDrone.byteblueprint'
backup=P/'Backups'/('Judge-feedback-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True)
for f in [scene,drone]:shutil.copy2(f,backup/f.name)
d=json.loads(scene.read_text(encoding='utf-8-sig'));catalog=json.loads((W/'Plugins/KarlsonDemo/Mining/CompanyResearchCatalog.json').read_text())
for o in d['gameObjects']:
 for c in o.get('components',[]):
  p=c['properties'];t=c['type']
  if t.endswith('MiningOperation3D'):p.update(expeditionsEnabled=True,wavesPerExpedition=6,preparationSeconds=45,earlyStartMaximumBonus=.15,pressureGrowthPerWave=.12,disruptorsFromWave=3,disruptorFraction=.12,automaticRefineryTimeMultiplier=1.35,freeAmmoEveryOperation=False,overclockEnabled=False)
  if t.endswith('MiningTrade3D'):p.update(researchDefinitionsJson=json.dumps(catalog),baseCycleSeconds=6,baseRoundsPerCycle=3,extraRoundsPerCycle=2,baseStorage=35)
  if t.endswith('MiningDefensePlanner3D'):p.update(sentryDamage=18,sentryInterval=.55,defenceRange=22,mineReplacementOreCost=10)
scene.write_text(json.dumps(d,indent=2))
d=json.loads(drone.read_text(encoding='utf-8-sig'))
def walk(x):
 if isinstance(x,dict):
  if x.get('type','').endswith('MiningSecurityDrone3D'):x['properties'].update(shotDamage=12,shotInterval=.35,attackRange=16)
  for v in x.values():walk(v)
 elif isinstance(x,list):
  for v in x:walk(v)
walk(d);drone.write_text(json.dumps(d,indent=2))
print('Installed timed expeditions, production tuning, weapon handling research and defence balance. Backups:',backup)
