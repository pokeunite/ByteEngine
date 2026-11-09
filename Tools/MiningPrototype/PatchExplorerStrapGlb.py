"""Apply Blender-verified weight repair without re-exporting animations or skeleton."""
import pathlib,json,struct,hashlib
root=pathlib.Path(__file__).resolve().parents[2]
source=pathlib.Path('C:/Users/codex/Documents/palclone/Assets/Characters/Explorer/Explorer.glb')
original=source.read_bytes();data=bytearray(original)
length=struct.unpack_from('<I',data,12)[0];doc=json.loads(data[20:20+length]);binary=20+length+8
patch=json.loads((root/'artifacts/explorer-strap-weight-patch.json').read_text())
keys={tuple(round(c,2) for c in row['co']):row['weights'] for row in patch}
assert len(keys)>20
skin=doc['skins'][doc['nodes'][51]['skin']]
joint_ids={doc['nodes'][node]['name']:i for i,node in enumerate(skin['joints'])}
def accessor(index):
 a=doc['accessors'][index];v=doc['bufferViews'][a['bufferView']]
 fmt={5121:'B',5123:'H',5126:'f'}[a['componentType']];size=struct.calcsize(fmt)*int(a['type'][-1])
 return a,binary+v.get('byteOffset',0)+a.get('byteOffset',0),v.get('byteStride',size),'<'+fmt*int(a['type'][-1])
changed=0;seen=set();allowed=set()
for prim in doc['meshes'][0]['primitives']:
 attrs=prim['attributes'];pa,po,ps,pf=accessor(attrs['POSITION']);ja,jo,js,jf=accessor(attrs['JOINTS_0']);wa,wo,ws,wf=accessor(attrs['WEIGHTS_0'])
 for i in range(pa['count']):
  point=struct.unpack_from(pf,data,po+i*ps);key=tuple(round(c*100,2) for c in point)
  if key not in keys:continue
  weights=keys[key];pairs=list(weights.items());assert len(pairs)<=4
  joints=[joint_ids[n] for n,w in pairs]+[0]*(4-len(pairs));values=[w for n,w in pairs]+[0]*(4-len(pairs))
  struct.pack_into(jf,data,jo+i*js,*joints);struct.pack_into(wf,data,wo+i*ws,*values)
  allowed.update(range(jo+i*js,jo+i*js+struct.calcsize(jf)));allowed.update(range(wo+i*ws,wo+i*ws+struct.calcsize(wf)))
  changed+=1;seen.add(key)
assert len(seen)==len(keys),(len(seen),len(keys))
assert changed==len(patch),(changed,len(patch))
assert all(i in allowed for i,(a,b) in enumerate(zip(original,data)) if a!=b)
out=root/'artifacts/Explorer-straps-fixed.glb';out.write_bytes(data)
report={'vertices':changed,'skeleton_and_animation_bytes_unchanged':True,'source_sha256':hashlib.sha256(original).hexdigest(),'fixed_sha256':hashlib.sha256(data).hexdigest(),'output':str(out)}
(root/'artifacts/explorer-strap-glb-validation.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
