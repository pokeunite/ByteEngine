"""Editable race props. Invoked inside Blender through the installed MCP bridge."""
import bpy,math,json,random
from mathutils import Vector
from pathlib import Path
ROOT=Path('C:/Users/codex/Documents/DuneCompany');OUT=ROOT/'Assets/RaceKit';ART=ROOT/'Art/RaceKit';OUT.mkdir(parents=True,exist_ok=True);ART.mkdir(parents=True,exist_ok=True)
PARTS=[];PATCHES=[(.20,.24,.17),(.12,.13,.12),(.64,.58,.43),(.73,.26,.055),(.065,.067,.059),(.40,.38,.32),(.85,.83,.73),(.28,.12,.055),(.42,.29,.17)]

def initialize():
    collection=bpy.data.collections.get('Dune Race Kit')
    if collection is None:
        collection=bpy.data.collections.new('Dune Race Kit');bpy.context.scene.collection.children.link(collection)
    return collection

def material(index):
    name='Race patch '+str(index);m=bpy.data.materials.get(name)
    if m:return m
    m=bpy.data.materials.new(name);m.use_nodes=True;n=m.node_tree.nodes;n.clear();out=n.new('ShaderNodeOutputMaterial');bsdf=n.new('ShaderNodeBsdfPrincipled');m.node_tree.links.new(bsdf.outputs['BSDF'],out.inputs['Surface']);bsdf.inputs['Base Color'].default_value=(*PATCHES[index],1);bsdf.inputs['Roughness'].default_value=.72;bsdf.inputs['Metallic'].default_value=.45 if index in [0,1,2,3,7] else 0
    # Preview uses the same atlas as the exported game asset.
    tex=n.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(ROOT/'Assets/ContractLevels/rock_09_diff_2k.jpg') if index==8 else str(OUT/'race-kit-albedo.png'),check_existing=True);m.node_tree.links.new(tex.outputs['Color'],bsdf.inputs['Base Color'])
    if index==8:bsdf.inputs['Metallic'].default_value=0;bsdf.inputs['Roughness'].default_value=.93
    return m

def add(obj,name,index):
    obj.name=name
    for c in list(obj.users_collection):c.objects.unlink(obj)
    initialize().objects.link(obj);obj.data.materials.clear();obj.data.materials.append(material(index));obj['race_patch']=index;PARTS.append(obj);return obj

def cube(name,p,s,index=0,bevel=.025):
    bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=add(bpy.context.object,name,index);o.dimensions=s;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if bevel:
        mod=o.modifiers.new('Manufactured edges','BEVEL');mod.width=min(bevel,min(s)*.2);mod.segments=2;bpy.ops.object.modifier_apply(modifier=mod.name)
    return o

def cylinder(name,p,r,h,index=1,vertices=24,axis=None):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=r,depth=h,location=p);o=add(bpy.context.object,name,index)
    if axis:o.rotation_euler=Vector(axis).to_track_quat('Z','Y').to_euler()
    for f in o.data.polygons:f.use_smooth=len(f.vertices)==4
    return o

def cone(name,p,r1,r2,h,index=3,vertices=32):
    bpy.ops.mesh.primitive_cone_add(vertices=vertices,radius1=r1,radius2=r2,depth=h,location=p);o=add(bpy.context.object,name,index)
    for f in o.data.polygons:f.use_smooth=len(f.vertices)==4
    return o

def torus(name,p,major,minor,index=1,axis=None):
    bpy.ops.mesh.primitive_torus_add(major_segments=32,minor_segments=8,location=p,major_radius=major,minor_radius=minor);o=add(bpy.context.object,name,index)
    if axis:o.rotation_euler=Vector(axis).to_track_quat('Z','Y').to_euler()
    for f in o.data.polygons:f.use_smooth=True
    return o

def beam(name,a,b,width=.07,index=1):
    a,b=Vector(a),Vector(b);o=cube(name,(a+b)*.5,(width,width,(b-a).length),index,min(.015,width*.15));o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler();return o

