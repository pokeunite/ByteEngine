"""Repeatable presentation pass. Backs up project scenes/art; never edits saves or exports.
Original procedural textures/merged scenery use existing licensed rock scans and sky.
"""
import argparse, copy, datetime, hashlib, io, json, math, random, shutil, struct, uuid
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT=Path('C:/Users/codex/Documents/DuneCompany')
OUT=ROOT/'Assets/Presentation'
NAMES=['Workshop','WinchRescue','AnchorRescue','AnchorRelay','ScrapyardSprint']
TITLES=['DRY RIVERBED','OLD EXCAVATION','SURVEY PIT','BROKEN BRIDGE','SCRAPYARD SPRINT']
FONT=ROOT/'Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf'
def guid(name):return str(uuid.uuid5(uuid.NAMESPACE_URL,'dune-presentation/'+name))
def write_meta(path,kind='Texture2D'):
    meta=Path(str(path)+'.meta')
    if not meta.exists():
        p={'guid':guid(path.name),'type':kind}
        if kind=='Model3D':
            p=json.loads((ROOT/'Assets/ContractLevels/rock_07.obj.meta').read_text(encoding='utf-8-sig'));p['guid']=guid(path.name);p.setdefault('modelImporter',{})['generateNormals']=True
        meta.write_text(json.dumps(p,indent=2))
def textures():
    rng=np.random.default_rng(42);y,x=np.mgrid[:512,:512]
    steel=np.zeros((512,512,3));steel[:]=[105,112,105]
    steel+=(np.sin(x*math.tau/32)*5+rng.normal(0,1.6,x.shape))[...,None]
    rust=((np.sin(x*.07+np.sin(y*.04))*np.sin(y*.09)> .88)&((y>410)|(x%128<8)))
    steel[rust]=[99,78,53]
    im=Image.fromarray(np.uint8(np.clip(steel,0,255)));d=ImageDraw.Draw(im)
    for xx in range(8,512,128):
        for yy in [22,252,482]:d.ellipse((xx-2,yy-2,xx+2,yy+2),fill=(54,58,54))
    im.save(OUT/'galvanized-steel.png')
    concrete=np.zeros((512,512,3));concrete[:]=[117,115,103]
    concrete+=(rng.normal(0,2,x.shape)+np.sin(x*.035)*2+np.cos(y*.025)*2)[...,None]
    concrete-=((np.exp(-((x-190)**2+(y-310)**2)/1500)*16))[...,None]
    Image.fromarray(np.uint8(np.clip(concrete,0,255))).save(OUT/'workshop-concrete.png')
    paint=np.zeros((512,512,3));paint[:]=[86,93,73];paint+=rng.normal(0,1.2,x.shape)[...,None]
    paint[(x%128<3)|(y%128<3)]=[57,60,50]
    Image.fromarray(np.uint8(np.clip(paint,0,255))).save(OUT/'equipment-paint.png')
    atlas=Image.new('RGB',(512,512),(40,45,39));d=ImageDraw.Draw(atlas)
    labels=['DUNE COMPANY','RECOVERY DEPOT','EXCAVATION / KEEP CLEAR','BRIDGE CLOSED','SCRAPYARD SPRINT','SERVICE BAY','DANGER / DROP','RECOVERY OPERATIONS']
    for i,label in enumerate(labels):
        top=i*64;d.rectangle((2,top+2,509,top+61),outline=(214,159,68),width=3)
        font=ImageFont.truetype(str(FONT),27 if len(label)>20 else 34)
        d.text((256,top+30),label,font=font,fill=(237,226,193),anchor='mm')
    atlas.save(OUT/'wayfinding-signs.png')
    Image.new('RGB',(32,32),(199,153,67)).save(OUT/'bay-markings.png')
    yy,xx=np.mgrid[:64,:64];alpha=np.uint8(np.clip(1-np.sqrt(((xx-31.5)/31.5)**2+((yy-31.5)/31.5)**2),0,1)**1.5*255)
    rgba=np.zeros((64,64,4),dtype=np.uint8);rgba[:,:,3]=alpha;Image.fromarray(rgba).save(OUT/'contact-shadow.png')
    for i,title in enumerate(TITLES):
        im=Image.new('RGB',(768,200),(30,35,31));d=ImageDraw.Draw(im)
        for j in range(7):
            line=[(k,155-j*13+math.sin(k*.012+j+i)*13) for k in range(0,769,8)]
            d.line(line,fill=(57+j*4,59+j*3,48+j*2),width=2)
        d.rectangle((20,20,25,179),fill=(232,159,52));d.text((44,42),f'FIELD FILE / {i+1:02}',font=ImageFont.truetype(str(FONT),21),fill=(191,177,143))
        d.text((44,82),title,font=ImageFont.truetype(str(FONT),40),fill=(241,233,208))
        d.text((44,145),'DUNE COMPANY  /  OPERATIONS',font=ImageFont.truetype(str(FONT),18),fill=(176,176,154))
        im.save(OUT/f'contract-{i}.png')
    for p in OUT.glob('*.png'):write_meta(p)

