"""Author native scene, animation profiles and editable event sheets. No runtime game script."""
import copy,datetime,json,pathlib,shutil,uuid
P=pathlib.Path('C:/Users/codex/Documents/ultra violence');NS=uuid.UUID('3541e9f7-fda2-48ed-a0c5-2f2f2eb6245d')
def uid(name):return str(uuid.uuid5(NS,name))
def write(path,data):
 path=P/path;path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n',encoding='utf-8');return path
def meta(path,kind):
 file=P/(path+'.meta')
 if not file.exists():write(path+'.meta',{'guid':uid(path),'type':kind})
 return json.loads(file.read_text(encoding='utf-8-sig'))['guid']
def vv(value):
 if isinstance(value,bool):return {'type':'Boolean','boolean':value}
 if isinstance(value,(int,float)):return {'type':'Number','number':value}
 if isinstance(value,list):return {'type':'Vector3','vector3':dict(zip(['x','y','z'],value))}
 return {'type':'String','string':value}
def val(value):return value if isinstance(value,dict) else {'kind':'Constant','constant':vv(value)}
def ref(member,component=None,obj=None):return {'kind':'Reference','reference':{'scope':'Component' if component else 'Self','objectName':obj,'componentType':component,'memberName':member}}
counter=0
def ins(kind,**args):
 global counter;counter+=1
 return {'instanceId':uid('instruction'+str(counter)),'id':kind,'arguments':{k:val(v) for k,v in args.items()}}
def rule(name,conditions,actions):
 for a,b in zip(actions,actions[1:]):a['nextActionId']=b['instanceId']
 roots=[c['instanceId'] for c in conditions if not c.get('_child')]
 for c in conditions:c.pop('_child',None)
 return {'id':uid(name),'displayName':name,'enabled':True,'conditions':conditions,'actions':actions,'hasExplicitConditionFlow':True,'connectedConditionIds':roots,'hasExplicitExecutionFlow':True,'firstActionId':actions[0]['instanceId'] if actions else None,'editorHeaderColor':'1D8D9D'}
def neg(condition):
 condition['_child']=True;n=ins('logic.not');n['conditionInputIds']=[condition['instanceId']];return [n,condition]
def module(name,rules):
 path='Assets/Events/'+name+'.byteevents';write(path,{'id':uid(name),'version':2,'name':name,'rules':rules});return {'guid':meta(path,'EventModule'),'path':path}
def comp(type_name,**props):return {'type':type_name,'enabled':True,'properties':props}
backup=P/'.byteengine/Backups'/('Before combat prototype '+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True,exist_ok=True)
shutil.copy2(P/'ultra violence.byteproject',backup/'ultra violence.byteproject')
scene=json.loads((P/'Scenes/Weapons Range.bytescene').read_text(encoding='utf-8-sig'));scene['sceneId']=uid('arena');scene['name']='Ultra Violence â€” Overdrive trial';scene['fixedSimulation']=False
objects=scene['gameObjects'];objects[:]=[o for o in objects if not o['name'].startswith('Practice target')]
template=copy.deepcopy(objects[0])
def obj(name,pos=(0,0,0),scale=(1,1,1),parent=None,components=None):
 o=copy.deepcopy(template);o.update(id=uid(name),name=name,active=True,parentId=parent,tags=[],layer=0,variables=[],components=components or []);o['transform']={'localPosition':dict(zip('xyz',pos)),'localRotation':{'x':0,'y':0,'z':0,'w':1},'localScale':dict(zip('xyz',scale))};objects.append(o);return o
def box(name,pos,scale,color,solid=True):
 components=[comp('MeshRenderer',usePrimitive=True,primitive='Cube',baseColor=color,roughness=.85,castShadows=True,receiveShadows=True)]
 if solid:components.append(comp('BoxCollider3D',size=[1,1,1]))
 return obj(name,pos,scale,components=components)
