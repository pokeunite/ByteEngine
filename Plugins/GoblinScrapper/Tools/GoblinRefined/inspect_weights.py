import struct,json
from pathlib import Path
p=Path(r'C:\Users\codex\Downloads\parts\refined\scrap_catapult_basket.glb');b=p.read_bytes();n=struct.unpack_from('<I',b,12)[0];j=json.loads(b[20:20+n]);binary=b[28+n:]
for m in j['meshes']:
 for pp in m['primitives']:
  idx=pp['attributes'].get('WEIGHTS_0');a=j['accessors'][idx] if idx is not None else None
  if not a: print('MISSING',m['name']);continue
  view=j['bufferViews'][a['bufferView']];off=view.get('byteOffset',0)+a.get('byteOffset',0);fmt={5126:'f',5123:'H',5121:'B'}[a['componentType']];sz=struct.calcsize(fmt);vals=[sum(struct.unpack_from('<4'+fmt,binary,off+i*view.get('byteStride',4*sz))) for i in range(a['count'])];print(m['name'],a['componentType'],min(vals),max(vals))
