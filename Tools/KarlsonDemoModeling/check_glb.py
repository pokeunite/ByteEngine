from pathlib import Path
import json,struct
for path in Path(r'C:/Users/codex/Downloads/byte engine files').glob('Karlson_*.glb'):
    data=path.read_bytes();length=struct.unpack_from('<I',data,12)[0];doc=json.loads(data[20:20+length])
    names=[a['name'] for a in doc.get('animations',[])]
    print(path.name,names,'skins',len(doc.get('skins',[])))
    assert set(names)=={'Idle','Fire','Reload','Equip'},names
    assert doc.get('skins')