def text(name,value,p,size,index=6):
    curve=bpy.data.curves.new(name,'FONT');curve.body=value;curve.size=size;curve.align_x='CENTER';curve.extrude=.002;curve.bevel_depth=.001;curve.font=bpy.data.fonts.load(str(ROOT/'Assets/GarageUI/fonts/BarlowCondensed-SemiBold.ttf'))
    obj=bpy.data.objects.new(name,curve);bpy.context.scene.collection.objects.link(obj);obj.location=p;obj.rotation_euler=(math.pi/2,0,0);bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.convert(target='MESH');return add(bpy.context.object,name,index)

def tyre(p=(0,0,.21)):
    torus('Tyre carcass',p,.33,.16,4);torus('Inner bead',(p[0],p[1],p[2]+.12),.20,.035,4);torus('Sidewall moulding',(p[0],p[1],p[2]+.14),.34,.018,4)
    for j in range(24):
        a=j*math.tau/24;o=cube('Tread block',(p[0]+.46*math.cos(a),p[1]+.46*math.sin(a),p[2]),(.11,.075,.22),4,.012);o.rotation_euler.z=a

def lattice(name,p,width,height):
    x,y,z=p
    for xx in [-width/2,width/2]:
        for yy in [-width/2,width/2]:beam(name+' upright',(x+xx,y+yy,z),(x+xx,y+yy,z+height),.085)
    bays=max(1,int(height/1.5))
    for i in range(bays):
        lo=z+height*i/bays;hi=z+height*(i+1)/bays
        for yy in [-width/2,width/2]:beam(name+' cross',(x-width/2,y+yy,lo),(x+width/2,y+yy,hi),.045);beam(name+' cross',(x+width/2,y+yy,lo),(x-width/2,y+yy,hi),.045)
        for xx in [-width/2,width/2]:beam(name+' side brace',(x+xx,y-width/2,lo),(x+xx,y+width/2,hi),.045)
    cube(name+' foot',(x,y,z+.03),(width+.35,width+.35,.06),1)
    for xx in [-width*.45,width*.45]:
        for yy in [-width*.45,width*.45]:cylinder('Foot bolt',(x+xx,y+yy,z+.09),.045,.05,1,8)

def corrugated_wall(name,x,y,z,w,h,axis='x'):
    if axis=='x':
        cube(name,(x,y,z),(w,.06,h),0,.01)
        for i in range(max(1,int(w/.18))):cube('Pressed corrugation',(x-w/2+i*.18,y-.04,z),(.07,.08,h),0,.015)
    else:
        cube(name,(x,y,z),(.06,w,h),0,.01)
        for i in range(max(1,int(w/.18))):cube('Pressed corrugation',(x-.04,y-w/2+i*.18,z),(.08,.07,h),0,.015)

