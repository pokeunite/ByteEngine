"""Install an editable pickup/drop demonstration without changing weapon animation assets."""
import argparse,copy,datetime,json,pathlib,shutil,uuid
parser=argparse.ArgumentParser();parser.add_argument('--project',type=pathlib.Path);parser.add_argument('--apply',action='store_true');args=parser.parse_args()
namespace=uuid.UUID('3ec94eda-2abb-40c5-9310-3f020b3eeb5d')
def uid(name):return str(uuid.uuid5(namespace,name))
def constant(value):return {'kind':'Constant','constant':{'type':'Boolean' if isinstance(value,bool) else 'Number' if isinstance(value,(int,float)) else 'String', 'boolean':value} if isinstance(value,bool) else {'type':'Number','number':value} if isinstance(value,(int,float)) else {'type':'String','string':value}}
def instruction(kind,name,**values):return {'id':kind,'instanceId':uid(name),'arguments':{k:constant(v) for k,v in values.items()}}
def rule(name,conditions,actions,roots=None):
 for a,b in zip(actions,actions[1:]):a['nextActionId']=b['instanceId']
 return {'id':uid(name),'displayName':name,'enabled':True,'conditions':conditions,'actions':actions,'subEvents':[],'hasExplicitConditionFlow':True,'connectedConditionIds':roots or [c['instanceId'] for c in conditions],'hasExplicitExecutionFlow':True,'firstActionId':actions[0]['instanceId'],'editorHeaderColor':'42A8B6'}
module={'id':uid('module'),'version':2,'name':'Weapon inventory — editable pickup and drop','requiredComponents':[],'rules':[
 rule('E — pick up nearest available weapon',[instruction('input.keyPressed','Pickup key',key='E'),instruction('bytebard.karlsondemo.inputActive','Pickup input',target='Self'),instruction('bytebard.karlsondemo.canPickUp','Pickup allowed',target='Self',nearest=True,pickup='Last Ray Hit')],[instruction('bytebard.karlsondemo.pickUp','Collect nearest',target='Self',nearest=True,pickup='Last Ray Hit',equip=True)]),
 rule('G — drop equipped weapon',[instruction('input.keyPressed','Drop key',key='G'),instruction('bytebard.karlsondemo.inputActive','Drop input',target='Self'),instruction('bytebard.karlsondemo.hasWeapon','Drop available',target='Self')],[instruction('bytebard.karlsondemo.drop','Drop weapon',target='Self')]) ]}
root=pathlib.Path(__file__).resolve().parents[1];sample=root/'Plugins/KarlsonDemo/Samples/Weapon inventory.byteevents';sample.write_text(json.dumps(module,indent=2)+'\n',encoding='utf-8')
if args.project:
 project=args.project;path=project/'Scenes/Weapons Range.bytescene';scene=json.loads(path.read_text(encoding='utf-8-sig'));objects=scene['gameObjects']
 player=next(o for o in objects if any(c['type']=='bytebard.karlsondemo.FpsGunLab3D' for c in o['components']))
 equipment=next(c['properties'] for c in player['components'] if c['type']=='bytebard.karlsondemo.FpsGunLab3D');equipment.update(startWithNoWeapons=False,carryLimit=2,startingSlot=0)
 template=next(o for o in objects if o['name']=='Practice target 1');rigs=[o for o in objects if any(c['type']=='bytebard.karlsondemo.WeaponInstance3D' for c in o['components'])]
 for rig in rigs:
  definition=next(c['properties'] for c in rig['components'] if c['type']=='bytebard.karlsondemo.WeaponDefinition3D');slot=definition['slot'];instance=next(c['properties'] for c in rig['components'] if c['type']=='bytebard.karlsondemo.WeaponInstance3D');instance['startInInventory']=slot==0
  name=definition.get('displayName',rig['name'])+' pickup';obj=next((o for o in objects if o['id']==uid(name)),None)
  if obj is None:
   obj=copy.deepcopy(template);obj.update(id=uid(name),name=name,tags=[],layer=0,parentId=None,active=slot!=0);obj['transform'].update(localPosition={'x':(slot-1)*2,'y':.2,'z':3},localScale={'x':.8,'y':.4,'z':.5});obj['components']=[copy.deepcopy(next(c for c in template['components'] if c['type']=='MeshRenderer')),{'type':'bytebard.karlsondemo.WeaponPickup3D','enabled':True,'properties':{'weaponObjectId':rig['id'],'pickupRadius':2.5}}];objects.append(obj)
  obj['components'][0]['properties']['baseColor']=[[.2,.6,1,1],[.9,.6,.15,1],[.3,.8,.4,1]][slot%3]
 controls=next(c['properties'] for o in objects if o['name']=='Controls' for c in o['components'] if c['type']=='UiText');controls['text']='WASD MOVE / SHIFT RUN / E PICK UP / G DROP / 1–3 EQUIP CARRIED / LMB FIRE / R RELOAD | CARRY LIMIT 2'
 event_component=next(c for c in player['components'] if c['type']=='EventModuleComponent');asset='Assets/Events/Weapon inventory.byteevents';asset_id=uid('asset')
 if not any(m.get('path')==asset for m in event_component['properties']['modules']):event_component['properties']['modules'].append({'guid':asset_id,'path':asset})
 sprint=rule('Hold Shift — run at 6',[instruction('input.keyHeld','Sprint key',key='LeftShift')],[instruction('character.setMoveSpeed','Run speed',speed=6)])
 not_shift=instruction('logic.not','Not sprint');shift=instruction('input.keyHeld','Walk shift',key='LeftShift');not_shift['conditionInputIds']=[shift['instanceId']]
 walk=rule('Release Shift — walk at 3',[not_shift,shift],[instruction('character.setMoveSpeed','Walk speed',speed=3)],[not_shift['instanceId']]);module['rules'] += [sprint,walk]
 next(c['properties'] for c in player['components'] if c['type']=='CharacterController3D')['moveSpeed']=3
 if args.apply:
  backup=project/'.byteengine/Backups'/('Weapon inventory '+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True);shutil.copy2(path,backup/path.name)
  destination=project/asset
  if destination.exists():shutil.copy2(destination,backup/destination.name)
  destination.write_text(json.dumps(module,indent=2)+'\n',encoding='utf-8');meta=destination.with_suffix('.byteevents.meta')
  if not meta.exists():meta.write_text(json.dumps({'guid':asset_id,'type':'EventModule'},indent=2)+'\n')
  path.write_text(json.dumps(scene,indent=2,ensure_ascii=False)+'\n',encoding='utf-8');print('Installed inventory demonstration; backup:',backup)
 else:print('Preview: pistol start, carry limit 2, three pickup proxies, E/G and Shift rules. Pass --apply to install.')
