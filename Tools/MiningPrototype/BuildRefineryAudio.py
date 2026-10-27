"""Layered refinery mix from the project's existing licensed CC0 recordings; PCM16 mono.
No tonal UI chimes. Source recordings and edit provenance are retained in the manifest.
"""
import array,wave,math,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];SOURCE=ROOT/'artifacts/mining-audio';OUT=ROOT/'artifacts/refinery-audio';OUT.mkdir(parents=True,exist_ok=True);RATE=48000
def read(name):
 with wave.open(str(SOURCE/(name+'.wav'))) as w:
  assert w.getsampwidth()==2 and w.getnchannels()==1 and w.getframerate()==RATE
  return [x/32768 for x in array.array('h',w.readframes(w.getnframes()))]
def lowpass(v,hz):
 alpha=1-math.exp(-math.tau*hz/RATE);y=0;out=[]
 for x in v:y+=alpha*(x-y);out.append(y)
 return out
def pitch(v,speed):
 n=int(len(v)/speed);out=[]
 for i in range(n):
  p=i*speed;j=min(int(p),len(v)-2);t=p-j;out.append(v[j]*(1-t)+v[j+1]*t)
 return out
def trim(v,seconds):
 peak=max(abs(x) for x in v);first=next((i for i,x in enumerate(v) if abs(x)>peak*.04),0)
 return v[max(0,first-96):max(0,first-96)+int(seconds*RATE)]
def mix(*layers):
 out=[0.0]*max(int(offset*RATE)+len(v) for v,offset,gain in layers)
 for v,offset,gain in layers:
  start=int(offset*RATE)
  for i,x in enumerate(v):out[start+i]+=x*gain
 return out
stats=[]
def write(name,v,rms,loop=False):
 mean=sum(v)/len(v);v=[x-mean for x in v]
 if loop:
  # Circular smoothing of the seam; preserve phase and length.
  n=int(.045*RATE);difference=v[-1]-v[0]
  for i in range(n):
   t=i/max(1,n-1);v[i]+=difference*.5*(1-t);v[-1-i]-=difference*.5*(1-t)
 else:
  n=min(int(.003*RATE),len(v)//2);tail=min(int(.055*RATE),len(v)//2)
  for i in range(n):v[i]*=i/n
  for i in range(tail):v[-1-i]*=i/tail
 power=math.sqrt(sum(x*x for x in v)/len(v));gain=min(rms/max(power,1e-8),.82/max(abs(x) for x in v));v=[x*gain for x in v]
 with wave.open(str(OUT/(name+'.wav')),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(array.array('h',[round(x*32767) for x in v]).tobytes())
 stats.append({'asset':name+'.wav','seconds':len(v)/RATE,'peak':max(abs(x) for x in v),'rms':math.sqrt(sum(x*x for x in v)/len(v)),'loop':loop})
 return v
machine=read('refinery-loop');machine=lowpass(machine,4800);machine=write('refinery-machine',machine,.11,True)
belt=read('press-loop');belt=lowpass(pitch(belt,.9),3200);belt=write('refinery-conveyor',belt,.09,True)
metal=trim(read('metal-hit'),.34);roll=trim(read('ammo-clink'),.26)
impacts=[]
for i,(speed,delay) in enumerate([(.72,.075),(.84,.048),(.65,.10)],1):
 v=mix((lowpass(pitch(metal,speed),4000),0,1),(lowpass(pitch(roll,.8+ i*.06),3000),delay,.65))
 impacts.append(write('refinery-ore-'+str(i),v,.11))
click=trim(read('dry-trigger'),.065)
opened=write('refinery-menu-open',mix((lowpass(pitch(click,1.14),3500),0,1),(lowpass(pitch(click,.85),1800),.043,.26)),.08)
closed=write('menu-close',lowpass(pitch(click,.81),2700),.075)
demo=[0.0]*(RATE*8)
def add(v,t,gain):
 for i,x in enumerate(v[:max(0,len(demo)-int(t*RATE))]):demo[int(t*RATE)+i]+=x*gain
add(opened,.15,.3)
add(machine[:RATE*5],1,.54);add(belt[:RATE*6],1,.42)
for i,t in enumerate([3.8,4.03,4.27,4.52,4.8,5.1]):add(impacts[i%3],t,.65)
add(closed,7.35,.3);write('refinery-mix-preview',demo,.065)
sources=json.loads((SOURCE/'SOURCES.json').read_text())
selected=[s for s in sources if Path(s.get('engine_file','').replace('\\','/')).stem in ['refinery-loop','press-loop','metal-hit','ammo-clink','dry-trigger']]
assert all(s['license']=='CC0' for s in selected)
(OUT/'SOURCES.json').write_text(json.dumps({'sources':selected,'edits':'Low-pass filtering, timing edits, resampling, layer mixing, seam smoothing, DC removal and gain staging. No new downloads. Original project recordings preserved.','assets':stats},indent=2))
print(json.dumps(stats,indent=2))
