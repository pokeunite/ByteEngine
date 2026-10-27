import json,shutil,uuid,datetime,hashlib,subprocess,copy
from pathlib import Path
repo=Path(__file__).resolve().parents[2];root=Path('C:/Users/codex/Documents/palclone');engine=Path('C:/Users/codex/ByteEngine')
check=subprocess.run(['powershell','-NoProfile','-Command',"if(Get-Process ByteEngine -ErrorAction SilentlyContinue){exit 1}"],capture_output=True)
if check.returncode:raise RuntimeError('Save and close the editor first')
backup=root/'.byteengine/Backups'/('main-menu-pressure-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True)
scene=root/'Scenes/Main.bytescene';shutil.copy2(scene,backup/'Main.bytescene');data=json.loads(scene.read_text(encoding='utf-8-sig'))
def asset(src,relative,kind):
 dest=root/relative;dest.parent.mkdir(parents=True,exist_ok=True)
 if dest.exists():shutil.copy2(dest,backup/(dest.name+'.previous'))
 shutil.copy2(src,dest);meta=Path(str(dest)+'.meta');guid=json.loads(meta.read_text())['guid'] if meta.exists() else str(uuid.uuid4());meta.write_text(json.dumps({'guid':guid,'type':kind}));return {'guid':guid,'path':relative}
drill=asset(repo/'artifacts/mining-mainmenu-pass/cnc-drill-loop.wav','Assets/Audio/Mining/Effects Pass/cnc-drill-loop.wav','AudioClip')
texture=asset(repo/'artifacts/mining-mainmenu-pass/Unity-Explosion01-5x5.png','Assets/VFX/Mining/Textures/UnityLabs-Explosion01-light-5x5.png','Texture2D')
for obj in data['gameObjects']:
 for c in obj.get('components',[]):
  p=c.get('properties',{})
  if c['type'].endswith('.MiningAudio3D'):p.update(drillLoop=drill,campMusicVolume=.25)
  if c['type'].endswith('.MiningPauseMenu3D'):p.update(showMainMenuOnStart=True,companyTitle='DUNE COMPANY')
  if c['type'].endswith('.FpsGunLab3D'):p['allowWeaponDropping']=False
  if c['type'].endswith('.MiningOperation3D'):p.update(hunterMoveSpeed=6,raiderMoveSpeed=4.8,hunterWindup=.32,hunterRecovery=.27,hunterAttackCooldown=.62,raiderWindup=.5,raiderRecovery=.38,raiderAttackCooldown=.78,spawnInterval=.62)
  if c['type'].endswith('.MiningEffects3D'):p.update(explosionLaunchSpeed=8.5,explosionLift=4)
  if c['type']=='CombatFeedback3D':p['knockbackSpeed']=4.8
scene.write_text(json.dumps(data,indent=2,ensure_ascii=False),encoding='utf-8')
for event in (root/'Assets/Events').rglob('*.byteevents'):
 d=json.loads(event.read_text(encoding='utf-8-sig'));changed=False
 for rule in d.get('rules',[]):
  if any('drop' in str(action.get('id','')).lower() for action in rule.get('actions',[])):
   rule['enabled']=False;changed=True
 if changed:shutil.copy2(event,backup/event.name);event.write_text(json.dumps(d,indent=2,ensure_ascii=False),encoding='utf-8')
for name in ['Mine blast','Mortar impact']:
 path=root/'Assets/VFX/Mining'/(name+'.bvfx');shutil.copy2(path,backup/path.name);fx=json.loads(path.read_text());core=fx['Layers'][0]
 core.update(Name='Unity Labs CC0 fire and rolling smoke',Texture={'Guid':texture['guid'],'CachedProjectPath':texture['path'],'ProjectPath':texture['path'],'IsEmpty':False},FlipbookColumns=5,FlipbookRows=5,Lifetime=.85,StartSize=2.8 if name=='Mine blast' else 3.8,EndSize=3.6 if name=='Mine blast' else 4.6,Brightness=1.1,Additive=False,OpacityOverLife={'Keys':[{'Time':0,'Value':1},{'Time':.72,'Value':1},{'Time':1,'Value':0}]})
 for layer in fx['Layers'][1:]:
  if layer['Name']=='Trailing soot':layer.update(Burst=6,MaxParticles=12,Lifetime=1.1)
  if layer['Name']=='Hot chips':layer.update(Speed=7,Lifetime=.55)
  if layer['Name']=='Ground pressure ring':layer.update(Lifetime=.28,EndSize=7.5)
 if not any(l['Name']=='Initial blast flash' for l in fx['Layers']):
  flash=copy.deepcopy(core);flash.update(Name='Initial blast flash',Texture={'Guid':str(uuid.UUID(int=0)),'CachedProjectPath':'','ProjectPath':'','IsEmpty':True},FlipbookColumns=1,FlipbookRows=1,Sprite='Star',Lifetime=.07,StartSize=1.4,EndSize=3.4,Brightness=2,Additive=True,OpacityOverLife=None,StartColor={'X':1,'Y':.88,'Z':.55,'W':1},EndColor={'X':1,'Y':.6,'Z':.15,'W':0});fx['Layers'].append(flash)
 path.write_text(json.dumps(fx,indent=2))
# Match every plugin copy, without replacing the correctly paired editor/player core.
package=repo/'artifacts/plugins/KarlsonDemo.byteplugin'
targets=[repo/'PluginLibrary/KarlsonDemo.byteplugin',root/'Plugins/KarlsonDemo.byteplugin',root/'PluginLibrary/KarlsonDemo.byteplugin',engine/'PluginLibrary/KarlsonDemo.byteplugin',engine/'Dist/ByteEngine/PluginLibrary/KarlsonDemo.byteplugin',engine/'Editor/ByteEngine.Editor/bin/Debug/net9.0-windows/win-x64/PluginLibrary/KarlsonDemo.byteplugin']
for target in targets:target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(package,target)
source=repo/'Plugins/KarlsonDemo'
for file in source.rglob('*'):
 if file.is_file() and not {'bin','obj'}.intersection(file.relative_to(source).parts):
  target=root/'PluginSource/KarlsonDemo'/file.relative_to(source);target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(file,target)
sourceart=root/'SourceArt/Main Menu and Combat Pass';sourceart.mkdir(parents=True,exist_ok=True)
original=Path('C:/Users/codex/Downloads/765223__ryan_anceriz__cnc-machine.wav')
if original.exists():shutil.copy2(original,sourceart/'cnc-machine-original.wav')
else:shutil.copy2(repo/'artifacts/mining-mainmenu-pass/drill-original-converted.wav',sourceart/'cnc-machine-full-converted.wav')
shutil.copy2(repo/'artifacts/mining-mainmenu-pass/Unity-Explosion01.zip',sourceart/'Unity-Labs-Explosion01-light-original.zip')
shutil.copy2(repo/'Docs/Concepts/AlienMining/Pause-Settings-Yellow-Concept.png',sourceart/'Pause-Settings-Yellow-Concept.png')
(sourceart/'SOURCES.json').write_text(json.dumps({'drill':{'source':'User supplied 765223__ryan_anceriz__cnc-machine.wav','changes':'Mono 48 kHz WAV, sustained 8–36s section, overlap loop seam and gain adjustment'},'explosion':{'author':'Thomas Iche / Unity Labs Paris','license':'CC0','source':'https://unity.com/blog/engine-platform/free-vfx-image-sequences-flipbooks','atlas':'Explosion01-light, 25 frames, 5x5','changes':'TGA converted to PNG; original retained'}},indent=2))
# The familiar installed command delegates to the maintained source, rather than the old checkout.
script=engine/'Rebuild-ByteEngine-And-Refresh-Dist.ps1'
if script.exists():shutil.copy2(script,backup/'Rebuild-ByteEngine-And-Refresh-Dist.ps1.previous')
shutil.copy2(repo/'Rebuild-ByteEngine-And-Refresh-Dist.ps1',script)
(engine/'.byteengine-build-source.json').write_text(json.dumps({'SourceRoot':str(repo),'ProjectFile':str(root/'palclone.byteproject')},indent=2))
report=engine/'Dist/ByteEngine/build-identity.json'
if report.exists():d=json.loads(report.read_text(encoding='utf-8-sig'));d['PluginSHA256']=hashlib.sha256(package.read_bytes()).hexdigest().upper();report.write_text(json.dumps(d,indent=2))
print('Main menu, supplied drill audio, softer camp music, no-drop inventory and faster combat installed. Backup:',backup)
