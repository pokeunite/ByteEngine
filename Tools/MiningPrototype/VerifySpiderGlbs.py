import json,struct,math
from pathlib import Path
root=Path('artifacts/spider-models');report=[]
for kind in ['PlayerHunter','DrillBreaker']:
 file=root/(kind+'-Unrigged.glb');raw=file.read_bytes();magic,ver,total=struct.unpack_from('<III',raw);assert magic==0x46546c67 and ver==2 and total==len(raw)
 size,typ=struct.unpack_from('<II',raw,12);doc=json.loads(raw[20:20+size]);assert not doc.get('skins') and not doc.get('animations');assert len(doc['meshes'])==1;assert len([n for n in doc['nodes'] if 'mesh' in n])==1
 bin=raw[28+size:];triangles=0;vertices=0;low=1000
 for prim in doc['meshes'][0]['primitives']:
  a=doc['accessors'][prim['attributes']['POSITION']];view=doc['bufferViews'][a['bufferView']];start=view.get('byteOffset',0)+a.get('byteOffset',0);stride=view.get('byteStride',12);points=[struct.unpack_from('<fff',bin,start+i*stride) for i in range(a['count'])];assert all(math.isfinite(x) for p in points for x in p);low=min(low,min(p[1] for p in points));vertices+=len(points);triangles+=doc['accessors'][prim['indices']]['count']//3;assert 'JOINTS_0' not in prim['attributes']
 core=json.loads((root/(kind+'-upload-core.json')).read_text());assert core['closed'] and core['bodyAndEightLegsConnected']
 report.append({'file':file.name,'triangles':triangles,'verticesWithMaterialSplits':vertices,'lowestY':low,'unrigged':True,'singleMesh':True,'walkingLegs':8,'connectedCore':True,'materials':len(doc['materials'])});print('PASS',file.name,triangles,'triangles, unrigged, one mesh, eight connected legs')
(root/'gltf-verification.json').write_text(json.dumps(report,indent=2))
