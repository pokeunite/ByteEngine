"""Targeted arena-movement / projectile / VFX pass. Keeps the user's weapon ordering and scene edits."""
import json,pathlib,uuid,datetime,shutil,copy,wave,array,math
P=pathlib.Path('C:/Users/codex/Documents/ultra violence');ROOT=pathlib.Path(__file__).resolve().parents[2]
NS=uuid.UUID('8addd7c8-a963-4fe6-bf48-8e91126129bf')
def uid(s):return str(uuid.uuid5(NS,s))
def load(p):return json.loads((P/p).read_text(encoding='utf-8-sig'))
backup=P/'.byteengine/Backups'/('Before arena feel '+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True)
def save(p,d):
 f=P/p
 if f.exists():shutil.copy2(f,backup/f.name)
 f.parent.mkdir(parents=True,exist_ok=True);f.write_text(json.dumps(d,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
def asset(p,kind):
 if not (P/(p+'.meta')).exists():save(p+'.meta',{'guid':uid(p),'type':kind})
 return {'guid':load(p+'.meta')['guid'],'path':p}
def vv(v):return {'type':'Boolean','boolean':v}if isinstance(v,bool)else {'type':'Number','number':v}if isinstance(v,(int,float))else {'type':'String','string':v}
def val(v):return v if isinstance(v,dict)else {'kind':'Constant','constant':vv(v)}
def ref(n,obj=None):return {'kind':'Reference','reference':{'scope':'Object'if obj else 'Self','memberName':n,'objectName':obj}}
serial=0
def ins(kind,**kw):
 global serial;serial+=1
 return {'id':kind,'instanceId':uid('feel instruction'+str(serial)),'arguments':{k:val(v)for k,v in kw.items()}}
def rule(name,conditions,actions):
 for a,b in zip(actions,actions[1:]):a['nextActionId']=b['instanceId']
 return {'id':uid(name),'displayName':name,'enabled':True,'conditions':conditions,'actions':actions,'hasExplicitConditionFlow':True,'connectedConditionIds':[x['instanceId']for x in conditions],'hasExplicitExecutionFlow':True,'firstActionId':actions[0]['instanceId'],'editorHeaderColor':'187C91'}
scene_path='Scenes/Combat Prototype.bytescene';s=load(scene_path);objects=s['gameObjects'];player=next(o for o in objects if o['name']=='FPS Character')
def component(o,t):return next((c['properties']for c in o['components']if c['type']==t),None)
def attach(o,path):
 m=component(o,'EventModuleComponent')
 if m is None:m={'modules':[]};o['components'].append({'type':'EventModuleComponent','enabled':True,'properties':m})
 if not any(a['path']==path for a in m['modules']):m['modules'].append(asset(path,'EventModule'))
motor=component(player,'CharacterController3D');motor.update(arenaMovement=True,moveSpeed=7,acceleration=95,deceleration=65,groundFriction=8,stopSpeed=4,airAcceleration=22,maximumAirSpeed=13,jumpForce=7.8,gravity=24,coyoteTime=.12,jumpBuffer=.13)
for o in objects:
 cam=component(o,'Camera3D')
 if cam is not None:cam['fieldOfView']=72
 if o['name'].endswith(' visual') and component(o,'SkeletalMeshRenderer')is not None:
  if component(o,'bytebard.afterimage.Afterimage3D')is None:o['components'].append({'type':'bytebard.afterimage.Afterimage3D','enabled':True,'properties':{'emitting':False,'interval':.075,'lifetime':.38,'minimumDistance':.045,'maximumGhosts':4,'color':[.1,.75,1,.22]}})
  path='Assets/Events/Afterimages.byteevents';attach(o,path)
 if component(o,'AnimationController')is not None and 'view model'in o['name']:component(o,'AnimationController')['runThreshold']=8.5
save('Assets/Events/Afterimages.byteevents',{'id':uid('Afterimages'),'name':'Afterimages','version':2,'rules':[rule('Overdrive — leave frozen silhouettes',[ins('variable.compare',left=ref('Overdrive','FPS Character'),operator='==',right=True)],[ins('bytebard.afterimage.emit',enabled=True)]),rule('Normal time — stop emitting, let ghosts fade',[ins('variable.compare',left=ref('Overdrive','FPS Character'),operator='==',right=False)],[ins('bytebard.afterimage.emit',enabled=False)])]})
path='Assets/Events/Arena movement.byteevents';save(path,{'id':uid('Arena movement'),'name':'Arena movement','version':2,'rules':[rule('Arena movement — editable acceleration and air steering',[ins('system.triggerOnce')],[ins('character.configureArenaMovement',enabled=True,acceleration=95,friction=8,stopSpeed=4,airAcceleration=22,airCap=13)])]});attach(player,path)
inventory='Assets/Events/Weapon inventory.byteevents';m=load(inventory)
for r in m['rules']:
 for a in r['actions']:
  if a['id']=='character.setMoveSpeed':
   for v in a['arguments'].values():
    if v.get('constant',{}).get('type')=='Number':
     running=not any(c['id']=='logic.not'for c in r['conditions']);v['constant']['number']=10.5 if running else 7;r['displayName']='Hold Shift — sprint at 10.5'if running else 'Release Shift — move at 7'
save(inventory,m)
# Step cadence and animation thresholds follow the faster native motor.
path='Assets/Events/Audio/Player and cybernetics audio.byteevents';m=load(path)
for r in m['rules']:
 for c in r['conditions']:
  if c['id']=='variable.compare'and c['arguments'].get('left',{}).get('reference',{}).get('memberName')=='Speed':c['arguments']['right']=val(8.5)
save(path,m)
def vec(x,y,z):return dict(X=x,Y=y,Z=z)
def color(r,g,b,a):return dict(X=r,Y=g,Z=b,W=a)
def layer(name,**kw):
 d=dict(Name=name,Enabled=True,Shape='Point',RenderMode='Billboard',Sprite='SoftDisc',Additive=True,LocalSpace=True,MaxParticles=20,Rate=0,Burst=1,Lifetime=.045,LifetimeVariation=.2,Speed=0,SpeedVariation=.3,Direction=vec(0,0,-1),Spread=8,Gravity=vec(0,0,0),Drag=0,StartSize=.22,EndSize=.02,StartColor=color(1,.9,.6,1),EndColor=color(1,.25,.02,0));d.update(kw);return d
for role,factor in [('Pistol',1),('Rifle',.85),('Shotgun',1.5)]:
 path=f'Assets/VFX/{role} layered muzzle.bvfx'
 save(path,dict(Version=1,Name=role+' layered muzzle',Loop=False,Duration=.22,ParticleBudget=28,Layers=[
  layer('White hot core',StartSize=.25*factor,EndSize=.04,Lifetime=.035),
  layer('Directional flame',RenderMode='Stretched',Burst=4,Speed=3.5,Stretch=.08,Spread=12,StartSize=.11*factor,EndSize=.015,Lifetime=.055,StartColor=color(1,.55,.08,.85)),
  layer('Radial sparks',RenderMode='Stretched',Sprite='Spark',Burst=6,Speed=3,Spread=65,StartSize=.018,EndSize=.003,Lifetime=.09,Stretch=.04,StartColor=color(1,.7,.15,.7)),
  layer('Fading smoke',Additive=False,LocalSpace=False,Burst=3,Speed=.7,Spread=22,Lifetime=.22,Delay=.025,StartSize=.10*factor,EndSize=.32*factor,StartColor=color(.45,.48,.52,.10),EndColor=color(.5,.5,.5,0))]));asset(path,'VfxEffect')
for o in objects:
 d=component(o,'bytebard.karlsondemo.WeaponDefinition3D')
 if d is None:continue
 role='Pistol'if 'pistol'in o['name'].lower()else 'Shotgun'if any(n in o['name'].lower()for n in ['shotgun','spas','saps'])else 'Rifle'
 d.update(shotMode='Projectile',projectileSpeed=150 if role=='Rifle'else 120,projectileLifetime=.8,maxActiveProjectiles=96,showShotTrace=True,showCameraAimRay=False,traceWidth=.014,projectileTrailLength=1.3,muzzleFlashSize=1,muzzleLightIntensity=3.2 if role=='Shotgun'else 2.2,muzzleLightRange=3.5,muzzleFlashEffect=asset(f'Assets/VFX/{role} layered muzzle.bvfx','VfxEffect'))
 # Assign by actual weapon identity, preserving the user's slot ordering.
 mods=component(o,'EventModuleComponent')['modules'];mods[:]=[x for x in mods if not x['path'].startswith('Assets/Events/Audio/Weapon audio ')];attach(o,'Assets/Events/Audio/Weapon audio '+role+'.byteevents')
save(scene_path,s)
print('Updated scene, native movement tuning, afterimages, projectile weapons and layered muzzle VFX. Backup:',backup)
