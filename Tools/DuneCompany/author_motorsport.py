"""Three reference-led courses. Only new scenes/assets; preserves player saves and existing contracts."""
import copy,datetime,json,math,random,shutil,uuid
from pathlib import Path
import numpy as np
from PIL import Image,ImageDraw
ROOT=Path('C:/Users/codex/Documents/DuneCompany');OUT=ROOT/'Assets/RaceKit';LEVEL=ROOT/'Assets/ContractLevels'
def guid(s):return str(uuid.uuid5(uuid.NAMESPACE_URL,'dune-motorsport/'+s))
def meta(p,kind='Texture2D'):
    data={'guid':guid(str(p.relative_to(ROOT))),'type':kind}
    if kind=='Model3D':data['modelImporter']={'importScale':1,'generateNormals':False,'preferEmbeddedMaterials':True,'importAnimations':False,'rigType':'Generic'}
    Path(str(p)+'.meta').write_text(json.dumps(data,indent=2))
def vec(v):return dict(zip(('x','y','z'),map(float,v)))
def node(name,p=(0,0,0),scale=(1,1,1),yaw=0,components=None):
    return dict(id=guid(name),name=name,active=True,tags=[],layer=0,parentId=None,transform={'localPosition':vec(p),'localRotation':dict(x=0,y=math.sin(yaw/2),z=0,w=math.cos(yaw/2)),'localScale':vec(scale)},components=components or [],variables=[])
def comp(t,**p):return dict(type=t,enabled=True,properties=p)
def smooth(v):v=np.clip(v,0,1);return v*v*(3-2*v)
def nearest(x,z,route):
    distance=np.full(np.broadcast(x,z).shape,1e12,dtype=float);height=np.zeros_like(distance);along=np.zeros_like(distance);total=0
    for a,b in zip(route,route[1:]):
        dx,dz=b[0]-a[0],b[2]-a[2];length=math.hypot(dx,dz)
        if length<.001:continue
        t=np.clip(((x-a[0])*dx+(z-a[2])*dz)/(length*length),0,1);d=(x-a[0]-dx*t)**2+(z-a[2]-dz*t)**2;update=d<distance
        height=np.where(update,a[1]+(b[1]-a[1])*t,height);along=np.where(update,total+t*length,along);distance=np.minimum(distance,d);total+=length
    return np.sqrt(distance),height,along
def resample(points,step=3):
    points=np.array(points,float);lengths=np.r_[0,np.cumsum(np.linalg.norm(np.diff(points[:,[0,2]],axis=0),axis=1))]
    at=np.linspace(0,lengths[-1],math.ceil(lengths[-1]/step)+1)
    return np.stack([np.interp(at,lengths,points[:,i]) for i in range(3)],axis=1)
def catmull(points):
    arr=np.array(points,float);output=[]
    for i in range(len(arr)-1):
        a,b,c,d=arr[max(0,i-1)],arr[i],arr[i+1],arr[min(len(arr)-1,i+2)]
        for t in np.linspace(0,1,12,endpoint=False):output.append(.5*((2*b)+(-a+c)*t+(2*a-5*b+4*c-d)*t*t+(-a+3*b-3*c+d)*t*t*t))
    return np.array(output+[arr[-1]])
def base_scene(title):
    source=json.loads((ROOT/'Scenes/Workshop.bytescene').read_text(encoding='utf-8-sig'));keep={'Medium interactive sand field','Camera - Rover','Sun - Late Afternoon','Desert Sky and Dust Haze','Vehicle Workshop - Editable Build','Garage environment','Garage UX v2 canvas','Soft sky fill - vehicle readability'}
    ids={o['id'] for o in source['gameObjects'] if o['name'] in keep}
    while True:
        expanded=ids|{o['id'] for o in source['gameObjects'] if o.get('parentId') in ids}
        if expanded==ids:break
        ids=expanded
    source['gameObjects']=[o for o in source['gameObjects'] if o['id'] in ids];source['name']=title;source['sceneId']=guid(title)
    for o in source['gameObjects']:
        for c in o.get('components',[]):
            if c['type'].endswith('DuneWorkshop3D'):c['properties'].update(dustAmount=1,exhaustAmount=.7)
            if c['type']=='SkyEnvironment':c['properties'].update(fogDensity=.0006,fogMaxOpacity=.25)
    return source
