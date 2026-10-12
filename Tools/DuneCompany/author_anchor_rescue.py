"""Author one new dual-winch puzzle, retaining the first two contracts and saves."""
import json,copy,math,datetime,shutil,uuid,importlib.util
from pathlib import Path
ROOT=Path('C:/Users/codex/Documents/DuneCompany')
spec=importlib.util.spec_from_file_location('levels',Path(__file__).with_name('author_starter_levels.py'))
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
BASE_HEIGHT,BASE_PACKING=m.height,m.packing
def height(x,z,winch=True):
    h=BASE_HEIGHT(x,z,True)
    # Replace the old site with a broad excavated basin, 8m below its rim.
    r=math.sqrt(((x-17)/13)**2+((z-73)/18)**2)
    basin=1-m.smooth((r-.42)/.58)
    if z>48:h=h*(1-basin)+(-4)*basin
    # One narrow access cut, bending around a bank at the bottom.
    d,y=m.route(x,z,[(12,50,4),(12,60,.4),(12,62,.4),(12,70,-4),(20,76,-4)])
    blend=1-m.smooth((d-2.8)/1.5)
    if z>=50:h=h*(1-blend)+y*blend
    # Broad stable staging area above the cut.
    staging=1-m.smooth((math.hypot(x-12,z-49)-5)/3)
    h=h*(1-staging)+4*staging
    return h
def packing(x,z,winch=True):
    if z>=54 and abs(x-12)<5:return .03
    if math.hypot(x-12,z-49)<7:return 1
    if z>60:return .08
    return BASE_PACKING(x,z,True)
def run(root):
    backup=root/'.byteengine/backups'/('anchor-rescue-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True)
    scene=json.loads((root/'Scenes/WinchRescue.bytescene').read_text(encoding='utf-8-sig'))
    scene['name']='Down With the Patient'
    # Existing scanned art is reused, never introduce untextured placeholder crates.
    scene['gameObjects']=[o for o in scene['gameObjects'] if not any(s in o['name'] for s in ['abandoned equipment crate','terrace retaining wall'])]
    for o in scene['gameObjects']:
        pos=o['transform']['localPosition'];scale=o['transform']['localScale']
        if o['name'].startswith(('Contract landscape /','Desert dressing /')):
            if pos['z']>48 and abs(pos['x']-12)<8:pos['x']+=14 if pos['x']>=12 else -14
            pos['y']=height(pos['x'],pos['z'])+scale['y']*.42
        for c in o.get('components',[]):
            p=c.get('properties',{})
            if c['type'].endswith('.DesertTerrain3D'):p.update(heightmapPath='Assets/ContractLevels/anchor-height.png',packedSurfaceMaskPath='Assets/ContractLevels/anchor-packed.png',heightmapHeight=18,baseHeight=-5,heightmapHash='',sandState='',sculptState='',autoLoadSavedSand=False,looseGrip=.3,packedGrip=1.1,looseResistance=.22)
            if c['type'].endswith('.DuneRecoveryContract3D'):
                o['transform']['localPosition']=m.vector(20,0,76);p.update(anchorMission=True,winchMission=True,title='Down With the Patient',payment=850,depressionDepth=0)
    # Anchor is a textured solid rock with a visible steel hook, not a floating prompt.
    rock=copy.deepcopy(next(o for o in scene['gameObjects'] if any('rock_09.obj' in c.get('properties',{}).get('modelPath','') for c in o.get('components',[]))))
    rock.update(id=str(uuid.uuid4()),name='Anchor rock / old steel eye',parentId=None)
    rock['transform']['localPosition']=m.vector(12,5.5,40);rock['transform']['localScale']=m.vector(3.8,3,3.2)
    scene['gameObjects'].append(rock)
    eye=m.object_('Anchor eye',12,6.4,41.5,.35,.35,.35,[.35,.38,.4,1]);eye['components'].append(dict(type='bytebard.dunecompany.DuneWinchAnchor3D',enabled=True,properties=dict(label='Steel eye')));eye['parentId']=rock['id'];eye['transform']['localPosition']=m.vector(0,.24,.48);eye['transform']['localScale']=m.vector(.35/3.8,.35/3,.35/3.2);scene['gameObjects'].append(eye)
    # Write the shared render/collision heightfield at a range that includes the deep floor.
    oldheight,oldpacking=m.height,m.packing
    # png() has the original [-3,13] range; map our [-5,13] to that encoding range.
    m.height=lambda x,z,w:(height(x,z)+5)*16/18-3
    m.packing=lambda x,z,w:packing(x,z)
    # Our functions use originals via module references, so keep explicit originals below.
    globals()['original_height']=oldheight;globals()['original_packing']=oldpacking
    folder=root/'Assets/ContractLevels'
    m.png(folder/'anchor-height.png',True);m.png(folder/'anchor-packed.png',True,True)
    m.height,m.packing=oldheight,oldpacking
    path=root/'Scenes/AnchorRescue.bytescene'
    if path.exists():shutil.copy2(path,backup/path.name)
    path.write_text(json.dumps(scene,indent=2))
    for name in ['Workshop','WinchRescue','AnchorRescue']:
        path=root/'Scenes'/f'{name}.bytescene';s=json.loads(path.read_text(encoding='utf-8-sig'));shutil.copy2(path,backup/(name+'-ui.bytescene'))
        if not any(o['name']=='Contract row anchor' for o in s['gameObjects']):
            row=copy.deepcopy(next(o for o in s['gameObjects'] if o['name']=='Contract row winch'));row.update(id=str(uuid.uuid4()),name='Contract row anchor');row['components'][0]['properties'].update(label='Down With the Patient / $850   +',offset=[27,208]);s['gameObjects'].append(row)
        path.write_text(json.dumps(s,indent=2))
    build=json.loads((root/'Assets/RecoveryBlueprints/Winch-rescue-truck.json').read_text())
    rail=copy.deepcopy(next(b for b in build if b['Id']==0));rail.update(Id=max(b['Id'] for b in build)+1,Parent=0,Position=[0,0,-2]);build.append(rail)
    winch=copy.deepcopy(next(b for b in build if b['Type']==33));winch.update(Id=max(b['Id'] for b in build)+1,Parent=rail['Id'],Position=[0,.5,-2.5],ControlKeys={'Attach':'J','Reel in':'U','Pay out':'O'});build.append(winch)
    next(b for b in build if b['Type']==33)['ControlKeys']={'Attach':'H','Reel in':'E','Pay out':'Q'}
    diagnostic=root/'.byteengine/Diagnostics';diagnostic.mkdir(parents=True,exist_ok=True)
    (diagnostic/'Anchor-rescue-truck.json').write_text(json.dumps(build,indent=2))
    # Keep the tested solution out of the player's blueprint library.
    visible=root/'Assets/RecoveryBlueprints/Anchor-rescue-truck.json'
    if visible.exists() and json.loads(visible.read_text())==build:visible.unlink()
    print('Authored AnchorRescue and third contract row; backups:',backup)
if __name__=='__main__':run(ROOT)
