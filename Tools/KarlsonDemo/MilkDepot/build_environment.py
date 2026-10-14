import bpy, math, json, random
from pathlib import Path
from mathutils import Vector

OUT = Path('C:/Users/codex/Downloads/byte engine files/Milk Depot Environment')
PROJECT = Path('C:/Users/codex/Documents/karlson demo')
ASSETS = PROJECT / 'Assets/Environment/MilkDepot'
for p in [OUT, OUT/'Previews', OUT/'Textures', ASSETS/'Models', ASSETS/'Textures']:
    p.mkdir(parents=True, exist_ok=True)
library = bpy.data.scenes.new('01 - Modular asset library')
level = bpy.data.scenes.new('02 - Milk Depot assembled reference')
bpy.context.window.scene = library
library.unit_settings.system = 'METRIC'
level.unit_settings.system = 'METRIC'
materials = {}
catalog = []
masters = {}
placements = []
parts = []

def mat(name, color, metal=0, rough=.55, emission=0, texture=False):
    m = bpy.data.materials.new('MD_' + name); m.use_nodes = True
    m.diffuse_color = (*color,1)
    bs = m.node_tree.nodes.get('Principled BSDF')
    bs.inputs['Base Color'].default_value = (*color,1)
    bs.inputs['Metallic'].default_value = metal
    bs.inputs['Roughness'].default_value = rough
    if emission:
        bs.inputs['Emission Color'].default_value = (*color,1)
        bs.inputs['Emission Strength'].default_value = emission
    if texture:
        rng = random.Random(114 + len(materials))
        image = bpy.data.images.new('MD_' + name + '_BaseColor', width=256,height=256)
        pixels=[]
        for y in range(256):
            for x in range(256):
                noise = rng.uniform(-.018,.018) + math.sin(y*.16)*.004
                pixels.extend([max(.0,min(1,c+noise)) for c in color] + [1])
        image.pixels.foreach_set(pixels)
        image.filepath_raw=str(OUT/'Textures'/f'{name}_BaseColor.png');image.file_format='PNG';image.save();image.pack()
        node=m.node_tree.nodes.new('ShaderNodeTexImage');node.image=image
        m.node_tree.links.new(node.outputs['Color'],bs.inputs['Base Color'])
    materials[name]=m
    return m

mat('Concrete',(.70,.73,.73),rough=.76,texture=True)
mat('Floor',(.12,.155,.17),rough=.63,texture=True)
mat('Orange',(.95,.24,.035),metal=.12,rough=.39,texture=True)
mat('Steel',(.24,.30,.33),metal=.72,rough=.34)
mat('Dark',(.035,.052,.064),metal=.18,rough=.55)
mat('White',(.87,.91,.90),rough=.36)
mat('Cyan',(.02,.65,.78),rough=.28,emission=2)
mat('Warm', (1,.70,.31),rough=.3,emission=2)
mat('Yellow',(.93,.64,.045),metal=.12,rough=.4)
mat('Red',(.65,.045,.025),metal=.25,rough=.4)
mat('Glass',(.055,.15,.18),metal=.5,rough=.18)

def finish(o,name,material,bevel=0):
    o.name=name
    if material:o.data.materials.append(materials[material])
    if o.type=='MESH':
        bpy.context.view_layer.objects.active=o
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        if bevel:
            mod=o.modifiers.new('Manufactured edge radius','BEVEL');mod.width=bevel;mod.segments=2
            bpy.ops.object.modifier_apply(modifier=mod.name)
        # Weighted flat normals preserve planar faces and smooth the chamfers.
        for face in o.data.polygons:face.use_smooth=True
        mod=o.modifiers.new('Weighted face normals','WEIGHTED_NORMAL');mod.keep_sharp=True;mod.weight=50
        bpy.ops.object.modifier_apply(modifier=mod.name)
    parts.append(o);return o

def box(name,p,s,material,bevel=.012):
    bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.dimensions=s
    return finish(o,name,material,min(bevel,min(s)*.25))

def cyl(name,p,r,depth,material,rotation=(0,0,0),vertices=20,bevel=.006):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=depth,location=p,rotation=rotation)
    return finish(bpy.context.object,name,material,bevel)

def rod(name,a,b,r,material):
    a,b=Vector(a),Vector(b);o=cyl(name,(a+b)/2,r,(b-a).length,material)
    o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o

def text(value,p,size,material='White',rotation=(math.pi/2,0,0)):
    curve=bpy.data.curves.new('Label '+value,'FONT');curve.body=value;curve.align_x='CENTER';curve.align_y='CENTER'
    curve.size=size;curve.extrude=.0008;curve.bevel_depth=.0003;curve.bevel_resolution=0
    o=bpy.data.objects.new('Engraved '+value,curve);library.collection.objects.link(o);o.location=p;o.rotation_euler=rotation
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.object.convert(target='MESH');o=bpy.context.object;o.data.materials.append(materials[material]);parts.append(o)

def bolts(face_y,xs,zs):
    for x in xs:
        for z in zs:cyl('Recessed fastener',(x,face_y,z),.017,.008,'Steel',(math.pi/2,0,0),6,.001)

