"""Install camp expansion assets/tuning without exporting or changing player saves."""
from pathlib import Path
import json,copy,uuid,shutil,datetime
P=Path('C:/Users/codex/Documents/palclone');W=Path(__file__).resolve().parents[2]
B=P/'SourceArt/Research Support Backups'/datetime.datetime.now().strftime('expansion-%Y%m%d-%H%M%S');B.mkdir(parents=True,exist_ok=True)
def write(path,data):
 if path.exists():
  backup=B/path.relative_to(P);backup.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(path,backup)
 path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(data,indent=2)+'\n',encoding='utf-8')
def guid(name):return str(uuid.uuid5(uuid.NAMESPACE_URL,'hazard-pay/camp-expansion/'+name))
def comp(kind,**props):return dict(type=kind,enabled=True,properties=props)
scene=P/'Scenes/Main.bytescene';data=json.loads(scene.read_text(encoding='utf-8-sig'));objects=data['gameObjects']
op=next(o for o in objects if any(c['type'].endswith('MiningOperation3D') for c in o.get('components',[])))
for o in objects:
 for c in o.get('components',[]):
  props=c.get('properties',{});kind=c['type'].split('.')[-1]
  if kind=='MiningCampLayout3D':props.update(buildMinimum=[-25,-85],buildMaximum=[45,26])
  if kind=='MiningDefensePlanner3D':props.update(placementRange=30,mortarBlastRadius=10)
  if kind=='MiningResearchStation3D':props.update(maximumBarricades=0,extraRefineryCost=250)
  if kind=='MiningTrade3D':
   props['liveMarketPriceBonus']=1.2;catalog=json.loads(props['researchDefinitionsJson']);next(n for n in catalog if n['Id']=='rd')['Cost']=200;props['researchDefinitionsJson']=json.dumps(catalog)
write(scene,data)
blueprint=json.loads((P/'Assets/Blueprints/Mining/Research/FieldBarricade.byteblueprint').read_text())
source=next(o for o in objects if any(c['type'].endswith('MiningRefineryOutput3D') for c in o.get('components',[])))
root=copy.deepcopy(source);root.update(id=guid('refinery-root'),name='Additional refinery',parentId=None)
root['transform']['localPosition']=dict(x=0,y=0,z=0)
root['components'].append(comp('bytebard.karlsondemo.MiningAuxiliaryRefinery3D',operationObjectId=op['id'],loadCapacity=50))
blueprint.update(id=guid('refinery-blueprint'),name='Additional refinery',root=root,children=[])
def children(parent):
 for original in objects:
  if original.get('parentId')!=parent:continue
  child=copy.deepcopy(original);child['id']=guid(original['id']);child['parentId']=root['id'] if parent==source['id'] else guid(parent);blueprint['children'].append(child);children(original['id'])
children(source['id'])
dest=P/'Assets/Blueprints/Mining/Research/ExtraRefinery.byteblueprint';write(dest,blueprint)
if not Path(str(dest)+'.meta').exists():write(Path(str(dest)+'.meta'),dict(guid=guid('refinery-asset'),type='Blueprint'))
drone=P/'Assets/Blueprints/Mining/Research/SecurityDrone.byteblueprint';d=json.loads(drone.read_text());props=d['root']['components'][0]['properties'];props.update(followSpeed=7,patrolLength=75,pursuitRadius=65,selfDestructBatteryFraction=.1,diveSpeed=13,maximumFlightHeight=12,flightClearance=.6);write(drone,d)
event=P/'Assets/Events/Mining/money.byteevents';e=json.loads(event.read_text());rule=copy.deepcopy(e['rules'][0]);rule['id']=guid('ore-cheat-rule');rule['displayName']='F10 - add 100 refined ore (testing)'
for condition in rule['conditions']:
 condition['instanceId']=guid('ore-cheat-condition');condition['arguments']['key']['constant']['string']='F10'
for action in rule['actions']:
 action['instanceId']=guid('ore-cheat-action');action['id']='bytebard.karlsondemo.mining.addRefinedOre';action['arguments']['amount']['constant']['number']=100
e['rules']=[r for r in e['rules'] if r.get('displayName')!=rule['displayName']]+[rule];write(event,e)
print('Installed wider camp bounds, tuning, refinery blueprint and F10 ore cheat on money object. Backups: '+str(B))
