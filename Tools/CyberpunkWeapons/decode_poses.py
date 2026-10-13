import json,struct
from pathlib import Path
p=Path(r'C:/Users/codex/Downloads/byte engine files/Cyberpunk Weapons v3/Cyberpunk_Pistol.glb');d=p.read_bytes();n=struct.unpack_from('<I',d,12)[0];j=json.loads(d[20:20+n]);binary=d[28+n:]
def values(a):
 a=j['accessors'][a];v=j['bufferViews'][a['bufferView']];c={'VEC3':3,'VEC4':4,'SCALAR':1,'MAT4':16}[a['type']];base=v.get('byteOffset',0)+a.get('byteOffset',0);return [struct.unpack_from('<'+'f'*c,binary,base+i*v.get('byteStride',c*4)) for i in range(a['count'])]
for name in ['Idle','Fire','Equip']:
 anim=next(a for a in j['animations'] if a['name']==name)
 for bone in ['Slide','WeaponRoot','WeaponMotion']:
  node=next(i for i,n in enumerate(j['nodes']) if n.get('name')==bone);print(name,bone,[(ch['target']['path'],values(anim['samplers'][ch['sampler']]['output'])[:8]) for ch in anim['channels'] if ch['target']['node']==node])
print('mesh nodes',[(x['name'],x.get('translation'),x.get('scale')) for x in j['nodes'] if 'mesh' in x])
