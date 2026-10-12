"""Repair parented anchors and author the four-contract canyon relay."""
import json,copy,math,shutil,datetime,uuid,importlib.util
from pathlib import Path
ROOT=Path('C:/Users/codex/Documents/DuneCompany')
spec=importlib.util.spec_from_file_location('anchor',Path(__file__).with_name('author_anchor_rescue.py'))
a=importlib.util.module_from_spec(spec);spec.loader.exec_module(a);m=a.m
ROUTE=[(0,12,0),(10,28,1),(-10,44,4),(10,60,1),(-10,78,-3)]
def height(x,z,w=True):
    if z<12:return a.BASE_HEIGHT(x,z,False)
    d,y=m.route(x,z,ROUTE)
    width=4.2
    for px,pz,py in ROUTE[1:]:
        if math.hypot(x-px,z-pz)<7:width=6
    bank=6*m.smooth((d-width)/3)
    return y+bank*m.smooth((z-12)/8)
def packing(x,z,w=True):
    d,y=m.route(x,z,ROUTE)
    return .08 if z>20 and d<6 else .8
def parent_eye(eye,rock):
    eye['parentId']=rock['id'];eye['name']='Steel eye / '+rock['name']
    eye['transform']['localPosition']=m.vector(0,.24,.48)
    eye['transform']['localRotation']=dict(x=0,y=0,z=0,w=1)
    scale=rock['transform']['localScale']
    eye['transform']['localScale']=m.vector(.35/scale['x'],.35/scale['y'],.35/scale['z'])
def run(root):
    backup=root/'.byteengine/backups'/('anchor-relay-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True)
    for name in ['anchor-height.png','relay-height.png','relay-packed.png']:
        asset=root/'Assets/ContractLevels'/name
        if asset.exists():shutil.copy2(asset,backup/name)
    path=root/'Scenes/AnchorRescue.bytescene';shutil.copy2(path,backup/path.name)
    deep=json.loads(path.read_text(encoding='utf-8-sig'))
    rock=next(o for o in deep['gameObjects'] if o['name']=='Anchor rock / old steel eye')
    eye=next(o for o in deep['gameObjects'] if any(c['type'].endswith('DuneWinchAnchor3D') for c in o.get('components',[])))
    rock['transform'].update(localPosition=m.vector(12,5.5,40),localRotation=dict(x=0,y=0,z=0,w=1))
    parent_eye(eye,rock)
    # Clear a generous turn-around above the descent, retaining the single anchor.
    deep['gameObjects']=[o for o in deep['gameObjects'] if o in [rock,eye] or not (o['name'].startswith(('Contract landscape /','Desert dressing /')) and math.hypot(o['transform']['localPosition']['x']-12,o['transform']['localPosition']['z']-47)<15)]
    oldheight,oldpack=m.height,m.packing
    def spacious(x,z,w=True):
        h=a.height(x,z);flat=1-m.smooth((math.hypot(x-12,z-43)-9)/3)
        return h*(1-flat)+4*flat
    m.height=lambda x,z,w:(spacious(x,z)+5)*16/18-3;m.packing=a.packing
    m.png(root/'Assets/ContractLevels/anchor-height.png',True)
    path.write_text(json.dumps(deep,indent=2))
    relay=copy.deepcopy(deep);relay['name']='Anchor Relay'
    relay['gameObjects']=[o for o in relay['gameObjects'] if o['name']!=rock['name'] and not any(c['type'].endswith('DuneWinchAnchor3D') for c in o.get('components',[])) and not o['name'].startswith(('Contract landscape /','Desert dressing /'))]
    for o in relay['gameObjects']:
        for c in o.get('components',[]):
            p=c.get('properties',{})
            if c['type'].endswith('DesertTerrain3D'):p.update(heightmapPath='Assets/ContractLevels/relay-height.png',packedSurfaceMaskPath='Assets/ContractLevels/relay-packed.png',heightmapHash='',sandState='',sculptState='',looseGrip=.22,looseResistance=.30,maximumRutDepth=.18)
            if c['type'].endswith('DuneRecoveryContract3D'):
                o['transform']['localPosition']=m.vector(-10,0,78);p.update(relayMission=True,anchorMission=True,winchMission=True,title='Anchor Relay',payment=1200,resetPosition=[-6,0,73])
    m.height=lambda x,z,w:(height(x,z)+5)*16/18-3;m.packing=packing
    m.png(root/'Assets/ContractLevels/relay-height.png',True);m.png(root/'Assets/ContractLevels/relay-packed.png',True,True)
    m.height,m.packing=oldheight,oldpack
    for index,(x,z) in enumerate([(12,57),(-12,41),(12,25)],1):
        r=copy.deepcopy(rock);r.update(id=str(uuid.uuid4()),name=f'Relay station {index}',parentId=None)
        r['transform']['localPosition']=m.vector(x,height(x,z)+1.2,z);r['transform']['localScale']=m.vector(2.6,2.4,2.6)
        e=copy.deepcopy(eye);e['id']=str(uuid.uuid4());e['components'][-1]['properties']['label']=f'Anchor station {index}';parent_eye(e,r)
        relay['gameObjects'].extend([r,e])
    # Scanned boulders decorate the upper banks, clear of the drivable cut.
    for index,z in enumerate(range(22,88,6)):
        for x in [-24,24]:
            r=copy.deepcopy(rock);r.update(id=str(uuid.uuid4()),name=f'Canyon bank rock {index} / {x}',parentId=None)
            r['transform']['localScale']=m.vector(4+index%3,2.5+index%2,4)
            r['transform']['localPosition']=m.vector(x,height(x,z)+1,z)
            relay['gameObjects'].append(r)
    out=root/'Scenes/AnchorRelay.bytescene'
    if out.exists():shutil.copy2(out,backup/out.name)
    out.write_text(json.dumps(relay,indent=2))
    for name in ['Workshop','WinchRescue','AnchorRescue','AnchorRelay']:
        p=root/'Scenes'/f'{name}.bytescene';shutil.copy2(p,backup/(name+'-ui.bytescene'));s=json.loads(p.read_text())
        if not any(o['name']=='Contract row relay' for o in s['gameObjects']):
            row=copy.deepcopy(next(o for o in s['gameObjects'] if o['name']=='Contract row anchor'));row.update(id=str(uuid.uuid4()),name='Contract row relay');row['components'][0]['properties'].update(label='Anchor Relay / $1200   +',offset=[27,246]);s['gameObjects'].append(row)
        p.write_text(json.dumps(s,indent=2))
    print('Fixed parented anchor, cleared approach, authored Anchor Relay; backup:',backup)
if __name__=='__main__':run(ROOT)
