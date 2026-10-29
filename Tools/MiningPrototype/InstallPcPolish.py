"""Apply explicit, editable PC tuning to the authored scene. Never touches saves."""
import json, pathlib, shutil, datetime
root=pathlib.Path('C:/Users/codex/Documents/palclone')
path=root/'Scenes/Main.bytescene'
data=json.loads(path.read_text(encoding='utf-8-sig'))
index={o['id']:o for o in data['gameObjects']}
backup=pathlib.Path('artifacts/pc-polish-backup')/datetime.datetime.now().strftime('%Y%m%d-%H%M%S')
backup.mkdir(parents=True,exist_ok=True);shutil.copy2(path,backup/path.name)
for obj in data['gameObjects']:
 for c in obj['components']:
  p=c['properties'];kind=c['type'].split('.')[-1]
  if kind=='MiningTrade3D':p.update(outputCost=140,storageCost=110,efficiencyCost=180,upgradeCostGrowth=2.15,efficiencyPerLevel=.2,baseStorage=60,extraStoragePerLevel=30)
  if kind=='MiningOperation3D':p.update(refineryBaseCost=130,refinerySpeedBaseCost=160,refineryYieldBaseCost=240,refineryCostGrowth=2.15,raiderDamage=6,hunterDamage=6,raiderAttackCooldown=1.4,raiderRecovery=.65)
  if kind=='WeaponDefinition3D' and p.get('displayName')=='Pistol':p.update(spreadDegrees=.1,accuracyDegrees=.1,movingInaccuracyDegrees=.35,maximumBloomDegrees=1,bloomPerShotDegrees=.18)
cover_roots={o['id'] for o in data['gameObjects'] if 'cover' in o['name'].lower() and o['name'].startswith(('R09','R10'))}
def in_cover(obj):
 while obj:
  if obj['id'] in cover_roots:return True
  obj=index.get(obj.get('parentId'))
 return False
for obj in data['gameObjects']:
 if not in_cover(obj):continue
 for c in obj['components']:
  if c['type']=='BoxCollider3D':c['enabled']=False
 render=next((c['properties'] for c in obj['components'] if c['type']=='MeshRenderer'),None)
 if render and not any(c['type']=='MeshCollider3D' for c in obj['components']):
  obj['components'].append(dict(type='MeshCollider3D',enabled=True,properties=dict(model=dict(guid=render['modelGuid'],path=render['modelPath']),meshKey=render['meshKey'],mode='StaticTriangles',size=[1,1,1],center=[0,0,0],isTrigger=False,useProjectMatrix=True,collisionMask=4294967295)))
tmp=path.with_suffix('.bytescene.tmp');tmp.write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8');tmp.replace(path)
print('Updated authored PC tuning; scene backed up to',backup)
