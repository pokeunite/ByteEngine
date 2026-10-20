"""Add sound assets and native audio event sheets without rebuilding the user's scene."""
import json,pathlib,uuid,copy,datetime,shutil,math,random,wave,array
P=pathlib.Path('C:/Users/codex/Documents/ultra violence');NS=uuid.UUID('f09524eb-42de-4d74-a6e3-e40ba44a8bf0')
def uid(name):return str(uuid.uuid5(NS,name))
def write(path,data):
 path=P/path;path.parent.mkdir(parents=True,exist_ok=True);path.write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
def meta(path,kind):
 f=P/(path+'.meta')
 if not f.exists():write(path+'.meta',{'guid':uid(path),'type':kind})
 return json.loads(f.read_text(encoding='utf-8-sig'))['guid']
def vv(v):return {'type':'Boolean','boolean':v} if isinstance(v,bool) else {'type':'Number','number':v} if isinstance(v,(float,int)) else {'type':'String','string':v}
def value(v):return v if isinstance(v,dict) else {'kind':'Constant','constant':vv(v)}
def ref(name,component=None):return {'kind':'Reference','reference':{'scope':'Component' if component else 'Self','componentType':component,'memberName':name}}
counter=0
def ins(kind,**args):
 global counter;counter+=1
 return {'id':kind,'instanceId':uid('instruction'+str(counter)),'arguments':{k:value(v) for k,v in args.items()}}
def neg(c):n=ins('logic.not');n['conditionInputIds']=[c['instanceId']];c['_child']=True;return [n,c]
def rule(name,conditions,actions):
 for a,b in zip(actions,actions[1:]):a['nextActionId']=b['instanceId']
 roots=[c['instanceId'] for c in conditions if not c.pop('_child',False)]
 return {'id':uid(name),'displayName':name,'enabled':True,'conditions':conditions,'actions':actions,'hasExplicitConditionFlow':True,'connectedConditionIds':roots,'hasExplicitExecutionFlow':True,'firstActionId':actions[0]['instanceId'] if actions else None,'editorHeaderColor':'A377C6'}
backup=P/'.byteengine/Backups'/('Before sound pass '+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True)
scene_path='Scenes/Combat Prototype.bytescene';shutil.copy2(P/scene_path,backup/'Combat Prototype.bytescene');scene=json.loads((P/scene_path).read_text(encoding='utf-8-sig'));objects=scene['gameObjects'];base=copy.deepcopy(objects[0])
def object(name,components,parent=None):
 existing=next((o for o in objects if o['id']==uid(name)),None)
 if existing is not None:return existing
 o=copy.deepcopy(base);o.update(id=uid(name),name=name,components=components,parentId=parent,active=True,tags=[],layer=0,variables=[]);o['transform']={'localPosition':{'x':0,'y':0,'z':0},'localRotation':{'x':0,'y':0,'z':0,'w':1},'localScale':{'x':1,'y':1,'z':1}};objects.append(o);return o
def comp(name,**p):return {'type':name,'enabled':True,'properties':p}
def module(name,rules):
 path='Assets/Events/Audio/'+name+'.byteevents';existing=P/path
 if existing.exists():shutil.copy2(existing,backup/existing.name)
 write(path,{'id':uid(name),'name':name,'version':2,'rules':rules});return {'guid':meta(path,'EventModule'),'path':path}
def attach(o,sheet):
 c=next((c for c in o['components'] if c['type']=='EventModuleComponent'),None)
 if c is None:c=comp('EventModuleComponent',modules=[]);o['components'].append(c)
 if not any(s.get('path')==sheet['path'] for s in c['properties']['modules']):c['properties']['modules'].append(sheet)
def variable(o,name,amount):
 if not any(v['name']==name for v in o['variables']):o['variables'].append({'name':name,'value':vv(amount)})
