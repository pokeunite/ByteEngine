"""Prepare genuine CC0 firearm recordings into trimmed, level-matched native WAV clips."""
import pathlib,wave,array,math,json,uuid,shutil,datetime
ROOT=pathlib.Path(__file__).resolve().parents[2];P=pathlib.Path('C:/Users/codex/Documents/ultra violence')
source=ROOT/'artifacts/sfx-weapons/recorded/Prepared SFX Library';dest=P/'Assets/Audio/SFX/Firearms';dest.mkdir(parents=True,exist_ok=True)
for name,path,limit in [('Pistol shot','1911/A_42P.wav',.65),('Rifle shot','AK-47/C_28P.wav',.38),('Shotgun blast','Mossberg/N_30P.wav',.85)]:
 with wave.open(str(source/path))as w:
  raw=w.readframes(w.getnframes());width=w.getsampwidth();channels=w.getnchannels();rate=w.getframerate()
 samples=[sum(int.from_bytes(raw[n+c*width:n+(c+1)*width],'little',signed=True)for c in range(channels))/channels for n in range(0,len(raw),width*channels)]
 peak=max(abs(v)for v in samples);start=max(0,next(i for i,v in enumerate(samples)if abs(v)>peak*.12)-int(.002*rate));end=min(len(samples),start+int(limit*rate));cut=samples[start:end]
 # Real recording already supplies the transient/body; only remove DC, trim leading silence and fade the tail.
 dc=sum(cut)/len(cut);cut=[v-dc for v in cut];gain=27500/max(abs(v)for v in cut)
 # 96k to 48k by pair averaging (these are 96k recordings), preserving mono compatibility.
 out=[]
 for i in range(0,len(cut)-1,2):
  fade=min(1,(len(cut)-i)/(rate*.035));out.append(int((cut[i]+cut[i+1])*.5*gain*fade))
 with wave.open(str(dest/(name+'.wav')),'wb')as w:w.setparams((1,2,rate//2,len(out),'NONE','not compressed'));w.writeframes(array.array('h',out).tobytes())
 print(name,'onset',round(start/rate,3),'duration',len(out)/(rate//2),'peak',max(abs(v)for v in out))
license_text='''Recorded firearm sound sources — CC0 1.0 public domain dedication
The Free Firearm Sound Library: Ben Jaszczak, Brian Nelson, Kevin Heras, Matthew Nanney.
https://opengameart.org/content/the-free-firearm-sound-library
https://creativecommons.org/publicdomain/zero/1.0/
Selected near-distance recordings: 1911 A_42P, AK-47 C_28P, Mossberg N_30P.
Changes: first gunshot extracted, stereo averaged to mono, 96kHz to 48kHz, DC removal, level matching, 35ms tail fade.
No synthetic gunshot layers remain in the active sound sources.
'''
(dest/'License and sources.txt').write_text(license_text,encoding='utf-8')
scene_path=P/'Scenes/Combat Prototype.bytescene';scene=json.loads(scene_path.read_text(encoding='utf-8-sig'));backup=P/'.byteengine/Backups'/('Before recorded gun audio '+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True);shutil.copy2(scene_path,backup/scene_path.name)
for o in scene['gameObjects']:
 role={'SFX Pistol':'Pistol shot','SFX Rifle':'Rifle shot','SFX Shotgun':'Shotgun blast'}.get(o['name'])
 if role is None:continue
 path='Assets/Audio/SFX/Firearms/'+role+'.wav';meta=P/(path+'.meta')
 if not meta.exists():meta.write_text(json.dumps({'guid':str(uuid.uuid5(uuid.NAMESPACE_URL,path)),'type':'AudioClip'}),encoding='utf-8')
 for c in o['components']:
  if c['type']=='AudioSource3D':c['properties'].update(clip={'guid':json.loads(meta.read_text(encoding='utf-8-sig'))['guid'],'path':path},pitch=1,volume=.7 if role=='Rifle shot'else .82 if role=='Shotgun blast'else .76)
scene_path.write_text(json.dumps(scene,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
