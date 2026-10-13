import json,struct
from pathlib import Path
for p in Path(r'C:/Users/codex/Downloads/byte engine files/Karlson V2').glob('*.glb'):
 data=p.read_bytes();n=struct.unpack_from('<I',data,12)[0];j=json.loads(data[20:20+n]);print(p.name,len(j['meshes']),len(j['skins']),[x.get('name') for x in j['nodes'] if 'Rig' in x.get('name','')],[(x['name'],x.get('mesh')) for x in j['nodes'] if 'Rifle' in x.get('name','')][:3])
