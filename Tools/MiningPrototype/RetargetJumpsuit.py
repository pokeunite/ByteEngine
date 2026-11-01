"""Retain clip rotations/timing; adapt translation tracks to the fitted bind.
Embed the downloaded PBR textures and preserve the complete 51-joint hierarchy.
"""
import copy,json,pathlib,struct,math
from PreserveExplorerAnimation import read,worlds,inv,mul,rows,TYPES,FORMATS
root=pathlib.Path('C:/Users/codex/Documents/palclone')
src,sbin=read(pathlib.Path('C:/Users/codex/Downloads/byte engine files/synty new.glb'))
new,nbin=read(root/'SourceArt/HazardPayJumpsuit/JumpsuitBind.glb')
si=next(i for i,n in enumerate(src['nodes']) if n.get('skin')==0 and 'mesh' in n)
ni=next(i for i,n in enumerate(new['nodes']) if 'skin' in n and 'mesh' in n)
ss=src['skins'][src['nodes'][si]['skin']];ns=new['skins'][new['nodes'][ni]['skin']]
source_names=[src['nodes'][i]['name'] for i in ss['joints']];new_names=[new['nodes'][i]['name'] for i in ns['joints']]
assert set(source_names)==set(new_names)
joint_map={i:source_names.index(n) for i,n in enumerate(new_names)}
sw=worlds(src);nw=worlds(new);desired=copy.deepcopy(sw)
for sj in ss['joints']:
 nj=ns['joints'][new_names.index(src['nodes'][sj]['name'])]
 for r in range(3):desired[sj][r][3]=nw[nj][r][3]
parents={c:i for i,n in enumerate(src['nodes']) for c in n.get('children',[])}
result={k:copy.deepcopy(v) for k,v in src.items() if k not in ['buffers','bufferViews','accessors','meshes','materials','images','textures','samplers']}
offsets={}
for sj in ss['joints']:
 local=mul(inv(desired[parents[sj]]),desired[sj]) if sj in parents else desired[sj]
 t=[local[r][3] for r in range(3)];old=src['nodes'][sj].get('translation',[0,0,0]);offsets[sj]=[t[i]-old[i] for i in range(3)]
 result['nodes'][sj]['translation']=t
result['asset']={'version':'2.0','generator':'Hazard Pay / JUMPSUIT by DanlyVostok / fitted animation rig'}
result['accessors']=[];result['bufferViews']=[];result['materials']=copy.deepcopy(new['materials']);result['meshes']=[copy.deepcopy(new['meshes'][new['nodes'][ni]['mesh']])]
for n in result['nodes']:n.pop('mesh',None);n.pop('skin',None)
result['nodes'][si].update(name='Hazard Pay Jumpsuit',mesh=0,skin=0)
data=bytearray();cache={};views={}
def append(value,target=None,stride=None):
 while len(data)%4:data.append(0)
 v={'buffer':0,'byteOffset':len(data),'byteLength':len(value)};data.extend(value)
 if target:v['target']=target
 if stride:v['byteStride']=stride
 result['bufferViews'].append(v);return len(result['bufferViews'])-1
def values_accessor(a,values):
 a=copy.deepcopy(a);a.pop('byteOffset',None);a.pop('min',None);a.pop('max',None)
 fmt='<'+FORMATS[a['componentType']]*TYPES[a['type']]
 a['bufferView']=append(b''.join(struct.pack(fmt,*v) for v in values));result['accessors'].append(a);return len(result['accessors'])-1
def access(tag,doc,binary,index,change=None):
 key=(tag,index,change)
 if key in cache:return cache[key]
 a=copy.deepcopy(doc['accessors'][index])
 if change:
  values=rows(doc,binary,index)
  if change=='joint':values=[tuple(joint_map[int(j)] for j in v) for v in values]
  elif change=='position':values=[tuple(sum(transform[r][c]*v[c] for c in range(3))+transform[r][3] for r in range(3)) for v in values]
  elif change=='normal':
   normal=inv(transform);out=[]
   for v in values:
    p=[sum(normal[c][r]*v[c] for c in range(3)) for r in range(3)];length=math.sqrt(sum(x*x for x in p));out.append(tuple(x/max(length,1e-12) for x in p))
   values=out
  index=values_accessor(a,values)
  if change=='position':result['accessors'][index].update(min=[min(v[c] for v in values) for c in range(3)],max=[max(v[c] for v in values) for c in range(3)])
 else:
  vi=a['bufferView'];keyv=(tag,vi)
  if keyv not in views:
   v=doc['bufferViews'][vi];start=v.get('byteOffset',0);views[keyv]=append(binary[start:start+v['byteLength']],v.get('target'),v.get('byteStride'))
  a['bufferView']=views[keyv];result['accessors'].append(a);index=len(result['accessors'])-1
 cache[key]=index;return index
