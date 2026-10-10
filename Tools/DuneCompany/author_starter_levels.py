"""Author the two starter contract landscapes in the Documents game project.
Produces 16-bit heightmaps shared by terrain rendering/collision. Backups are
mandatory; no export or save-game mutation. Run with --apply to write the project.
"""
import argparse, copy, json, math, struct, zlib, uuid, shutil, datetime
from pathlib import Path
ROOT=Path("C:/Users/codex/Documents/DuneCompany")
SIZE=192
TOW=[(0,0,0),(0,18,.15),(12,35,1.7),(-10,52,.7),(0,70,.7)]
EASY=[(0,10,0),(-20,24,.2),(-30,45,.3),(-20,62,.7),(0,70,.7)]
WINCH=[(0,0,0),(-15,15,.5),(-20,33,1.5),(0,45,3),(12,59,4),(12,70,4)]
def smooth(t):
 t=max(0,min(1,t));return t*t*(3-2*t)
def route(x,z,points):
 best=(1e9,0)
 for a,b in zip(points,points[1:]):
  dx,dz=b[0]-a[0],b[1]-a[1];t=max(0,min(1,((x-a[0])*dx+(z-a[1])*dz)/(dx*dx+dz*dz)))
  dist=math.hypot(x-a[0]-dx*t,z-a[1]-dz*t)
  if dist<best[0]:best=(dist,a[2]+(b[2]-a[2])*smooth(t))
 return best
def height(x,z,winch):
 if winch:
  h=4*smooth((z-12)/42)+1.3*(.5+.5*math.sin(x*.09+z*.07))
  d,y=route(x,z,WINCH);h=y+(h-y)*smooth((d-5)/7)
  # Broad level apron; shallow bowl has a drivable south-facing exit.
  apron=1-smooth((math.hypot(x-12,z-64)-10)/6);h=h*(1-apron)+4*apron
  r=math.hypot(x-12,z-70);h-=1.7*(1-smooth(r/6.5))
 else:
  h=1.1+1.9*(.5+.5*math.sin(x*.13+z*.09))+1.1*(.5+.5*math.cos(z*.14-x*.04))
  d,y=route(x,z,TOW);e,ey=route(x,z,EASY)
  if e<d:d,y=e,ey
  h=y+(h-y)*smooth((d-5)/7)
  site=1-smooth((math.hypot(x,z-70)-9)/5);h=h*(1-site)+.7*site
  h-=.16*(1-smooth(math.hypot(x,z-70)/4))
 # Keep the garage/depot floor and launch area unchanged.
 h*=smooth((math.hypot(x,z)-9)/8)
 return h

def packing(x,z,winch):
 d,_=route(x,z,WINCH if winch else TOW)
 value=(1-smooth((d-3)/4))*(.7 if winch else .45)
 if winch:
  apron=1-smooth((math.hypot(x-12,z-61)-6)/3);value=max(value,apron)
  value*=smooth((math.hypot(x-12,z-70)-4)/3)
 else:
  e,_=route(x,z,EASY);value=max(value,(1-smooth((e-3)/4))*.85)
 return max(value,1-smooth((math.hypot(x,z)-7)/4))

def png(path,winch,mask=False):
 def chunk(k,v):return struct.pack('>I',len(v))+k+v+struct.pack('>I',zlib.crc32(k+v)&0xffffffff)
 raw=bytearray()
 for z in range(SIZE+1):
  raw.append(0)
  for x in range(SIZE+1):raw.extend(struct.pack('>H',round(max(0,min(1,packing(x-SIZE/2,z-SIZE/2,winch) if mask else (height(x-SIZE/2,z-SIZE/2,winch)+3)/16))*65535)))
 path.write_bytes(b'\x89PNG\r\n\x1a\n'+chunk(b'IHDR',struct.pack('>IIBBBBB',SIZE+1,SIZE+1,16,0,0,0,0))+chunk(b'IDAT',zlib.compress(raw,9))+chunk(b'IEND',b''))
def rocks(folder):
 # Original low-poly rock meshes; no third-party art dependency.
 template=json.loads((ROOT/'Assets/GritGarage/salvage-crate.glb.meta').read_text(encoding='utf-8-sig'))
 for variant in range(3):
  name=f'sandstone-{variant}';lines=['# ByteEngine original sandstone; CC0','o sandstone']
  for ring,y in enumerate([-.5,-.15,.38]):
   for i in range(8):
    angle=i*math.tau/8+ring*.13;radius=[.43,.57,.34][ring]*(1+.16*math.sin(i*4.2+variant))
    lines.append(f'v {math.cos(angle)*radius:.5f} {y+.08*math.sin(i*2+variant):.5f} {math.sin(angle)*radius:.5f}')
  for ring in range(2):
   for i in range(8):
    a=ring*8+i+1;b=ring*8+(i+1)%8+1;c=b+8;d=a+8
    lines.extend([f'f {a} {b} {c}',f'f {a} {c} {d}'])
  for i in range(1,7):lines.extend([f'f 1 {i+2} {i+1}',f'f 17 {17+i} {18+i}'])
  asset=folder/(name+'.obj');asset.write_text('\n'.join(lines))
  meta=copy.deepcopy(template);meta['guid']=str(uuid.uuid5(uuid.NAMESPACE_URL,'dune-company/'+name));(folder/(name+'.obj.meta')).write_text(json.dumps(meta,indent=2))
 (folder/'LICENSE.txt').write_text('Original procedural sandstone meshes and heightmaps authored for Dune Company. Dedicated to CC0 1.0.\n')

