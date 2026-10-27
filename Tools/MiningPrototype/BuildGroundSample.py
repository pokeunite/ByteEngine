"""Build a small static glTF surface. Texture pixels are copied unchanged from imagegen."""
import json, math, struct
from pathlib import Path

out=Path('Docs/Concepts/AlienMining/GroundSkySample')
positions=[]; normals=[]; uvs=[]
nx,nz=12,14
def point(x,z):
    edge=min(x,nx-x,z,nz-z)
    height=.008+min(edge,1)*(.009+.009*math.sin(x*.9+z*.7)*math.cos(z*.5-x*.3))
    return (x-nx/2,height,z-nz/2)
for z in range(nz):
 for x in range(nx):
  a,b,c,d=point(x,z),point(x+1,z),point(x,z+1),point(x+1,z+1)
  tris=[(a,c,b),(b,c,d)] if (x+z)%2==0 else [(a,c,d),(a,d,b)]
  for tri in tris:
   ab=[tri[1][i]-tri[0][i] for i in range(3)];ac=[tri[2][i]-tri[0][i] for i in range(3)]
   n=(ab[1]*ac[2]-ab[2]*ac[1],ab[2]*ac[0]-ab[0]*ac[2],ab[0]*ac[1]-ab[1]*ac[0]);length=math.sqrt(sum(v*v for v in n));n=tuple(v/length for v in n)
   for p in tri:positions.extend(p);normals.extend(n);uvs.extend(((p[0]+nx/2)/nx,(p[2]+nz/2)/nz))
blob=bytearray();views=[];accessors=[]
def data(values,kind,count,limits=None):
 while len(blob)%4:blob.append(0)
 start=len(blob);blob.extend(struct.pack('<'+'f'*len(values),*values));views.append({'buffer':0,'byteOffset':start,'byteLength':len(blob)-start,'target':34962});a={'bufferView':len(views)-1,'componentType':5126,'count':count,'type':kind}
 if limits:a['min'],a['max']=limits
 accessors.append(a);return len(accessors)-1
count=len(positions)//3
data(positions,'VEC3',count,([min(positions[i::3]) for i in range(3)],[max(positions[i::3]) for i in range(3)]));data(normals,'VEC3',count);data(uvs,'VEC2',count)
while len(blob)%4:blob.append(0)
start=len(blob);blob.extend((out/'OrangeSand-BaseColor.png').read_bytes());views.append({'buffer':0,'byteOffset':start,'byteLength':len(blob)-start})
doc={'asset':{'version':'2.0','generator':'ByteEngine ground material sample'},'scene':0,'scenes':[{'nodes':[0]}],'nodes':[{'name':'Orange Sand Sample','mesh':0}],'meshes':[{'name':'Sand surface','primitives':[{'attributes':{'POSITION':0,'NORMAL':1,'TEXCOORD_0':2},'material':0}]}],'materials':[{'name':'Orange sand','doubleSided':True,'pbrMetallicRoughness':{'baseColorTexture':{'index':0},'metallicFactor':0,'roughnessFactor':.96}}],'textures':[{'source':0,'sampler':0}],'samplers':[{'magFilter':9729,'minFilter':9987,'wrapS':10497,'wrapT':10497}],'images':[{'bufferView':3,'mimeType':'image/png'}],'accessors':accessors,'bufferViews':views,'buffers':[{'byteLength':len(blob)}]}
encoded=json.dumps(doc,separators=(',',':')).encode();encoded+=b' '*((-len(encoded))%4);blob+=b'\0'*((-len(blob))%4)
result=struct.pack('<III',0x46546c67,2,12+8+len(encoded)+8+len(blob))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(blob),0x004e4942)+blob
(out/'OrangeSand-Sample.glb').write_bytes(result)
print(f'Sample: {count//3} triangles, 12 x 14 metres, 8–26 mm visual relief; generated texture embedded unchanged.')
