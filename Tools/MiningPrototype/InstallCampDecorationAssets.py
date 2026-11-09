from pathlib import Path
import shutil,json,uuid
W=Path(__file__).resolve().parents[2];P=Path('C:/Users/codex/Documents/palclone');source=W/'artifacts/camp-decorations';dest=P/'Assets/Models/Environment/CampDecorations';dest.mkdir(parents=True,exist_ok=True)
for asset in json.loads((source/'game-assets.json').read_text()):
 name=asset['asset']+'.glb';shutil.copy2(source/name,dest/name);meta=dest/(name+'.meta')
 if not meta.exists():meta.write_text(json.dumps({'guid':str(uuid.uuid5(uuid.NAMESPACE_URL,'hazard-pay/decorations/'+name)),'type':'Model3D'},indent=2))
art=P/'SourceArt/Decorated Camp';art.mkdir(parents=True,exist_ok=True)
for name in ['HazardPay-DecoratedCamp-Review.blend','layout.json','game-assets.json','Decoration-Library.png','Decorated-Camp-Overview.png','Workshop-Detail.png']:
 if (source/name).exists():shutil.copy2(source/name,art/name)
for name in ['BuildCampDecorations.py','AssembleDecoratedCamp.py','OptimizeCampDecorationExports.py']:
 shutil.copy2(W/'Tools/MiningPrototype'/name,art/name)
print('Installed 15 optimized static models and editable Blender sources.')
