import sys,json,re
from pathlib import Path
sys.path.insert(0,str(Path('Tools/Inspection/python').resolve()))
import UnityPy
root=Path(r'C:\Besiege\Besiege_Data');out=Path('output/goblin-scraper/besiege-parts-audit');out.mkdir(parents=True,exist_ok=True);(out/'reference-icons').mkdir(exist_ok=True)
env=UnityPy.load(str(root/'sharedassets0.assets'),str(root/'resources.assets'),str(root/'globalgamemanagers.assets'))
icons=[];prefabs=[];errors=[]
for obj in env.objects:
 if obj.type.name=='Texture2D':
  d=obj.read()
  if re.match(r'^\d+_',d.m_Name):
   name=re.sub(r'[^A-Za-z0-9_-]','_',d.m_Name);dest=out/'reference-icons'/f'{name}.png';d.image.save(dest);icons.append({'name':d.m_Name,'file':str(dest),'path_id':obj.path_id,'source':'sharedassets0.assets'})
 if obj.type.name=='MonoBehaviour':
  try:
   d=obj.read();script=d.m_Script.deref().read()
   if script.m_ClassName=='BlockPrefabContainer':
    try:tree=obj.read_typetree();prefabs.append(tree)
    except Exception as e:errors.append(str(e))
  except Exception:pass
(out/'installed-reference-index.json').write_text(json.dumps(icons,indent=2));(out/'serialized-prefabs.json').write_text(json.dumps(prefabs,indent=2,default=str))
print('Block icons:',len(icons),'prefabs:',len(prefabs),'errors:',errors[:2],flush=True)