def update_garage_glb(path):
    raw=path.read_bytes();size,kind=struct.unpack_from('<II',raw,12);data=json.loads(raw[20:20+size]);start=20+size;length,_=struct.unpack_from('<II',raw,start);blob=bytearray(raw[start+8:start+8+length])
    for material in data.get('materials',[]):
        name=material.get('name','');p=material.setdefault('pbrMetallicRoughness',{})
        texture='galvanized-steel.png' if 'galvanized' in name.lower() else 'workshop-concrete.png' if 'concrete' in name.lower() else None
        if texture:
            while len(blob)%4:blob.append(0)
            image=(OUT/texture).read_bytes();view=len(data.setdefault('bufferViews',[]));data['bufferViews'].append({'buffer':0,'byteOffset':len(blob),'byteLength':len(image)});blob.extend(image)
            idx=len(data.setdefault('images',[]));data['images'].append({'name':texture,'mimeType':'image/png','bufferView':view});tex=len(data.setdefault('textures',[]));data['textures'].append({'source':idx})
            p.update(baseColorTexture={'index':tex},baseColorFactor=[1,1,1,1],roughnessFactor=.78 if 'steel' in texture else .92,metallicFactor=.35 if 'steel' in texture else 0)
        if 'oxidized' in name.lower():p.update(baseColorFactor=[.30,.27,.20,1],roughnessFactor=.8,metallicFactor=.5)
    while len(blob)%4:blob.append(0)
    # Remove superseded embedded images so repeated authoring does not grow the GLB.
    original_image_views={im['bufferView'] for im in data.get('images',[]) if 'bufferView' in im}
    used_textures=set()
    def visit_textures(value,remap=None):
        if isinstance(value,dict):
            for key,item in value.items():
                if key.endswith('Texture') and isinstance(item,dict) and 'index' in item:
                    if remap is None:used_textures.add(item['index'])
                    else:item['index']=remap[item['index']]
                visit_textures(item,remap)
        elif isinstance(value,list):
            for item in value:visit_textures(item,remap)
    visit_textures(data.get('materials',[]));texture_map={old:new for new,old in enumerate(sorted(used_textures))}
    data['textures']=[data['textures'][i] for i in sorted(used_textures)];visit_textures(data.get('materials',[]),texture_map)
    used_images=sorted({t['source'] for t in data['textures']});image_map={old:new for new,old in enumerate(used_images)}
    data['images']=[data['images'][i] for i in used_images]
    for t in data['textures']:t['source']=image_map[t['source']]
    image_views={im['bufferView'] for im in data['images'] if 'bufferView' in im};views=[];view_map={};new_blob=bytearray()
    for i,view in enumerate(data['bufferViews']):
        if i in original_image_views and i not in image_views:continue
        while len(new_blob)%4:new_blob.append(0)
        start=view.get('byteOffset',0);view=copy.deepcopy(view);view_map[i]=len(views);view['byteOffset']=len(new_blob);new_blob.extend(blob[start:start+view['byteLength']]);views.append(view)
    def remap_views(value):
        if isinstance(value,dict):
            for key,item in value.items():
                if key=='bufferView':value[key]=view_map[item]
                elif key!='bufferViews':remap_views(item)
        elif isinstance(value,list):
            for item in value:remap_views(item)
    remap_views(data);data['bufferViews']=views;blob=new_blob
    while len(blob)%4:blob.append(0)
    data['buffers'][0]['byteLength']=len(blob);js=json.dumps(data,separators=(',',':')).encode();js+=b' '*((-len(js))%4)
    path.write_bytes(struct.pack('<III',0x46546c67,2,12+8+len(js)+8+len(blob))+struct.pack('<II',len(js),0x4e4f534a)+js+struct.pack('<II',len(blob),0x004e4942)+blob)

