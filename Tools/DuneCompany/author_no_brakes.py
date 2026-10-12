"""Replace the fourth contract with a physical runaway chase; preserve saves."""
import json,copy,math,shutil,datetime,uuid,importlib.util
from pathlib import Path
spec=importlib.util.spec_from_file_location('relay',Path(__file__).with_name('author_anchor_relay.py'))
r=importlib.util.module_from_spec(spec);spec.loader.exec_module(r);m=r.m
m.SIZE=288
UPPER=[(0,6,0),(0,36,6),(0,48,8),(0,82,12),(0,114,12)]
ESCAPE=[(0,6,0),(15,15,0),(18,32,1),(12,48,3),(10,64,10)]
def height(x,z,w=True):
    if z<10:return 0
    du,yu=m.route(x,z,UPPER);de,ye=m.route(x,z,ESCAPE)
    base=-5+.35*math.sin(x*.2)*math.sin(z*.1)
    upper=1-m.smooth((du-11)/3)
    # A real gap in the heightfield, not a painted obstacle.
    if 38<z<46:upper*=m.smooth((abs(z-42)-3.5)/.5)
    # Extra room for the load's swing near the upper anchor; preserve the bridge gap.
    escape_width=10+4*m.smooth((z-48)/8)
    lower=1-m.smooth((de-escape_width)/3)
    h=base*(1-upper)+yu*upper
    if lower>0:h=max(h,base*(1-lower)+ye*lower)
    # Flat landing/braking yard; generous space to catch the moving load.
    yard=1-m.smooth((math.hypot(x,z-6)-11)/5)
    h=h*(1-yard)
    return h
def packing(x,z,w=True):return .95 if min(m.route(x,z,UPPER)[0],m.route(x,z,ESCAPE)[0])<7 else .25
def run(root):
    backup=root/'.byteengine/backups'/('no-brakes-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True)
    path=root/'Scenes/AnchorRelay.bytescene';shutil.copy2(path,backup/path.name)
    s=json.loads(path.read_text(encoding='utf-8-sig'));s['name']='No Brakes'
    rock=copy.deepcopy(next(o for o in s['gameObjects'] if o['name'].startswith(('Relay station','Bridge-side anchor'))))
    eye=copy.deepcopy(next(o for o in s['gameObjects'] if any(c['type'].endswith('DuneWinchAnchor3D') for c in o.get('components',[]))))
    s['gameObjects']=[o for o in s['gameObjects'] if not o['name'].startswith(('Relay station','Bridge-side anchor','Yard anchor','Chase canyon /','Steel eye /','Canyon bank rock','Contract landscape /','Desert dressing /'))]
    for o in s['gameObjects']:
        for c in o.get('components',[]):
            p=c.get('properties',{})
            # Enlarge world coverage while retaining the previous coarse mesh count.
            # The moving contact field still samples and deforms at 25 cm.
            if c['type'].endswith('DesertTerrain3D'):p.update(cells=192,spacing=1.5,heightmapPath='Assets/ContractLevels/runaway-height.png',packedSurfaceMaskPath='Assets/ContractLevels/runaway-packed.png',heightmapHeight=18,baseHeight=-5,heightmapHash='',sandState='',sculptState='',looseGrip=.6,looseResistance=.10,contactRutDepth=.16,maximumRutDepth=.18)
            if c['type'].endswith('DuneRecoveryContract3D'):
                o['transform']['localPosition']=m.vector(-3,0,110);p.update(runawayMission=True,relayMission=False,anchorMission=True,winchMission=True,title='No Brakes',payment=1800,depressionDepth=0,resetPosition=[3,0,114],deliveryHalfWidth=7)
            if o['name']=='Contract row relay':p['label']='No Brakes / $1800   +'
    for name,mask in [('runaway-height.png',False),('runaway-packed.png',True)]:
        asset=root/'Assets/ContractLevels'/name
        if asset.exists():shutil.copy2(asset,backup/name)
        m.height=lambda x,z,w:(height(x,z)+5)*16/18-3;m.packing=packing;m.png(asset,True,mask)
    for i,(x,z,label) in enumerate([(14,61,'Bridge-side anchor'),(8,11,'Yard anchor')]):
        a=copy.deepcopy(rock);a.update(id=str(uuid.uuid4()),name=label,parentId=None)
        a['transform'].update(localPosition=m.vector(x,height(x,z)+1,z),localScale=m.vector(2.6,2.4,2.6))
        e=copy.deepcopy(eye);e['id']=str(uuid.uuid4());r.parent_eye(e,a)
        next(c for c in e['components'] if c['type'].endswith('DuneWinchAnchor3D'))['properties']['label']=label
        s['gameObjects'].extend([a,e])
    # Reuse scanned boulders. Keep the chase and lower landing lane clear.
    for i,z in enumerate(range(24,124,7)):
        for x in [-17,34]:
            a=copy.deepcopy(rock);a.update(id=str(uuid.uuid4()),name=f'Chase canyon / {i} / {x}',parentId=None)
            a['transform'].update(localPosition=m.vector(x,height(x,z)+1,z),localScale=m.vector(5+i%3,4,5))
            s['gameObjects'].append(a)
    for x in [-10,10]:
        for z in [37,47]:s['gameObjects'].append(m.object_('broken bridge warning post',x,height(x,z)+1,z,.18,2,.18,[.85,.52,.10,1]))
    path.write_text(json.dumps(s,indent=2))
    print('Authored No Brakes chase, real bridge gap, lower escape road and parented anchors. Backup:',backup)
if __name__=='__main__':run(Path('C:/Users/codex/Documents/DuneCompany'))
