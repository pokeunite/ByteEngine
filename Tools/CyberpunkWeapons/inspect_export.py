import json,struct
from pathlib import Path
p=Path(r'C:/Users/codex/Downloads/byte engine files/Cyberpunk Weapons v3/Cyberpunk_Pistol.glb');data=p.read_bytes();n=struct.unpack_from('<I',data,12)[0];j=json.loads(data[20:20+n]);print([(a['name'],len(a['channels'])) for a in j['animations']]);print([(i,node['name']) for i,node in enumerate(j['nodes']) if node.get('name') in ['Slide','WeaponMotion','Frame']]);print(len(j['skins']),len(j['skins'][0]['joints']))