class Details:
    def __init__(self,name):self.name=name;self.vertices=[];self.uv=[];self.faces=[]
    def box(self,p,s,angle=0,sign=None):
        cx,cy,cz=p;sx,sy,sz=s;points=[(-1,-1,-1),(1,-1,-1),(1,1,-1),(-1,1,-1),(-1,-1,1),(1,-1,1),(1,1,1),(-1,1,1)]
        points=[(cx+a*sx/2*math.cos(angle)+c*sz/2*math.sin(angle),cy+b*sy/2,cz-a*sx/2*math.sin(angle)+c*sz/2*math.cos(angle)) for a,b,c in points]
        for face_index,face in enumerate([(0,3,2,1),(4,5,6,7),(0,4,7,3),(1,2,6,5),(0,1,5,4),(3,7,6,2)]):
            start=len(self.vertices);self.vertices.extend(points[i] for i in face)
            v0,v1=(0,1) if sign is None else (1-(sign+1)/8,1-sign/8)
            self.uv.extend([(1,v0),(1,v1),(0,v1),(0,v0)] if face_index==0 else [(0,v0),(1,v0),(1,v1),(0,v1)]);self.faces.append((start+1,start+2,start+3));self.faces.append((start+1,start+3,start+4))
    def cylinder(self,p,r,h,n=12):
        cx,cy,cz=p
        for i in range(n):
            a,b=i*math.tau/n,(i+1)*math.tau/n;start=len(self.vertices)
            self.vertices.extend([(cx+r*math.cos(a),cy-h/2,cz+r*math.sin(a)),(cx+r*math.cos(a),cy+h/2,cz+r*math.sin(a)),(cx+r*math.cos(b),cy+h/2,cz+r*math.sin(b)),(cx+r*math.cos(b),cy-h/2,cz+r*math.sin(b))])
            self.uv.extend([(i/n,0),(i/n,1),((i+1)/n,1),((i+1)/n,0)]);self.faces.extend([(start+1,start+2,start+3),(start+1,start+3,start+4)])
    def write(self,texture):
        path=OUT/(self.name+'.obj');lines=[f'mtllib {self.name}.mtl',f'o {self.name}',f'usemtl {self.name}']
        lines+=['v '+' '.join(f'{x:.5f}' for x in p) for p in self.vertices];lines+=['vt '+' '.join(f'{x:.5f}' for x in p) for p in self.uv];lines+=['f '+' '.join(f'{x}/{x}' for x in f) for f in self.faces]
        path.write_text('\n'.join(lines));(OUT/(self.name+'.mtl')).write_text(f'newmtl {self.name}\nKd 1 1 1\nNs 12\nmap_Kd {texture}\n');write_meta(path,'Model3D');return path

def object_(name,path,parent=None):
    key=guid(path.name).replace('-','');return {'id':guid(name),'name':name,'active':True,'parentId':parent,'tags':[],'layer':0,'transform':{'localPosition':dict(x=0,y=0,z=0),'localRotation':dict(x=0,y=0,z=0,w=1),'localScale':dict(x=1,y=1,z=1)},'components':[{'type':'MeshRenderer','enabled':True,'properties':{'usePrimitive':False,'modelGuid':guid(path.name),'modelPath':path.relative_to(ROOT).as_posix(),'meshKey':key+':mesh:'+path.stem,'materialKey':key+':material:'+path.stem,'baseColor':[1,1,1,1],'roughness':.85,'metallic':.15,'castShadows':True,'receiveShadows':True,'cullMode':'None'}}],'variables':[]}
def ground_sampler(scene):
    terrain=next((c['properties'] for o in scene['gameObjects'] for c in o['components'] if c['type'].endswith('.DesertTerrain3D')),None)
    if not terrain:return lambda x,z:0
    image=np.array(Image.open(ROOT/terrain['heightmapPath'])).astype(float)/65535;cells=terrain['cells'];spacing=terrain['spacing'];base=terrain.get('baseHeight',0);height=terrain['heightmapHeight']
    def sample(x,z):
        px=min(image.shape[1]-1,max(0,(x/spacing+cells/2)));py=min(image.shape[0]-1,max(0,(z/spacing+cells/2)));ix,iy=int(px),int(py);fx,fy=px-ix,py-iy;ix1=min(ix+1,image.shape[1]-1);iy1=min(iy+1,image.shape[0]-1)
        return base+height*((1-fy)*((1-fx)*image[iy,ix]+fx*image[iy,ix1])+fy*((1-fx)*image[iy1,ix]+fx*image[iy1,ix1]))
    return sample

