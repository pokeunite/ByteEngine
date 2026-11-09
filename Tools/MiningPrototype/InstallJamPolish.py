from pathlib import Path
import json,shutil,datetime,uuid,math,wave,struct
W=Path(__file__).resolve().parents[2];P=Path('C:/Users/codex/Documents/palclone');scene=P/'Scenes/Main.bytescene';data=json.loads(scene.read_text(encoding='utf-8-sig'));backup=P/'SourceArt/Jam Polish Backups'/datetime.datetime.now().strftime('%Y%m%d-%H%M%S');backup.mkdir(parents=True);shutil.copy2(scene,backup/scene.name)
catalog=Path('Plugins/KarlsonDemo/Mining/CompanyResearchCatalog.json')
early={'shotgun':2,'mine':2,'sentry':3,'ar':4,'tracking':3,'mortar':5,'pellet':4,'rotary':6}
def revise(nodes):
 for n in nodes:
  n['MinimumRound']=early.get(n['Id'],min(n.get('MinimumRound',0),6))
  if n['Id']=='ar':n['Prerequisites']=['shotgun']
  if n['Id']=='mortar':n['Prerequisites']=['sentry']
 if not any(n['Id']=='rd' for n in nodes):
  n=dict(nodes[0]);n.update(Id='rd',Name='Field R&D',Description='Place a field research station. Order finite-battery security drones and destructible barricades. Available after the first operation.',Cost=60,Prerequisites=['charter'],Effect=0,Amount=0,Stat='ResearchStationGrant',MaxRank=1,RankCosts=[],Branch='Base',MinimumRound=2,Available=True,X=840,Y=65);nodes.append(n)
 return nodes
nodes=revise(json.loads(catalog.read_text()));catalog.write_text(json.dumps(nodes,indent=2)+'\n')
opOwner=next(o for o in data['gameObjects'] if any(c['type'].endswith('MiningOperation3D') for c in o.get('components',[])))
trade=next(c['properties'] for c in opOwner['components'] if c['type'].endswith('MiningTrade3D'));trade['upgradeMapCostMultiplier']=round(trade.get('upgradeMapCostMultiplier',.8)*.85,6);trade['researchDefinitionsJson']=json.dumps(revise(json.loads(trade['researchDefinitionsJson'])))
for o in data['gameObjects']:
 for c in o.get('components',[]):
  props=c.get('properties',{})
  if c['type'].endswith('MiningDefensePlanner3D'):props['mineBlastRadius']=5
  if c['type'].endswith('MiningEffects3D'):
   props['mineExplosionScale']=2.5;props['mortarExplosionScale']=3.2
  if c['type'].endswith('MiningOperation3D'):props['strandedSeconds']=10;props['spawnSpacing']=1.8
audio=P/'Assets/Audio/Mining/Support/mine-warning.wav';audio.parent.mkdir(parents=True,exist_ok=True)
with wave.open(str(audio),'wb') as f:
 f.setparams((1,2,44100,0,'NONE','not compressed'));samples=[]
 for i in range(int(.32*44100)):
  t=i/44100;phase=t%.16;envelope=min(1,phase/.005)*max(0,min(1,(.095-phase)/.01)) if phase<.095 else 0;samples.append(struct.pack('<h',int(math.sin(math.tau*1350*t)*envelope*16000)))
 f.writeframes(b''.join(samples))
meta=audio.with_name(audio.name+'.meta');gid=str(uuid.uuid5(uuid.NAMESPACE_URL,'hazardpay/mine-warning'));meta.write_text(json.dumps({'guid':gid,'type':'AudioClip'},indent=2))
for o in data['gameObjects']:
 for c in o.get('components',[]):
  if c['type'].endswith('MiningAudio3D'):c['properties']['mineBeep']={'guid':gid,'path':audio.relative_to(P).as_posix()}
def ref(name):
 path='Assets/Blueprints/Mining/Research/'+name+'.byteblueprint';return {'guid':json.loads((P/(path+'.meta')).read_text())['guid'],'path':path}
if not any(c['type'].endswith('MiningResearchStation3D') for c in opOwner['components']):
 tent=next(o for o in data['gameObjects'] if o['name']=='Company tent');pos=tent['transform']['localPosition'];position=[pos['x']+6,0,pos['z']]
 opOwner['components'].append({'type':'bytebard.karlsondemo.MiningResearchStation3D','enabled':True,'properties':{'operationObjectId':opOwner['id'],'stationBlueprint':ref('ResearchStation'),'droneBlueprint':ref('SecurityDrone'),'barricadeBlueprint':ref('FieldBarricade'),'stationPosition':position,'unlockId':'rd','droneCost':90,'barricadeOreCost':15,'maximumBarricades':3}})
scene.write_text(json.dumps(data,indent=2)+'\n');print(json.dumps({'backup':str(backup),'map_multiplier':trade['upgradeMapCostMultiplier'],'earlier_operations':early,'rd_operation':2,'mine_blast':5,'drone_cost':90}))