def create(name):
    global PARTS;PARTS=[];initialize();random.seed(name)
    # Existing generated props remain editable in their named asset collections.
    old=bpy.data.collections.get('Asset / '+name)
    if old:
        for o in list(old.objects):bpy.data.objects.remove(o,do_unlink=True)
        bpy.data.collections.remove(old)
    if name=='tyre-barrier':
        for x in [-.85,0,.85]:
            for z in [.2,.53,.86]:tyre((x,0,z))
        beam('Retaining strap',(-1.25,0,1.03),(1.25,0,1.03),.045,1)
    elif name=='loose-tyre':tyre()
    elif name=='traffic-cone':
        cube('Rubber foot',(0,0,.04),(.52,.52,.08),4,.04);cone('Orange cone',(0,0,.39),.21,.055,.65);cone('Reflective band',(0,0,.48),.145,.117,.13,6);torus('Base bead',(0,0,.09),.195,.012,3)
    elif name=='chevron-sign':
        cube('Board',(0,0,1.35),(2.4,.08,1.1),3,.045)
        for x in [-.85,.85]:cube('Post',(x,0,.7),(.08,.10,1.4),1)
        for cx in [-.72,0,.72]:
            beam('Chevron black',(cx-.21,-.051,1.67),(cx+.10,-.051,1.35),.15,4);beam('Chevron black',(cx+.10,-.051,1.35),(cx-.21,-.051,1.03),.15,4)
        for x in [-1.05,1.05]:
            for z in [.93,1.77]:cylinder('Board rivet',(x,-.058,z),.035,.028,1,8,(0,1,0))
    elif name=='route-flag':
        cube('Pole base',(0,0,.055),(.6,.6,.11),1);cylinder('Flag pole',(0,0,1.7),.035,3.4)
        vertices=[];faces=[]
        for i in range(13):
            for j in range(6):vertices.append((i/12*.95,.06*math.sin(i*.8+j*.5)*i/12,3.25-j/5*.9))
        for i in range(12):
            for j in range(5):a=i*6+j;faces.append((a,a+6,a+7,a+1))
        mesh=bpy.data.meshes.new('Folded fabric');mesh.from_pydata(vertices,[],faces);o=bpy.data.objects.new('Orange flag',mesh);bpy.context.scene.collection.objects.link(o);add(o,o.name,3)
        text('Flag mark','D',( .48,-.07,2.68),.42,6)
    elif name=='finish-gantry':
        for x in [-6,6]:lattice('Finish tower',(x,0,0),.85,5.5)
        beam('Top truss',(-6,0,5.5),(6,0,5.5),.18);beam('Top truss',(-6,0,6.15),(6,0,6.15),.14)
        for i in range(12):beam('Top diagonal',(-6+i,0,5.5),(-5+i,0,6.15),.055)
        cube('Orange banner',(0,-.08,5.53),(11.2,.12,1.1),3,.03);text('Finish letters','FINISH',(0,-.16,5.10),1.06)
    elif name=='launch-tree':
        cube('Weighted platform',(0,0,.08),(.9,.7,.16),1,.04);cylinder('Mast',(0,0,1.50),.09,2.8);cube('Signal backplate',(0,0,1.93),(.7,.11,1.80),1,.05)
        for x in [-.19,.19]:
            for i,z in enumerate([2.54,2.24,1.94,1.64,1.34]):
                cylinder('Lens housing',(x,-.095,z),.13,.14,4,24,(0,1,0));cylinder('Lens '+str(i),(x,-.18,z),.103,.03,3 if i<3 else 7 if i==3 else 0,24,(0,1,0));torus('Lens bezel',(x,-.195,z),.112,.014,1,(0,1,0))
        cube('Ready sign',(0,0,3.01),(1.15,.08,.38),1);text('Ready letters','READY',(0,-.06,2.90),.25)
        cube('Electrical junction',(0,.12,.47),(.38,.22,.5),0);beam('Cable conduit',(0,.13,.65),(0,.13,2.8),.023,4)
    elif name=='container':
        cube('Container floor',(0,0,.14),(6.0,2.4,.28),1,.03)
        for y in [-1.2,1.2]:corrugated_wall('Long wall',0,y,1.45,6,2.55)
        for x in [-3,3]:corrugated_wall('End wall',x,0,1.45,2.4,2.55,'y')
        cube('Roof',(0,0,2.77),(6.05,2.45,.10),0)
        for y in [-1.23,1.23]:
            for z in [.15,2.72]:beam('Container frame',(-3,y,z),(3,y,z),.13,1)
        for x in [-3,3]:
            for y in [-1.18,1.18]:
                cube('Corner casting',(x,y,.17),(.20,.20,.24),1);cube('Corner casting',(x,y,2.66),(.20,.20,.24),1)
        for y in [-.58,.58]:
            beam('Door locking rod',(-3.07,y,.4),(-3.07,y,2.4),.045,1)
            for z in [.45,1.4,2.35]:cube('Door hinge',(-3.12,y,z),(.09,.20,.07),1)
        cube('Orange stripe',(0,-1.255,1.48),(.42,.025,2.3),3);text('Container number','DUNE / 07',(1.2,-1.28,1.48),.27)
    elif name=='workshop-shed':
        for x in [-4,4]:corrugated_wall('Shed side',x,0,1.8,5.5,3.6,'y')
        corrugated_wall('Rear wall',0,2.75,1.8,8,3.6)
        for x in [-3,3]:corrugated_wall('Front pier',x,-2.75,1.8,2,3.6)
        for x in [-2,2]:beam('Door frame',(x,-2.8,0),(x,-2.8,3.3),.14,1)
        beam('Door header',(-2,-2.8,3.3),(2,-2.8,3.3),.16,1)
        for side in [-1,1]:
            o=cube('Pitched roof',(side*2,0,3.87),(4.25,5.8,.10),2,.02);o.rotation_euler.y=side*math.atan(.7/4)
            for i in range(30):o=cube('Roof rib',(side*2,-2.8+i*.19,3.95),(4.28,.055,.06),2,.01);o.rotation_euler.y=side*math.atan(.7/4)
        cube('Shop sign',(0,-2.88,3.15),(4.3,.06,.62),1);text('Shop lettering','DUNE COMPANY',(0,-2.94,2.97),.43)
        cube('Service apron',(0,-2.9,.03),(8.3,4,.06),5);cube('Workbench',(2,1,.85),(2,.8,.15),2)
        for x in [1.2,2.8]:
            for y in [.7,1.3]:beam('Bench leg',(x,y,.05),(x,y,.85),.06,1)
    elif name=='service-canopy':
        for x in [-2,2]:
            for y in [-1.6,1.6]:beam('Canopy pole',(x,y,0),(x,y,2.7),.065,1);beam('Guy rope',(x,y,2.6),(x*1.4,y*1.5,.1),.012,4)
        verts=[];faces=[]
        for i in range(17):
            for j in range(13):x=-2+i/4;y=-1.6+j*3.2/12;verts.append((x,y,2.7+.28*(1-(x/2)**2)-.04*math.cos(y*3)))
        for i in range(16):
            for j in range(12):a=i*13+j;faces.append((a,a+13,a+14,a+1))
        mesh=bpy.data.meshes.new('Canopy cloth');mesh.from_pydata(verts,[],faces);o=bpy.data.objects.new('Canvas canopy',mesh);bpy.context.scene.collection.objects.link(o);add(o,o.name,2)
    elif name=='fuel-tank':
        cylinder('Tank shell',(0,0,1.15),.65,3.2,2,40,(1,0,0))
        for x in [-1.6,1.6]:cylinder('Dished end',(x,0,1.15),.64,.10,2,40,(1,0,0));torus('Tank hoop',(x*.70,0,1.15),.655,.025,1,(1,0,0))
        for x in [-.95,.95]:cube('Tank saddle',(x,0,.31),(.22,1.12,.62),1);cube('Tank foot',(x,0,.045),(.45,1.4,.09),1)
        cylinder('Inspection cap',(0,0,1.85),.16,.09,1);beam('Outlet pipe',(1.7,0,1.1),(1.7,-.48,1.1),.09,1);torus('Valve wheel',(1.7,-.48,1.36),.11,.016,3)
        text('Fuel stencil','FUEL',(0,-.65,1.04),.25,1)
    elif name=='barrel':
        cylinder('Barrel shell',(0,0,.46),.29,.90,0,32)
        for z in [.04,.28,.66,.88]:torus('Rolled barrel ring',(0,0,z),.292,.015,1)
        cylinder('Barrel lid',(0,0,.925),.284,.018,1,32);cylinder('Bung',( .13,0,.95),.035,.018,1,12);cube('Hazard label',(0,-.296,.46),(.18,.014,.22),3)
    elif name=='utility-crate':
        cube('Equipment case',(0,0,.48),(1.35,.85,.96),0,.055)
        for x in [-.58,.58]:
            for y in [-.35,.35]:cube('Corner protector',(x,y,.48),(.09,.10,.88),1)
        cube('Lid',(0,0,1.0),(1.39,.89,.10),0,.03)
        for x in [-.35,.35]:cube('Case latch',(x,-.445,.87),(.12,.05,.15),1)
        beam('Handle',(-.18,-.46,.52),(.18,-.46,.52),.045,1)
    elif name=='quarry-conveyor':
        for y in [-2.5,2.5]:
            for x in [-.85,.85]:beam('Conveyor leg',(x,y,0),(x,y,2.5+(y+2.5)*.4),.12,1)
        for x in [-.75,.75]:beam('Belt rail',(x,-4,1.9),(x,4,5.1),.15,1);beam('Lattice rail',(x,-4,1.15),(x,4,4.35),.10,1)
        for i in range(16):
            y=-4+i*.5;z=1.9+(y+4)*.4;cylinder('Belt roller',(0,y,z),.11,1.65,1,16,(1,0,0));beam('Conveyor bracing',(-.75,y,z-.75),(-.75,y+.5,z+.2),.055,1);beam('Conveyor bracing',(.75,y,z-.75),(.75,y+.5,z+.2),.055,1)
        belt=cube('Rubber belt',(0,0,3.59),(1.35,8.7,.07),4,.01);belt.rotation_euler.x=math.atan(.4)
        cube('Feed hopper',(0,-3.4,2.5),(1.8,1.4,.55),0,.04);cube('Motor housing',(1.0,3.7,4.7),(.65,.9,.45),0,.05)
        for i in range(8):beam('Hopper railing',(-1,-3.6+i*.2,2.9),(-1,-3.6+i*.2,3.3),.025,1)
    elif name=='silo':
        for x,y in [(-1,-1),(-1,1),(1,-1),(1,1)]:beam('Silo leg',(x,y,0),(x,y,2.8),.14,1)
        cone('Hopper',(0,0,2.15),.25,1.23,1.65,2);cylinder('Storage cylinder',(0,0,4.52),1.23,3.15,2,40);cone('Silo roof',(0,0,6.27),1.26,.18,.48,1)
        for z in [3.1,4.3,5.7]:torus('Seam band',(0,0,z),1.245,.018,1)
        for x in [-.24,.24]:beam('Ladder rail',(x,-1.34,.1),(x,-1.34,6.12),.045,1)
        for i in range(23):beam('Ladder rung',(-.24,-1.35,.15+i*.26),(.24,-1.35,.15+i*.26),.03,1)
        torus('Roof rail',(0,0,6.66),.8,.028,1)
        for a in range(8):x,y=.8*math.cos(a*math.pi/4),.8*math.sin(a*math.pi/4);beam('Roof stanchion',(x,y,6.3),(x,y,6.66),.025,1)
        cube('Orange identification band',(0,-1.25,4.7),(.6,.025,1.2),3);text('Silo number','07',(0,-1.28,4.75),.38,6)
    elif name=='service-mast':
        lattice('Service mast',(0,0,0),1.1,8)
        for z in [3.5,7]:
            cube('Access deck',(0,0,z),(2.1,2.1,.10),1)
            for side in [-1,1]:beam('Handrail',(-1,side,z+.9),(1,side,z+.9),.04,1);beam('Handrail',(side,-1,z+.9),(side,1,z+.9),.04,1)
        for x in [-.24,.24]:beam('Access ladder',(x,-.65,0),(x,-.65,7),.035,1)
        for i in range(27):beam('Mast rung',(-.24,-.65,i*.26),(.24,-.65,i*.26),.025,1)
        cube('Electrical cabinet',(0,0,7.55),(.75,.6,.75),0,.03)
    elif name.startswith('cliff-'):
        # Remodel the licensed scan in Blender, preserving its photographed UVs.
        # Broad interlocking buttresses replace the first pass's repetitive columns.
        variant=int(name[-1]);rng=random.Random(430+variant)
        for j in range(4):
            bpy.ops.wm.obj_import(filepath=str(ROOT/'Assets/ContractLevels/rock_09.obj'),forward_axis='NEGATIVE_Z',up_axis='Y')
            o=bpy.context.object;add(o,'Eroded sandstone buttress '+str(j),8);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
            simplify=o.modifiers.new('Landscape scan detail','DECIMATE');simplify.ratio=.5;bpy.ops.object.modifier_apply(modifier=simplify.name)
            bounds=[Vector(v) for v in o.bound_box];lo=Vector(tuple(min(v[k] for v in bounds) for k in range(3)));hi=Vector(tuple(max(v[k] for v in bounds) for k in range(3)));size=hi-lo
            target=Vector((rng.uniform(3.1,4.1),rng.uniform(2.7,3.8),rng.uniform(3.2,5.1)))
            for v in o.data.vertices:v.co=Vector(((v.co.x-(lo.x+hi.x)/2)*target.x/size.x,(v.co.y-(lo.y+hi.y)/2)*target.y/size.y,(v.co.z-lo.z)*target.z/size.z))
            o.location=((-2.0 if j%2==0 else 1.9)+rng.uniform(-.3,.3),(-1 if j<2 else 1)+rng.uniform(-.3,.3),-.05);o.rotation_euler.z=rng.uniform(-.35,.35)
            # Scan geometry has real fissures; retain hard faces and authored normals.
    elif name=='dry-bush':
        rng=random.Random(94)
        for j in range(28):
            angle=rng.random()*math.tau;tip=Vector((rng.uniform(.25,.65)*math.cos(angle),rng.uniform(.25,.65)*math.sin(angle),rng.uniform(.35,.8)));cylinder('Dry woody stem',tip*.5,.009,tip.length,2,5,tip)
            for sign in [-1,1]:
                a=tip*.5;b=Vector((tip[0]+sign*.12,tip[1]+sign*.06,tip[2]*.85));cylinder('Twig',(a+b)*.5,.006,(b-a).length,2,5,b-a)
    else:raise ValueError(name)
    return export(name)

