"""Original compact GLB workshop models and previews; no third-party art dependencies."""
import json,math,struct,copy,sys
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
ROOT=Path(sys.argv[1]) if len(sys.argv)>1 else Path('C:/Users/codex/Documents/DuneCompany')
OUT=ROOT/'Assets/DuneParts';(OUT/'glb').mkdir(parents=True,exist_ok=True);(OUT/'cutouts').mkdir(exist_ok=True)
PALETTE=[(.52,.47,.34,1),(.14,.16,.14,1),(.82,.31,.10,1),(.28,.29,.28,1),(.07,.065,.055,1)]
class Model:
 def __init__(self):self.parts=[]
 def tri(self,points,mat=0,node='Fixed_Body'):
  a,b,c=map(np.array,points);n=np.cross(b-a,c-a);n=n/max(np.linalg.norm(n),1e-8);self.parts.append((node,mat,[list(a)+list(n)+[0,0],list(b)+list(n)+[1,0],list(c)+list(n)+[1,1]]))
 def box(self,center,size,mat=0,node='Fixed_Body'):
  c=np.array(center);h=np.array(size)/2;v=[c+h*np.array([1 if i&1 else -1,1 if i&2 else -1,1 if i&4 else -1]) for i in range(8)]
  for a,b,c,d in [(0,2,3,1),(4,5,7,6),(0,4,6,2),(1,3,7,5),(0,1,5,4),(2,6,7,3)]:self.tri([v[a],v[b],v[c]],mat,node);self.tri([v[a],v[c],v[d]],mat,node)
 def cylinder(self,center,radius,length,mat=1,node='Fixed_Body',axis='x',segments=20):
  def point(a,d):
   v=[d,radius*math.cos(a),radius*math.sin(a)];v=v if axis=='x' else [v[1],v[0],v[2]] if axis=='y' else [v[1],v[2],v[0]];return np.array(center)+v
  for i in range(segments):
   a=math.tau*i/segments;b=math.tau*(i+1)/segments;p,q,r,s=point(a,-length/2),point(b,-length/2),point(b,length/2),point(a,length/2)
   self.tri([p,q,r],mat,node);self.tri([p,r,s],mat,node)
   for side in [-1,1]:
    mid=np.array(center)+({'x':np.array([1,0,0]),'y':np.array([0,1,0]),'z':np.array([0,0,1])}[axis])*side*length/2
    vertices=[mid,point(a,side*length/2),point(b,side*length/2)];self.tri(vertices if side>0 else vertices[::-1],mat,node)
 def cordwrap(self,x,center,radius):
  for i in range(20):
   a=math.tau*i/20;b=math.tau*(i+1)/20;pa=np.array([x,center[0]+radius*math.cos(a),center[1]+radius*math.sin(a)]);pb=np.array([x,center[0]+radius*math.cos(b),center[1]+radius*math.sin(b)]);mid=(pa+pb)/2
   self.box(mid,[.018,abs(pb[1]-pa[1])+.022,abs(pb[2]-pa[2])+.022],4)
 def save(self,name):
  gltf={'asset':{'version':'2.0','generator':'ByteEngine original modular parts'},'scene':0,'scenes':[{'nodes':[]}],'nodes':[],'meshes':[],'materials':[{'pbrMetallicRoughness':{'baseColorFactor':c,'metallicFactor':.3 if i<4 else 0,'roughnessFactor':.8}} for i,c in enumerate(PALETTE)],'accessors':[],'bufferViews':[],'buffers':[]};binary=bytearray();groups={}
  for node,mat,verts in self.parts:groups.setdefault(node,{}).setdefault(mat,[]).extend(verts)
  for node,mats in groups.items():
   primitives=[]
   for mat,vertices in mats.items():
    arr=np.array(vertices,dtype='<f4');attrs={}
    for semantic,indices,typ in [('POSITION',[0,1,2],'VEC3'),('NORMAL',[3,4,5],'VEC3'),('TEXCOORD_0',[6,7],'VEC2')]:
     chunk=arr[:,indices].copy();offset=len(binary);binary.extend(chunk.tobytes());view=len(gltf['bufferViews']);gltf['bufferViews'].append({'buffer':0,'byteOffset':offset,'byteLength':chunk.nbytes,'target':34962});accessor={'bufferView':view,'componentType':5126,'count':len(chunk),'type':typ}
     if semantic=='POSITION':accessor.update(min=chunk.min(axis=0).tolist(),max=chunk.max(axis=0).tolist())
     attrs[semantic]=len(gltf['accessors']);gltf['accessors'].append(accessor)
    primitives.append({'attributes':attrs,'material':mat,'mode':4})
   mesh=len(gltf['meshes']);gltf['meshes'].append({'name':node,'primitives':primitives});gltf['scenes'][0]['nodes'].append(len(gltf['nodes']));gltf['nodes'].append({'name':node,'mesh':mesh})
  gltf['buffers']=[{'byteLength':len(binary)}];j=json.dumps(gltf,separators=(',',':')).encode();j+=b' '*((-len(j))%4);binary+=b'\0'*((-len(binary))%4);data=struct.pack('<III',0x46546c67,2,28+len(j)+len(binary))+struct.pack('<II',len(j),0x4e4f534a)+j+struct.pack('<II',len(binary),0x004e4942)+binary;(OUT/'glb'/f'{name}.glb').write_bytes(data)
  image=Image.new('RGBA',(256,256));draw=ImageDraw.Draw(image);triangles=[]
  for node,mat,vertices in self.parts:
   pts=[v[:3] for v in vertices];project=[(p[0]*.83-p[2]*.55,-p[1]+p[0]*.28+p[2]*.38) for p in pts];triangles.append((sum(p[0]+p[2] for p in pts),mat,project,vertices[0][3:6]))
  allpts=[p for _,_,points,_ in triangles for p in points];low=np.min(allpts,axis=0);high=np.max(allpts,axis=0);scale=218/max(high-low)
  for _,mat,points,n in sorted(triangles):
   light=.55+.45*max(0,np.dot(n,np.array([.4,.8,.5])));color=tuple(int(min(255,c*255*light)) for c in PALETTE[mat][:3])+(255,);draw.polygon([((p[0]-low[0])*scale+19,(p[1]-low[1])*scale+19) for p in points],fill=color)
  image.save(OUT/'cutouts'/f'{name}.png')
  bounds=np.array([v[:3] for _,_,vertices in self.parts for v in vertices]);return bounds.min(axis=0).tolist(),bounds.max(axis=0).tolist()
