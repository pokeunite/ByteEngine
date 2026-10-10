"""Install selected Poly Haven CC0 scans; keep garage approach open. No export."""
import json, struct, uuid, shutil, datetime, math, importlib.util
from pathlib import Path
ROOT=Path('C:/Users/codex/Documents/DuneCompany')
SOURCE=Path('.artifacts/desert-dressing-assets')

def install(root):
    folder=root/'Assets/ContractLevels'
    backup=root/'.byteengine/backups'/('selected-desert-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'))
    backup.mkdir(parents=True)
    spec=importlib.util.spec_from_file_location('levels',Path(__file__).with_name('author_starter_levels.py'))
    levels=importlib.util.module_from_spec(spec);spec.loader.exec_module(levels)
    template=json.loads((folder/'sandstone-0.obj.meta').read_text(encoding='utf-8-sig'))
    # Preserve original scan UVs/normals, normalize bounds for predictable scene sizing.
    for name in ['rock_07','rock_09']:
        src=SOURCE/name;g=json.loads((src/f'{name}.gltf').read_text());blob=(src/g['buffers'][0]['uri']).read_bytes()
        def read(i):
            a=g['accessors'][i];v=g['bufferViews'][a['bufferView']];n={'SCALAR':1,'VEC2':2,'VEC3':3}[a['type']]
            fmt={5126:'f',5123:'H',5125:'I'}[a['componentType']];size=struct.calcsize('<'+fmt*n)
            offset=v.get('byteOffset',0)+a.get('byteOffset',0)
            return [struct.unpack_from('<'+fmt*n,blob,offset+j*v.get('byteStride',size)) for j in range(a['count'])]
        p=g['meshes'][0]['primitives'][0];v=read(p['attributes']['POSITION']);uv=read(p['attributes']['TEXCOORD_0']);normal=read(p['attributes']['NORMAL']);indices=read(p['indices'])
        lo=[min(a[i] for a in v) for i in range(3)];hi=[max(a[i] for a in v) for i in range(3)];extent=[hi[i]-lo[i] for i in range(3)]
        lines=[f'mtllib {name}.mtl',f'o {name}',f'usemtl {name}']
        lines+=['v '+' '.join(str((a[i]-(lo[i]+hi[i])/2)/extent[i]) for i in range(3)) for a in v]
        lines+=['vt '+str(a[0])+' '+str(1-a[1]) for a in uv]
        # Inverse-transpose of the normalization scale.
        for a in normal:
            n=[a[i]*extent[i] for i in range(3)];length=math.sqrt(sum(t*t for t in n));lines.append('vn '+' '.join(str(t/length) for t in n))
        for i in range(0,len(indices),3):
            refs=[indices[j][0]+1 for j in range(i,i+3)];lines.append('f '+' '.join(f'{j}/{j}/{j}' for j in refs))
        (folder/f'{name}.obj').write_text('\n'.join(lines))
        shutil.copy2(src/f'textures/{name}_diff_2k.jpg',folder/f'{name}_diff_2k.jpg')
        (folder/f'{name}.mtl').write_text(f'newmtl {name}\nKd 1 1 1\nNs 4\nmap_Kd {name}_diff_2k.jpg\n')
        meta=json.loads(json.dumps(template));meta['guid']=str(uuid.uuid5(uuid.NAMESPACE_URL,'dune-company/'+name));meta['importer']['filter']='Linear';meta['modelImporter']['generateNormals']=False
        (folder/f'{name}.obj.meta').write_text(json.dumps(meta,indent=2))
    shutil.copy2(SOURCE/'qwantani_afternoon_puresky_2k.hdr',folder/'qwantani_afternoon_puresky_2k.hdr')
    (folder/'SELECTED-ASSETS-LICENSE.md').write_text('Poly Haven assets, CC0 1.0:\nhttps://polyhaven.com/license\nhttps://polyhaven.com/a/rock_07\nhttps://polyhaven.com/a/rock_09\nhttps://polyhaven.com/a/qwantani_afternoon_puresky\nScanned geometry converted to normalized OBJ, original UVs retained.\n')
    for sceneName,winch in [('Workshop',False),('WinchRescue',True)]:
        path=root/'Scenes'/f'{sceneName}.bytescene';shutil.copy2(path,backup/path.name);scene=json.loads(path.read_text(encoding='utf-8-sig'))
        objects=[];count=0
        for o in scene['gameObjects']:
            isRock=any(c['type']=='MeshRenderer' and 'sandstone-' in c.get('properties',{}).get('modelPath','') for c in o.get('components',[]))
            if isRock:
                pos=o['transform']['localPosition'];scale=o['transform']['localScale']
                # Remove the giant enclosure and all boulders near the garage/depot.
                if o['name'].startswith('Desert dressing / distant ridge') or math.hypot(pos['x'],pos['z'])<28:continue
                name=['rock_07','rock_09'][count%2];count+=1;guid=uuid.uuid5(uuid.NAMESPACE_URL,'dune-company/'+name)
                for c in o['components']:
                    if c['type']=='MeshRenderer':c['properties'].update(modelPath=f'Assets/ContractLevels/{name}.obj',modelGuid=str(guid),meshKey=f'{guid.hex}:mesh:{name}',materialKey=f'{guid.hex}:material:{name}',baseColor=[1,1,1,1],castShadows=True)
                # Scans remain boulders, not stretched cliff walls.
                width=min(5.5,scale['x']);depth=min(5,scale['z']);height=min(2.7,scale['y'])
                o['transform']['localScale']=dict(x=width,y=height,z=depth)
                pos['y']=levels.height(pos['x'],pos['z'],winch)+height*.42
            for c in o.get('components',[]):
                if c['type']=='SkyEnvironment':
                    c['properties'].update(environmentMapPath='Assets/ContractLevels/qwantani_afternoon_puresky_2k.hdr',environmentMapGuid=str(uuid.UUID(int=0)),environmentRotationDegrees=0,skyIntensity=.85,environmentIntensity=.7,exposure=1,ambientIntensity=.38,fogColor=[.72,.77,.8])
            objects.append(o)
        scene['gameObjects']=objects;path.write_text(json.dumps(scene,indent=2))
        print(sceneName, count,'scanned rocks; garage clear radius 28m')
    print('Backup:',backup)
if __name__=='__main__':install(ROOT)