def export(name):
    bpy.ops.object.select_all(action='DESELECT')
    for o in PARTS:o.select_set(True)
    bpy.context.view_layer.objects.active=PARTS[0];bpy.ops.object.join();obj=bpy.context.object;obj.name=name
    bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
    collection=bpy.data.collections.new('Asset / '+name);initialize().children.link(collection)
    for c in list(obj.users_collection):c.objects.unlink(obj)
    collection.objects.link(obj)
    if not name.startswith('cliff-'):
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.02);bpy.ops.object.mode_set(mode='OBJECT')
    uv=obj.data.uv_layers.active.data
    for face in obj.data.polygons:
        mat=obj.data.materials[face.material_index];patch=int(mat.name.split(' ')[-1])
        for loop in face.loop_indices:
            u,v=uv[loop].uv;uv[loop].uv=(u,v) if patch==8 else ((patch+.04+u*.92)/8,.025+v*.95)
    # One shared PBR atlas/material permits instancing without dozens of material draws.
    atlas=bpy.data.materials.get('Race kit atlas')
    if not atlas:
        atlas=material(0).copy();atlas.name='Race kit atlas';n=atlas.node_tree.nodes;bsdf=next(n for n in n if n.type=='BSDF_PRINCIPLED');packed=n.new('ShaderNodeTexImage');packed.image=bpy.data.images.load(str(OUT/'race-kit-pbr.png'),check_existing=True);packed.image.colorspace_settings.name='Non-Color';sep=n.new('ShaderNodeSeparateColor');atlas.node_tree.links.new(packed.outputs['Color'],sep.inputs['Color']);atlas.node_tree.links.new(sep.outputs['Green'],bsdf.inputs['Roughness']);atlas.node_tree.links.new(sep.outputs['Blue'],bsdf.inputs['Metallic']);normal=n.new('ShaderNodeTexImage');normal.image=bpy.data.images.load(str(OUT/'race-kit-normal.png'),check_existing=True);normal.image.colorspace_settings.name='Non-Color';nm=n.new('ShaderNodeNormalMap');atlas.node_tree.links.new(normal.outputs['Color'],nm.inputs['Color']);atlas.node_tree.links.new(nm.outputs['Normal'],bsdf.inputs['Normal'])
    obj.data.materials.clear();obj.data.materials.append(material(8) if name.startswith('cliff-') else atlas)
    for f in obj.data.polygons:f.material_index=0
    # Origin remains at the ground plane; keep every source mesh editable in the .blend.
    bpy.ops.wm.obj_export(filepath=str(OUT/(name+'.obj')),export_selected_objects=True,forward_axis='NEGATIVE_Z',up_axis='Y',export_materials=True,export_normals=True)
    bpy.ops.export_scene.gltf(filepath=str(ART/(name+'.glb')),export_format='GLB',use_selection=True,export_animations=False,export_apply=True)
    # Portable OBJ material references resolve beside the model in ByteEngine.
    texture='../ContractLevels/rock_09_diff_2k.jpg' if name.startswith('cliff-') else 'race-kit-albedo.png'
    (OUT/(name+'.mtl')).write_text('newmtl Race_kit_atlas\nKd 1 1 1\nKs 0.1 0.1 0.1\nNs 32\nmap_Kd '+texture+'\n')
    path=OUT/(name+'.obj');content=path.read_text();content='\n'.join('usemtl Race_kit_atlas' if line.startswith('usemtl ') else line for line in content.splitlines());path.write_text(content+'\n')
    bounds=[list(obj.matrix_world@Vector(v)) for v in obj.bound_box];stats={'name':name,'vertices':len(obj.data.vertices),'triangles':sum(len(f.vertices)-2 for f in obj.data.polygons),'bounds':bounds}
    duplicate=obj.copy();duplicate.data=obj.data.copy();bpy.context.scene.collection.objects.link(duplicate);bpy.ops.object.select_all(action='DESELECT');duplicate.select_set(True);bpy.context.view_layer.objects.active=duplicate
    mod=duplicate.modifiers.new('Distance detail','DECIMATE');mod.ratio=.18;bpy.ops.object.modifier_apply(modifier=mod.name)
    bpy.ops.wm.obj_export(filepath=str(OUT/(name+'-lod.obj')),export_selected_objects=True,forward_axis='NEGATIVE_Z',up_axis='Y',export_materials=True,export_normals=True)
    (OUT/(name+'-lod.mtl')).write_text('newmtl Race_kit_atlas\nKd 1 1 1\nNs 32\nmap_Kd '+texture+'\n');lodpath=OUT/(name+'-lod.obj');lodpath.write_text('\n'.join('usemtl Race_kit_atlas' if line.startswith('usemtl ') else line for line in lodpath.read_text().splitlines())+'\n');stats['lodTriangles']=sum(len(f.vertices)-2 for f in duplicate.data.polygons);bpy.data.objects.remove(duplicate,do_unlink=True)
    (OUT/(name+'-stats.json')).write_text(json.dumps(stats,indent=2));return stats

