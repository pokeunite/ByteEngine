import json,wave
from pathlib import Path
import numpy as np
ROOT=Path(__file__).resolve().parents[2];SOURCE=ROOT/'artifacts/mining-audio';OUT=ROOT/'artifacts/mining-effects-pass/audio';OUT.mkdir(parents=True,exist_ok=True);RATE=48000
def read(path):
 with wave.open(str(path)) as w:
  assert(w.getnchannels(),w.getsampwidth(),w.getframerate())==(1,2,RATE)
  return np.frombuffer(w.readframes(w.getnframes()),dtype='<i2').astype(float)/32768
def source(name):return read(SOURCE/(name+'.wav'))
def pitch(x,speed):return np.interp(np.arange(0,len(x)-1,speed),np.arange(len(x)),x)
def low(x,hz):
 a=1-np.exp(-np.pi*2*hz/RATE);y=0;out=np.empty(len(x))
 for i,v in enumerate(x):y+=a*(v-y);out[i]=y
 return out
def trim(x,seconds):
 audible=np.flatnonzero(np.abs(x)>np.max(np.abs(x))*.025);start=max(0,int(audible[0])-96) if len(audible) else 0;return x[start:start+int(seconds*RATE)]
def mix(*parts):
 out=np.zeros(max(len(x)+int(t*RATE) for x,t,g in parts))
 for x,t,g in parts:start=int(t*RATE);out[start:start+len(x)]+=x*g
 return out
report=[]
def write(name,x,rms,loop=False):
 x=x-np.mean(x)
 if loop:
  n=min(int(.055*RATE),len(x)//3);d=x[-1]-x[0];ramp=np.linspace(.5,0,n);x[:n]+=d*ramp;x[-n:]-=d*ramp[::-1]
 else:
  n=min(100,len(x)//3);x[:n]*=np.linspace(0,1,n);n=min(int(.08*RATE),len(x)//3);x[-n:]*=np.linspace(1,0,n)
 # Gentle transient compression gives defence cues body instead of merely clipping peaks.
 x=np.tanh(x/max(float(np.sqrt(np.mean(x*x))),.001)*.35)
 x*=min(rms/max(float(np.sqrt(np.mean(x*x))),.001),.88/max(float(np.max(np.abs(x))),.001))
 with wave.open(str(OUT/(name+'.wav')),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(np.rint(x*32767).astype('<i2').tobytes())
 report.append({'asset':name+'.wav','seconds':len(x)/RATE,'rms':float(np.sqrt(np.mean(x*x))),'peak':float(np.max(np.abs(x))),'loop':loop});return x
drill=read(ROOT/'artifacts/mining-effects-pass/drill-recording.wav');drill=drill[int(.12*RATE):-int(.10*RATE)];drill=low(pitch(drill,.90),6000);bed=source('drill-loop');bed=np.resize(bed,len(drill));write('rock-drill-loop',drill+bed*.25,.20,True)
rifle=read(Path('C:/Users/codex/Documents/palclone/Assets/Audio/Weapons/First Hunt/rifle-shot.wav'));rifle=trim(rifle,.3);metal=trim(source('metal-hit'),.15)
write('sentry-shot',mix((low(pitch(rifle,.92),6500),0,1),(low(pitch(metal,.72),1500),0,.22)),.17)
write('repeater-shot',mix((pitch(rifle,1.12),0,1),(low(metal,2000),.015,.12)),.15)
write('defence-explosion',mix((low(trim(source('explosion'),1.4),7500),0,1),(low(pitch(trim(source('cannon'),.6),.8),900),.035,.3)),.21)
write('mortar-launch',low(trim(pitch(source('cannon'),.88),.8),4800),.19)
click=trim(source('dry-trigger'),.07)
write('terminal-open',mix((low(pitch(click,.95),2800),0,1),(low(pitch(click,1.12),1800),.07,.28)),.085)
write('terminal-select',low(pitch(click,1.25),2300),.065)
write('terminal-purchase',mix((low(pitch(click,1.12),3200),0,1),(low(pitch(click,.86),2200),.09,.65)),.09)
(OUT.parent/'SOURCES.json').write_text(json.dumps({'drill':{'title':'Pneumatic jackhammer #5','author':'Joseph SARDIN','license':'CC0','url':'https://bigsoundbank.com/pneumatic-jackhammer-5-s3129.html','download':'https://bigsoundbank.com/UPLOAD/mp3/3129.mp3','edits':'Mono WAV conversion, trimmed active section, pitch resampling, low-pass and motor-bed mixing, loop seam smoothing, gentle compression and gain staging.'},'explosionSprites':{'title':'2D Explosion Animations #2 | Frame by frame','author':'Sinestesia','license':'CC0','url':'https://opengameart.org/content/2d-explosion-animations-2-frame-by-frame','download':'https://opengameart.org/sites/default/files/free%20explosion%20animations%202.zip','sheets':'Half Sized/4.png and Half Sized/3.png, each 2048x2048, 8x8 frames. Unmodified.'},'otherSounds':'Derived from existing game recordings; original source manifests are retained in SourceAudio/Freesound Selected and Assets/Audio/Mining/Selected/SOURCES.json. Player gun recordings are not overwritten.','assets':report},indent=2))
print(json.dumps(report,indent=2))
