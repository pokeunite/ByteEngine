"""Read-only installed block geometry audit. No original textures are exported."""
from pathlib import Path
import sys,json
sys.path.insert(0,str(Path('Tools/Inspection/python').resolve()))
import UnityPy
from UnityPy.helpers.MeshHelper import MeshHandler
source=Path(r'C:\Besiege\Besiege_Data')
out=Path('.artifacts/besiege-geometry-reference');out.mkdir(parents=True,exist_ok=True)
env=UnityPy.load(str(source/'level0'),str(source/'sharedassets0.assets'),str(source/'globalgamemanagers.assets'))
objects={o.path_id:o for o in env.objects if o.assets_file.name=='level0'}
audit=json.loads(Path('output/goblin-scraper/besiege-parts-audit/level0-prefabs-decoded.json').read_text(encoding='utf-8'))
selected=json.loads(Path('output/goblin-scraper/selected-contraption-parts.json').read_text(encoding='utf-8'))['parts'];ids={p['reference_id'] for p in selected}
def vector(v):return [float(v.x),float(v.y),float(v.z)]
def quat(v):return [float(v.x),float(v.y),float(v.z),float(v.w)]
def components(go):
 return [(c[1] if isinstance(c,tuple) else c.component).deref() for c in go.m_Component]
def transform(go):return next(o.read() for o in components(go) if o.type.name=='Transform')
meshes={};parts=[]
for record in audit:
 info=record.get('data',{}).get('Info',{});id=info.get('ID')
 if id not in ids:continue
 root=objects[info['gameObject']['m_PathID']].read();t=transform(root);nodes=[]
 def visit(tr,parent=-1,skip=False):
  go=tr.m_GameObject.deref().read();name=go.m_Name
  skip=skip or (parent>=0 and not getattr(go,'m_IsActive',True)) or any(term in name.lower() for term in ['shadow','brokenvis','directionarrow','rotationarrow','limitsvisual','bounds','collider','trigger','particle','fire','smoke','trail','raycast'])
  if skip:return
  node=dict(name=name,transform_id=tr.object_reader.path_id,parent=parent,position=vector(tr.m_LocalPosition),rotation=quat(tr.m_LocalRotation),scale=vector(tr.m_LocalScale));index=len(nodes);nodes.append(node)
  comps=components(go);renderer=next((o.read() for o in comps if o.type.name in ['MeshRenderer','SkinnedMeshRenderer']),None)
  filters=[o.read() for o in comps if o.type.name=='MeshFilter']
  if renderer and getattr(renderer,'m_Enabled',True):
   meshptr=filters[0].m_Mesh if filters else getattr(renderer,'m_Mesh',None)
   if meshptr and meshptr.path_id:
    mesh=meshptr.deref().read();key=str(mesh.object_reader.assets_file.name)+'-'+str(mesh.object_reader.path_id)
    if key not in meshes:
     h=MeshHandler(mesh);h.process();sub=h.get_triangles()
     meshes[key]=dict(name=mesh.m_Name,vertices=h.m_Vertices,uv=h.m_UV0 or [],submeshes=sub)
    node['mesh']=key;node['materials']=[m.deref().read().m_Name if m.path_id else 'Missing' for m in renderer.m_Materials]
    regions=[]
    for subindex,triangles in enumerate(meshes[key]['submeshes']):
     image=None
     if subindex<len(renderer.m_Materials) and renderer.m_Materials[subindex].path_id:
      mat=renderer.m_Materials[subindex].deref().read()
      for prop,tex in mat.m_SavedProperties.m_TexEnvs:
       if prop.name=='_MainTex' and tex.m_Texture.path_id:
        try:image=tex.m_Texture.deref().read().image.convert('RGB')
        except Exception:pass
     cats=[];uv=meshes[key]['uv']
     for tri in triangles:
      cat='iron'
      if image is not None and uv:
       u=sum(uv[v][0] for v in tri)/3;v=sum(uv[v][1] for v in tri)/3
       red,green,blue=image.getpixel((int((u%1)*(image.width-1)),int((1-v%1)*(image.height-1))))
       if red>green*1.18 and red>blue*1.30 and red>35:cat='wood'
       elif max(red,green,blue)>175:cat='steel'
      cats.append(cat)
     regions.append(cats)
    node['material_regions']=regions
  for child in tr.m_Children:visit(child.deref().read(),index,skip)
 visit(t)
 part=dict(id=id,name=info['name'],nodes=nodes,behavior=record.get('behavior_data',{}),info=info)
 parts.append(part);print(id,info['name'],'meshes',sum('mesh' in n for n in nodes),flush=True)
(out/'geometry.json').write_text(json.dumps(dict(source=str(source),parts=parts,meshes=meshes),separators=(',',':')),encoding='utf-8')
print('REFERENCE_GEOMETRY_COMPLETE',len(parts),len(meshes),flush=True)