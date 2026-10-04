from pathlib import Path
import struct,json
for p in Path('Designs/BattlePrototype/assets').glob('*.glb'):
 b=p.read_bytes();n=struct.unpack_from('<I',b,12)[0];j=json.loads(b[20:20+n]);blob=b[28+n:]
 for field in ['extensionsUsed','extensionsRequired']:
  if j.get(field)==[]:j.pop(field)
 for tex in j.get('textures',[]):
  if tex.get('extensions')=={}:tex.pop('extensions')
 doc=json.dumps(j,separators=(',',':')).encode();doc+=b' '*((-len(doc))%4);p.write_bytes(struct.pack('<III',0x46546c67,2,28+len(doc)+len(blob))+struct.pack('<II',len(doc),0x4e4f534a)+doc+struct.pack('<II',len(blob),0x004e4942)+blob)
 if p.name=='red-goblin.glb':print([(m.get('name'),m.get('pbrMetallicRoughness',{}).get('baseColorFactor')) for m in j.get('materials',[])])