def asset(key,category,dimensions,description,collision='box'):
    global parts
    bpy.ops.object.select_all(action='DESELECT')
    for o in parts:o.select_set(True)
    bpy.context.view_layer.objects.active=parts[0]
    bpy.ops.object.join();o=bpy.context.object;o.name=key
    library.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
    o['asset_id']=key;o['category']=category;o['units']='metres';o['purpose']=description
    o.asset_mark()
    o.asset_data.description=description
    masters[key]=o
    o.data.calc_loop_triangles()
    catalog.append({'id':key,'category':category,'dimensions_m':dimensions,'description':description,'collision':collision,
                    'triangles':len(o.data.loop_triangles),'file':key+'.glb','placements':0})
    parts=[];print('ASSET',key,len(o.data.loop_triangles),flush=True)

def wall(width):
    box('Precast wall',(0,0,1.5),(width,.30,3),'Concrete',.025)
    for y in [-.157,.157]:
        box('Recessed base trim',(0,y,.13),(width,.018,.26),'Dark',.005)
        box('Top cap',(0,y,2.91),(width,.022,.12),'White',.008)
        box('Orange wayfinding band',(0,y,1.10),(width,.022,.24),'Orange',.005)
        for x in [-width/2+.05,width/2-.05]:box('Panel vertical joint',(x,y,1.62),(.012,.012,2.36),'Steel',.001)
    asset(f'Wall_{width}m','Architecture',[width,.34,3],'Bevelled precast panel, recessed skirt, cap and orange identification band')

for size in [2,4]:
    box('Tile support',(0,0,-.075),(size,size,.15),'Dark',.006)
    box('Recessed walking panel',(0,0,-.008),(size-.025,size-.025,.016),'Floor',.006)
    for x in [-size/2+.06,size/2-.06]:
        for y in [-size/2+.06,size/2-.06]:cyl('Flush floor screw',(x,y,.001),.015,.003,'Steel',vertices=6,bevel=0)
    asset(f'Floor_{size}m','Architecture',[size,size,.15],'Dark modular walking surface with real panel seams and flush fasteners')
box('Structural slab',(0,0,-.40),(2,2,.8),'Concrete',.008)
asset('Foundation_2m','Architecture',[2,2,.8],'Solid structural foundation beneath walking tiles')
wall(2);wall(4)
box('Column',(0,0,1.5),(.5,.5,3),'Concrete',.025)
box('Foot',(0,0,.12),(.56,.56,.24),'Dark')
box('Cap',(0,0,2.94),(.55,.55,.12),'White')
for y in [-.26,.26]:box('Column band',(0,y,1.1),(.50,.015,.24),'Orange',.002)
asset('Wall_CornerColumn','Architecture',[.56,.56,3],'Corner column conceals joins between rotated wall modules')
box('End pillar',(0,0,1.5),(.34,.40,3),'Steel',.018)
asset('Wall_EndCap','Architecture',[.34,.40,3],'Finished jamb for exposed wall ends')

for key,window in [('Doorway_4m',False),('Window_4m',True)]:
    for x in [-1.75,1.75]:box('Wall pier',(x,0,1.5),(.5,.3,3),'Concrete',.02)
    box('Lintel',(0,0,2.8),(3,.3,.4),'Concrete',.02)
    if window:
        box('Sill wall',(0,0,.45),(3,.3,.9),'Concrete',.02)
        box('Tinted observation pane',(0,0,1.65),(2.9,.04,1.4),'Glass',.003)
    for x in [-1.51,1.51]:box('Recessed jamb',(x,-.03,1.4),(.09,.42,2.8),'Steel')
    box('Header',(0,-.03,2.58),(3,.42,.09),'Steel')
    if window:box('Window sill',(0,-.03,.94),(3.1,.42,.10),'Steel')
    else:
        box('Door header label',(0,-.245,2.81),(1.4,.032,.21),'Orange',.008);text('DEPOT',(0,-.267,2.81),.13)
        for x in [-1.51,1.51]:box('Jamb guide light',(x,-.251,1.55),(.026,.025,1.05),'Cyan',.004)
    asset(key,'Architecture',[4,.46,3],'Framed observation window' if window else 'Open 3m-clear doorway with illuminated jambs','compound boxes')

box('Balcony structural deck',(0,0,-.14),(2,2,.28),'Concrete',.02)
box('Balcony floor',(0,0,-.008),(1.97,1.97,.016),'Floor',.004)
box('Orange fascia',(0,-1.02,-.11),(2,.05,.12),'Orange')
asset('Balcony_2m','Traversal',[2,2,.28],'Load-bearing upper walkway tile with contrasting fascia')

def rail(key,corner=False):
    for x in [-.92,.92]:
        box('Bolted foot',(x,0,.018),(.17,.17,.036),'Steel',.004)
        rod('Upright',(x,0,.05),(x,0,1.08),.027,'Steel')
    for z in [.48,1.08]:rod('Guardrail',(-.95,0,z),(.95,0,z),.03,'Steel')
    box('Kickplate',(0,0,.12),(2,.04,.14),'Orange',.004)
    if corner:
        rod('Corner return',(.92,0,1.08),(.92,.90,1.08),.03,'Steel')
        rod('Corner upright',(.92,.90,.03),(.92,.90,1.08),.027,'Steel')
    asset(key,'Traversal',[2,1 if corner else .17,1.11],'Safety rail with bolted posts, tubular handrails and orange kickplate','compound boxes')
