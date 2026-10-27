import subprocess,json,wave,array,math,hashlib
from pathlib import Path
ff=str(Path('artifacts/video-decoder/imageio_ffmpeg/binaries/ffmpeg-win-x86_64-v7.1.exe').resolve());out=Path('artifacts/mining-audio');out.mkdir(parents=True,exist_ok=True)
names={'W1':'pistol-shot','W6':'dry-trigger','W8':'ammo-clink','C4':'alien-growl','M2':'refinery-loop','M1':'drill-loop','M4':'press-loop','F1':'metal-hit','F3':'body-hit-source','F4':'player-hurt','U1':'ui-confirm','U2':'ui-notification','U3':'ui-alert','U4':'ui-error','U5':'credits','A3':'critical-alarm','D1':'explosion','D2':'cannon'}
records=json.loads(Path('artifacts/selected-freesound/manifest.json').read_text());manifest=[]
def save(name,a):
 peak=max(map(abs,a),default=1);gain=min(3,23197/max(peak,1));a=array.array('h',(max(-32767,min(32767,round(v*gain))) for v in a))
 p=out/(name+'.wav')
 with wave.open(str(p),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(48000);w.writeframes(a.tobytes())
 return str(p)
for r in records:
 pcm=subprocess.check_output([ff,'-i',r['source_file'],'-f','s16le','-ac','1','-ar','48000','-hide_banner','-loglevel','error','pipe:1']);a=array.array('h');a.frombytes(pcm);name=names[r['id']]
 if r['id']=='F3':
  # Split discrete impacts on silence, avoiding an audible six-second sequence per hit.
  block=480;energies=[max(map(abs,a[i:i+block]),default=0) for i in range(0,len(a),block)];threshold=max(350,max(energies)*.035);starts=[];quiet=20
  for i,e in enumerate(energies):
   if e>threshold:
    if quiet>=10:starts.append(max(0,i*block-480))
    quiet=0
   else:quiet+=1
  for i,start in enumerate(starts[:3]):save('body-hit-'+str(i+1),a[start:min(len(a),start+int(.42*48000))])
  if not starts:raise RuntimeError('No body impact found')
  print('Body hit segments:',[round(x/48000,2) for x in starts],flush=True)
 if name.endswith('-loop'):
  n=960
  if len(a)>n*4:
   tail=array.array('h',(round(a[-n+i]*(1-i/(n-1))+a[i]*(i/(n-1))) for i in range(n)));a=a[n:-n]+tail
 else:
  threshold=max(90,max(map(abs,a),default=1)*.003);active=[i for i,v in enumerate(a) if abs(v)>threshold]
  if active:a=a[max(0,active[0]-240):min(len(a),active[-1]+2400)]
 file=save(name,a);r.update(engine_file=file,mono=True,sample_rate=48000,duration_seconds=len(a)/48000,edit='Decoded HQ preview to mono PCM16; conservative peak normalisation. Loop seam blended 20ms; one-shot outer silence trimmed.');manifest.append(r);print(name,round(len(a)/48000,2),flush=True)
(out/'SOURCES.json').write_text(json.dumps(manifest,indent=2),encoding='utf8')
lines=['# Freesound audio credits and source record','','All selected sounds are CC0 except the following:','','**SFX_Impact_Bullet_Hit_Body_1** by **Marcus Dellicompagni (PoundSoundUK)** — https://freesound.org/people/PoundSoundUK/sounds/719198/ — Creative Commons Attribution 4.0: https://creativecommons.org/licenses/by/4.0/ . Modified: decoded from Freesound HQ MP3 preview, converted to mono PCM, cut into short impact variations and peak-normalised.','','The other authors are credited voluntarily below. HQ preview files were used because Freesound original downloads require an authenticated account. Converting previews to WAV does not restore lossless source quality. No reload or footstep recording was replaced.','','| Engine asset | Original | Author | Licence |','|---|---|---|---|']
for r in manifest:lines.append(f"| {Path(r['engine_file']).name} | [{r['title']}]({r['url']}) | {r['author']} | {r['license']} |")
(out/'ATTRIBUTION.md').write_text('\n'.join(lines),encoding='utf8')