def stage_preview():
    scene=bpy.context.scene
    # Display assets on a neutral contact sheet, leaving their authored source coordinates unchanged.
    for i,c in enumerate(initialize().children):
        for obj in c.objects:obj.location=((i%5)*13,(i//5)*13,0)
    for o in list(scene.objects):
        if o.name in ['Cube','Camera','Light'] or o.name.startswith(('Preview ground','Preview key','Preview sun','Race kit preview camera')):bpy.data.objects.remove(o,do_unlink=True)
    bpy.ops.mesh.primitive_plane_add(size=100,location=(25,20,-.03));plane=bpy.context.object;plane.name='Preview ground';m=bpy.data.materials.new('Preview sand');m.diffuse_color=(.50,.43,.31,1);plane.data.materials.append(m)
    bpy.ops.object.camera_add(location=(72,-56,76));cam=bpy.context.object;cam.name='Race kit preview camera';cam.rotation_euler=(Vector((24,20,1))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=85;scene.camera=cam
    bpy.ops.object.light_add(type='AREA',location=(22,-20,50));light=bpy.context.object;light.name='Preview key';light.data.energy=12000;light.data.shape='DISK';light.data.size=35
    bpy.ops.object.light_add(type='SUN',location=(0,0,20));sun=bpy.context.object;sun.name='Preview sun';sun.rotation_euler=(.3,-.6,-.3);sun.data.energy=2.0;scene.world.color=(.22,.25,.30)
    scene.render.engine='BLENDER_EEVEE_NEXT';scene.render.resolution_x=1800;scene.render.resolution_y=1200;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.filepath=str(ART/'RaceKit-contact-sheet.png');bpy.ops.wm.save_as_mainfile(filepath=str(ART/'DuneCompany-RaceKit.blend'))
    return {'file':str(ART/'DuneCompany-RaceKit.blend'),'preview':scene.render.filepath}