player=next(o for o in objects if o['name']=='FPS Character');player_id='id:'+player['id'];player['transform']['localPosition']={'x':0,'y':.05,'z':10}
player['components'] += [comp('HealthComponent',maxHealth=100,currentHealth=100,destroyOnDeath=False),comp('ResourceMeter3D',capacity=100,initialAmount=100)]
player['variables']=[{'name':'Overdrive','value':vv(False)}]
floor=next(o for o in objects if o['name']=='Solid practice floor');floor['transform']['localScale']={'x':38,'y':2,'z':38};floor['transform']['localPosition']={'x':0,'y':-1,'z':-3};next(c['properties'] for c in floor['components'] if c['type']=='MeshRenderer')['baseColor']=[.09,.13,.17,1]
for name,pos,size in [('Back',(0,2,-21),(38,4,1)),('Left',(-19,2,-3),(1,4,38)),('Right',(19,2,-3),(1,4,38)),('Front',(0,2,16),(38,4,1))]:box(name+' arena wall',pos,size,[.12,.16,.22,1])
for x in [-18,18]:box('Cyan arena rail '+str(x),(x,.12,-3),(.07,.12,35),[.05,.75,.85,1],False)
clock=obj('Enemy time domain',components=[comp('SimulationRate3D',rate=1)])
canvas=next(o for o in objects if any(c['type']=='UiCanvas' for c in o['components']))
next(c['properties'] for c in canvas['components'] if c['type']=='UiCanvas')['scaleMode']='ConstantPixels'
crosshair=next(c['properties'] for o in objects if o['name']=='Crosshair' for c in o['components'] if c['type']=='UiText');crosshair.update(anchor='Center',offset=[0,0])
def text(name,content,offset,size=22,anchor='TopLeft',color=(.92,.97,1,1),visible=True):return obj(name,parent=canvas['id'],components=[comp('UiText',text=content,size=size,anchor=anchor,offset=offset,color=list(color),shadowColor=[0,0,0,.9],shadowOffset=[1,1],wrapWidth=700,visible=visible,orderInLayer=20)])
def bar(name,offset,color):return obj(name,parent=canvas['id'],components=[comp('UiWidget',kind='ProgressBar',anchor='BottomRight',offset=offset,size=[260,16],color=[.03,.05,.08,.95],fillColor=color,maximum=100,value=100,cornerRadius=2,orderInLayer=10)])
bar('Health bar',[-28,-88],[.95,.2,.25,1]);bar('Energy bar',[-28,-40],[.05,.8,1,1]);text('Health label','HEALTH 100',[-28,-108],22,'BottomRight');text('Energy label','OVERDRIVE 100',[-28,-60],22,'BottomRight')
text('Combat objective','CLEAR THE ARENA',[28,70],28);text('Combat brief','Rushers close in. Gunners fire bolts. Heavies punish close range.',[28,107],20)
text('Overdrive status','OVERDRIVE ACTIVE',[-28,140],23,'TopRight',(.1,.9,1,1),False)
text('Defeat message','SYSTEM FAILURE\nENTER â€” RETRY',[0,-80],36,'Center',(1,.35,.35,1),False);text('Victory message','AREA CLEARED\nENTER â€” RUN IT AGAIN',[0,-80],36,'Center',(.1,.95,1,1),False)
effect=obj('Overdrive screen effect',components=[comp('ScreenOverlay',tint=[.05,.6,.9,.035],edgeColor=[.05,.75,1,.38],edgeWidth=.07,strength=0,orderInLayer=-100)])
controls=next(c['properties'] for o in objects if o['name']=='Controls' for c in o['components'] if c['type']=='UiText');controls.update(text='WASD MOVE   SHIFT RUN   HOLD RMB OVERDRIVE   E PICKUP   G DROP   1/2/3 EQUIP   R RELOAD',size=20,wrapWidth=1180)
enemy_names=[]
for role,model,hp,speed,attack_range,cooldown,clip,locations in [
 ('Rusher','Leela',55,2.7,1.8,1.0,'Punch',[(-5,0,-1),(6,0,-5)]),
 ('Gunner','George',85,1.7,13,2.0,'Shoot',[(-8,0,-6),(8,0,-8)]),
 ('Heavy','Mike',180,1.25,2.8,2.3,'Kick',[(0,0,-16),(11,0,-18)])]:
 model_path='Assets/Enemies/Quaternius/'+model+'.gltf';model_guid=meta(model_path,'Model3D')
 profile_path='Assets/Animation/Enemy '+role+'.byteanim';profile=json.loads((P/'Assets/Animation/Sketchfab Pistol.byteanim').read_text(encoding='utf-8-sig'));profile['name']=role+' â€” original Quaternius rig';profile['rig']['referenceModel']={'guid':model_guid,'cachedProjectPath':model_path};profile['rig']['type']='Generic';profile['locomotion'].update(enabled=False,driveFromCharacterController=False,idle='Idle',walk='Walk',run='Run');profile['actions']=[{'name':'Attack','clip':clip,'loop':False,'blendIn':.06,'blendOut':.08,'playbackSpeed':1,'priority':10,'interruptible':True},{'name':'Death','clip':'Death','loop':False,'blendIn':.04,'blendOut':0,'playbackSpeed':1,'priority':100,'interruptible':False}];write(profile_path,profile);profile_id=meta(profile_path,'AnimationProfile')
 rules=[rule(role+' â€” chase animation',[ins('enemyAI.isChasing',target='Self')],[ins('animation.setBaseClip',target='Self',clip='Run' if role=='Rusher' else 'Walk')]),rule(role+' â€” idle / aim animation',[ins('enemyAI.isAttacking',target='Self')],[ins('animation.setBaseClip',target='Self',clip='Idle')]),rule(role+' â€” idle outside detection',[ins('enemyAI.isIdle',target='Self')],[ins('animation.setBaseClip',target='Self',clip='Idle')])]
 attack=[ins('animation.playNamedAction',target='Self',action='Attack',retrigger=True),ins('time.wait',seconds=.45 if role=='Heavy' else .18)]
 if role=='Gunner':attack.append(ins('projectile.fireAtObject',target='Self',other=player_id,offset=[0,1,0],clock='id:'+clock['id']))
 else:attack.append(ins('combat.damageInRange',target='Self',other=player_id,range=attack_range+.2,amount=12 if role=='Rusher' else 28))
 conditions=[ins('enemyAI.attackFired',target='Self')]
 if role=='Gunner':conditions.append(ins('object.hasLineOfSight',target='Self',other=player_id))
 rules.append(rule(role+' â€” attack tells and '+('projectile' if role=='Gunner' else 'damage'),conditions,attack))
 for movement_rule in rules[:3]:
  gate=neg(ins('health.isDead',target='Self'));movement_rule['connectedConditionIds'].append(gate[0]['instanceId'])
  for condition in gate:condition.pop('_child',None)
  movement_rule['conditions']+=gate
 rules.append(rule(role+' â€” death animation and disable hitbox',[ins('health.isDead',target='Self'),ins('system.triggerOnce')],[ins('animation.setBaseClip',target='Self',clip=''),ins('animation.playNamedAction',target='Self',action='Death',forceInterrupt=True),ins('object.setComponentEnabled',target='Self',component='BoxCollider3D',enabled=False)]))
 sheet=module('Enemy '+role,rules)
 for index,position in enumerate(locations):
  name=role+' '+str(index+1);enemy_names.append(name)
  components=[comp('HealthComponent',maxHealth=hp,currentHealth=hp,destroyOnDeath=False),comp('SimpleEnemyAI3D',targetId=player['id'],moveSpeed=speed,detectionRange=27,attackRange=attack_range,stopDistance=attack_range-.15,damage=0,attackCooldown=cooldown,automaticDamage=False),comp('BoxCollider3D',size=[1.1,2.4,1],center=[0,1.2,0]),comp('AnimationController',animationProfileGuid=profile_id,animationProfilePath=profile_path,driveLocomotion=False,manualBaseClip='Idle',rootMotionMode='InPlace'),comp('SkeletalMeshRenderer',modelGuid=model_guid,modelPath=model_path,visible=True),comp('EventModuleComponent',modules=[sheet])]
  if role=='Gunner':components.append(comp('ProjectileLauncher3D',projectileSpeed=10,damage=14,fireCooldown=.2,muzzleOffset=[0,1.5,-.8]))
  renderer=next(c for c in components if c['type']=='SkeletalMeshRenderer');components.remove(renderer)
  enemy=obj(name,position,parent=clock['id'],components=components)
  visual_scale={'Rusher':.36,'Gunner':.37,'Heavy':.56}[role]
  visual=obj(name+' visual',scale=(visual_scale,)*3,parent=enemy['id'],components=[renderer]);visual['transform']['localRotation']={'x':0,'y':0,'z':0,'w':1}