def seat_anchor_eyes(scene):
    """Fit the functional attachment transform to the scanned rock, not its bounding box."""
    by_id={o['id']:o for o in scene['gameObjects']}
    for eye in scene['gameObjects']:
        if not any(c['type'].endswith('.DuneWinchAnchor3D') for c in eye['components']):continue
        rock=by_id.get(eye.get('parentId'))
        if not rock:continue
        renderer=next((c['properties'] for c in rock['components'] if c['type']=='MeshRenderer'),{})
        path=ROOT/renderer.get('modelPath','')
        if path.suffix.lower()!='.obj':continue
        vertices=[];faces=[]
        for line in path.read_text().splitlines():
            fields=line.split()
            if fields and fields[0]=='v':vertices.append(list(map(float,fields[1:4])))
            elif fields and fields[0]=='f':
                ids=[int(f.split('/')[0])-1 for f in fields[1:]]
                faces.extend((ids[0],ids[i],ids[i+1]) for i in range(1,len(ids)-1))
        triangles=np.asarray(vertices)[np.asarray(faces)];a=triangles[:,0];e1=triangles[:,1]-a;e2=triangles[:,2]-a
        origin=np.array([0,.15,2.]);direction=np.array([0,0,-1.]);h=np.cross(direction,e2);det=(e1*h).sum(1)
        inv=np.divide(1,det,out=np.zeros_like(det),where=abs(det)>1e-8);s=origin-a;u=inv*(s*h).sum(1);q=np.cross(s,e1);v=inv*(q*direction).sum(1);t=inv*(e2*q).sum(1)
        hits=np.where((abs(det)>1e-8)&(u>=0)&(v>=0)&(u+v<=1)&(t>0))[0]
        if not len(hits):raise ValueError('No rock surface for '+eye['name'])
        i=hits[np.argmin(t[hits])];normal=np.cross(e1[i],e2[i]);normal/=np.linalg.norm(normal)
        if normal[2]<0:normal=-normal
        scale=np.array([rock['transform']['localScale'][k] for k in 'xyz'])
        # Centre is 3 cm proud of the stone; the tube contacts its mounting surface.
        point=origin+direction*t[i]+normal*(.03/np.linalg.norm(normal*scale))
        rotation=np.r_[np.cross([0,0,1],normal),1+normal[2]];rotation/=np.linalg.norm(rotation)
        eye['transform']['localPosition']=dict(zip('xyz',map(float,point)))
        eye['transform']['localRotation']=dict(zip('xyzw',map(float,rotation)))

def clear_landmark_position(objs,sx,sz):
    # Keep the full ladder footprint outside the conservative rotated rock bounds.
    rocks=[o for o in objs if any(c['type']=='MeshRenderer' and 'rock_' in c.get('properties',{}).get('modelPath','') for c in o['components'])]
    for dx,dz in [(6,-6),(9,-6),(12,-6),(6,-10),(12,-10),(15,-10)]:
        x,z=sx+dx,sz+dz
        if all(math.hypot(x-o['transform']['localPosition']['x'],z-o['transform']['localPosition']['z']) > math.hypot(o['transform']['localScale']['x'],o['transform']['localScale']['z'])/2+2 for o in rocks):return x,z
    raise ValueError('No clear ladder footprint')

