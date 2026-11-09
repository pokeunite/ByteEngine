import pathlib,json,datetime,shutil
root=pathlib.Path('C:/Users/codex/Documents/palclone');scene=root/'Scenes/Main.bytescene'
backup=root/'SourceArt/Operation Director Backups'/datetime.datetime.now().strftime('%Y%m%d-%H%M%S');backup.mkdir(parents=True)
shutil.copy2(scene,backup/scene.name);data=json.loads(scene.read_text(encoding='utf-8-sig'))
op=next(c for o in data['gameObjects'] for c in o.get('components',[]) if c['type'].endswith('MiningOperation3D'))['properties']
op.update(stagedSpawning=True,initialWaveFraction=2/3,reinforcementBatch=4,killsBeforeReinforcement=3,minimumReinforcementGap=3,maximumAlive=16,recoverStrandedEnemies=True,strandedSeconds=22,finalEnemyMarkerDelay=5,overclockEnabled=True,overclockOfferSeconds=12,overclockHoldSeconds=.8,overclockBonusFraction=.5,overclockEnemyFraction=.5)
contracts=json.loads(op['contractsJson'])
if [c['Id'] for c in contracts]!=['survey','deep','overdrive']:raise RuntimeError('Custom contract IDs need review before migration')
base=dict(EnemyMultiplier=1,OreMultiplier=1,SpawnIntervalMultiplier=1,SpeedMultiplier=1,DamageMultiplier=1,HealthMultiplier=1,RaiderHealthMultiplier=1,HunterHealthMultiplier=1)
contracts=[dict(base,Id='survey',Name='Rig siege',Difficulty='LOW RISK',Description='Mostly rig raiders. Place defences in their approach lanes.',HunterFraction=.18,RaiderHealthMultiplier=1.15,SpeedMultiplier=.95),dict(base,Id='deep',Name='Hunter run',Difficulty='HIGH RISK',Description='Fast hunters pressure you. Keep moving and protect your firing space.',EnemyMultiplier=1.3,OreMultiplier=1.7,HunterFraction=.78,SpawnIntervalMultiplier=.8,SpeedMultiplier=1.1,HunterHealthMultiplier=.85),dict(base,Id='overdrive',Name='Swarm site',Difficulty='SEVERE',Description='Many fragile spiders. Shotguns, mines and mortar create space.',EnemyMultiplier=2.1,OreMultiplier=2.5,HunterFraction=.5,SpawnIntervalMultiplier=.55,HealthMultiplier=.7,DamageMultiplier=1.1)]
op['contractsJson']=json.dumps(contracts,separators=(',',':'));scene.write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
# Synchronize editable plugin source, preserving every replaced file.
source=pathlib.Path('Plugins/KarlsonDemo');target=root/'PluginSource/KarlsonDemo'
for file in source.rglob('*'):
 if not file.is_file() or any(p in ['bin','obj'] for p in file.relative_to(source).parts):continue
 relative=file.relative_to(source);destination=target/relative
 if destination.exists() and destination.read_bytes()==file.read_bytes():continue
 if destination.exists():
  saved=backup/'PluginSource'/relative;saved.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(destination,saved)
 destination.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(file,destination)
print(json.dumps({'scene':str(scene),'backup':str(backup),'contracts':[c['Name'] for c in contracts]}))