def active(value):return ins('variable.compare',left=ref('Overdrive'),right=value,operator='==')
def set_active(value):return ins('variable.set',target=ref('Overdrive'),value=value)
ability=[rule('Q pressed â€” activate with 25 energy',[ins('input.mousePressed',button='Right'),ins('bytebard.karlsondemo.inputActive',target='Self'),ins('resource.atLeast',target='Self',amount=25),*neg(ins('health.isDead',target='Self'))],[set_active(True)]),
 rule('Q no longer held â€” return to normal time',[*neg(ins('input.mouseHeld',button='Right'))],[set_active(False)]),
 rule('Energy exhausted â€” shut down',[*neg(ins('resource.atLeast',target='Self',amount=.1))],[set_active(False)]),
 rule('Death â€” shut down ability',[ins('health.isDead',target='Self')],[set_active(False)]),
 rule('Input released to menu â€” shut down',[*neg(ins('bytebard.karlsondemo.inputActive',target='Self'))],[set_active(False)]),
 rule('Overdrive â€” slow enemies, drain 22 energy/sec',[active(True)],[ins('object.setSimulationRate',target='id:'+clock['id'],rate=.25),ins('resource.changePerSecond',target='Self',rate=-22,unscaled=True),ins('screen.setStrength',target='id:'+effect['id'],strength=1),ins('ui.show',target='Overdrive status')]),
 rule('Normal â€” restore enemies, recharge 16 energy/sec',[active(False)],[ins('object.setSimulationRate',target='id:'+clock['id'],rate=1),ins('resource.changePerSecond',target='Self',rate=16,unscaled=True),ins('screen.setStrength',target='id:'+effect['id'],strength=0),ins('ui.hide',target='Overdrive status')]),
 rule('HUD â€” health and energy values',[ins('system.always')],[ins('ui.setBarFromHealth',target='Health bar',source='Self'),ins('ui.setBarFromResource',target='Energy bar',source='Self'),ins('ui.setFormattedText',target='Health label',format='HEALTH {0:0}',value0=ref('CurrentHealth','HealthComponent')),ins('ui.setFormattedText',target='Energy label',format='OVERDRIVE {0:0}',value0=ref('Current','ResourceMeter3D'))]),
 rule('Defeat â€” show retry and stop player',[ins('health.isDead',target='Self')],[ins('ui.show',target='Defeat message'),ins('object.setComponentEnabled',target='Self',component='PlayerController3D',enabled=False),ins('object.setComponentEnabled',target='Self',component='FpsGunLab3D',enabled=False)]),
 rule('Defeat â€” Enter restarts',[ins('health.isDead',target='Self'),ins('input.keyPressed',key='Enter')],[ins('scene.restart')]),
 rule('All six threats eliminated â€” victory',[ins('health.isDead',target=name) for name in enemy_names],[ins('ui.show',target='Victory message')]),
 rule('Victory â€” Enter restarts',[*[ins('health.isDead',target=name) for name in enemy_names],ins('input.keyPressed',key='Enter')],[ins('scene.restart')])]
