"""Short mechanical press strokes and delivery layers from existing CC0 project recordings."""
import wave,array,math,json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2];SOURCE=ROOT/'artifacts/mining-audio';OUT=ROOT/'artifacts/press-audio';OUT.mkdir(parents=True,exist_ok=True);RATE=48000
def read(name):
 with wave.open(str(SOURCE/(name+'.wav'))) as w:
  assert(w.getnchannels(),w.getsampwidth(),w.getframerate())==(1,2,RATE)
  return [v/32768 for v in array.array('h',w.readframes(w.getnframes()))]
def low(v,hz):
 alpha=1-math.exp(-math.tau*hz/RATE);y=0;out=[]
 for x in v:y+=alpha*(x-y);out.append(y)
 return out
def pitch(v,speed):
 out=[]
 for i in range(int(len(v)/speed)):
  p=i*speed;j=min(int(p),len(v)-2);t=p-j;out.append(v[j]*(1-t)+v[j+1]*t)
 return out
def trim(v,seconds):
 level=max(abs(x) for x in v)*.035;first=next((i for i,x in enumerate(v) if abs(x)>level),0);start=max(0,first-96);return v[start:start+int(seconds*RATE)]
def mix(*layers):
 out=[0.0]*max(len(v)+int(t*RATE) for v,t,g in layers)
 for v,t,g in layers:
  start=int(t*RATE)
  for i,x in enumerate(v):out[start+i]+=x*g
 return out
stats=[]
def write(name,v,rms,loop=False):
 mean=sum(v)/len(v);v=[x-mean for x in v]
 if loop:
  n=min(int(.04*RATE),len(v)//2);difference=v[-1]-v[0]
  for i in range(n):g=(1-i/(n-1))*.5;v[i]+=difference*g;v[-1-i]-=difference*g
 else:
  for i in range(min(96,len(v)//2)):v[i]*=i/96
  n=min(int(.035*RATE),len(v)//2)
  for i in range(n):v[-1-i]*=i/n
 power=math.sqrt(sum(x*x for x in v)/len(v));gain=min(rms/max(power,1e-8),.8/max(abs(x) for x in v));v=[x*gain for x in v]
 with wave.open(str(OUT/(name+'.wav')),'wb') as w:w.setnchannels(1);w.setsampwidth(2);w.setframerate(RATE);w.writeframes(array.array('h',[round(x*32767) for x in v]).tobytes())
 stats.append({'asset':name+'.wav','seconds':len(v)/RATE,'peak':max(abs(x) for x in v),'rms':math.sqrt(sum(x*x for x in v)/len(v)),'loop':loop});return v
motor=write('press-motor',low(pitch(read('drill-loop'),1.12),2400),.10,True)
belt=write('press-conveyor',low(pitch(read('press-loop'),1.14),3300),.085,True)
click=trim(read('dry-trigger'),.075);hit=trim(read('metal-hit'),.18);roll=trim(read('ammo-clink'),.28)
stroke=write('press-stroke',mix((low(pitch(hit,1.12),2700),0,.75),(low(click,4000),.018,.65),(low(pitch(click,.83),2300),.115,.26)),.095)
impacts=[]
for i,speed in enumerate([.94,1.12,.83],1):
 impacts.append(write('press-bucket-'+str(i),mix((low(pitch(roll,speed),5000),0,1),(low(pitch(click,1.3),4000),.023,.16)),.09))
opened=write('press-menu-open',mix((low(pitch(click,1.04),3200),0,1),(low(pitch(click,1.22),2100),.032,.18)),.075)
demo=[0.0]*(RATE*7)
def add(v,t,g):
 start=int(t*RATE)
 for i,x in enumerate(v[:len(demo)-start]):demo[start+i]+=x*g
add(opened,.12,.28);add(motor[:RATE*5],.7,.54);add(belt[:int(RATE*5.6)],.7,.48)
for t in [1.2,2.2,3.2,4.2,5.2]:add(stroke,t,.48)
for i,t in enumerate([2.8,3.45,4.1,4.75,5.4,6.05]):add(impacts[i%3],t,.58)
with wave.open(str(ROOT/'artifacts/refinery-audio/menu-close.wav')) as w:closed=[v/32768 for v in array.array('h',w.readframes(w.getnframes()))]
add(closed,6.75,.28)
write('press-mix-preview',demo,.065)
sources=json.loads((SOURCE/'SOURCES.json').read_text());selected=[s for s in sources if Path(s.get('engine_file','').replace('\\','/')).stem in ['drill-loop','press-loop','metal-hit','ammo-clink','dry-trigger']];assert all(s['license']=='CC0' for s in selected)
(OUT/'SOURCES.json').write_text(json.dumps({'sources':selected,'edits':'Motor/belt filtering and resampling; stroke and small-metal impact edits; click layering; seam smoothing; gain staging. Existing originals preserved.','assets':stats},indent=2))
print(json.dumps(stats,indent=2))
