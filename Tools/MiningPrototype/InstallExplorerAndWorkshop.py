"""Install the explorer and explicit workshop tuning. Preserve IDs, sockets and saves."""
import datetime,json,pathlib,shutil,uuid
root=pathlib.Path('C:/Users/codex/Documents/palclone')
scene_path=root/'Scenes/Main.bytescene'
scene=json.loads(scene_path.read_text(encoding='utf-8-sig'))
backup=pathlib.Path('artifacts/explorer-workshop-backup')/datetime.datetime.now().strftime('%Y%m%d-%H%M%S')
backup.mkdir(parents=True,exist_ok=True)
shutil.copy2(scene_path,backup/scene_path.name)
asset=root/'Assets/Characters/HazardPayExplorer.glb'
if not asset.exists():raise FileNotFoundError(asset)
meta=asset.with_suffix('.glb.meta')
if not meta.exists():
 source=json.loads((root/'Assets/Characters/synty new.glb.meta').read_text(encoding='utf-8-sig'))
 source['guid']=str(uuid.uuid4());meta.write_text(json.dumps(source,indent=2)+'\n')
model_id=json.loads(meta.read_text(encoding='utf-8-sig'))['guid']
player_id=next(c['properties']['playerObjectId'] for o in scene['gameObjects'] for c in o['components'] if c['type'].endswith('MiningOperation3D'))
player=next(o for o in scene['gameObjects'] if o['id']==player_id)
profile_source=root/'Assets/Animations/SyntyDirectionalLocomotion.byteanim'
profile_path=root/'Assets/Animations/HazardPayExplorer.byteanim'
profile=json.loads(profile_source.read_text(encoding='utf-8-sig'));profile['name']='Hazard Pay Explorer'
profile['rig']['referenceModel']={'guid':model_id,'cachedProjectPath':'Assets/Characters/HazardPayExplorer.glb'}
profile['locomotion']['turning']['enabled']=False
if profile_path.exists():shutil.copy2(profile_path,backup/profile_path.name)
profile_path.write_text(json.dumps(profile,indent=2)+'\n')
profile_meta=profile_path.with_suffix('.byteanim.meta')
if not profile_meta.exists():
 source=json.loads(profile_source.with_suffix('.byteanim.meta').read_text(encoding='utf-8-sig'));source['guid']=str(uuid.uuid4());profile_meta.write_text(json.dumps(source,indent=2)+'\n')
profile_id=json.loads(profile_meta.read_text())['guid']
for c in player['components']:
 p=c['properties']
 if c['type']=='ModelHierarchyInstance':p.update(modelGuid=model_id,modelPath='Assets/Characters/HazardPayExplorer.glb')
 if c['type']=='AnimationController':p.update(animationProfileGuid=profile_id,animationProfilePath='Assets/Animations/HazardPayExplorer.byteanim')
for o in scene['gameObjects']:
 for c in o['components']:
  p=c['properties'];kind=c['type'].split('.')[-1]
  if kind=='MiningOperation3D':p.update(refineryBatch=3,extraBatchPerLevel=1,refinerySeconds=3,refinerySpeedPerLevel=.25,linearRefinerySpeedUpgrades=True,minimumRefinerySeconds=1,queueRefineryBatches=True,automaticDefeatRecovery=True,defeatFadeSeconds=.65,defeatBlackSeconds=.4)
  if kind=='MiningTrade3D':p.update(useProductionCycles=True,baseCycleSeconds=5,cycleSecondsReductionPerLevel=.5,minimumCycleSeconds=1,baseRoundsPerCycle=5,extraRoundsPerCycle=1,baseStorage=35,extraStoragePerLevel=15)
  if kind=='MiningAmmoBucket3D':p.update(pickupFlightSeconds=.28,maximumPickupTrails=12)
tmp=scene_path.with_suffix('.bytescene.tmp');tmp.write_text(json.dumps(scene,indent=2)+'\n');tmp.replace(scene_path)
print('Explorer assigned to existing player; original profile, IDs, events and saves preserved. Backup:',backup)