def setvar(name,v):return ins('variable.set',target=ref(name),value=v)
def compare(left,op,right):return ins('variable.compare',left=left,operator=op,right=right)
def synth(name,duration,kind,seed):
 rng=random.Random(seed);rate=32000;filtered=0;result=[]
 for n in range(int(duration*rate)):
  t=n/rate;noise=rng.uniform(-1,1);filtered=filtered*.72+noise*.28
  if kind=='gun':sample=(noise*.45*math.exp(-t*65)+filtered*.9*math.exp(-t*16)+math.sin(2*math.pi*(92*t-22*t*t))*.65*math.exp(-t*26))*(1-math.exp(-t*6000))
  else:
   freq=660 if kind=='up' else 190
   sample=.45*math.sin(2*math.pi*(freq*t+(900 if kind=='up' else -100)*t*t))*math.sin(math.pi*t/duration)**2
  result.append(sample)
 peak=max(abs(x) for x in result);pcm=array.array('h',(int(x/peak*27500) for x in result));folder=P/'Assets/Audio/SFX/Designed';folder.mkdir(parents=True,exist_ok=True)
 with wave.open(str(folder/(name+'.wav')),'wb') as out:out.setparams((1,2,rate,len(pcm),'NONE','not compressed'));out.writeframes(pcm.tobytes())
for entry in [('Pistol shot',.21,'gun',1),('Rifle shot',.14,'gun',7),('Shotgun blast',.42,'gun',3),('Energy ready',.25,'up',0),('Energy unavailable',.18,'down',0)]:synth(*entry)
library=object('Sound library',[])
sounds={
 'Pistol':('Designed/Pistol shot.wav',.72,False,1), 'Rifle':('Designed/Rifle shot.wav',.62,False,1.2),'Shotgun':('Designed/Shotgun blast.wav',.9,False,.85),
 'Equip':('Kenney/impactMetal_light_001.wav',.28,False,1.1),'Reload':('Kenney/impactPlate_light_002.wav',.4,False,1),'Reload finish':('Kenney/impactPlate_light_001.wav',.28,False,1.2),'Dry fire':('Kenney/impactGeneric_light_000.wav',.35,False,1.15),
 'Hit marker':('Kenney/impactMetal_light_003.wav',.14,False,1.6),'Metal impact':('Kenney/impactMetal_002.wav',.35,True,1),'Pickup':('Designed/Energy ready.wav',.28,False,1.2),'Drop':('Kenney/impactMetal_heavy_000.wav',.32,False,1),
 'Footstep':('Kenney/footstep_concrete_000.wav',.34,False,1),'Run step':('Kenney/footstep_concrete_001.wav',.42,False,1.1),'Land':('Kenney/footstep_concrete_002.wav',.55,False,.8),'Jump':('Kenney/footstep_concrete_001.wav',.22,False,1.3),
 'Hurt':('Kenney/impactPunch_heavy_000.wav',.45,False,1),'Rusher step':('Kenney/footstep_concrete_000.wav',.23,True,1.15),'Gunner step':('Kenney/impactMetal_light_000.wav',.17,True,.85),'Heavy step':('Kenney/impactMetal_heavy_000.wav',.42,True,.7),
 'Swing':('Kenney/impactPunch_medium_000.wav',.3,True,.8),'Enemy bolt':('Kenney/laserSmall_002.wav',.4,True,.8),'Enemy hit':('Kenney/impactMetal_light_001.wav',.2,True,1.4),'Enemy death':('Kenney/explosionCrunch_000.wav',.55,True,.85),
 'Overdrive on':('Kenney/forceField_000.wav',.38,False,1),'Overdrive off':('Kenney/forceField_002.wav',.28,False,.9),'Overdrive hum':('Kenney/engineCircular_000.wav',.055,False,.75),
 'Energy full':('Designed/Energy ready.wav',.18,False,1),'Energy denied':('Designed/Energy unavailable.wav',.3,False,1),'Victory':('Kenney/impactBell_heavy_000.wav',.42,False,1.15),'Defeat':('Kenney/lowFrequency_explosion_000.wav',.48,False,.9),'Ambient':('Kenney/computerNoise_003.wav',.018,False,.75)}
