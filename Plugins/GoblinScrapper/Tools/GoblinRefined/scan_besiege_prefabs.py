import sys,struct,json
from pathlib import Path
sys.path.insert(0,str(Path('Tools/Inspection/python').resolve()));import UnityPy
root=Path(r'C:\Besiege\Besiege_Data');out=Path('output/goblin-scraper/besiege-parts-audit');scripts={o.path_id:o.read().m_ClassName for o in UnityPy.load(str(root/'globalgamemanagers.assets')).objects if o.type.name=='MonoScript'}
found=[]
for file in sorted(list(root.glob('*.assets'))+list(root.glob('level[0-9]*'))):
 env=UnityPy.load(str(file))
 for obj in env.objects:
  if obj.type.name!='MonoBehaviour':continue
  raw=obj.get_raw_data()
  if len(raw)<28:continue
  fileid,pathid=struct.unpack_from('<iq',raw,16)
  if scripts.get(pathid)=='BlockPrefabContainer':
   try:tree=obj.read_typetree();result={'file':file.name,'path_id':obj.path_id,'data':tree}
   except Exception as e:result={'file':file.name,'path_id':obj.path_id,'error':str(e),'raw_hex':raw.hex()}
   found.append(result)
 print(file.name,'containers',len(found),flush=True) if found else None
(out/'prefab-container-records.json').write_text(json.dumps(found,indent=2,default=str));print('Total containers',len(found),flush=True)

