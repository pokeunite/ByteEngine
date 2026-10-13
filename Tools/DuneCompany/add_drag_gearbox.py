"""Targeted gearbox authoring and flat drag surface; preserves campaign saves."""
import json,shutil,uuid
from pathlib import Path
import numpy as np
from PIL import Image
ROOT=Path('C:/Users/codex/Documents/DuneCompany');REPO=Path('C:/Users/codex/ByteEngine')
backup=ROOT/'Backups/drag-gearbox-20261013';backup.mkdir(parents=True,exist_ok=True)
def update(path,text):
    path=Path(path)
    if path.exists():
        old=backup/path.relative_to(ROOT) if path.is_relative_to(ROOT) else backup/'plugin-source'/path.name
        old.parent.mkdir(parents=True,exist_ok=True)
        if not old.exists():shutil.copy2(path,old)
    path.write_text(text,encoding='utf-8')
def replace(file,a,b):
    p=REPO/file;s=p.read_text(encoding='utf-8-sig');assert a in s,file;update(p,s.replace(a,b))
replace('Plugins/DuneCompany/DuneWorkshop3D.Racing.cs','.Where(c=>c.Enabled).ToArray();ResetRaceFeedback();','.Where(c=>c.Enabled&&!c.DriveSurface).ToArray();ResetRaceFeedback();')
replace('Plugins/DuneCompany/DuneWorkshop3D.Ui.cs','p.Type switch{33 or 35 or 36=>','p.Type switch{39=>[("Final drive ratio",.5f,2f,"Power")],33 or 35 or 36=>')
replace('Plugins/DuneCompany/DuneWorkshop3D.Garage.cs','SelectedContract>=4?"Seat, engine and wheels / "+','SelectedContract==6?"Seat, engine, two wheels and gearbox required":SelectedContract>=4?"Seat, engine and wheels / "+')
replace('Plugins/DuneCompany/DuneWorkshop3D.cs','if(Building&&!ReadyToDrive){','if(Building&&DragRace&&!HasGearbox){_message="Fit a gearbox from Drive before driving on the drag strip.";_contractRun=false;return;}\n  if(Building&&!ReadyToDrive){')
replace('Plugins/DuneCompany/DuneWorkshop3D.MotorsportMenus.cs','A straight 400-metre run against a physical rival. Build for acceleration and traction.','A flat 400-metre asphalt run against a physical rival. A gearbox is compulsory. Fit it from Drive, then tune the final drive ratio for acceleration versus top speed.')
replace('Plugins/DuneCompany/LandVehiclePhysics.Assemblies.cs','public required PlacedBlock[] Engines;','public required PlacedBlock[] Engines;public required PlacedBlock[] Gearboxes;')
replace('Plugins/DuneCompany/LandVehiclePhysics.Assemblies.cs','Mass=parts.Sum','Gearboxes=parts.Select(p=>p.Block).Where(b=>b.Type==39).ToArray(),Mass=parts.Sum')
replace('Plugins/DuneCompany/LandVehiclePhysics.cs','engines.Max(b=>b.SpeedLimit));','engines.Max(b=>b.SpeedLimit));float finalDrive=FinalDriveRatio(rig.Gearboxes);power*=finalDrive;maximumSpeed/=finalDrive;')
replace('Plugins/DuneCompany/DuneWorkshop3D.Support.cs','34 or 38 or 25','34 or 38 or 39 or 25')
replace('Plugins/DuneCompany/DuneWorkshop3D.cs','34 or 38 or 25','34 or 38 or 39 or 25')
manifest=REPO/'Plugins/DuneCompany/plugin.json';m=json.loads(manifest.read_text());m['version']='0.24.0';update(manifest,json.dumps(m,indent=2)+'\n')
catalog=json.loads((ROOT/'Assets/DuneParts/catalog.json').read_text(encoding='utf-8-sig'))
assert not any(p['id']==39 for p in catalog)
catalog.append(dict(id=39,name='gearbox',label='Gearbox',file='Assets/DuneParts/glb/39_gearbox.glb',preview='Assets/DuneParts/cutouts/39_gearbox.png',group='Drive',function='Compulsory for drag races. Final drive ratio: higher gives more torque and less top speed. Multiple boxes do not stack. Mount to a structural rail or mechanical plate.',min=[-.25,0,-.35],max=[.25,.48,.35],mass=30,power=0,radius=.55,supportWidth=.5,supportLength=.5,price=0,tuningCost=0))
update(ROOT/'Assets/DuneParts/catalog.json',json.dumps(catalog,indent=2)+'\n');update(REPO/'Plugins/DuneCompany/Content/DuneParts/catalog.json',json.dumps(catalog,indent=2)+'\n')
scenePath=ROOT/'Scenes/DeadStraight.bytescene';scene=json.loads(scenePath.read_text(encoding='utf-8-sig'))
for o in scene['gameObjects']:
    for c in o['components']:
        if c['type'].endswith('DesertTerrain3D'):c['properties'].update(contactRutDepth=0,maximumRutDepth=0,sandState='',sculptState='',autoLoadSavedSand=False)
scene['gameObjects'].append(dict(id=str(uuid.uuid4()),name='Drag strip solid flat driving surface',active=True,tags=[],layer=0,parentId=None,transform=dict(localPosition=dict(x=2,y=-.03,z=-275),localRotation=dict(x=0,y=0,z=0,w=1),localScale=dict(x=1,y=1,z=1)),components=[dict(type='bytebard.dunecompany.DuneWorldObstacle3D',enabled=True,properties=dict(center=[0,0,0],size=[16,.3,590],driveSurface=True))],variables=[]))
update(scenePath,json.dumps(scene,indent=2)+'\n')
# A wide constant plateau includes tyre tracks, staging and the full braking area.
for filename,value in [('drag-height.png',4095),('drag-packed.png',65535)]:
    path=ROOT/'Assets/ContractLevels'/filename;shutil.copy2(path,backup/filename);data=np.array(Image.open(path)).astype(np.uint16);axis=np.linspace(-640,640,data.shape[0]);x,z=np.meshgrid(axis,axis);data[(x>=-10)&(x<=14)&(z>=-575)&(z<=25)]=value;Image.fromarray(data).save(path)
for suffix in ['', '-lod']:
    path=ROOT/f'Assets/RaceKit/drag-asphalt-road{suffix}.obj';s=path.read_text();lines=[]
    for line in s.splitlines():
        if line.startswith('v '):v=line.split();v[2]='0.12000';line=' '.join(v)
        lines.append(line)
    update(path,'\n'.join(lines)+'\n')
print('Flat strip, gearbox catalog and plugin changes authored; saves preserved.')