# Skeleton bind uses the original mesh coordinate system and the fitted heads.
inverse_binds=[mul(inv(desired[j]),sw[si]) for j in ss['joints']]
ibm=[tuple(m[r][c] for c in range(4) for r in range(4)) for m in inverse_binds]
result['skins'][0]['inverseBindMatrices']=values_accessor({'componentType':5126,'count':len(ibm),'type':'MAT4'},ibm)
for animation,original in zip(result['animations'],src['animations']):
 translation={c['sampler']:c['target']['node'] for c in original['channels'] if c['target']['path']=='translation'}
 for k,sampler in enumerate(animation['samplers']):
  sampler['input']=access('source',src,sbin,sampler['input'])
  if k in translation:
   delta=offsets.get(translation[k],[0,0,0]);idx=sampler['output'];values=[tuple(v[i]+delta[i] for i in range(3)) for v in rows(src,sbin,idx)];sampler['output']=values_accessor(src['accessors'][idx],values)
  else:sampler['output']=access('source',src,sbin,sampler['output'])
first_bind=rows(new,nbin,ns['inverseBindMatrices'])[0]
bind_matrix=[[first_bind[c*4+r] for c in range(4)] for r in range(4)]
# Blender bakes a selected skin's object transform into its inverse binds. The
# effective mesh space comes from jointWorld * inverseBind, not its node matrix.
authored_mesh_space=mul(nw[ns['joints'][0]],bind_matrix)
transform=mul(inv(sw[si]),authored_mesh_space)
for p in result['meshes'][0]['primitives']:
 for semantic,index in list(p['attributes'].items()):p['attributes'][semantic]=access('mesh',new,nbin,index,'position' if semantic=='POSITION' else 'normal' if semantic=='NORMAL' else 'joint' if semantic.startswith('JOINTS_') else None)
 if 'indices' in p:p['indices']=access('mesh',new,nbin,p['indices'])
for key in ['textures','samplers','images']:
 if key in new:result[key]=copy.deepcopy(new[key])
for im in result.get('images',[]):
 v=new['bufferViews'][im['bufferView']];start=v.get('byteOffset',0);im['bufferView']=append(nbin[start:start+v['byteLength']])
result.pop('extensionsUsed',None);result.pop('extensionsRequired',None)
for m in result['materials']:m.pop('extensions',None)
result['buffers']=[{'byteLength':len(data)}]
encoded=json.dumps(result,separators=(',',':')).encode();encoded+=b' '*((-len(encoded))%4);data+=b'\0'*((-len(data))%4)
out=root/'Assets/Characters/HazardPayJumpsuit.glb';out.parent.mkdir(parents=True,exist_ok=True)
out.write_bytes(struct.pack('<III',0x46546c67,2,28+len(encoded)+len(data))+struct.pack('<II',len(encoded),0x4e4f534a)+encoded+struct.pack('<II',len(data),0x004e4942)+data)
checked,bin=read(out)
for old,actual in zip(src['animations'],checked['animations']):
 assert old['name']==actual['name'] and old['channels']==actual['channels']
 for a,b in zip(old['samplers'],actual['samplers']):assert rows(src,sbin,a['input'])==rows(checked,bin,b['input'])
report={'asset':str(out),'bones':len(source_names),'animations':len(result['animations']),'embedded_images':len(result.get('images',[])),'animation_timing_preserved':True,'bind_translation_retargeted':True,'render_parts':len(result['meshes'][0]['primitives'])}
(root/'SourceArt/HazardPayJumpsuit/retarget-validation.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
