import json,struct
from pathlib import Path
p=Path(r'C:\Users\codex\Documents\Goblin Scraper\Assets\refinded parts\scrap_weapon_mount.glb');b=p.read_bytes();n=struct.unpack_from('<I',b,12)[0];j=json.loads(b[20:20+n]);print([(x.get('name'),x.get('rotation'),x.get('translation')) for x in j['nodes']]);print(j['animations'][0]['name'])