def vector(x,y,z):return dict(x=x,y=y,z=z)
def object_(name,x,y,z,sx,sy,sz,color,angle=0,collide=False):
 components=[dict(type='MeshRenderer',enabled=True,properties=dict(usePrimitive=True,primitive='Cube',baseColor=color,roughness=1,metallic=0,castShadows=False,receiveShadows=True,blendMode='Opaque'))]
 if name.startswith('sandstone'):
  variant=sum(ord(c) for c in name)%3;asset=f'sandstone-{variant}';guid=uuid.uuid5(uuid.NAMESPACE_URL,'dune-company/'+asset)
  components[0]['properties'].update(usePrimitive=False,modelGuid=str(guid),modelPath=f'Assets/ContractLevels/{asset}.obj',meshKey=f'{guid.hex}:mesh:{asset}',baseColor=[.26,.18,.105,1],shading='Lit')
 if collide:components.append(dict(type='bytebard.dunecompany.DuneWorldObstacle3D',enabled=True,properties=dict(size=[1,1,1])))
 return dict(id=str(uuid.uuid4()),name='Contract landscape / '+name,active=True,tags=[],layer=0,parentId=None,transform=dict(localPosition=vector(x,y,z),localRotation=dict(x=0,y=math.sin(angle/2),z=0,w=math.cos(angle/2)),localScale=vector(sx,sy,sz)),components=components,variables=[])
def author(name,winch,backup):
 path=ROOT/'Scenes'/f'{name}.bytescene';shutil.copy2(path,backup/path.name)
 scene=json.loads(path.read_text(encoding='utf-8-sig'));scene['gameObjects']=[o for o in scene['gameObjects'] if not o['name'].startswith('Contract landscape / ')]
 for o in scene['gameObjects']:
  for c in o.get('components',[]):
   if c['type'].endswith('.DesertTerrain3D'):
    p=c['properties'];p.update(useReferenceSandSurface=False,cells=SIZE,spacing=1,heightmapPath=f'Assets/ContractLevels/{"extraction" if winch else "riverbed"}-height.png',packedSurfaceMaskPath=f'Assets/ContractLevels/{"extraction" if winch else "riverbed"}-packed.png',heightmapHeight=16,baseHeight=-3,heightmapHash='',sandState='',sculptState='',autoLoadSavedSand=False,lodNearDistance=35,lodFarDistance=90,textureWorldSize=12,normalStrength=.35)
   if c['type'].endswith('.DuneRecoveryContract3D'):
    o['transform']['localPosition']=vector(12 if winch else 0,0,70);c['properties'].update(depressionDepth=0,deliveryHalfWidth=6)
 # Rocks flank the playable track, never form an invisible mandatory gate.
 points=WINCH if winch else TOW
 for i,(a,b) in enumerate(zip(points,points[1:])):
  dx,dz=b[0]-a[0],b[1]-a[1];length=math.hypot(dx,dz);nx,nz=-dz/length,dx/length
  for j in range(1,4):
   t=j/4;x,z=a[0]+dx*t,a[1]+dz*t
   for side in [-1,1]:
    xx,zz=x+nx*side*(10+j),z+nz*side*(10+j);hh=height(xx,zz,winch)
    scene['gameObjects'].append(object_(f'sandstone {i}-{j}-{side}',xx,hh+1.2,zz,3+j*.4,2.4,2+j*.5,[.48,.36,.24,1],i*.7+j*.3,True))
 # Readable route-edge posts, orange at site, pale at depot.
 for i,(x,z,y) in enumerate(points[1:]):
  for side in [-1,1]:
   xx=x+side*6.7;hh=height(xx,z,winch)
   scene['gameObjects'].append(object_(f'route post {i}-{side}',xx,hh+.65,z,.18,1.3,.18,[.9,.53,.12,1]))
 if winch:
  for i in range(3):
   x,z=30+i*3,62;hh=height(x,z,True)
   scene['gameObjects'].append(object_(f'abandoned equipment crate {i}',x,hh+.7,z,1.5,1.4,2,[.28,.31,.29,1],.2,True))
  for x in [-2,27]:
   z=76;hh=height(x,z,True);scene['gameObjects'].append(object_('terrace retaining wall',x,hh+1,z,2,2,10,[.38,.33,.27,1],0,True))
 else:
  for i,(x,z,y) in enumerate(EASY[1:-1]):
   scene['gameObjects'].append(object_(f'western detour marker {i}',x-6,height(x-6,z,False)+.7,z,.2,1.4,.2,[.8,.8,.63,1]))
 for side in [-1,1]:
  scene['gameObjects'].append(object_('depot entrance',side*6.7,.9,8,.3,1.8,.3,[.83,.79,.61,1]))
 path.write_text(json.dumps(scene,indent=2),encoding='utf-8')

def main():
 parser=argparse.ArgumentParser();parser.add_argument('--apply',action='store_true');args=parser.parse_args()
 if not args.apply:raise SystemExit('Use --apply to author project scenes with backups.')
 backup=ROOT/'.byteengine/backups'/('starter-levels-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True)
 folder=ROOT/'Assets/ContractLevels';folder.mkdir(parents=True,exist_ok=True);rocks(folder)
 for name,winch in [('Workshop',False),('WinchRescue',True)]:
  png(folder/f'{"extraction" if winch else "riverbed"}-height.png',winch);png(folder/f'{"extraction" if winch else "riverbed"}-packed.png',winch,True);author(name,winch,backup)
 print('Authored two mission landscapes; backups:',backup)
if __name__=='__main__':main()
