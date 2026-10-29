"""Keep source glTF skeleton/animation samples exact while replacing only its mesh.

Blender may resample curves or alter joint coordinate bases on export. This
reconciles the authored mesh at bind time and copies the supplied animation
accessors byte-for-byte, retaining the original node and joint identities.
"""
import copy, json, math, pathlib, struct
ROOT=pathlib.Path('C:/Users/codex/Documents/palclone')
SOURCE=pathlib.Path('C:/Users/codex/Downloads/byte engine files/synty new.glb')
AUTHORED=ROOT/'SourceArt/HazardPayExplorer/HazardPayExplorer.glb'
OUTPUT=ROOT/'Assets/Characters/HazardPayExplorer.glb'
def read(path):
 data=path.read_bytes();magic,version,size=struct.unpack_from('<III',data)
 if magic!=0x46546c67 or version!=2 or size!=len(data):raise ValueError('Invalid GLB')
 at=12;doc=None;binary=b''
 while at<len(data):
  length,kind=struct.unpack_from('<II',data,at);chunk=data[at+8:at+8+length];at+=8+length
  if kind==0x4e4f534a:doc=json.loads(chunk)
  elif kind==0x004e4942:binary=chunk
 return doc,binary
I=lambda:[[int(r==c) for c in range(4)] for r in range(4)]
def mul(a,b):return [[sum(a[r][k]*b[k][c] for k in range(4)) for c in range(4)] for r in range(4)]
def inv(m):
 a=[row[:]+i for row,i in zip(m,I())]
 for k in range(4):
  best=max(range(k,4),key=lambda r:abs(a[r][k]));a[k],a[best]=a[best],a[k];scale=a[k][k]
  if abs(scale)<1e-12:raise ValueError('Singular bind transform')
  a[k]=[x/scale for x in a[k]]
  for r in range(4):
   if r!=k:f=a[r][k];a[r]=[x-f*y for x,y in zip(a[r],a[k])]
 return [row[4:] for row in a]
def local(n):
 if 'matrix' in n:return [[n['matrix'][c*4+r] for c in range(4)] for r in range(4)]
 x,y,z,w=n.get('rotation',[0,0,0,1]);s=n.get('scale',[1,1,1]);t=n.get('translation',[0,0,0])
 m=[[1-2*(y*y+z*z),2*(x*y-z*w),2*(x*z+y*w),t[0]],[2*(x*y+z*w),1-2*(x*x+z*z),2*(y*z-x*w),t[1]],[2*(x*z-y*w),2*(y*z+x*w),1-2*(x*x+y*y),t[2]],[0,0,0,1]]
 for r in range(3):
  for c in range(3):m[r][c]*=s[c]
 return m
def worlds(doc):
 parent={c:i for i,n in enumerate(doc['nodes']) for c in n.get('children',[])};cache={}
 def world(i):
  if i not in cache:cache[i]=mul(world(parent[i]),local(doc['nodes'][i])) if i in parent else local(doc['nodes'][i])
  return cache[i]
 return [world(i) for i in range(len(doc['nodes']))]
TYPES={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16};FORMATS={5121:'B',5123:'H',5125:'I',5126:'f'}
def rows(doc,binary,index):
 a=doc['accessors'][index];v=doc['bufferViews'][a['bufferView']];fmt=FORMATS[a['componentType']];n=TYPES[a['type']];size=struct.calcsize('<'+fmt*n);stride=v.get('byteStride',size);start=v.get('byteOffset',0)+a.get('byteOffset',0)
 return [struct.unpack_from('<'+fmt*n,binary,start+i*stride) for i in range(a['count'])]
