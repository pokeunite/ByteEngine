"""Install mastered spider feedback without changing plugin/runtime code."""
import array, datetime, json, math, pathlib, shutil, subprocess, sys, uuid, wave

repo = pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0, str(repo / 'artifacts/audio-tools'))
import imageio_ffmpeg

project = pathlib.Path('C:/Users/codex/Documents/palclone')
scene = project / 'Scenes/Main.bytescene'
backup = project / 'SourceArt/Audio-Mix-Backups' / datetime.datetime.now().strftime('%Y%m%d-%H%M%S')
backup.mkdir(parents=True, exist_ok=True)
shutil.copy2(scene, backup / scene.name)
dest = project / 'Assets/Audio/Mining/Selected'
ffmpeg = imageio_ffmpeg.get_ffmpeg_exe()
outputs = [
 ('spider-hit-loud.wav', dest / 'body-hit-1.wav', 'highpass=f=110,acompressor=threshold=0.08:ratio=4:attack=2:release=65:makeup=3,volume=10,alimiter=limit=0.95:level=false'),
 ('spider-death-screech.wav', repo / 'artifacts/spider-death-source.mp3', 'silenceremove=start_periods=1:start_threshold=-45dB,highpass=f=160,acompressor=threshold=0.12:ratio=3:attack=3:release=90:makeup=2,volume=2,alimiter=limit=0.95:level=false,afade=t=out:st=1.0:d=0.15'),
]
refs = []
for name, source, filters in outputs:
 target = dest / name
 subprocess.run([ffmpeg, '-hide_banner', '-loglevel', 'error', '-y', '-i', str(source), '-af', filters, '-ac', '1', '-ar', '48000', '-c:a', 'pcm_s16le', str(target)], check=True)
 meta = pathlib.Path(str(target) + '.meta')
 guid = json.loads(meta.read_text())['guid'] if meta.exists() else str(uuid.uuid4())
 meta.write_text(json.dumps({'guid': guid, 'type': 'AudioClip'}))
 refs.append({'guid': guid, 'path': 'Assets/Audio/Mining/Selected/' + name})
 with wave.open(str(target)) as w:
  samples = array.array('h', w.readframes(w.getnframes()))
  print(name, 'seconds', round(len(samples)/48000, 3), 'peak', round(max(map(abs,samples))/32768,3), 'RMS', round(math.sqrt(sum(x*x for x in samples)/len(samples))/32768,3))
data = json.loads(scene.read_text(encoding='utf-8-sig'))
count = 0
for obj in data['gameObjects']:
 for component in obj['components']:
  if component['type'].endswith('MiningAudio3D'):
   component['properties'].update(spiderHitSound=refs[0], spiderDeathSound=refs[1], spiderHitVolume=1, spiderDeathVolume=1, creatureHearingDistance=16, creatureRolloff=.2)
   count += 1
if count != 1:
 raise RuntimeError(f'Expected exactly one audio manager, found {count}; scene not written')
scene.write_text(json.dumps(data, indent=2)+'\n', encoding='utf-8')
(project / 'SourceArt/spider-feedback-credits.txt').write_text('Spider death: Goblin Death.wav by spookymodem\nhttps://freesound.org/people/spookymodem/sounds/249813/\nCC0 1.0. Source: Freesound HQ preview. Edited: mono, trimmed onset, compression, gain and fade.\nSpider hit: SFX_Impact_Bullet_Hit_Body_1 by Marcus Dellicompagni (PoundSoundUK)\nhttps://freesound.org/people/PoundSoundUK/sounds/719198/\nCC BY 4.0. Existing credited source, further compressed and amplified.\n', encoding='utf-8')
print('Installed explicit spider hit/death bindings. Scene backup:', backup)