rail('Railing_2m');rail('Railing_Corner',True)

for rise,n in [(1,5),(2.5,14)]:
    run=n*.30;width=2
    for i in range(n):
        h=rise*(i+1)/n;y=-run/2+(i+.5)*.3
        box('Concrete step',(0,y,h/2),(width,.30,h),'Concrete',.012)
        box('Anti slip tread',(0,y,h+.003),(1.93,.25,.006),'Floor',.002)
        box('Visible step nose',(0,y-.142,h+.003),(1.95,.023,.012),'Orange',.002)
    for x in [-1.05,1.05]:
        for i in [0,n//2,n-1]:
            y=-run/2+(i+.5)*.3;z=rise*(i+1)/n
            rod('Stair baluster',(x,y,z),(x,y,z+1.0),.025,'Steel')
        rod('Continuous sloping handrail',(x,-run/2+.15,rise/n+1.0),(x,run/2-.15,rise+1.0),.034,'Steel')
    asset(f'Stair_{str(rise).replace(".","_")}m','Traversal',[2.17,run,rise+1.04],f'{n} equal risers, .30m tread, handrails and anti-slip nosings','stepped boxes or controller ramp')
box('Landing',(0,0,-.15),(2,2,.30),'Concrete',.02);box('Landing top',(0,0,-.005),(1.96,1.96,.01),'Floor',.003)
asset('Landing_2m','Traversal',[2,2,.30],'Flat intermediate platform and stair landing')
box('Training step',(0,0,.25),(2,2,.5),'Concrete',.03);box('Training pad',(0,0,.503),(1.92,1.92,.006),'Floor',.002)
for y in [-1.005,1.005]:box('Step stripe',(0,y,.40),(1.9,.02,.12),'Orange',.004)
asset('TrainingStep_05m','Traversal',[2,2,.51],'Half-metre jump lesson block with protected chamfered edges')
box('Raised jump platform',(0,0,.5),(2,2,1),'Concrete',.03);box('Pad',(0,0,1.003),(1.9,1.9,.006),'Floor',.002)
for y in [-1.01,1.01]:box('Pad edge',(0,y,.92),(1.9,.025,.10),'Orange')
asset('JumpPlatform_1m','Traversal',[2,2,1.01],'One-metre jump platform above a safe recovery floor')

for key,w,h in [('Cover_Low',2,1),('Cover_Tall',2,1.8),('Cover_Pillar',1,2.5)]:
    box('Protective cover body',(0,0,h/2),(w,1,h),'Concrete',.045)
    box('Cap',(0,0,h-.035),(w+.035,1.035,.07),'White',.012)
    for y in [-.506,.506]:
        box('Panel shadow seam',(0,y,h*.52),(w-.12,.025,h*.60),'Dark',.01)
        box('Cover panel',(0,y*1.03,h*.52),(w-.17,.022,h*.57),'Concrete',.012)
        box('Orange face band',(0,y*1.06,.18),(w-.15,.022,.23),'Orange',.005)
    asset(key,'Combat cover',[w+.035,1.06,h], 'Heavy cover with inset panels and orange orientation marking')

def crate(key,size):
    w,d,h=size
    box('Recessed cargo shell',(0,0,h/2),(w-.08,d-.08,h-.08),'Dark',.025)
    box('Cargo body',(0,0,h/2),(w-.12,d-.12,h-.13),'Concrete',.025)
    for x in [-w/2+.06,w/2-.06]:
        for y in [-d/2+.06,d/2-.06]:box('Corner bumper',(x,y,h/2),(.12,.12,h),'Steel',.012)
    for y in [-d/2+.035,d/2-.035]:
        for z in [.08,h-.08]:box('Protective frame',(0,y,z),(w,.08,.10),'Steel',.009)
        box('Inset orange cargo panel',(0,y-.045,h*.50),(w*.55,.025,h*.50),'Orange',.012)
    box('Lid',(0,0,h-.05),(w-.07,d-.07,.10),'Concrete',.02)
    box('Recessed lid centre',(0,0,h+.001),(w*.6,d*.6,.008),'Dark',.002)
    for x in [-w*.34,w*.34]:box('Lid latch',(x,-d/2-.012,h-.12),(.13,.06,.16),'Steel',.007)
    text('MD-07',(0,-d/2-.02,h*.55),min(w*.14,.19))
    asset(key,'Props',size,'Stackable freight crate with metal frame, latches, recessed lid and depot ID')
crate('Crate_Small',[.8,.8,.8]);crate('Crate_Large',[1.4,1.2,1.2])
for y in [-.45,0,.45]:box('Pallet bearer',(0,y,.08),(1.35,.12,.16),'Steel',.014)
for x in [-.55,-.28,0,.28,.55]:box('Pallet deck slat',(x,0,.18),(.22,1.10,.06),'Concrete',.006)
asset('Pallet','Props',[1.35,1.10,.21],'Industrial pallet with open fork channels')
cyl('Drum shell',(0,0,.55),.32,1.1,'Steel',bevel=.018)
for z in [.08,.36,.76,1.04]:cyl('Drum reinforced ring',(0,0,z),.338,.048,'Dark',bevel=.005)
cyl('Drum lid',(0,0,1.108),.30,.018,'Concrete');cyl('Bung',(.12,0,1.13),.035,.025,'Steel',vertices=12)
box('Orange ID panel',(0,-.326,.56),(.25,.026,.36),'Orange');text('MD',(0,-.343,.56),.14)
asset('Utility_Drum','Props',[.676,.676,1.15],'Sealed utility drum with rolled rings and screw cap')

box('Rack foot',(0,0,.10),(2,.65,.20),'Dark',.02)
box('Rack back',(0,.23,1.05),(2,.12,1.85),'Steel',.02)
box('Rack inset',(0,.157,1.15),(1.84,.025,1.38),'Dark')
for x in [-.85,.85]:box('Rack pillar',(x,.10,1),(.08,.32,1.8),'Orange')
for z in [.6,1,1.4]:
    box('Mounting rail',(0,.10,z),(1.72,.07,.04),'Steel',.005)
    for x in [-.55,0,.55]:box('Weapon retaining hook',(x,-.02,z),(.04,.28,.06),'Steel',.007)
box('Rack header',(0,.06,1.95),(2,.30,.21),'Orange');text('ARMORY',(0,-.097,1.95),.16)
asset('Weapon_Rack','Gameplay props',[2,.67,2.06],'Three-position weapon rack; use existing animated gun models for the weapons')

box('Ammo case',(0,0,.20),(.55,.32,.4),'Dark',.018)
box('Ammo lid',(0,0,.405),(.58,.35,.07),'Yellow',.01)
box('Handle',(0,0,.49),(.20,.045,.06),'Steel',.005)
for x in [-.19,.19]:box('Latch',(x,-.18,.36),(.055,.04,.10),'Steel',.004)
text('AMMO',(0,-.168,.23),.08)
asset('Ammo_Case','Gameplay props',[.58,.36,.52],'Ammunition supply case with hinged lid, latches and carry handle')
box('Pickup pedestal',(0,0,.07),(.38,.38,.14),'Dark',.01)
box('Glowing pickup core',(0,0,.37),(.23,.23,.38),'Yellow',.018)
for x in [-.14,.14]:box('Core cage rail',(x,0,.38),(.027,.28,.43),'Steel',.004)
box('Pickup cap',(0,0,.60),(.30,.30,.05),'White',.01)
text('+',(0,-.13,.36),.16,'Dark')
asset('Ammo_Pickup','Gameplay props',[.38,.38,.63],'Readable yellow ammo pickup; mesh only, gameplay configured in the engine')
cyl('Milk bottle',(0,0,.16),.09,.28,'White',vertices=24,bevel=.01)
cyl('Bottle shoulder',(0,0,.295),.07,.07,'White',vertices=24,bevel=.015)
cyl('Bottle neck',(0,0,.34),.041,.07,'White',vertices=24)
cyl('Bottle cap',(0,0,.382),.047,.022,'Orange',vertices=24,bevel=.003)
box('Milk label',(0,-.092,.16),(.13,.01,.115),'Orange',.005);text('MILK',(0,-.099,.16),.038)
asset('Milk_Bottle','Gameplay props',[.18,.20,.395],'Hero objective bottle with shoulder, screw cap and readable depot label')

box('Tower footing',(0,0,.18),(5,5,.36),'Dark',.04)
box('Milk tower',(0,0,4.05),(4.6,4.6,7.8),'Concrete',.06)
box('Orange landmark face',(0,-2.315,4.05),(4.40,.06,7.65),'Orange',.02)
for x in [-2.27,2.27]:box('Tower frame',(x,-2.36,4.05),(.12,.14,7.9),'Steel',.012)
box('Dark entrance recess',(0,-2.355,1.40),(2.6,.08,2.65),'Dark',.008)
for x in [-1.30,1.30]:box('Entry light',(x,-2.42,1.50),(.055,.03,2.4),'Cyan',.006)
text('MILK',(0,-2.365,6.2),.90)
# Bottle pictogram modeled as a flattened silhouette.
box('Bottle pictogram body',(0,-2.366,4.6),(.65,.02,1.05),'White',.08)
box('Bottle pictogram neck',(0,-2.366,5.25),(.32,.02,.28),'White',.025)
box('Bottle pictogram lid',(0,-2.366,5.43),(.40,.02,.09),'White',.015)
box('Roof slab',(0,0,8.06),(4.85,4.85,.24),'Steel',.025)
for x in [-2.30,2.30]:box('Roof parapet',(x,0,8.43),(.16,4.70,.6),'Concrete',.02)
box('Roof rear parapet',(0,2.3,8.43),(4.70,.16,.6),'Concrete',.02)
for x in [-1.2,1.2]:box('Rooftop vent',(x,.7,8.40),(.8,1.0,.52),'Steel',.018)
cyl('Service antenna',(1.85,1.6,9.0),.025,1.6,'Steel',vertices=12)
asset('Milk_Exit_Tower','Landmark',[5,5,9.8],'Orange objective landmark with modeled MILK lettering, roof services and recessed entry','compound boxes; doorway needs cutout collider')

box('Door leaf',(0,0,1.25),(2.35,.11,2.50),'Steel',.024)
for x in [-.57,.57]:box('Door inset',(x,-.067,1.28),(1.02,.024,2.15),'Dark',.014)
box('Door centre seam',(0,-.084,1.25),(.024,.02,2.48),'Cyan',.004)
for x in [-.16,.16]:box('Door handle',(x,-.105,1.20),(.035,.06,.30),'Steel',.007)
asset('Exit_Door','Gameplay props',[2.35,.19,2.5],'Recessed paired exit door, mesh ready for separate engine door behavior')

for key,caption in [('Zone_Sign','03  COURTYARD'),('Arrow_Sign','>  MILK')]:
    box('Sign backing',(0,0,.23),(1.5,.08,.46),'Dark',.012)
    box('Sign orange insert',(0,-.05,.23),(1.43,.025,.39),'Orange',.008)
    text(caption,(0,-.068,.23),.15)
    bolts(-.069,[-.67,.67],[.09,.37])
    asset(key,'Signage',[1.50,.10,.46],'Raised lettering and fastened orange wayfinding panel')

box('Wall light body',(0,0,.12),(.70,.15,.24),'Steel',.016)
box('Diffuser',(0,-.09,.12),(.61,.025,.13),'Warm',.008)
for x in [-.30,.30]:box('Fixture guard',(x,-.107,.12),(.025,.025,.16),'Dark',.003)
asset('Wall_Light','Lighting',[.70,.22,.24],'Protected warm wall fixture; add native engine light for scene illumination')
box('Ceiling cassette',(0,0,0),(1.5,.50,.12),'Steel',.01)
for x in [-.30,.30]:box('Ceiling diffuser',(x,0,-.070),(.49,.38,.02),'White',.005)
asset('Ceiling_Light','Lighting',[1.50,.50,.14],'Twin recessed ceiling light panel')
box('Light strip housing',(0,0,0),(2,.055,.055),'Steel',.004)
box('Light strip lens',(0,-.032,0),(1.94,.02,.022),'Cyan',.003)
asset('Cyan_LightStrip_2m','Lighting',[2,.075,.055],'Thin cyan LED strip for ledges and route landmarks')

box('Vent recess',(0,0,.30),(.8,.07,.6),'Dark',.007)
for z in [.07,.15,.23,.31,.39,.47,.55]:
    o=box('Vent louvre',(0,-.045,z),(.72,.075,.025),'Steel',.003);o.rotation_euler.x=.25
for x in [-.385,.385]:box('Vent frame',(x,-.035,.3),(.03,.08,.61),'Concrete',.004)
asset('Wall_Vent','Service props',[.80,.12,.61],'Inset vent with angled blades and frame')
rod('Service pipe',(0,0,0),(0,2,0),.065,'Steel')
for y in [.1,1.9]:cyl('Pipe flange',(0,y,0),.10,.04,'Dark',(math.pi/2,0,0))
for y in [.35,1.65]:box('Pipe clamp',(0,y,-.08),(.19,.08,.11),'Orange',.009)
asset('ServicePipe_2m','Service props',[.2,2,.17],'Flanged service pipe with mounting clamps','none')
for i in range(8):
    a=i*math.pi/16;b=(i+1)*math.pi/16
    rod('Bent pipe segment',(.25*math.cos(a),.25*math.sin(a),0),(.25*math.cos(b),.25*math.sin(b),0),.065,'Steel')
asset('ServicePipe_Elbow','Service props',[.40,.40,.13],'Quarter bend for modular service pipe runs','none')

box('Junction box',(0,0,.3),(.45,.18,.6),'Steel',.015)
box('Inset panel',(0,-.105,.3),(.36,.02,.50),'Concrete',.012)
box('Label',(0,-.12,.36),(.23,.012,.12),'Orange',.003);text('240V',(0,-.130,.36),.065)
bolts(-.123,[-.15,.15],[.10,.50])
asset('Junction_Box','Service props',[.45,.25,.60],'Electrical junction cabinet with fasteners and voltage label')
box('Terminal base',(0,0,.12),(.65,.60,.24),'Dark',.02)
box('Terminal body',(0,0,.76),(.50,.40,1.3),'Steel',.03)
box('Terminal screen frame',(0,-.225,1.17),(.48,.09,.40),'Dark',.016)
box('Terminal screen',(0,-.277,1.19),(.38,.02,.26),'Glass',.006)
text('DEPOT ONLINE',(0,-.291,1.19),.045,'Cyan')
for x in [-.12,0,.12]:cyl('Input button',(x,-.228,.90),.032,.018,'Orange',(math.pi/2,0,0),16,.003)
box('Terminal orange badge',(0,-.216,.46),(.32,.025,.17),'Orange',.004)
asset('Depot_Terminal','Service props',[.65,.60,1.42],'Industrial terminal with recessed screen, buttons and status label')

for x in [-.65,.65]:box('Bench leg',(x,0,.25),(.10,.45,.50),'Steel')
box('Bench seat',(0,0,.51),(1.6,.50,.10),'Concrete',.02)
box('Bench back',(0,.24,.87),(1.6,.10,.55),'Concrete',.02)
asset('Bench','Props',[1.6,.55,1.15],'Simple depot waiting bench with durable molded seat')
cyl('Waste bin',(0,0,.38),.25,.76,'Dark',bevel=.02)
cyl('Bin rim',(0,0,.75),.27,.06,'Steel')
box('Waste ID',(0,-.255,.47),(.19,.02,.13),'Orange',.003)
asset('Waste_Bin','Props',[.54,.54,.79],'Utility bin with reinforced rim and identification strip')

# Static articulated training dummy, not an animated enemy character.
for x in [-.16,.16]:
    box('Dummy foot',(x,-.09,.08),(.23,.40,.16),'Dark',.024)
    cyl('Dummy ankle',(x,0,.19),.07,.14,'Steel')
    box('Dummy shin',(x,0,.42),(.18,.20,.40),'Steel',.025)
    cyl('Dummy knee',(x,0,.66),.10,.22,'Dark',(0,math.pi/2,0))
    box('Dummy thigh',(x,0,.85),(.22,.25,.30),'Red',.025)
box('Dummy pelvis',(0,0,1.02),(.50,.28,.20),'Dark',.03)
box('Dummy chest',(0,0,1.30),(.59,.32,.45),'Red',.045)
box('Chest plate',(0,-.19,1.31),(.39,.065,.29),'Steel',.02)
text('TARGET',(0,-.226,1.31),.06,'White')
cyl('Dummy neck',(0,0,1.59),.06,.13,'Steel')
box('Dummy head',(0,0,1.74),(.30,.28,.25),'Steel',.035)
box('Dummy visor',(0,-.151,1.75),(.24,.025,.085),'Cyan',.008)
for side in [-1,1]:
    cyl('Shoulder',(side*.36,0,1.40),.10,.14,'Dark',(0,math.pi/2,0))
    rod('Upper arm',(side*.40,0,1.36),(side*.49,-.02,1.10),.075,'Red')
    cyl('Elbow',(side*.49,-.02,1.06),.07,.12,'Steel',(0,math.pi/2,0))
    rod('Forearm',(side*.49,-.02,1.03),(side*.50,-.06,.86),.065,'Steel')
    box('Gripper',(side*.50,-.06,.80),(.12,.12,.14),'Dark',.012)
asset('Training_Dummy','Gameplay props',[1.12,.42,1.87],'Detailed static mechanical practice target; not a rigged enemy or AI')

box('Beacon base',(0,0,.07),(.5,.5,.14),'Steel',.014)
cyl('Beacon mast',(0,0,.55),.085,.95,'Dark')
cyl('Beacon lens',(0,0,1.05),.16,.22,'Cyan',bevel=.01)
cyl('Beacon top',(0,0,1.19),.18,.07,'Steel')
asset('Checkpoint_Beacon','Gameplay props',[.5,.5,1.23],'Checkpoint location marker; checkpoint behavior remains engine-authored')
cyl('Bollard foot',(0,0,.03),.17,.06,'Steel')
cyl('Safety bollard',(0,0,.48),.11,.92,'Orange',bevel=.015)
for z in [.32,.62]:cyl('Bollard dark band',(0,0,z),.113,.10,'Dark',bevel=.002)
asset('Safety_Bollard','Props',[.34,.34,.98],'Protective bollard for door corners and service machinery')

def place(key,p=(0,0,0),angle=0,name=None):
    master=masters[key];o=master.copy();o.data=master.data;o.name=name or key+' instance'
    level.collection.objects.link(o);o.location=p;o.rotation_euler.z=angle
    placements.append({'asset':key,'position':list(p),'rotation_z':angle})
    next(c for c in catalog if c['id']==key)['placements']+=1
    return o

# Reference layout: connected primary route with a safe lower floor.
rooms=[(-28,-20,-20,-12),(-20,-8,-20,-12),(-28,-8,-12,4),(-28,-24,4,16),(-24,4,12,16),(4,24,0,16),(20,26,16,22),(-8,4,-20,0)]
cells=set()
for x0,x1,y0,y1 in rooms:
    for x in range(x0,x1,2):
        for y in range(y0,y1,2):cells.add((x,y))
for x,y in sorted(cells):place('Floor_2m',(x+1,y+1,0))
for x,y in sorted(cells):
    # Exposed edges only. Avoid wall duplication where rooms meet.
    if (x-2,y) not in cells:place('Wall_2m',(x,y+1,0),math.pi/2)
    if (x+2,y) not in cells:place('Wall_2m',(x+2,y+1,0),math.pi/2)
    if (x,y-2) not in cells:place('Wall_2m',(x+1,y,0))
    if (x,y+2) not in cells:place('Wall_2m',(x+1,y+2,0))

def wallrun(a,b,axis='x',gaps=[]):
    length=abs(b-a);start=min(a,b)
    for value in range(int(start),int(start+length),2):
        if any(lo<=value<hi for lo,hi in gaps):continue
        if axis=='x':place('Wall_2m',(value+1,wallrun.fixed,0))
        else:place('Wall_2m',(wallrun.fixed,value+1,0),math.pi/2)
wallrun.fixed=-12;wallrun(-28,-8,gaps=[(-26,-22)])
place('Doorway_4m',(-24,-12,0))
wallrun.fixed=-20;wallrun(-20,-12,axis='y',gaps=[(-18,-14)])
place('Doorway_4m',(-20,-16,0),math.pi/2)
wallrun.fixed=4;wallrun(-28,-8,gaps=[(-28,-24)])
place('Doorway_4m',(-26,4,0))
wallrun.fixed=4;wallrun(0,16,axis='y',gaps=[(12,16)])
place('Doorway_4m',(4,14,0),math.pi/2)
wallrun.fixed=-8;wallrun(-20,4,axis='y',gaps=[(-18,-14),(-4,0)])
place('Doorway_4m',(-8,-16,0),math.pi/2)
place('Doorway_4m',(-8,-2,0),math.pi/2)

# Spawn furnishing and the training sequence.
place('Weapon_Rack',(-24,-18.9,0))
place('Bench',(-27.2,-16,0),math.pi/2)
place('Depot_Terminal',(-21,-18.8,0))
place('Ammo_Case',(-23,-18.2,0))
place('Zone_Sign',(-24,-19.78,2.2))
for x,z in [(-18,.5),(-14,1),(-10,.5)]:place('TrainingStep_05m' if z==.5 else 'JumpPlatform_1m',(x,-16,0))

# Courtyard cover islands, staggered enough to leave two clear flanks.
for x,y,angle in [(-22,-7,0),(-15,-6,.0),(-24,-1,math.pi/2),(-16,0,math.pi/2),(-11,-8,0)]:place('Cover_Low',(x,y,0),angle)
place('Cover_Tall',(-18,-3,0),math.pi/2)
place('Crate_Large',(-12,1.5,0));place('Crate_Small',(-12,1.5,1.2))
for x,y in [(-25,-5),(-14,-1),(-11,-7)]:place('Training_Dummy',(x,y,0),math.pi)
place('Checkpoint_Beacon',(-26,-10,0))
place('Ammo_Pickup',(-23,1.2,0));place('Ammo_Pickup',(-11,-4,0))

# Both balconies face the courtyard. Upper walkway spans remain backed by floor underneath.
for x in range(-28,-8,2):
    place('Balcony_2m',(x+1,3,2.5))
    if x not in [-24,-22]:place('Railing_2m',(x+1,2,2.5))
for y in range(-10,2,2):
    place('Balcony_2m',(-27,y+1,2.5))
    if y not in [-8,-6]:place('Railing_2m',(-26,y+1,2.5),math.pi/2)
place('Stair_2_5m',(-23,-.10,0))
place('Landing_2m',(-23,3,2.5))
place('Stair_2_5m',(-23.9,-7,0),math.pi/2)
place('Landing_2m',(-27,-7,2.5))

# Short-range corridor: recesses and utilities, with cover only at its wider ends.
place('Cover_Low',(-26,8,0),math.pi/2)
place('Ammo_Case',(-25.5,11,0))
place('Training_Dummy',(-26,13.4,0))
place('Training_Dummy',(-9,14,0),math.pi/2)
for x in [-21,-15,-9]:
    place('Wall_Vent',(x,15.80,1.3),math.pi)
    place('Wall_Light',(x,15.77,2.5),math.pi)
place('Junction_Box',(-18,15.77,.8),math.pi)
for x in [-20,-18,-16,-14]:place('ServicePipe_2m',(x,15.7,2.8),-math.pi/2)

# Optional elevated shortcut from the east side of the jump lane to the final arena.
place('Stair_2_5m',(-3,-16.1,0))
for y in range(-12,0,2):
    place('Balcony_2m',(-3,y+1,2.5))
    place('Railing_2m',(-4,y+1,2.5),math.pi/2);place('Railing_2m',(-2,y+1,2.5),math.pi/2)
for x in range(-4,6,2):
    place('Balcony_2m',(x+1,1,2.5));place('Railing_2m',(x+1,0,2.5))
place('Landing_2m',(5,1,2.5));place('Stair_2_5m',(8.1,1,0),math.pi/2)

# Final arena, orange exit tower and props.
for x,y,angle in [(9,7,0),(17,5,math.pi/2),(14,12,0)]:place('Cover_Low',(x,y,0),angle)
place('Cover_Pillar',(12,8.5,0))
for x,y in [(7,12),(20,10),(18,2.5)]:place('Training_Dummy',(x,y,0),math.pi)
place('Pallet',(21,3,0));place('Crate_Large',(21,3,.21))
place('Utility_Drum',(22.4,4.3,0));place('Crate_Small',(6,5,0))
place('Ammo_Pickup',(10,3,0));place('Ammo_Pickup',(20,13,0))
place('Checkpoint_Beacon',(6,14,0))
place('Milk_Exit_Tower',(23,19,0));place('Exit_Door',(23,16.59,.08))
place('Milk_Bottle',(23,15.9,.8));place('Depot_Terminal',(25.2,15.6,0))
for x in [21,25]:place('Safety_Bollard',(x,15.4,0))
place('Arrow_Sign',(22,15.76,2.25),math.pi)
place('Waste_Bin',(-26.8,-18.5,0))

# Additional structural detail emphasizes construction rather than raw cubes.
for x,y in [(-28,-20),(-20,-20),(-28,4),(-28,16),(4,16),(24,0),(24,16),(-8,-20)]:place('Wall_CornerColumn',(x,y,0))
for x,y,a in [(-26,-19.80,0),(-18,-19.80,0),(-10,-19.80,0),(-27.79,-6,math.pi/2),(-27.79,1,math.pi/2),(23.79,4,-math.pi/2),(23.79,11,-math.pi/2)]:place('Wall_Light',(x,y,2.5),a)
for x in [-27,-23,-19,-15,-11]:place('Cyan_LightStrip_2m',(x,1.93,2.51))

# Library showroom with asset labels, separated from assembled level.
bpy.context.window.scene=library
for i,(key,o) in enumerate(masters.items()):
    o.location=((i%8)*7,(i//8)*8,0)
    curve=bpy.data.curves.new(key+' caption','FONT');curve.body=key.replace('_',' ');curve.size=.34;curve.align_x='CENTER'
    label=bpy.data.objects.new(key+' caption',curve);library.collection.objects.link(label)
    label.location=(o.location.x,o.location.y-3.0,.025)
    label.data.materials.append(materials['White'])

def camera(scene,name,position,target,ortho=None,lens=28):
    d=bpy.data.cameras.new(name);o=bpy.data.objects.new(name,d);scene.collection.objects.link(o)
    o.location=position;o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
    d.lens=lens;d.clip_end=500
    if ortho:d.type='ORTHO';d.ortho_scale=ortho
    scene.camera=o;return o

def lighting(scene):
    world=bpy.data.worlds.new(scene.name+' world');world.use_nodes=True;world.node_tree.nodes['Background'].inputs[0].default_value=(.12,.17,.22,1);world.node_tree.nodes['Background'].inputs[1].default_value=.45;scene.world=world
    d=bpy.data.lights.new('Large soft daylight','AREA');d.energy=14000;d.shape='DISK';d.size=32
    o=bpy.data.objects.new('Large soft daylight',d);scene.collection.objects.link(o);o.location=(-10,-16,35);o.rotation_euler=(Vector((0,0,0))-o.location).to_track_quat('-Z','Y').to_euler()
    d=bpy.data.lights.new('Directional fill','SUN');d.energy=1.8;d.angle=.35
    o=bpy.data.objects.new('Directional fill',d);scene.collection.objects.link(o);o.rotation_euler=(.45,-.25,-.5)
    scene.render.engine='CYCLES';scene.cycles.samples=24;scene.cycles.use_denoising=True;scene.cycles.device='CPU'
    scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG'
    scene.view_settings.view_transform='AgX'

lighting(level);lighting(library)
overview=camera(level,'Overview',(-68,-78,72),(-2,0,0),ortho=75)
eye=camera(level,'Courtyard player view',(-21,-10,1.7),(-14,2,1.7),lens=22)
final=camera(level,'Final arena player view',(7,2,1.7),(23,17,3),lens=22)
camera(library,'Kit overview',(72,-64,73),(24,20,0),ortho=72)

# Export each asset at the same consistent local origin, independent of showroom placement.
bpy.context.window.scene=library
for key,o in masters.items():
    old=o.location.copy();o.location=(0,0,0)
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.export_scene.gltf(filepath=str(ASSETS/'Models'/f'{key}.glb'),export_format='GLB',use_selection=True,use_active_scene=True,
        export_animations=False,export_extras=True,export_yup=True,export_apply=True)
    o.location=old
print('EXPORTED',len(masters),'modular assets',flush=True)
level.camera=overview
bpy.context.window.scene=level
# Export only environment meshes, without presentation cameras or lights.
bpy.ops.object.select_all(action='DESELECT')
for o in level.objects:
    if o.type=='MESH':o.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(ASSETS/'MilkDepot_Assembled.glb'),export_format='GLB',use_selection=True,use_active_scene=True,
    export_animations=False,export_extras=True,export_yup=True,export_apply=True)

for c in catalog:
    c['pivot']='floor-centred, Blender Z-up; exported glTF Y-up'
report={'version':1,'units':'metres','unique_assets':len(catalog),'instances':len(placements),'shared_materials':len(materials),
        'source':'MilkDepot_Environment.blend','assets':catalog,'placements':placements,
        'notes':['No active game scene was replaced.','Training dummy is static, not rigged.','Lighting and gameplay require engine components.','Collision specs are guidance, not imported physics components.']}
(OUT/'asset_catalog.json').write_text(json.dumps(report,indent=2))
(ASSETS/'asset_catalog.json').write_text(json.dumps(report,indent=2))
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'MilkDepot_Environment.blend'))
for scene,cam,filename,size in [(level,overview,'MilkDepot_Overview.png',(1680,1200)),
                               (level,eye,'MilkDepot_Courtyard.png',(1600,900)),
                               (level,final,'MilkDepot_FinalArena.png',(1600,900)),
                               (library,library.camera,'MilkDepot_Kit.png',(1680,1200))]:
    bpy.context.window.scene=scene;scene.camera=cam;scene.render.resolution_x=size[0];scene.render.resolution_y=size[1]
    scene.render.filepath=str(OUT/'Previews'/filename)
    print('RENDER_START',filename,flush=True);bpy.ops.render.render(write_still=True);print('RENDER_DONE',filename,flush=True)
bpy.context.window.scene=level;level.camera=overview
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'MilkDepot_Environment.blend'))
result={'assets':len(catalog),'instances':len(placements),'source':str(OUT/'MilkDepot_Environment.blend')}