def run():
 src,sbin=read(SOURCE);new,nbin=read(AUTHORED)
 si=next(i for i,n in enumerate(src['nodes']) if n.get('skin')==0 and 'mesh' in n)
 ni=next(i for i,n in enumerate(new['nodes']) if 'skin' in n and 'mesh' in n and 'Explorer' in n.get('name',''))
 ss=src['skins'][src['nodes'][si]['skin']];ns=new['skins'][new['nodes'][ni]['skin']]
 source_names=[src['nodes'][i]['name'] for i in ss['joints']];new_names=[new['nodes'][i]['name'] for i in ns['joints']]
 if set(source_names)!=set(new_names):raise ValueError('Exported skeleton does not contain the exact source joints')
 joint_map={i:source_names.index(name) for i,name in enumerate(new_names)}
 transform=mul(inv(worlds(src)[si]),worlds(new)[ni]);normal=inv(transform)
 result={k:copy.deepcopy(v) for k,v in src.items() if k not in ['buffers','bufferViews','accessors','meshes','materials','images','textures','samplers']}
 result['asset']={'version':'2.0','generator':'Hazard Pay explorer / exact supplied rig and animation preservation'}
 result['accessors']=[];result['bufferViews']=[];result['materials']=copy.deepcopy(new.get('materials',[]));result['meshes']=[copy.deepcopy(new['meshes'][new['nodes'][ni]['mesh']])]
 result['meshes'][0]['name']='HazardPay Explorer'
 for n in result['nodes']:n.pop('mesh',None);n.pop('skin',None)
 result['nodes'][si]['name']='HazardPay Explorer';result['nodes'][si]['mesh']=0;result['nodes'][si]['skin']=0
 output=bytearray();accessor_cache={};view_cache={}
 def append(data,target=None,stride=None):
  while len(output)%4:output.append(0)
  start=len(output);output.extend(data);v={'buffer':0,'byteOffset':start,'byteLength':len(data)}
  if target:v['target']=target
  if stride:v['byteStride']=stride
  result['bufferViews'].append(v);return len(result['bufferViews'])-1
 def accessor(tag,doc,binary,index,change=None):
  key=(tag,index,change)
  if key in accessor_cache:return accessor_cache[key]
  a=copy.deepcopy(doc['accessors'][index]);a.pop('name',None)
  if 'sparse' in a:raise ValueError('Sparse animation/mesh accessor needs explicit handling')
  if change:
   values=rows(doc,binary,index);changed=[]
   for value in values:
    if change=='joint':changed.append(tuple(joint_map[int(j)] for j in value))
    elif change=='position':changed.append(tuple(sum(transform[r][c]*value[c] for c in range(3))+transform[r][3] for r in range(3)))
    elif change=='normal':
     v=[sum(normal[c][r]*value[c] for c in range(3)) for r in range(3)];length=math.sqrt(sum(x*x for x in v));changed.append(tuple(x/max(length,1e-12) for x in v))
    else:raise ValueError(change)
   fmt=FORMATS[a['componentType']]*TYPES[a['type']];data=b''.join(struct.pack('<'+fmt,*v) for v in changed)
   a['bufferView']=append(data,34962);a.pop('byteOffset',None)
   if change=='position':a['min']=[min(v[c] for v in changed) for c in range(3)];a['max']=[max(v[c] for v in changed) for c in range(3)]
  else:
   vi=a['bufferView'];keyv=(tag,vi)
   if keyv not in view_cache:
    v=doc['bufferViews'][vi];start=v.get('byteOffset',0);view_cache[keyv]=append(binary[start:start+v['byteLength']],v.get('target'),v.get('byteStride'))
   a['bufferView']=view_cache[keyv]
  result['accessors'].append(a);idx=len(result['accessors'])-1;accessor_cache[key]=idx;return idx
 for skin in result['skins']:skin['inverseBindMatrices']=accessor('source',src,sbin,skin['inverseBindMatrices'])
 for animation in result['animations']:
  for sampler in animation['samplers']:
   for field in ['input','output']:sampler[field]=accessor('source',src,sbin,sampler[field])
 for primitive in result['meshes'][0]['primitives']:
  for semantic,index in list(primitive['attributes'].items()):
   change='position' if semantic=='POSITION' else 'normal' if semantic=='NORMAL' else 'joint' if semantic.startswith('JOINTS_') else None
   primitive['attributes'][semantic]=accessor('mesh',new,nbin,index,change)
  if 'indices' in primitive:primitive['indices']=accessor('mesh',new,nbin,primitive['indices'])
  if 'targets' in primitive:raise ValueError('Unexpected morph geometry')
 result['buffers']=[{'byteLength':len(output)}];result.pop('extensionsUsed',None);result.pop('extensionsRequired',None)
 # ByteEngine's portable material path uses core glTF PBR. Blender's optional
 # coat/emission extensions must not remain after their declarations are removed.
 for material in result['materials']:material.pop('extensions',None)
 # The authored materials use plain PBR factors, not external textures.
 if any('Texture' in key for m in result['materials'] for block in [m,m.get('pbrMetallicRoughness',{})] for key in block):raise ValueError('Unexpected texture dependency')
 encoded=json.dumps(result,separators=(',',':')).encode();encoded+=b' '*((-len(encoded))%4);output+=b'\0'*((-len(output))%4)
 glb=struct.pack('<III',0x46546c67,2,12+8+len(encoded)+8+len(output))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(output),0x004e4942)+output
 OUTPUT.parent.mkdir(parents=True,exist_ok=True)
 if OUTPUT.exists():raise FileExistsError('Do not overwrite an existing imported character: '+str(OUTPUT))
 OUTPUT.write_bytes(glb)
 checked,checked_binary=read(OUTPUT)
 for original,actual in zip(src['animations'],checked['animations']):
  assert original['name']==actual['name'] and original['channels']==actual['channels']
  for a,b in zip(original['samplers'],actual['samplers']):
   for field in ['input','output']:assert rows(src,sbin,a[field])==rows(checked,checked_binary,b[field])
 assert ss['joints']==checked['skins'][0]['joints']
 assert rows(src,sbin,ss['inverseBindMatrices'])==rows(checked,checked_binary,checked['skins'][0]['inverseBindMatrices'])
 report={'asset':str(OUTPUT),'bytes':len(glb),'bones':len(source_names),'animations':len(src['animations']),'source_animation_samples_exact':True,'source_inverse_bind_matrices_exact':True,'bind_mesh_transform':transform,'render_parts':len(checked['meshes'][0]['primitives'])}
 (ROOT/'SourceArt/HazardPayExplorer/export-validation.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
if __name__=='__main__':run()
