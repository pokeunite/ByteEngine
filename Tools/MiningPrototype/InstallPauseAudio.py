import json,shutil,uuid,datetime,subprocess
from pathlib import Path
repo=Path(__file__).resolve().parents[2];root=Path('C:/Users/codex/Documents/palclone');engine=Path('C:/Users/codex/ByteEngine')
check=subprocess.run(['powershell','-NoProfile','-Command',"if(Get-Process ByteEngine -ErrorAction SilentlyContinue){exit 1}"],capture_output=True)
if check.returncode:raise RuntimeError('Save and close ByteEngine first')
backup=root/'.byteengine/Backups'/('pause-audio-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True)
scene=root/'Scenes/Main.bytescene';shutil.copy2(scene,backup/'Main.bytescene');data=json.loads(scene.read_text(encoding='utf-8-sig'))
def asset(file):
 dest=root/'Assets/Audio/Mining/Player'/file;dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(repo/'artifacts/mining-pause-audio'/file,dest)
 meta=Path(str(dest)+'.meta');guid=json.loads(meta.read_text())['guid'] if meta.exists() else str(uuid.uuid4());meta.write_text(json.dumps({'guid':guid,'type':'AudioClip'}));return {'guid':guid,'path':dest.relative_to(root).as_posix()}
rail=asset('RailJet.wav');steps=[asset(f'sand-step-{i+1}.wav') for i in range(4)]
font={}
for obj in data['gameObjects']:
 for c in obj.get('components',[]):
  if c['type'].endswith('.MiningBusinessUi3D'):font={k:v for k,v in c['properties'].items() if k in ['headingFont','bodyFont']}
for obj in data['gameObjects']:
 for c in list(obj.get('components',[])):
  if c['type'].endswith('.MiningAudio3D'):
   p=c['properties'];p.update(battleMusic=rail,battleMusicVolume=.3,ammoPickupSound={'guid':json.loads((root/'Assets/Audio/Weapons/First Hunt/pistol-reload.wav.meta').read_text())['guid'],'path':'Assets/Audio/Weapons/First Hunt/pistol-reload.wav'},ammoPickupPitch=1.15,ammoPickupVolume=.5,footstepVolume=.4,refineryVolumeMultiplier=4,refineryConveyorVolume=.58,refineryOreVolume=.8)
   for i,a in enumerate(steps):p['footstep'+str(i+1)]=a
   credit='RailJet by Deceased Superior Technician (DST)\nhttps://opengameart.org/content/railjet-long-seamless-loop\nhttps://www.nosoapradio.us\nCC BY 3.0 - https://creativecommons.org/licenses/by/3.0/\nConverted OGG to stereo PCM WAV.\nSand footsteps: Fantozzi, CC0, https://opengameart.org/content/fantozzis-footsteps-grasssand-stone'
   if 'RailJet by' not in p.get('requiredAttribution',''):p['requiredAttribution']=p.get('requiredAttribution','')+'\n\n'+credit
   if not any(x['type'].endswith('.MiningPauseMenu3D') for x in obj['components']):obj['components'].append({'type':'bytebard.karlsondemo.MiningPauseMenu3D','enabled':True,'properties':font|{'persistSettings':True,'saveSlot':'mining-player-settings.json','mouseSensitivity':.12}})
scene.write_text(json.dumps(data,indent=2,ensure_ascii=False),encoding='utf-8')
targets=[repo/'PluginLibrary/KarlsonDemo.byteplugin',root/'Plugins/KarlsonDemo.byteplugin',root/'PluginLibrary/KarlsonDemo.byteplugin',engine/'PluginLibrary/KarlsonDemo.byteplugin',engine/'Dist/ByteEngine/PluginLibrary/KarlsonDemo.byteplugin',engine/'Editor/ByteEngine.Editor/bin/Debug/net9.0-windows/win-x64/PluginLibrary/KarlsonDemo.byteplugin']
for i,target in enumerate(targets):
 target.parent.mkdir(parents=True,exist_ok=True)
 if target.exists():shutil.copy2(target,backup/f'Plugin-{i}.byteplugin')
 shutil.copy2(repo/'artifacts/plugins/KarlsonDemo.byteplugin',target)
for folder in [engine/'Dist/ByteEngine',engine/'Editor/ByteEngine.Editor/bin/Debug/net9.0-windows/win-x64',engine/'Editor/ByteEngine.Editor/bin/Release/net9.0-windows/win-x64']:
 if (folder/'ByteEngine.Core.dll').exists():
  shutil.copy2(folder/'ByteEngine.Core.dll',backup/('Core-'+str(abs(hash(str(folder))))+'.dll'));shutil.copy2(repo/'Engine/ByteEngine.Core/bin/Release/net9.0/ByteEngine.Core.dll',folder/'ByteEngine.Core.dll')
source=repo/'Plugins/KarlsonDemo'
for f in source.rglob('*'):
 if f.is_file() and not {'bin','obj'}.intersection(f.relative_to(source).parts):
  dest=root/'PluginSource/KarlsonDemo'/f.relative_to(source);dest.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(f,dest)
art=root/'SourceArt/Pause Audio';art.mkdir(parents=True,exist_ok=True)
for name in ['RailJet.ogg','footsteps.7z']:shutil.copy2(repo/'artifacts/mining-pause-audio'/name,art/name)
(art/'SOURCES.json').write_text(json.dumps({'music':{'title':'RailJet','author':'Deceased Superior Technician (DST)','source':'https://opengameart.org/content/railjet-long-seamless-loop','license':'CC BY 3.0','authorLink':'https://www.nosoapradio.us','changes':'Converted OGG to stereo 48 kHz PCM WAV'},'footsteps':{'author':'Fantozzi','source':'https://opengameart.org/content/fantozzis-footsteps-grasssand-stone','license':'CC0','changes':'Four alternating sand steps converted to mono 48 kHz PCM WAV'}},indent=2))
print('Installed 3.41.0 and updated editor input/pause support. Backup:',backup)
