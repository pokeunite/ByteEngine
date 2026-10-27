import json,struct,math
from pathlib import Path
OUT=Path('artifacts/defence-models');report=[]
for file in sorted(OUT.glob('*.glb')):
 raw=file.read_bytes();magic,version,total=struct.unpack_from('<III',raw);assert magic==0x46546c67 and version==2 and total==len(raw)
 size,kind=struct.unpack_from('<II',raw,12);doc=json.loads(raw[20:20+size]);offset=20+size;binary=raw[offset+8:]
 formats={5120:('b',1),5121:('B',1),5122:('h',2),5123:('H',2),5125:('I',4),5126:('f',4)};widths={'SCALAR':1,'VEC2':2,'VEC3':3,'VEC4':4,'MAT4':16}
 def values(index):
  a=doc['accessors'][index];view=doc['bufferViews'][a['bufferView']];fmt,bytesize=formats[a['componentType']];width=widths[a['type']];start=view.get('byteOffset',0)+a.get('byteOffset',0);stride=view.get('byteStride',bytesize*width)
  return [struct.unpack_from('<'+fmt*width,binary,start+i*stride) for i in range(a['count'])]
 assert len(doc['skins'])==1 and len(doc['meshes'])==1
 skin=doc['skins'][0];joints=len(skin['joints']);vertices=0;triangles=0
 for p in doc['meshes'][0]['primitives']:
  attr=p['attributes'];points=values(attr['POSITION']);vertices+=len(points);triangles+=len(values(p['indices']))//3
  assert all(math.isfinite(x) for row in points for x in row)
  weights=values(attr['WEIGHTS_0']);jointnums=values(attr['JOINTS_0']);assert all(abs(sum(row)-1)<.0001 for row in weights)
  assert all(0<=j<joints for row in jointnums for j in row)
 clips=doc['animations'];assert len(clips)==5 and all(a['name'].startswith(file.stem+'_') for a in clips)
 animated=set()
 for a in clips:
  changed=False
  for sampler in a['samplers']:
   times=[x[0] for x in values(sampler['input'])];assert len(times)>1 and all(math.isfinite(t) for t in times) and all(b>a for a,b in zip(times,times[1:]));output=values(sampler['output']);assert all(math.isfinite(x) for row in output for x in row)
   changed|=any(any(abs(x-y)>1e-5 for x,y in zip(row,output[0])) for row in output[1:])
  if changed:animated.add(a['name'])
 assert any(n.endswith('_Deploy') for n in animated)
 if file.stem in ('Sentry','Repeater','Mortar'):assert any(n.endswith('_Fire') for n in animated)
 elif file.stem=='SupplyCache':assert any(n.endswith('_Open') for n in animated)
 else:assert any(n.endswith('_Trigger') for n in animated)
 report.append({'file':file.name,'vertices':vertices,'triangles':triangles,'joints':joints,'materials':len(doc['materials']),'clips':[a['name'] for a in clips],'animatedClips':sorted(animated)})
 print('PASS',file.name,triangles,'triangles,',joints,'joints, five correctly scoped clips')
assert len(report)==5
(OUT/'gltf-verification.json').write_text(json.dumps(report,indent=2))
