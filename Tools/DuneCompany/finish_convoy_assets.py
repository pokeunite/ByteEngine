from pathlib import Path
from PIL import Image,ImageOps
import json,uuid,shutil
root=Path('C:/Users/codex/Documents/DuneCompany');fixture=Path('C:/Users/codex/ByteEngine/.artifacts/polish-project')
image=Image.open(fixture/'Preview/Convoy-field.png');image=ImageOps.fit(image,(768,200),centering=(.5,.65));target=root/'Assets/Presentation/contract-8.png';image.save(target)
Path(str(target)+'.meta').write_text(json.dumps({'type':'Texture2D','importer':{'filter':'Linear'},'guid':str(uuid.uuid5(uuid.NAMESPACE_URL,'dune-convoy-card'))},indent=2))
for name in ['Convoy-field.png','Convoy-overview.png','Convoy-combat.png']:shutil.copy2(fixture/'Preview'/name,root/'Docs/CombatConcepts'/name)
blocks=json.loads((root/'Assets/RecoveryBlueprints/Recovery-demo.json').read_text(encoding='utf-8-sig'))
for b in blocks:
 if b['Type'] in [23,24,34,38]:b['SpeedLimit']=32
for part,pos in [(30,[0,1.2,-.6]),(33,[0,.8,2.7])]:
 if not any(b['Type']==part for b in blocks):blocks.append({'Id':max(b['Id'] for b in blocks)+1,'Type':part,'Parent':0,'Position':pos,'Rotation':[0,0,0,1],'Power':1})
Path('.artifacts/convoy-browser-build.json').write_text(json.dumps(blocks))
print('Actual game thumbnail, level previews and private browser test vehicle prepared')