def prop(scene,asset,x,z,yaw=0,scale=1,collision=False,y=None):
    i=len(scene['gameObjects']);s=(scale,scale,scale) if isinstance(scale,(float,int)) else scale
    p=(x,float(scene['_height'](x,z)) if y is None else y,z);cs=[comp('bytebard.dunecompany.DuneRaceProp3D',modelPath=f'Assets/RaceKit/{asset}.obj')]
    if collision:
        stats=json.loads((OUT/(asset+'-stats.json')).read_text());bounds=np.array([[a,c,-b] for a,b,c in stats['bounds']]);low=bounds.min(axis=0);high=bounds.max(axis=0)
        cs.append(comp('bytebard.dunecompany.DuneWorldObstacle3D',center=((low+high)*.5).tolist(),size=(high-low).tolist()))
    o=node(f"Motorsport / {scene['name']} / {asset} {i}",p,s,yaw,cs);scene['gameObjects'].append(o);return o
def terrain(scene,key,route,width,cells,spacing,rally=False):
    half=cells*spacing*.5
    def height(x,z):
        x,z=np.asarray(x),np.asarray(z);distance,h,along=nearest(x,z,route)
        base=1.5+1.1*np.sin(x*.032+z*.011)*np.cos(z*.043)+.6*np.sin(x*.11-z*.06)
        if rally:base+=32*smooth(-z/590)
        out=h+(base-h)*smooth((distance-width*.5)/10)
        out*=smooth((np.hypot(x,z)-10)/10)
        return out
    axis=np.linspace(-half,half,cells+1);x,z=np.meshgrid(axis,axis);d,h,a=nearest(x,z,route);heights=height(x,z);packing=.2+.79*(1-smooth((d-width*.5)/4));packing=np.maximum(packing,1-smooth((np.hypot(x,z)-10)/5))
    if key=='drag':
        plateau=(x>=-10)&(x<=14)&(z>=-575)&(z<=25);heights[plateau]=0;packing[plateau]=1
    Image.fromarray(np.uint16(np.clip((heights+4)/64,0,1)*65535)).save(LEVEL/(key+'-height.png'));Image.fromarray(np.uint16(packing*65535)).save(LEVEL/(key+'-packed.png'))
    for suffix in ['height','packed']:meta(LEVEL/(key+'-'+suffix+'.png'))
    for o in scene['gameObjects']:
        for c in o.get('components',[]):
            if c['type'].endswith(('DesertTerrain3D','InteractiveSand3D')):c['properties'].update(cells=cells,spacing=spacing,baseHeight=-4,heightmapHeight=64,heightmapPath=f'Assets/ContractLevels/{key}-height.png',packedSurfaceMaskPath=f'Assets/ContractLevels/{key}-packed.png',heightmapHash='',sandState='',sculptState='',autoLoadSavedSand=False,useDistanceLod=False,lodNearDistance=240,lodFarDistance=500,receiveShadowsDistance=100)
    # Match scenery and road exactly to the terrain's bilinear heightfield.
    quant=np.floor(np.clip((heights+4)/64,0,1)*65535)/65535*64-4
    def sampled(x,z):
        fx=np.clip((np.asarray(x)+half)/spacing,0,cells);fz=np.clip((np.asarray(z)+half)/spacing,0,cells);ix=np.minimum(fx.astype(int),cells-1);iz=np.minimum(fz.astype(int),cells-1);u=fx-ix;v=fz-iz
        return (quant[iz,ix]*(1-u)+quant[iz,ix+1]*u)*(1-v)+(quant[iz+1,ix]*(1-u)+quant[iz+1,ix+1]*u)*v
    scene['_height']=sampled;scene['_road']=route;scene['_width']=width;return sampled
def road(scene,key,points,width,material='gravel'):
    points=resample(points,2.0);lines=['# Original Dune Company course / CC0','o road','mtllib '+key+'-road.mtl'];normals=[];uv=[];cumulative=0
    for i,p in enumerate(points):
        d=points[min(i+1,len(points)-1)]-points[max(0,i-1)];d[1]=0;d/=max(.001,np.linalg.norm(d));side=np.array([-d[2],0,d[0]])
        if i:cumulative+=np.linalg.norm((p-points[i-1])[[0,2]])
        for sign in [-1,1]:
            q=p+side*width*.5*sign;q[1]=.12 if key=='drag-asphalt' else float(scene['_height'](q[0],q[2]))+.12;lines.append('v '+' '.join(f'{v:.5f}' for v in q));uv.append(f'vt {(sign+1)/2:.4f} {cumulative/6:.5f}')
    lines+=uv+['vn 0 1 0','usemtl Race_road']
    for i in range(len(points)-1):
        a=i*2+1;b=a+1;c=a+2;d=a+3
        lines.extend([f'f {a}/{a}/1 {b}/{b}/1 {c}/{c}/1',f'f {b}/{b}/1 {d}/{d}/1 {c}/{c}/1'])
    for suffix in ['','-lod']:
        path=OUT/(key+'-road'+suffix+'.obj');path.write_text('\n'.join(lines)+'\n');meta(path,'Model3D')
    (OUT/(key+'-road.mtl')).write_text(f'newmtl Race_road\nKd 1 1 1\nmap_Kd road-{material}.png\n')
    scene['gameObjects'].append(node('Motorsport / '+key+' road',components=[comp('bytebard.dunecompany.DuneRaceProp3D',modelPath=f'Assets/RaceKit/{key}-road.obj')]))