def author(scene,name,index):
    scene['gameObjects']=[o for o in scene['gameObjects'] if not o['name'].startswith('Presentation / ')]
    objs=scene['gameObjects'];height=ground_sampler(scene);steel=Details(name+'-field-equipment');signs=Details(name+'-wayfinding');concrete=Details(name+'-concrete-details');seat_anchor_eyes(scene)
    # Dress the existing obstacle footprint; collision and puzzle clearances are unchanged.
    for o in objs:
        if not o['name'].startswith('Contract landscape / '):continue
        if 'retaining wall' in o['name']:
            p=o['transform']['localPosition'];s=o['transform']['localScale']
            # Scanned stone faces cover the former white collision blocks.
            for j in range(3):
                path=ROOT/'Assets/ContractLevels'/('rock_07.obj' if j%2 else 'rock_09.obj');rock=object_(f"Presentation / retaining stone {o['id']} {j}",path)
                asset_guid=json.loads(Path(str(path)+'.meta').read_text(encoding='utf-8-sig'))['guid'];key=asset_guid.replace('-','');stem=path.stem
                rock['components'][0]['properties'].update(modelGuid=asset_guid,meshKey=key+':mesh:'+stem,materialKey=key+':material:'+stem,metallic=0,roughness=1)
                rock['transform'].update(localPosition=dict(x=p['x'],y=p['y'],z=p['z']+(j-1)*s['z']/3),localScale=dict(x=s['x']*1.4,y=s['y']*1.4,z=s['z']*.45))
                objs.append(rock)
            for c in o['components']:
                if c['type']=='MeshRenderer':c['properties']['visible']=False
        if 'equipment crate' in o['name']:
            p=o['transform']['localPosition'];s=o['transform']['localScale'];x,y,z=p['x'],p['y'],p['z'];steel.box((x,y,z),(s['x'],s['y'],s['z']))
            for xx in [x-s['x']*.4,x+s['x']*.4]:steel.box((xx,y,z-s['z']*.51),(.06,s['y']*.85,.06))
            steel.box((x,y+s['y']*.53,z),(s['x']*1.04,.07,s['z']*1.04));signs.box((x,y,z-s['z']*.52),(s['x']*.8,.3,.035),sign=2)
            for c in o['components']:
                if c['type']=='MeshRenderer':c['properties']['visible']=False
    # A broken bridge should read as a broken bridge, not four unexplained posts.
    if index==3:
        for x in [-12.8,12.8]:
            for z in [34,50]:
                y=height(x,z);steel.box((x,y+.6,z),(.25,.5,5));steel.box((x,y+1.1,z),(.12,.10,5))
                for dz in [-2,0,2]:steel.box((x,y+.85,z+dz),(.12,.8,.12))
    # Readable depot boundaries instead of a coloured slab. Entrance remains open.
    for x in [-7.5,7.5]:
        for z in [-4,6]:
            y=height(x,z);steel.cylinder((x,y+.48,z),.085,.96);steel.box((x,y+.75,z),(.20,.12,.20))
    signs.box((-8,height(-8,8)+2,8),(4,.95,.09),sign=1)
    for x in [-9.7,-6.3]:steel.box((x,height(x,8)+1,8),(.10,2,.10))
    # Job-site storytelling is placed beside, rather than across, playable routes.
    site=[(18,68),(34,63),(39,73),(-23,42),(25,52)][index];sx,sz=site;sy=height(sx,sz)
    for j in range(3):
        x,z=sx+j*1.3,sz-1+j*.3;y=height(x,z);steel.cylinder((x,y+.4,z),.27,.8);steel.box((x,y+.72,z),(.60,.035,.60))
    # Recognizable survey/service equipment, merged into three static draws per level.
    steel.box((sx,sy+.7,sz+3),(3,1.4,1.3));steel.box((sx,sy+1.45,sz+3),(3.15,.10,1.4))
    for x in [sx-1.2,sx+1.2]:steel.box((x,sy+.68,sz+2.32),(.06,1.1,.06))
    for z in [sz+2.5,sz+3.5]:steel.box((sx,sy+.16,z),(3.3,.20,.18))
    signs.box((sx,sy+1,sz+2.30),(1.9,.40,.035),sign=2 if index in [1,2] else 3 if index==3 else 4 if index==4 else 7)
    # Tall recognizable landmark away from the excavation and driving corridor.
    if index in [1,2]:
        lx,lz=clear_landmark_position(objs,sx,sz);ly=height(lx,lz)
        for x in [lx-1,lx+1]:steel.box((x,ly+3.5,lz),(.15,7,.15))
        for y in [1.5,3,4.5,6]:steel.box((lx,ly+y,lz),(2.1,.09,.13))
        steel.box((lx,ly+7,lz),(2.4,.35,.5))
    elif index==3:
        for x in [-15,15]:
            y=height(x,37);concrete.box((x,y+.7,37),(3,1.4,4));steel.box((x,y+1.45,37),(3.4,.12,4.3))
            signs.box((x,y+2.3,37),(2.7,.85,.1),sign=3)
    elif index==4:
        for x in [-12,12]:
            y=height(x,12);steel.box((x,y+3,12),(.20,6,.20))
        steel.box((0,height(0,12)+6,12),(24,.3,.3));signs.box((0,height(0,12)+5.5,12),(10,.8,.1),sign=4)
    for mesh,texture in [(steel,'equipment-paint.png'),(signs,'wayfinding-signs.png'),(concrete,'workshop-concrete.png')]:
        if mesh.vertices:objs.append(object_('Presentation / '+mesh.name,mesh.write(texture)))
    # Garage details share its visibility/cutaway hierarchy and do not obstruct the car.
    garage=next(o for o in objs if o['name']=='Garage environment');details=Details(name+'-garage-details')
    for x in [-3.1,3.1]:details.box((x,.055,0),(.07,.014,7.5))
    for z in [-3.7,3.7]:details.box((0,.055,z),(6.25,.014,.07))
    obj=object_('Presentation / '+details.name,details.write('bay-markings.png'),garage['id']);obj['components'][0]['properties']['castShadows']=False;objs.append(obj)
    branding=Details(name+'-garage-branding');branding.box((0,3.8,7.55),(5,.7,.04),sign=0);objs.append(object_('Presentation / '+branding.name,branding.write('wayfinding-signs.png'),garage['id']))
    # Distant scanned outcrops frame every camera direction without closing the garage.
    target=next((o['transform']['localPosition'] for o in objs if any(c['type'].endswith('.DuneRecoveryContract3D') for c in o['components'])),dict(x=0,z=70))
    for j in range(10):
        a=j*math.tau/10+.18;x,z=math.sin(a)*87,math.cos(a)*87
        if math.hypot(x-target['x'],z-target['z'])<25:continue
        rock_name='rock_07' if j%2 else 'rock_09';path=ROOT/'Assets/ContractLevels'/f'{rock_name}.obj';rock=object_('Presentation / skyline '+str(j),path);asset_guid=json.loads(Path(str(path)+'.meta').read_text(encoding='utf-8-sig'))['guid'];key=asset_guid.replace('-','')
        rock['components'][0]['properties'].update(modelGuid=asset_guid,meshKey=key+':mesh:'+rock_name,materialKey=key+':material:'+rock_name,castShadows=False,roughness=1,metallic=0)
        rock['transform'].update(localPosition=dict(x=x,y=height(x,z)+2.8,z=z),localScale=dict(x=15+j%3*3,y=8+j%3,z=13+j%2*3),localRotation=dict(x=0,y=math.sin(a/2),z=0,w=math.cos(a/2)));objs.append(rock)
    for o in objs:
        for c in o.get('components',[]):
            p=c.get('properties',{})
            if c['type']=='SkyEnvironment':
                p.update(environmentLightingEnabled=True,environmentIntensity=.45,ambientIntensity=.22,exposure=1,fogColor=[.71,.69,.61],fogDensity=.002,fogStartDistance=60,fogEndDistance=180,fogMaxOpacity=.45)
                p.setdefault('graphicsLook',{}).update(saturation=.94,contrast=1.035,warmth=.025,ao=0,bloom=0)
            if c['type']=='DirectionalLight':
                if o['name'].startswith('Sun'):p.update(color=[1,.94,.81],intensity=1.45,shadowStrength=.8,shadowDistance=48,shadowSoftness=1.4)
                elif 'Soft sky' in o['name']:p['intensity']=.25
            if c['type']=='PointLight':
                p['intensity']=5 if 'practical' in o['name'] else 8;p['color']=[1,.89,.73] if 'practical' in o['name'] else [.94,.96,1]
            if c['type']=='MeshRenderer':
                key=p.get('materialKey','')
                if 'galvanized' in key.lower():p.update(baseColor=[.85,.9,.86,1],roughness=.78,metallic=.3)
                if 'concrete' in key.lower():p.update(baseColor=[.85,.83,.77,1],roughness=.92,metallic=0)
                # Retain the collider and shape of existing barriers; give them believable surfaces.
                if o['name'].startswith('Contract landscape / ') and any(t in o['name'] for t in ['equipment crate','retaining wall']):
                    p.update(baseColor=[.40,.43,.34,1] if 'crate' in o['name'] else [.47,.43,.34,1],roughness=.95)
            if c['type']=='UiWidget':
                p.update(hoverColor=[.24,.21,.15,1],pressedColor=[.31,.25,.14,1],disabledColor=[.13,.14,.13,.85])
            if c['type']=='UiText':p['shadowColor']=[0,0,0,0]
    return scene

