"""Authored placement plan matching the reference's U-shaped enclosure and open forward valley."""
import json,math,random
from pathlib import Path
out=Path('Docs/Concepts/AlienMining/RockKit');models={a['id']:a for a in json.loads(Path('artifacts/mining-rock-kit/models.json').read_text())['assets']};rng=random.Random(271026);items=[]
def add(id,x,z,yaw=0,scale=(1,1,1),collision='none',group='Details',name=None,y=-.025):
 items.append({'id':id,'name':name or id+' '+group,'x':round(x,3),'y':round(y,3),'z':round(z,3),'yaw':round(yaw,3),'scale':[round(v,4) for v in scale],'collision':collision,'group':group})
def wall(id,x,z,yaw,height,group):
 b=models[id];width=b['max'][0]-b['min'][0];depth=b['max'][1]-b['min'][1];h=b['max'][2]-b['min'][2]
 add(id,x,z,yaw,(11.8/width,height/h,5.8/depth),'wall',group)
for i,x in enumerate(range(-29,53,9)):
 wall('R13' if i in (1,5,8) else 'R01',x,28+math.sin(i*.8)*.65,180+rng.uniform(-3,3),rng.uniform(12,16),'Rear boundary')
for side,x,angle in [('Left',-30,90),('Right',50,-90)]:
 for i in range(18):
  z=19-i*9
  id=('R13' if i%5==1 else ('R03' if side=='Right' and i%3==0 else ('R02' if i%4==3 else 'R01')))
  height=rng.uniform(11,15) if side=='Left' else rng.uniform(7.5,12)
  wall(id,x+math.sin(i*.6)*.7,z,angle+rng.uniform(-2.5,2.5),height,side+' boundary')
for x,z,yaw in [(-29,24,45),(49,24,-45)]:add('R04',x,z,yaw,(1.05,1.15,1),'parts','Boundary corners')
# Deep, overlapping upper strata break the fence-like silhouette of a single row.
for side,x,angle in [('Left',-36,90),('Right',56,-90)]:
 for i in range(17):
  z=23-i*9.4
  wall('R03' if i%3==0 else 'R01',x+rng.uniform(-1,1),z,angle+rng.uniform(-9,9),rng.uniform(19,28) if side=='Left' else rng.uniform(14,23),side+' upper strata')
  if i%2==0:
   b=models['R02'];wall('R02',x+(9 if side=='Left' else -9),z-2,angle+rng.uniform(-12,12),rng.uniform(3.5,6),side+' foothills')
for i,x in enumerate(range(-34,61,10)):
 wall('R03' if i%3==0 else 'R01',x,35+rng.uniform(-1,1),180+rng.uniform(-8,8),rng.uniform(19,27),'Rear upper strata')
for i in range(18):
 add('D02',-25+i*4.1,24,rng.uniform(0,360),(2.1,)*3,group='Rear foliage')
for side,x in [('Left',-28),('Right',48)]:
 for i in range(18):
  add('D02',x,19-i*8.8,rng.uniform(0,360),(2.4,)*3,group='Cliff ledge foliage',y=rng.uniform(1.5,3.2))
for i,(id,x,z,scale) in enumerate([('R09',26,-17,1.5),('R14',41,-4,1.25),('R10',-15,-38,1.15),('R09',26,-44,1.25),('R08',4,-58,1.25),('R10',36,-67,1.35),('R09',-18,-78,1.35),('R08',18,-92,1.2),('R14',38,-103,1.1),('R10',-14,-115,1),('R09',19,-126,1)]):
 add(id,x,z,rng.uniform(-130,130),(scale,scale,scale),'parts','Arena cover',id+' cover '+str(i+1))
 # Keep decorative plants near cover, leaving firing and navigation routes clear.
 for j in range(2):add('D02',x+rng.choice([-1,1])*rng.uniform(1.5,2.6),z+rng.uniform(-1.4,1.4),rng.uniform(0,360),(1.2,1.2,1.2),group='Cover foliage')
for side,x in [('Left',-26.4),('Right',46.4)]:
 for i in range(28):
  z=24-i*5.5
  add('D02',x+rng.uniform(-.5,.5),z,rng.uniform(0,360),(rng.uniform(1.5,2.4),)*3,group='Wall foliage')
  if i%3==0:add('D01',x+rng.uniform(-.35,.35),z-1.5,rng.uniform(0,360),(rng.uniform(.7,1.4),)*3,group='Wall crystal inserts')
  if i%2==0:add('R11',x+rng.uniform(-.7,.7),z-2,rng.uniform(0,360),(rng.uniform(.6,1.4),)*3,group='Wall rubble')
for i in range(26):
 x=rng.uniform(-22,42);z=rng.uniform(-132,-14)
 add('R12' if i%3 else 'R11',x,z,rng.uniform(0,360),(rng.uniform(.6,1.25),)*3,group='Ground scatter')
for i,(x,z,id,s) in enumerate([(-45,-157,'R05',1.7),(-12,-176,'R05',1.5),(38,-166,'R05',1.9),(69,-181,'R06',2.1),(-55,-193,'R06',2.4),(10,-222,'R06',2.1),(-10,-188,'R07',1.6),(-65,-94,'R06',2.5),(80,-112,'R05',1.9)]):add(id,x,z,rng.uniform(-30,30),(s,s,s),group='Distant landforms')
plan={'name':'Enclosed alien mining canyon','floor':{'centre':[10,-.15,-54],'size':[84,.3,168]},'navigation':{'origin':[-32,0,-138],'cell':.75,'width':112,'height':224},'campMinimum':[-25,-30],'campMaximum':[45,24],'spawnPoints':[[-20,0,-114],[-5,0,-124],[10,0,-111],[26,0,-122],[40,0,-116]],'items':items}
(out/'level-layout.json').write_text(json.dumps(plan,indent=2));print(len(items),'authored instances; 84 x 168 metre floor; 5 far-front entrances.')