def decoration(scene,rng,area,count,cliffs=True):
    route=scene['_road'];half=scene['_width']*.5
    for i in range(count):
        x=rng.uniform(area[0],area[1]);z=rng.uniform(area[2],area[3]);d,_,_=nearest(x,z,route)
        if d<half+15 or math.hypot(x,z)<28:continue
        if cliffs and i%5==0:
            # Rock bounds remain beyond the road, including large collision proxies.
            scale=rng.uniform(1.3,3.8)
            if d<half+scale*6+6:continue
            prop(scene,'cliff-'+str(i%3+1),x,z,rng.uniform(0,math.tau),scale,True)
        else:prop(scene,'dry-bush',x,z,rng.uniform(0,math.tau),rng.uniform(.8,1.8))
def depot(scene,x,z):
    prop(scene,'workshop-shed',x,z,0,1,True);prop(scene,'container',x-8,z+2,.1,1,True);prop(scene,'service-canopy',x+9,z+3,0,1,False);prop(scene,'fuel-tank',x-5,z-7,.3,1,True)
    for i in range(5):prop(scene,'barrel',x+6+i*.65,z-4,0,1,True);prop(scene,'utility-crate',x-8+i*1.1,z-4,0,1,True)
    prop(scene,'service-mast',x+5,z-7,0,1,True)