for name,(path,volume,spatial,pitch) in sounds.items():
 asset='Assets/Audio/SFX/'+path;guid=meta(asset,'AudioClip');object('SFX '+name,[comp('AudioSource3D',clip={'guid':guid,'path':asset},volume=volume,pitch=pitch,spatial=spatial,loop=name in ['Overdrive hum','Ambient'],playOnStart=False,bus='Sfx',minDistance=4,maxDistance=35,rolloffFactor=.7)],library['id'])
def play(name,emitter='Self',variation=.04):return ins('audio.playOneShot',target='SFX '+name,emitter=emitter,variation=variation,maxVoices=6)
def timer(name,duration):return ins('time.startTimer',name=name,duration=duration)
player=next(o for o in objects if o['name']=='FPS Character');camera=next(o for o in objects if any(c['type']=='Camera3D' for c in o['components']))
if not any(c['type']=='AudioListener3D' for o in objects for c in o['components']):camera['components'].append(comp('AudioListener3D',volume=1))
for o in objects[:]:
 definition=next((c for c in o['components'] if c['type']=='bytebard.karlsondemo.WeaponDefinition3D'),None)
 if definition is None:continue
 role={0:'Pistol',1:'Shotgun',2:'Rifle'}.get(definition['properties'].get('slot'))
 if role is None:continue
 rules=[]
 for trigger,sound in [('fired',role),('equipped','Equip'),('reloadStarted','Reload'),('reloadComplete','Reload finish'),('reloadCancelled','Equip')]:rules.append(rule(role+' — '+trigger+' sound',[ins('bytebard.karlsondemo.'+trigger,target='Self')],[play(sound)]))
 rules.append(rule(role+' — confirmed hit feedback',[ins('bytebard.karlsondemo.hit',target='Self')],[play('Hit marker'),play('Metal impact','Last Ray Hit')]))
 rules.append(rule(role+' — empty trigger click',[ins('input.mousePressed',button='Left'),ins('bytebard.karlsondemo.inputActive',target='Self'),ins('bytebard.karlsondemo.magazineEmpty',target='Self')],[play('Dry fire')]))
 attach(o,module('Weapon audio '+role,rules))
for name,start in [('AudioPreviousHealth',100),('AudioPreviousCount',1),('AudioWasOverdrive',False),('AudioPreviousEnergy',100)]:variable(player,name,start)
health=ref('CurrentHealth','HealthComponent');energy=ref('Current','ResourceMeter3D');count=ref('WeaponCount','FpsGunLab3D');active=ref('Overdrive')
rules=[rule('Sound — initialise cadence and ambience',[ins('system.triggerOnce')],[timer('Player steps',.2),setvar('AudioPreviousHealth',health),setvar('AudioPreviousCount',count),ins('audio.play',target='SFX Ambient')]),
 rule('Sound — successful pickup',[compare(count,'>',ref('AudioPreviousCount'))],[play('Pickup')]),rule('Sound — successful drop',[compare(count,'<',ref('AudioPreviousCount'))],[play('Drop')]),
 rule('Sound — player hurt',[compare(health,'<',ref('AudioPreviousHealth')),*neg(ins('health.isDead',target='Self'))],[play('Hurt')]),
 rule('Sound — normal footsteps',[ins('character.isMoving'),ins('character.isGrounded'),compare(ref('Speed','CharacterController3D'),'<',4),ins('time.timerFinished',name='Player steps')],[play('Footstep',variation=.08),timer('Player steps',.43)]),
 rule('Sound — running footsteps',[ins('character.isMoving'),ins('character.isGrounded'),compare(ref('Speed','CharacterController3D'),'>=',4),ins('time.timerFinished',name='Player steps')],[play('Run step',variation=.08),timer('Player steps',.28)]),
 rule('Sound — jump',[ins('input.keyPressed',key='Space'),ins('bytebard.karlsondemo.inputActive',target='Self'),ins('character.isGrounded')],[play('Jump')]),
 rule('Sound — land',[ins('character.justLanded')],[play('Land')]),
 rule('Sound — Overdrive begins',[compare(active,'==',True),compare(ref('AudioWasOverdrive'),'==',False)],[play('Overdrive on',variation=0),ins('audio.play',target='SFX Overdrive hum')]),
 rule('Sound — Overdrive ends',[compare(active,'==',False),compare(ref('AudioWasOverdrive'),'==',True)],[ins('audio.stop',target='SFX Overdrive hum'),play('Overdrive off',variation=0)]),
 rule('Sound — insufficient energy',[ins('input.mousePressed',button='Right'),*neg(ins('resource.atLeast',target='Self',amount=25))],[play('Energy denied',variation=0)]),
 rule('Sound — fully recharged',[compare(energy,'>=',100),compare(ref('AudioPreviousEnergy'),'<',100)],[play('Energy full',variation=0)]),
 rule('Sound — defeat',[ins('health.isDead',target='Self'),ins('system.triggerOnce')],[play('Defeat',variation=0),ins('audio.stop',target='SFX Ambient'),ins('audio.stop',target='SFX Overdrive hum')]),
 rule('Sound — victory',[*[ins('health.isDead',target=o['name']) for o in objects if any(c['type']=='SimpleEnemyAI3D' for c in o['components'])],*neg(ins('health.isDead',target='Self')),ins('system.triggerOnce')],[play('Victory',variation=0)]),
 rule('Sound — remember last frame',[ins('system.always')],[setvar('AudioPreviousHealth',health),setvar('AudioPreviousCount',count),setvar('AudioWasOverdrive',active),setvar('AudioPreviousEnergy',energy)])]
