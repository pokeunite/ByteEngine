from pathlib import Path
import sys,struct,json
sys.path.insert(0,str(Path('Tools/Inspection/python').resolve()));import UnityPy
from UnityPy.helpers.TypeTreeGenerator import TypeTreeGenerator
root=Path(r'C:\Besiege\Besiege_Data');env=UnityPy.load(str(root/'level0'),str(root/'sharedassets0.assets'),str(root/'globalgamemanagers.assets'))
obj=next(iter(env.objects));generator=TypeTreeGenerator(obj.assets_file.unity_version);generator.load_local_dll_folder(str(root/'Managed'));env.typetree_generator=generator
scripts={o.path_id:o.read().m_ClassName for o in env.objects if o.type.name=='MonoScript'};results=[]
from UnityPy.helpers.TypeTreeNode import TypeTreeNode
cache={}
def schema(cls):
 if cls in cache:return cache[cls]
 nodes=json.loads(generator.get_nodes_as_json('Assembly-CSharp.dll',cls))
 for i,n in enumerate(nodes):
  if n['m_Name']=='m_Enabled':n['m_MetaFlag']=16384
  if i+1<len(nodes) and nodes[i+1]['m_Type']=='Array' and nodes[i+1]['m_Level']==n['m_Level']+1 and i+3<len(nodes) and nodes[i+3]['m_Type']!='char':n['m_Type']='vector'
 cache[cls]=TypeTreeNode.from_list([TypeTreeNode(n['m_Level'],n['m_Type'],n['m_Name'],0,0,m_MetaFlag=n['m_MetaFlag']) for n in nodes]);return cache[cls]
node=schema('BlockPrefabContainer')
objects={o.path_id:o for o in env.objects if o.assets_file.name=='level0'}
for obj in env.objects:
 if obj.assets_file.name!='level0' or obj.type.name!='MonoBehaviour':continue
 raw=obj.get_raw_data();fi,pi=struct.unpack_from('<iq',raw,16)
 if scripts.get(pi)=='BlockPrefabContainer':
  try:
   tree=obj.read_typetree(node);info=tree['Info'];record={'path_id':obj.path_id,'data':tree}
   b=objects.get(info['blockBehaviour']['m_PathID'])
   if b:
    rawb=b.get_raw_data();_,scriptid=struct.unpack_from('<iq',rawb,16);cls=scripts.get(scriptid)
    record['actual_behavior_class']=cls
    if cls:
     try:record['behavior_data']=b.read_typetree(schema(cls))
     except Exception as e:record['behavior_decode_error']=str(e)
   go=objects.get(info['gameObject']['m_PathID'])
   if go:
    record['root_game_object']=go.read_typetree()
    for component in record['root_game_object']['m_Component']:
     ref=component[1] if isinstance(component,tuple) else component.get('component',component);other=objects.get(ref['m_PathID'])
     if other and other.type.name in ['Rigidbody','ConfigurableJoint','HingeJoint','FixedJoint','SpringJoint','CharacterJoint','BoxCollider','SphereCollider','CapsuleCollider']:
      record.setdefault('physics_components',[]).append({'type':other.type.name,'data':other.read_typetree()})
   results.append(record)
  except Exception as e:results.append({'path_id':obj.path_id,'error':str(e)})
Path('output/goblin-scraper/besiege-parts-audit/level0-prefabs-decoded.json').write_text(json.dumps(results,indent=2,default=str));print('Decoded',len(results),'prefab records;',sum('behavior_data' in r for r in results),'behavior records. Classes:',sorted({r.get('actual_behavior_class','ERROR') for r in results}),flush=True)