def winch(power=False,long=False):
 m=Model();m.box([0,.055,0],[.5,.11,.5]);m.box([-.2,.24,0],[.055,.32,.32],1);m.box([.2,.24,0],[.055,.32,.32],1);m.cylinder([0,.25,0],.125,.32,1)
 for x in [-.16,.16]:m.cylinder([x,.25,0],.155,.035,2)
 for i in range(13):m.cordwrap(-.135+i*.022,[.25,0],.14)
 m.cylinder([.28,.25,0],.095,.15,0);m.box([.29,.34,0],[.16,.07,.17],2);m.box([0,.25,-.21],[.3,.13,.045],1)
 for x in [-.135,.135]:m.cylinder([x,.25,-.245],.035,.15,3,axis='y',segments=12)
 for y in [.2,.3]:m.cylinder([0,y,-.245],.022,.23,3)
 if power:m.box([-.27,.25,0],[.1,.25,.25],2)
 if long:m.box([0,.09,.2],[.3,.05,.08],2)
 return m
def engine(race=False):
 m=Model();m.box([0,.05,0],[.5,.1,.5]);m.box([0,.22,0],[.36,.3,.36],1);m.box([0,.41,0],[.42,.12,.35],0)
 for x in [-.16,.16]:
  for z in [-.12,0,.12]:m.cylinder([x,.3,z],.07,.15,3,axis='y',segments=12)
 m.cylinder([0,.23,-.24],.1,.055,2,axis='z');m.box([.22,.2,.08],[.07,.21,.21],0)
 if race:
  m.box([0,.53,0],[.25,.14,.31],2)
  for z in [-.09,.09]:m.cylinder([0,.61,z],.065,.23,3)
 else:m.box([0,.48,.06],[.23,.08,.26],2);m.box([-.23,.22,.12],[.08,.21,.18],0)
 return m
def tyre():
 m=Model();m.box([0,0,.04],[.12,.12,.08],3);m.cylinder([0,0,-.18],.57,.38,4,'Moving_Wheel','z',32);m.cylinder([0,0,-.385],.29,.04,0,'Moving_Wheel','z',20)
 for i in range(28):
  a=math.tau*i/28
  for z in [-.3,-.08]:m.box([.56*math.cos(a),.56*math.sin(a),z],[.085,.085,.13],4,'Moving_Wheel')
 return m
catalog=[{k[0].lower()+k[1:]:v for k,v in part.items()} for part in json.loads((OUT/'catalog.json').read_text(encoding='utf-8-sig'))];specs=[(33,'powered_winch','Powered winch',winch(),35,0,0),(34,'low_gear_engine','Low-gear engine',engine(),55,5400,450),(35,'long_line_winch','Long-line winch',winch(long=True),42,0,650),(36,'power_winch','Power winch',winch(power=True),55,0,900),(37,'sand_tyre','Wide sand tyre',tyre(),22,0,125),(38,'race_engine','Race engine',engine(True),45,6500,1200)]
for id,name,label,model,mass,power,price in specs:
 filename=f'{id:02d}_{name}';lo,hi=model.save(filename);part=next((p for p in catalog if p['id']==id),None)
 if part is None:part=copy.deepcopy(next(p for p in catalog if p['id']==(13 if id==37 else 23 if power else 33)));catalog.append(part)
 part.update(id=id,name=name,label=label,file=f'Assets/DuneParts/glb/{filename}.glb',preview=f'Assets/DuneParts/cutouts/{filename}.png',min=lo,max=hi,mass=mass,power=power,price=price)
 effect={33:'12 kN pull / 24 m cable.',34:'High torque for heavy recovery / heavier than the stock engine.',35:'12 kN pull / 40 m cable / hook reach 22 m.',36:'20 kN pull / 24 m cable / heavier motor.',37:'Wide footprint / lower loose-sand resistance / $125 per tyre.',38:'High power / 42 m/s stock speed cap / lighter than the heavy engine.'}[id];part['function']=effect+' Buy once per physical part; tuning unlock is permanent per type.'
 if id==37:part.update(radius=.6,wheelCenterZ=-.18)
for part in catalog:
 id=part['id'];part['tuningCost']=150 if id in [23,24,33,34,35,36,38] else 75 if id in [13,14,15,37] else 100 if id in [16,17,18,19,20,21,22,32] else 25
(OUT/'catalog.json').write_text(json.dumps(sorted(catalog,key=lambda p:p['id']),indent=2)+'\n')
print('Created six GLB models and previews; five priced upgrade entries. No suspension upgrades.')