def save(scene,key,info):
    scene.pop('_height');scene.pop('_road');scene.pop('_width');path=ROOT/'Scenes'/f'{key}.bytescene'
    if path.exists():
        backup=ROOT/'Backups'/('motorsport-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True,exist_ok=True);shutil.copy2(path,backup/path.name)
    path.write_text(json.dumps(scene,indent=2)+'\n');meta(path,'Scene');(ROOT/'Docs/RaceConcepts'/f'{key}-layout.json').write_text(json.dumps(info,indent=2));print(key,len(scene['gameObjects']),'objects',flush=True)
def textures():
    rng=np.random.default_rng(318);y,x=np.mgrid[:1024,:1024]
    for kind,base in [('asphalt',[67,68,62]),('gravel',[133,119,93])]:
        a=np.zeros((1024,1024,3))+base;noise=rng.normal(0,7.5,(1024,1024))+np.sin(x*.16)*2+np.sin(y*.019+x*.011)*4;a+=noise[...,None]
        for centre in [240,270,720,750]:a-=np.exp(-((x-centre)/7)**2)[...,None]*(7 if kind=='asphalt' else 12)
        im=Image.fromarray(np.uint8(np.clip(a,0,255)));d=ImageDraw.Draw(im)
        if kind=='asphalt':
            for xx in [40,984]:d.line([(xx,0),(xx,1024)],fill=(179,169,139),width=8)
            d.line([(512,0),(512,430)],fill=(191,182,156),width=9)
        else:
            for centre in [260,320,675,735]:
                points=[(int(centre+30*math.sin(yy*.005+centre)),yy) for yy in range(0,1024,4)]
                d.line(points,fill=(72,67,56),width=16)
                d.line([(xx+20,yy) for xx,yy in points],fill=(111,99,77),width=6)
        p=OUT/('road-'+kind+'.png');im.save(p);meta(p)
def drift():
    scene=base_scene('Dust Bowl Drift');main=[[58*math.sin(a),0,-102+68*math.cos(a)] for a in np.linspace(0,math.tau,129)]
    eight=[[35*math.sin(a),0,-52+17*math.sin(2*a)] for a in np.linspace(0,math.tau,129)]
    entrance=[[0,0,0],[0,0,-24],[-21,0,-34],[-36,0,-51]];combined=main+entrance+eight
    terrain(scene,'drift',combined,14,512,1.5);road(scene,'drift-oval',main,14);road(scene,'drift-eight',eight,12);road(scene,'drift-entry',entrance,14)
    scene['gameObjects'].append(node('Drift rules',components=[comp('bytebard.dunecompany.DuneDriftContract3D',duration=120,targetScore=6000)]))
    # Central mesa follows the reference's elongated island; southern islands leave the crossing open.
    for i,(x,z) in enumerate([(-12,-115),(5,-112),(18,-121),(-4,-131)]):prop(scene,'cliff-'+str(i%3+1),x,z,i*.4,(2.1,2.6,1.8),True)
    for side in [-1,1]:prop(scene,'cliff-2',side*19,-52,side*.3,(1.25,1.0,1.0),True)
    for i,a in enumerate(np.linspace(0,math.tau,88,endpoint=False)):
        for edge in [-1,1]:
            x=(58+edge*9.0)*math.sin(a);z=-102+(68+edge*9.0)*math.cos(a)
            if edge==-1 and z>-79:continue
            if edge==1 and abs(x)<18 and z>-47:continue
            tangent=np.array([58*math.cos(a),0,-68*math.sin(a)]);yaw=math.atan2(-tangent[2],tangent[0]);asset='tyre-barrier' if i%3!=0 else 'traffic-cone';prop(scene,asset,x,z,yaw,.8 if asset=='tyre-barrier' else 1,asset=='tyre-barrier')
    for side in [-1,1]:
        for a in np.linspace(0,math.tau,12,endpoint=False):prop(scene,'loose-tyre',side*19+9*math.cos(a),-52+8*math.sin(a),0,1.15,False)
    for x,z,angle in [(-69,-95,-math.pi*.5),(67,-125,math.pi*.5),(0,-179,0)]:prop(scene,'chevron-sign',x,z,angle,1.3,True)
    depot(scene,-57,-20);prop(scene,'quarry-conveyor',25,-189,1.1,1.3,True);prop(scene,'silo',44,-185,0,1.2,True);prop(scene,'container',-22,-187,.05,1,True);prop(scene,'service-mast',-38,-158,0,1.1,True)
    decoration(scene,random.Random(51),(-150,150,-270,60),170)
    save(scene,'DustBowlDrift',dict(duration=120,targetScore=6000,ovalWidth=14,figureEightWidth=12,start=[0,0,0],centralIsland=[0,0,-120]))
def drag():
    scene=base_scene('Dead Straight');main=[[2,0,0],[2,0,-400],[2,0,-560]];returning=[[2,0,-560],[26,0,-560],[26,0,15],[2,0,15]]
    terrain(scene,'drag',main+returning,16,512,2.5);road(scene,'drag-asphalt',main,16,'asphalt');road(scene,'drag-return',returning,8)
    # Asphalt is a rigid, level surface. Preserve that behavior when reauthoring the course.
    scene['gameObjects'].append(node('Drag strip solid flat driving surface',(2,-.03,-275),components=[comp('bytebard.dunecompany.DuneWorldObstacle3D',size=[16,.3,590],driveSurface=True)]))
    for obj in scene['gameObjects']:
        for component in obj['components']:
            if component['type'].endswith('DesertTerrain3D'):component['properties'].update(contactRutDepth=0,maximumRutDepth=0)
    driver=node('Drag rival lane',components=[comp('RoutePath3D',waypointsJson=json.dumps([[4,.8,0],[4,.8,-150],[4,.8,-300],[4,.8,-415],[4,.8,-550]]),corridorWidth=4,gateRadius=5)])
    route=node('Drag route and rules',components=[comp('RoutePath3D',waypointsJson=json.dumps([[2,.05,0],[2,.05,-400]]),corridorWidth=12,gateRadius=5),comp('bytebard.dunecompany.DuneSprintContract3D',contractIndex=6,raceKind='Drag',opponentSpeed=28,opponentBlueprint='Assets/RecoveryBlueprints/Recovery-demo.json',drivingRouteObject=driver['id'])]);scene['gameObjects'] += [route,driver]
    prop(scene,'finish-gantry',2,-400,0,1,False);tree=prop(scene,'launch-tree',2,-6,0,1,False);tree['components'].append(comp('bytebard.dunecompany.DuneLaunchTree3D'))
    for z in range(8,-555,-12):
        for x in [-7,11]:prop(scene,'traffic-cone',x,z,0,1)
    for z in [-30,-160,-330,-410,-540]:
        for x in [-10,14]:prop(scene,'route-flag',x,z,0,1.1)
    for x in [-9,13]:
        for z in [8,-15,-100,-260,-410]:prop(scene,'tyre-barrier',x,z,math.pi*.5,.8,True)
    depot(scene,-30,1);prop(scene,'container',-34,-35,.2,1,True);prop(scene,'service-canopy',-19,-21,0,1,False)
    decoration(scene,random.Random(67),(-130,145,-610,55),300)
    save(scene,'DeadStraight',dict(raceDistance=400,brakingDistance=160,laneCentres=[0,4],finish=[2,0,-400],runoffEnd=-560,earlyThrottleDelay=.75))
def rally():
    scene=base_scene('Red Rock Rally');raw=[[x,0,z] for x,z in [(0,0),(12,-40),(-12,-80),(35,-120),(70,-150),(35,-185),(20,-225),(60,-260),(100,-280),(75,-320),(115,-350),(140,-380),(110,-410),(80,-405),(110,-450),(170,-480)]]
    route=catmull(raw);length=np.linalg.norm(np.diff(route[:,[0,2]],axis=0),axis=1).sum();route[:,[0,2]]*=900/length;route=resample(route,4);arc=np.r_[0,np.cumsum(np.linalg.norm(np.diff(route[:,[0,2]],axis=0),axis=1))]
    route[:,1]=32*smooth(arc/900)+1.35*np.exp(-((arc-565)/6.5)**2);route[:,1]*=smooth(arc/22)
    terrain(scene,'rally',route.tolist(),12,512,2.5,True);road(scene,'rally-gravel',route,12)
    gateArc=np.array([100,235,385,560,745,900]);gates=np.stack([np.interp(gateArc,arc,route[:,i]) for i in range(3)],axis=1);gates[:,1]=scene['_height'](gates[:,0],gates[:,2])+.05
    dense=route.copy();dense[:,1]=scene['_height'](dense[:,0],dense[:,2])+.8
    driver=node('Rally driving route',components=[comp('RoutePath3D',waypointsJson=json.dumps(dense.tolist()),corridorWidth=12,gateRadius=5)])
    rules=node('Rally gates and rules',components=[comp('RoutePath3D',waypointsJson=json.dumps(gates.tolist()),corridorWidth=12,gateRadius=5),comp('bytebard.dunecompany.DuneSprintContract3D',contractIndex=7,raceKind='Rally',opponentSpeed=18,opponentBlueprint='Assets/RecoveryBlueprints/Recovery-demo.json',drivingRouteObject=driver['id'])]);scene['gameObjects']+= [rules,driver]
    for i,p in enumerate(route):
        if i%6:continue
        direction=route[min(i+1,len(route)-1)]-route[max(0,i-1)];direction[1]=0;direction/=np.linalg.norm(direction);side=np.array([-direction[2],0,direction[0]])
        for sign in [-1,1]:
            q=p+side*sign*9;prop(scene,'route-flag',q[0],q[2],math.atan2(-direction[0],-direction[2]),.9)
        if i%18==0 and i>4:
            q=p+side*12;prop(scene,'chevron-sign',q[0],q[2],math.atan2(-direction[0],-direction[2]),1.25,True)
    # Layered rock walls at the pass, crest and upper hairpin, outside the collision corridor.
    for j,s in enumerate([280,320,370,415,450,510,600,670,725,795,840]):
        i=int(np.searchsorted(arc,s));p=route[i];direction=route[min(i+1,len(route)-1)]-route[max(0,i-1)];direction[1]=0;direction/=np.linalg.norm(direction);side=np.array([-direction[2],0,direction[0]])
        for sign in [-1,1]:
            q=p+side*(24+j%3*4)*sign
            if nearest(q[0],q[2],route)[0]<23:continue
            prop(scene,'cliff-'+str(j%3+1),q[0],q[2],j*.6,(2.5,3.0+(j%3)*.5,2.5),True)
    depot(scene,-30,0);end=route[-1];depot(scene,end[0]+25,end[2]);prop(scene,'finish-gantry',end[0],end[2],math.atan2(-(end[0]-route[-2,0]),-(end[2]-route[-2,2])),1,False)
    decoration(scene,random.Random(79),(-150,400,-630,80),550)
    save(scene,'RedRockRally',dict(distance=float(arc[-1]),roadWidth=12,climb=32,gates=gates.tolist(),aiWaypoints=len(dense),crestArc=565,finish=end.tolist()))
if __name__=='__main__':
    for p in OUT.glob('*.obj'):meta(p,'Model3D')
    textures();drift();drag();rally()
