from pathlib import Path
import json
scene=Path(r'C:\Users\codex\Documents\Goblin Scraper\Scenes\Main.bytescene')
backup=Path('.artifacts/goblin-free-before-publication');backup.mkdir(parents=True,exist_ok=True)
(backup/'Main.bytescene').write_bytes(scene.read_bytes())
doc=json.loads(scene.read_text(encoding='utf-8-sig'))
for obj in doc['gameObjects']:
    for comp in obj.get('components',[]):
        if comp['type']=='VehicleBuilder3D':comp['properties']['freeBuilding']=True
scene.write_text(json.dumps(doc,indent=2)+'\n',encoding='utf-8')