def apply():
    OUT.mkdir(parents=True,exist_ok=True);backup=ROOT/'.byteengine/backups'/('presentation-'+datetime.datetime.now().strftime('%Y%m%d-%H%M%S'));backup.mkdir(parents=True)
    textures();garage=ROOT/'Assets/GritGarage/grit-garage-v3.glb';shutil.copy2(garage,backup/garage.name);update_garage_glb(garage)
    report={'backup':str(backup),'scenes':[]}
    for index,name in enumerate(NAMES):
        path=ROOT/'Scenes'/f'{name}.bytescene';shutil.copy2(path,backup/path.name);scene=json.loads(path.read_text(encoding='utf-8-sig'));before=len(scene['gameObjects']);author(scene,name,index);path.write_text(json.dumps(scene,indent=2));report['scenes'].append({'name':name,'before':before,'after':len(scene['gameObjects'])})
    catalog=ROOT/'Assets/DuneParts/catalog.json';shutil.copy2(catalog,backup/catalog.name);parts=json.loads(catalog.read_text(encoding='utf-8-sig'))
    for p in parts:
        if p['id']==37:p['function']='Wide footprint / lower loose-sand resistance. Unlock once for unlimited copies; tuning is a separate one-time unlock.'
        if p['id']==34:p['function']='5.4 kN drive force / 22 m/s stock cap / 55 kg. High torque for loaded recovery. One unlock gives unlimited copies.'
        if p['id']==38:p['function']='6.5 kN drive force / 42 m/s stock cap / 45 kg. High-speed racing engine. One unlock gives unlimited copies.'
    catalog.write_text(json.dumps(parts,indent=2));(OUT/'AUTHORING-NOTICE.md').write_text('Original procedural presentation textures, wayfinding and merged detail geometry authored for Dune Company. CC0 1.0. Existing Poly Haven scans and selected sky retain their recorded CC0 sources. No new audio was added.\n')
    if (OUT/'menu-garage.png').exists():
        menu=ROOT/'Scenes/MainMenu.bytescene';shutil.copy2(menu,backup/menu.name);data=json.loads(menu.read_text(encoding='utf-8-sig'));objs=data['gameObjects'];objs[:]=[o for o in objs if not o['name'].startswith('Native gradient ') and o['name'] not in ['Presentation / Menu shade','Presentation / Menu promise']]
        base=next(o for o in objs if o['name']=='Abstract menu base');p=base['components'][0]['properties'];p.update(kind='Image',image={'path':'Assets/Presentation/menu-garage.png'},color=[1,1,1,1],stretchHorizontal=True,stretchVertical=True)
        image=Image.new('RGBA',(256,8));pixels=image.load()
        for x in range(256):
            t=x/255;alpha=.94 if t<.26 else .94-(.94-.22)*min(1,(t-.26)/.44)
            for y in range(8):pixels[x,y]=(9,13,11,round(alpha*255))
        image.save(OUT/'menu-shade.png');write_meta(OUT/'menu-shade.png');write_meta(OUT/'menu-garage.png')
        shade=copy.deepcopy(base);shade['id']=guid('menu-shade');shade['name']='Presentation / Menu shade';shade['components'][0]['properties'].update(image={'path':'Assets/Presentation/menu-shade.png'},orderInLayer=-8);objs.append(shade)
        title=next(o for o in objs if any(c['type']=='UiText' for c in o['components']));promise=copy.deepcopy(title);promise.update(id=guid('menu-promise'),name='Presentation / Menu promise',parentId=base.get('parentId'));promise['components']=[{'type':'UiText','enabled':True,'properties':{'text':'BUILD. RECOVER. GET PAID.','font':{'path':'Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf'},'size':18,'color':[.94,.66,.29,1],'anchor':'TopLeft','offset':[65,220],'wrapWidth':420,'visible':True,'orderInLayer':2}}];objs.append(promise);menu.write_text(json.dumps(data,indent=2))
    (OUT/'presentation-report.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--apply',action='store_true');args=parser.parse_args()
    if args.apply:apply()
    else:print('Pass --apply to author the backed-up game project. No export.')