attach(player,module('Player and cybernetics audio',rules))
for o in objects[:]:
 ai=next((c for c in o['components'] if c['type']=='SimpleEnemyAI3D'),None)
 if ai is None:continue
 role=o['name'].split()[0];hp=next(c['properties']['maxHealth'] for c in o['components'] if c['type']=='HealthComponent');variable(o,'AudioPreviousHealth',hp)
 rules=[rule(role+' sound — start step clock',[ins('system.triggerOnce')],[timer('Enemy steps',.1)]),rule(role+' sound — movement',[ins('enemyAI.isChasing',target='Self'),ins('time.timerFinished',name='Enemy steps')],[play(role+' step'),timer('Enemy steps',{'Rusher':.45,'Gunner':.7,'Heavy':.9}[role])]),rule(role+' sound — hit',[compare(health,'<',ref('AudioPreviousHealth')),*neg(ins('health.isDead',target='Self'))],[play('Enemy hit')]),rule(role+' sound — death',[ins('health.isDead',target='Self'),ins('system.triggerOnce')],[play('Enemy death')]),rule(role+' sound — remember health',[ins('system.always')],[setvar('AudioPreviousHealth',health)])]
 attach(o,module('Enemy audio '+role,rules))
for role in ['Rusher','Gunner','Heavy']:
 path='Assets/Events/Enemy '+role+'.byteevents';source=P/path;shutil.copy2(source,backup/source.name);data=json.loads(source.read_text(encoding='utf-8-sig'))
 for r in data['rules']:
  if not any(c['id']=='enemyAI.attackFired' for c in r['conditions']):continue
  if any(a['id']=='audio.playOneShot' for a in r['actions']):continue
  sound=play('Enemy bolt' if role=='Gunner' else 'Swing');r['actions'].append(sound)
  if role=='Gunner':
   previous=next(a for a in r['actions'] if a['id']=='projectile.fireAtObject');sound['nextActionId']=previous.get('nextActionId');previous['nextActionId']=sound['instanceId']
  else:
   previous=r['actions'][0];sound['nextActionId']=previous.get('nextActionId');previous['nextActionId']=sound['instanceId']
 write(path,data)
write(scene_path,scene)
(P/'Assets/Audio/SFX/Sources.txt').write_text('Kenney Sci-fi Sounds and Impact Sounds — CC0.\nhttps://kenney.nl/assets/sci-fi-sounds\nhttps://kenney.nl/assets/impact-sounds\nOriginal licenses included in Kenney/. Mono conversion and peak normalization applied.\nDesigned/ contains original generated gunshot and energy-cue WAVs; no third-party samples.\nAll triggers: Assets/Events/Audio. Sound library objects own clip, level, pitch and spatial settings.\n',encoding='utf-8')
print('Installed',len(sounds),'sound objects and 7 editable audio sheets. Backup:',backup)
