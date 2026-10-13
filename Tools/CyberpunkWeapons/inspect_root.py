import json,struct
from pathlib import Path
p=Path(r'C:/Users/codex/Downloads/byte engine files/Cyberpunk Weapons v3/Cyberpunk_Pistol.glb');d=p.read_bytes();n=struct.unpack_from('<I',d,12)[0];j=json.loads(d[20:20+n])
for i,node in enumerate(j['nodes']):
 if node.get('name') in ['WeaponRoot','WeaponMotion','Frame','MuzzleSocket','Cyber_Pistol_Rig','RightHand','LeftHand']:print(i,node)
