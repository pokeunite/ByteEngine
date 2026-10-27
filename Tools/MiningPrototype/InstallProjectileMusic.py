import json,shutil,uuid,datetime,subprocess,wave,array
from pathlib import Path
repo=Path(__file__).resolve().parents[2]
root=Path('C:/Users/codex/Documents/palclone')
engine=Path('C:/Users/codex/ByteEngine')
# Install only when the editor is closed; the separately exported running player is untouched.
check=subprocess.run(['powershell','-NoProfile','-Command',"if(Get-Process ByteEngine -ErrorAction SilentlyContinue){exit 1}"],capture_output=True)
if check.returncode: raise RuntimeError('Save and close ByteEngine before installation')
backup=root/'.byteengine/Backups'/('projectiles-music-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'))
backup.mkdir(parents=True)
scene=root/'Scenes/Main.bytescene';shutil.copy2(scene,backup/'Main.bytescene')
asset='Assets/Audio/Mining/Music/PYNCHON.wav';dest=root/asset;dest.parent.mkdir(parents=True,exist_ok=True)
# Transparent gain only: raise the quiet source without compression or clipping.
with wave.open(str(repo/'artifacts/mining-music/PYNCHON.wav')) as w:
 params=w.getparams();samples=array.array('h',w.readframes(w.getnframes()))
gain=.8*32767/max(abs(x) for x in samples)
samples=array.array('h',(round(x*gain) for x in samples))
with wave.open(str(dest),'wb') as w:w.setparams(params);w.writeframes(samples.tobytes())
meta=Path(str(dest)+'.meta');guid=json.loads(meta.read_text())['guid'] if meta.exists() else str(uuid.uuid4());meta.write_text(json.dumps({'guid':guid,'type':'AudioClip'}))
data=json.loads(scene.read_text(encoding='utf-8-sig'))
for obj in data['gameObjects']:
 for comp in obj.get('components',[]):
  props=comp.get('properties',{})
  if comp['type'].endswith('.MiningAudio3D'):props.update(campMusic={'guid':guid,'path':asset},campMusicVolume=.38,musicFadeSeconds=1.5,musicEnabled=True)
  if comp['type'].endswith('.MiningEffects3D'):props.update(mineExplosionScale=1.8,mortarExplosionScale=2.3)
scene.write_text(json.dumps(data,indent=2,ensure_ascii=False),encoding='utf-8')
targets=[repo/'PluginLibrary/KarlsonDemo.byteplugin',root/'Plugins/KarlsonDemo.byteplugin',root/'PluginLibrary/KarlsonDemo.byteplugin',engine/'PluginLibrary/KarlsonDemo.byteplugin',engine/'Dist/ByteEngine/PluginLibrary/KarlsonDemo.byteplugin',engine/'Editor/ByteEngine.Editor/bin/Debug/net9.0-windows/win-x64/PluginLibrary/KarlsonDemo.byteplugin']
for i,target in enumerate(targets):
 target.parent.mkdir(parents=True,exist_ok=True)
 if target.exists():shutil.copy2(target,backup/f'Plugin-{i}.byteplugin')
 shutil.copy2(repo/'artifacts/plugins/KarlsonDemo.byteplugin',target)
source=repo/'Plugins/KarlsonDemo'
for file in source.rglob('*'):
 if file.is_file() and not {'bin','obj'}.intersection(file.relative_to(source).parts):
  target=root/'PluginSource/KarlsonDemo'/file.relative_to(source);target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(file,target)
sourceart=root/'SourceArt/Camp Music';sourceart.mkdir(parents=True,exist_ok=True)
shutil.copy2(repo/'artifacts/mining-music/pynchon.mp3',sourceart/'pynchon-original.mp3')
(sourceart/'SOURCES.json').write_text(json.dumps({'title':'PYNCHON','author':'James Gargette (cinameng)','source':'https://opengameart.org/content/pynchon','license':'CC0','changes':'Converted MP3 to stereo 48 kHz PCM WAV; linear gain to 0.8 peak. Source retained.','gameVolume':.38},indent=2))
print('Installed version 3.40.0, backed up scene and packages:',backup)