ability_module=module('Overdrive and combat HUD',ability);next(c['properties']['modules'] for c in player['components'] if c['type']=='EventModuleComponent').append(ability_module)
combat_input=json.loads((P/'Assets/Events/FPS controls.byteevents').read_text(encoding='utf-8-sig'))
for input_rule in combat_input['rules']:
 gate=neg(ins('health.isDead',target='Self'))
 for condition in gate:condition.pop('_child',None)
 input_rule['conditions']+=gate
 if input_rule.get('hasExplicitConditionFlow'):input_rule.setdefault('connectedConditionIds',[]).append(gate[0]['instanceId'])
combat_input['id']=uid('Combat weapon input');combat_input['name']='Combat weapon input';input_path='Assets/Events/Combat weapon input.byteevents';write(input_path,combat_input);input_guid=meta(input_path,'EventModule')
for entry in next(c['properties']['modules'] for c in player['components'] if c['type']=='EventModuleComponent'):
 if entry.get('path')=='Assets/Events/FPS controls.byteevents':entry.update(guid=input_guid,path=input_path)
scene_path='Scenes/Combat Prototype.bytescene'
font_path='Assets/UI/Fonts/Rajdhani-SemiBold.ttf';font_id=meta(font_path,'Font')
for item in objects:
 for component in item['components']:
  if component['type']=='UiText':
   component['properties']['font']={'guid':font_id,'path':font_path}
   if item['name'] in ['Controls','Combat brief','Health label','Energy label']:component['properties']['size']=26
if (P/scene_path).exists():shutil.copy2(P/scene_path,backup/'Combat Prototype.bytescene')
write(scene_path,scene);project=json.loads((P/'ultra violence.byteproject').read_text(encoding='utf-8-sig'));project['startupScene']=scene_path;write('ultra violence.byteproject',project)
(P/'Assets/Enemies/Quaternius/Sources.txt').write_text('Author: Quaternius\nOriginal pack: https://quaternius.com/packs/animatedmech.html\nDistribution mirror: https://github.com/agentkaerf/FreeModels/tree/main/Animated%20Mech%20Pack%20-%20March%202021\nLicense: CC0 1.0; included License.txt\nOriginal rigs and embedded animations preserved.\n',encoding='utf-8')
print('Authored combat scene, three enemy sheets, Overdrive/HUD sheet, profiles and meters. Backup:',backup)
