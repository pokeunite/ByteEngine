"""Goblin Scraper reference kit, Blender 5.0. Units: metres, Blender Z-up / GLB Y-up.
Run blender --background --factory-startup --python build_refined.py -- <output-folder>.
"""
import bpy, math, os, sys, json, random, struct, traceback
import numpy as np
from mathutils import Vector, Quaternion, Matrix
from pathlib import Path

OUT = Path(sys.argv[sys.argv.index('--')+1]) if '--' in sys.argv else Path('output/refined')
OUT.mkdir(parents=True,exist_ok=True)
(OUT/'textures').mkdir(exist_ok=True)
(OUT/'previews').mkdir(exist_ok=True)
(OUT/'red_faction').mkdir(exist_ok=True)
random.seed(711)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
for col in list(bpy.data.collections):
    if col.name != 'Collection': bpy.data.collections.remove(col)
SCENE=bpy.context.scene
SCENE.unit_settings.system='METRIC'; SCENE.unit_settings.scale_length=1
SCENE.render.engine='BLENDER_EEVEE'
SCENE.render.resolution_x=512; SCENE.render.resolution_y=512; SCENE.render.resolution_percentage=100
SCENE.render.image_settings.file_format='PNG'
SCENE.render.film_transparent=False
SCENE.view_settings.view_transform='AgX'
SCENE.world.color=(.25,.25,.25)
SCENE.world.use_nodes=True
SCENE.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.45,.47,.46,1)
SCENE.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.55
SCENE.render.fps=30
SCENE.frame_start=1; SCENE.frame_end=61
MATS={}; ALL=[]; CUR=None

def texture(name, color, kind='metal', size=768):
    rng=np.random.default_rng(sum(map(ord,name))+71)
    y,x=np.mgrid[0:size,0:size]
    def coarse(s):
        grid=rng.random((s,s))
        # Smooth periodic interpolation, no additional Python packages.
        xx=x/size*s; yy=y/size*s
        xi=np.floor(xx).astype(int); yi=np.floor(yy).astype(int)
        fx=xx-xi; fy=yy-yi
        fx=fx*fx*(3-2*fx); fy=fy*fy*(3-2*fy)
        return ((1-fy)*((1-fx)*grid[yi%s,xi%s]+fx*grid[yi%s,(xi+1)%s])+
                fy*((1-fx)*grid[(yi+1)%s,xi%s]+fx*grid[(yi+1)%s,(xi+1)%s]))
    n=coarse(8)*.48+coarse(27)*.28+coarse(84)*.14+rng.random((size,size))*.10
    arr=np.ones((size,size,4),dtype=np.float32)
    c=np.array(color)
    arr[:,:,:3]=c[None,None,:]*(.72+n[:,:,None]*.50)
    rough=np.clip(.45+n*.32,.15,.95)
    if kind=='paint':
        rust=coarse(20)*.6+coarse(65)*.4
        mask=rust>.63
        arr[mask,:3]=np.array([.28,.115,.052])*(.6+n[mask,None]*.8)
        bare=(rust>.59)&(rust<.625)
        arr[bare,:3]=np.array([.18,.20,.205])*(.55+n[bare,None])
        pits=rng.random((size,size))>.992
        arr[pits,:3]*=.25
        rough=np.clip(.5+n*.4,.25,1)
    elif kind=='wood':
        grain=np.sin(y*.14+coarse(6)*18)*.07+np.sin(y*.8+x*.006)*.035
        arr[:,:,:3]*=(1+grain[:,:,None]*3)
        rough=np.clip(.66+n*.25,0,1)
    elif kind=='rubber':
        arr[:,:,:3]*=.85; rough=np.clip(.68+n*.25,0,1)
    elif kind=='steel':
        grain=(np.sin(x*.8+y*.04)*.025+coarse(35)*.05)
        arr[:,:,:3]+=grain[:,:,None]; rough=.27+n*.30
    arr[:,:,:3]=np.clip(arr[:,:,:3],0,1)
    img=bpy.data.images.new(name+'_albedo',size,size,alpha=True)
    img.pixels.foreach_set(arr.ravel()); img.filepath_raw=str(OUT/'textures'/f'{name}_albedo.png'); img.file_format='PNG'; img.save(); img.pack()
    r=np.ones_like(arr); r[:,:,:3]=rough[:,:,None]
    ri=bpy.data.images.new(name+'_roughness',size,size,alpha=True)
    ri.colorspace_settings.name='Non-Color'; ri.pixels.foreach_set(r.ravel())
    ri.filepath_raw=str(OUT/'textures'/f'{name}_roughness.png'); ri.file_format='PNG'; ri.save(); ri.pack()
    return img,ri

def material(key,color,kind,metallic):
    m=bpy.data.materials.new('Goblin_'+key); m.diffuse_color=(*color,1); m.use_nodes=True
    bs=m.node_tree.nodes.get('Principled BSDF'); bs.inputs['Metallic'].default_value=metallic
    img,rough=texture(key,color,kind)
    t=m.node_tree.nodes.new('ShaderNodeTexImage'); t.image=img
    r=m.node_tree.nodes.new('ShaderNodeTexImage'); r.image=rough
    m.node_tree.links.new(t.outputs['Color'],bs.inputs['Base Color'])
    m.node_tree.links.new(r.outputs['Color'],bs.inputs['Roughness'])
    MATS[key]=m; return m

material('green',(.235,.285,.095),'paint',.72)
material('red',(.39,.085,.04),'paint',.72)
material('iron',(.15,.17,.18),'metal',.86)
material('steel',(.43,.46,.46),'steel',.92)
material('rust',(.28,.105,.042),'metal',.58)
material('brass',(.46,.32,.125),'metal',.82)
material('rubber',(.05,.057,.052),'rubber',.0)
material('wood',(.23,.115,.047),'wood',.0)
material('black',(.025,.03,.032),'metal',.45)

class Part:
    def __init__(self,file,label,description,group='Core'):
        self.file=file; self.label=label; self.description=description; self.group=group
        self.col=bpy.data.collections.new(label); SCENE.collection.children.link(self.col)
        self.root=bpy.data.objects.new(file,None); self.col.objects.link(self.root)
        self.root.empty_display_type='PLAIN_AXES'; self.root.empty_display_size=.24
        self.root['units']='metres'; self.root['faction']='green'; self.root['mount_standard']='GS-F340-4B'
        self.objects=[]; self.groups={}; self.pivots={}; self.sockets=[]; self.clips=[]; self.rig=None
        ALL.append(self)
        print('MODELING',file,flush=True)
    def add(self,obj,mat='green',bone='Root'):
        for c in list(obj.users_collection): c.objects.unlink(obj)
        self.col.objects.link(obj); obj.parent=self.root
        obj.data.materials.append(MATS[mat]); obj.data.materials.append(MATS['steel'])
        self.objects.append(obj); self.groups.setdefault(bone,[]).append(obj)
        for f in obj.data.polygons: f.use_smooth=False
        return obj
    def bone(self,name,pos=(0,0,0),parent='Root'):
        self.pivots[name]=(Vector(pos),parent)
    def socket(self,name,pos,normal=(0,0,1),bone='Root'):
        e=bpy.data.objects.new('SOCKET_'+name,None); self.col.objects.link(e); e.parent=self.root
        e.location=pos; e.rotation_mode='QUATERNION'; e.rotation_quaternion=Vector(normal).to_track_quat('Z','Y')
        e.empty_display_type='ARROWS'; e.empty_display_size=.16
        e['connector']='GS-F340-4B'; e['bolt_spacing_m']=.25; e['animation_bone']=bone
        self.sockets.append(e)
        return e
    def clip(self,name,frames,loop=False):
        # frame map: {bone: [(frame, world rotation axis, radians, world translation)]}
        self.clips.append((name,frames,loop))


def part(file,label,description,group='Core'):
    global CUR
    CUR=Part(file,label,description,group)
    return CUR

def finish_mesh(obj,mat,bone,bevel=0):
    CUR.add(obj,mat,bone)
    if bevel>0:
        mod=obj.modifiers.new('Worn metal edge bevel','BEVEL'); mod.width=bevel; mod.segments=2; mod.affect='EDGES'; mod.material=1
        mod2=obj.modifiers.new('Weighted corner normals','WEIGHTED_NORMAL'); mod2.keep_sharp=True; mod2.weight=30
    return obj

def box(name,size,pos=(0,0,0),mat='green',bone='Root',bevel=.014,quat=None):
    bpy.ops.mesh.primitive_cube_add(size=1,location=pos); obj=bpy.context.object; obj.name=name
    obj.scale=size
    bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    if quat: obj.rotation_mode='QUATERNION'; obj.rotation_quaternion=quat
    return finish_mesh(obj,mat,bone,bevel)

def cylinder(name,radius,depth,pos=(0,0,0),mat='iron',axis=(0,0,1),bone='Root',verts=24,bevel=.008):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts,radius=radius,depth=depth,location=pos)
    obj=bpy.context.object; obj.name=name
    obj.rotation_mode='QUATERNION'; obj.rotation_quaternion=Vector(axis).to_track_quat('Z','Y')
    for f in obj.data.polygons: f.use_smooth=len(f.vertices)==4
    CUR.add(obj,mat,bone)
    for f in obj.data.polygons: f.use_smooth=len(f.vertices)==4
    if bevel:
        mod=obj.modifiers.new('Machined bevel','BEVEL'); mod.width=bevel; mod.segments=2; mod.material=1
    return obj

def tube(name,a,b,r,mat='iron',bone='Root',verts=16):
    a=Vector(a); b=Vector(b); return cylinder(name,r,(b-a).length,(a+b)*.5,mat,b-a,bone,verts)

def beam(name,a,b,width=.16,mat='green',bone='Root'):
    a=Vector(a); b=Vector(b)
    return box(name,(width,width,(b-a).length),(a+b)*.5,mat,bone,.013,(b-a).to_track_quat('Z','Y'))

def torus(name,major,minor,pos=(0,0,0),mat='iron',axis=(0,0,1),bone='Root',majorseg=40,minseg=8):
    bpy.ops.mesh.primitive_torus_add(major_segments=majorseg,minor_segments=minseg,
        major_radius=major,minor_radius=minor,location=pos)
    obj=bpy.context.object; obj.name=name; obj.rotation_mode='QUATERNION'; obj.rotation_quaternion=Vector(axis).to_track_quat('Z','Y')
    CUR.add(obj,mat,bone)
    for f in obj.data.polygons: f.use_smooth=True
    return obj

def mesh(name,verts,faces,mat='green',bone='Root',bevel=.008):
    data=bpy.data.meshes.new(name); data.from_pydata(verts,[],faces); data.update()
    obj=bpy.data.objects.new(name,data); CUR.col.objects.link(obj)
    return finish_mesh(obj,mat,bone,bevel)

def bolt(pos,normal=(0,0,1),bone='Root',scale=1):
    p=Vector(pos); n=Vector(normal).normalized()
    cylinder('Hexagonal bronze bolt',.027*scale,.026*scale,p+n*.012*scale,'brass',n,bone,6,.002)
    cylinder('Steel washer',.035*scale,.008*scale,p,'steel',n,bone,16,.001)

def flange(pos,normal=(0,0,1),bone='Root',socket=None):
    p=Vector(pos); n=Vector(normal).normalized(); q=n.to_track_quat('Z','Y')
    # Real open square sleeve, 340 mm outside, 140 mm opening, 250 mm bolt centres.
    for delta,size in [((-.12,0,0),(.10,.34,.065)),((.12,0,0),(.10,.34,.065)),((0,-.12,0),(.14,.10,.065)),((0,.12,0),(.14,.10,.065))]:
        box('Universal flange rim',size,p+q@Vector(delta),'iron',bone,.01,q)
    for x in [-.125,.125]:
        for y in [-.125,.125]: bolt(p+q@Vector((x,y,.037)),n,bone)
    for x in [-.145,.145]:
        # Scarred corner plates on outside face.
        box('Flange corner chip',(.025,.11,.009),p+q@Vector((x,0,.034)),'steel',bone,.002,q)
    if socket: CUR.socket(socket,p,n,bone)

def rivets_line(a,b,count=6,normal=(0,0,1),bone='Root'):
    a=Vector(a); b=Vector(b)
    for t in np.linspace(0,1,count): bolt(a.lerp(b,float(t)),normal,bone,.75)

def curve_tube(name,points,radius,mat='iron',bone='Root',closed=False):
    data=bpy.data.curves.new(name,'CURVE'); data.dimensions='3D'; data.resolution_u=2; data.bevel_depth=radius; data.bevel_resolution=2
    spline=data.splines.new('POLY'); spline.points.add(len(points)-1)
    for p,v in zip(spline.points,points): p.co=(*v,1)
    spline.use_cyclic_u=closed
    obj=bpy.data.objects.new(name,data); CUR.col.objects.link(obj)
    bpy.ops.object.select_all(action='DESELECT'); obj.select_set(True); bpy.context.view_layer.objects.active=obj
    bpy.ops.object.convert(target='MESH'); CUR.add(obj,mat,bone)
    for f in obj.data.polygons: f.use_smooth=True
    return obj

def spring(pos,radius=.13,length=.55,turns=6,bone='Root'):
    p=Vector(pos); pts=[]
    for t in np.linspace(0,1,turns*24+1):
        a=t*turns*math.tau; pts.append(tuple(p+Vector((radius*math.cos(a),radius*math.sin(a),length*(t-.5)))))
    return curve_tube('Coiled steel suspension spring',pts,.026,'rust',bone)

def exhaust(pos,height=.6,bone='Root'):
    p=Vector(pos); end=p+Vector((0,-.08,height))
    curve_tube('Bent exhaust stack',[p,p+Vector((0,0,height*.7)),end],.065,'steel',bone)
    cylinder('Exhaust open throat',.049,.006,end+Vector((0,0,.01)),'black',(0,0,1),bone,20,0)
    torus('Exhaust rolled lip',.058,.009,end,'iron',(0,0,1),bone)
    for z in [.15,.38]: cylinder('Pipe retaining collar',.076,.04,p+Vector((0,0,z)),'iron',(0,0,1),bone,20)

def anim_rotate(name,bone,axis,turns=1,frames=61):
    CUR.clip(name,{bone:[(1+i*(frames-1)//4,axis,math.tau*turns*i/4,(0,0,0)) for i in range(5)]},True)

def anim_swing(name,bone,axis,degrees,frames=61,loop=False):
    CUR.clip(name,{bone:[(1,axis,0,(0,0,0)),(frames//2,axis,math.radians(degrees),(0,0,0)),(frames,axis,0,(0,0,0))]},loop)

def animate_slide(name,bone,delta,frames=61,loop=False):
    CUR.clip(name,{bone:[(1,(0,0,1),0,(0,0,0)),(frames//2,(0,0,1),0,delta),(frames,(0,0,1),0,(0,0,0))]},loop)

print('MATERIALS_READY',flush=True)
# Fast direct-mesh equivalents avoid Blender operator dependency updates per bolt.
def box(name,size,pos=(0,0,0),mat='green',bone='Root',bevel=.014,quat=None):
    sx,sy,sz=[s*.5 for s in size]
    v=[(-sx,-sy,-sz),(-sx,-sy,sz),(-sx,sy,-sz),(-sx,sy,sz),(sx,-sy,-sz),(sx,-sy,sz),(sx,sy,-sz),(sx,sy,sz)]
    faces=[(0,4,6,2),(1,3,7,5),(0,1,5,4),(2,6,7,3),(0,2,3,1),(4,5,7,6)]
    obj=mesh(name,v,faces,mat,bone,bevel); obj.location=pos
    if quat: obj.rotation_mode='QUATERNION'; obj.rotation_quaternion=quat
    return obj

def cylinder(name,radius,depth,pos=(0,0,0),mat='iron',axis=(0,0,1),bone='Root',verts=24,bevel=.008):
    vv=[]
    for z in [-depth*.5,depth*.5]:
        for i in range(verts):
            a=i*math.tau/verts; vv.append((radius*math.cos(a),radius*math.sin(a),z))
    ff=[tuple(range(verts-1,-1,-1)),tuple(verts+i for i in range(verts))]
    ff += [(i,(i+1)%verts,(i+1)%verts+verts,i+verts) for i in range(verts)]
    obj=mesh(name,vv,ff,mat,bone,bevel); obj.location=pos
    obj.rotation_mode='QUATERNION'; obj.rotation_quaternion=Vector(axis).to_track_quat('Z','Y')
    for f in obj.data.polygons: f.use_smooth=len(f.vertices)==4
    return obj

def torus(name,major,minor,pos=(0,0,0),mat='iron',axis=(0,0,1),bone='Root',majorseg=40,minseg=8):
    vv=[]; ff=[]
    for i in range(majorseg):
        a=i*math.tau/majorseg
        for j in range(minseg):
            b=j*math.tau/minseg; r=major+minor*math.cos(b)
            vv.append((r*math.cos(a),r*math.sin(a),minor*math.sin(b)))
    for i in range(majorseg):
        for j in range(minseg):
            ff.append((i*minseg+j,((i+1)%majorseg)*minseg+j,((i+1)%majorseg)*minseg+(j+1)%minseg,i*minseg+(j+1)%minseg))
    obj=mesh(name,vv,ff,mat,bone,0); obj.location=pos
    obj.rotation_mode='QUATERNION'; obj.rotation_quaternion=Vector(axis).to_track_quat('Z','Y')
    for f in obj.data.polygons: f.use_smooth=True
    return obj


# ----------------------------- CORE KIT -----------------------------
def chassis(length=2.0):
    for x in [-.50,.50]:
        box('Patched box-section longitudinal rail',(.20,length,.20),(x,0,0))
        for yy in np.linspace(-length/2+.24,length/2-.24,3):
            box('Riveted rail patch',(.216,.28,.035),(x,float(yy),.118),'green',bevel=.005)
            rivets_line((x-.065,yy-.10,.14),(x-.065,yy+.10,.14),3)
            rivets_line((x+.065,yy-.10,.14),(x+.065,yy+.10,.14),3)
        flange((x,-length/2,0),(0,-1,0),socket=f'Rail_{"L" if x<0 else "R"}_Rear')
        flange((x,length/2,0),(0,1,0),socket=f'Rail_{"L" if x<0 else "R"}_Front')
    for yy in np.linspace(-length/2+.15,length/2-.15,3 if length<3 else 5):
        box('Chassis cross member',(1.18,.16,.17),(0,float(yy),0))
        for x in [-.60,.60]: flange((x,float(yy),0),(1 if x>0 else -1,0,0),socket=f'Side_{x}_{yy:.2f}')
    for x in [-.50,.50]:
        for yy in [-length/2+.2,length/2-.2]:
            box('Corner reinforcement',(.29,.29,.035),(x,yy,.128),'iron',bevel=.008)
            for dx in [-.09,.09]: bolt((x+dx,yy,.151))
    for yy in [-.65,.65]: CUR.socket(f'Axle_{yy}',(0,yy,-.15),(0,0,-1))
    CUR.socket('Cab',(0,.47,.18)); CUR.socket('Engine',(0,-.56,.18))
    CUR.socket('Deck',(0,0,.14))

part('scrap_frame_2x1','Short chassis frame','Riveted open chassis; 2 m rails and multi-face structural mount sockets.')
chassis(2)
part('scrap_frame_long','Long chassis frame','Extended 3.8 m open ladder chassis for trucks, towers and multi-axle war machines.')
chassis(3.8)

for length in [1,2]:
    part(f'scrap_beam_{length}m',f'{length} m structural beam','Four-bolt universal-flange extension rail with side and top mounting faces.')
    box('Square structural beam',(.20,length,.20))
    for y in [-length/2,length/2]: flange((0,y,0),(0,1 if y>0 else -1,0),socket='End_Front' if y>0 else 'End_Rear')
    for y in np.linspace(-length/2+.20,length/2-.20,2 if length==1 else 3):
        box('Reinforced beam band',(.245,.13,.245),(0,float(y),0),'iron',bevel=.009)
        for n in [(1,0,0),(-1,0,0),(0,0,1)]:
            flange(Vector((0,float(y),0))+Vector(n)*.14,n,socket=f'Branch_{y:.1f}_{n}')

part('scrap_corner_joint','Elbow connector','Right-angle extension joint with identical perpendicular square flanges.')
box('Elbow body X',(.45,.20,.20),(.10,0,0)); box('Elbow body Z',(.20,.20,.45),(0,0,.10))
flange((.33,0,0),(1,0,0),socket='X'); flange((0,0,.33),(0,0,1),socket='Up')
flange((-.14,0,0),(-1,0,0),socket='Back')
beam('Diagonal elbow brace',(.25,0,.02),(.02,0,.25),.09,'iron')
part('scrap_t_connector','T and cross connector','Five-face branching block for tall, wide and asymmetric construction.')
box('Reinforced connector block',(.32,.32,.32))
for i,n in enumerate([(1,0,0),(-1,0,0),(0,1,0),(0,-1,0),(0,0,1)]):
    flange(Vector(n)*.27,n,socket=f'Face_{i}')
    beam('Connector neck',(0,0,0),Vector(n)*.27,.20)

# Wheel profiles and tread lugs. Pivot is the actual rolling axle, X axis.
def wheel(radius,width,large=False):
    bone='Wheel'; CUR.bone(bone)
    # Closed lathed carcass with rounded shoulders and flat sidewalls.
    profile=[(-width*.52,radius*.46),(-width*.52,radius*.77),(-width*.40,radius*.95),(-width*.24,radius),
             (width*.24,radius),(width*.40,radius*.95),(width*.52,radius*.77),(width*.52,radius*.46)]
    verts=[]; faces=[]; seg=48
    for xx,rr in profile:
        for i in range(seg):
            a=i*math.tau/seg; verts.append((xx,rr*math.cos(a),rr*math.sin(a)))
    for j in range(len(profile)-1):
        for i in range(seg):
            k=j*seg+i; kn=j*seg+(i+1)%seg; faces.append((k,kn,kn+seg,k+seg))
    faces.append(tuple(range(seg-1,-1,-1))); faces.append(tuple((len(profile)-1)*seg+i for i in range(seg)))
    tyre=mesh('Heavy rounded rubber carcass',verts,faces,'rubber',bone,0)
    for f in tyre.data.polygons: f.use_smooth=True
    for x in [-width*.53,width*.53]:
        cylinder('Inset wheel dish',radius*.54,.028,(x,0,0),'green',(1,0,0),bone,40)
        torus('Steel wheel rim ring',radius*.56,.035,(x,0,0),'steel',(1,0,0),bone)
        torus('Rusty rim retaining bead',radius*.63,.024,(x,0,0),'rust',(1,0,0),bone)
        cylinder('Reinforced axle hub',.14,.11,(x,0,0),'iron',(1,0,0),bone,24)
        cylinder('Bronze axle cap',.088,.025,(x+(.065 if x>0 else -.065),0,0),'brass',(1,0,0),bone,12)
        for i in range(8):
            a=i*math.tau/8; bolt((x+(.025 if x>0 else -.025),radius*.36*math.cos(a),radius*.36*math.sin(a)),(1 if x>0 else -1,0,0),bone)
    for i in range(24 if large else 28):
        a=i*math.tau/(24 if large else 28)
        for side in [-1,1]:
            if large:
                q=Quaternion((1,0,0),a)@Quaternion((0,0,1),side*.44)
                box('Angled tractor chevron tread',(.60*width,.145,.075),
                    (side*width*.22,radius*math.sin(a),radius*math.cos(a)),'rubber',bone,.015,q)
            else:
                q=Quaternion((1,0,0),a)
                box('Staggered off-road tread lug',(.43*width,.095,.065),
                    (side*width*.22,radius*math.sin(a),radius*math.cos(a)),'rubber',bone,.011,q)
    CUR.socket('Axle_hub',(0,0,0),(1,0,0))
    anim_rotate('Roll',bone,(1,0,0))

part('scrap_wheel_small','Narrow off-road wheel','Knobby reinforced tire, bronze hub and animated X-axis rolling. Diameter 0.76 m.')
wheel(.38,.24)
part('scrap_wheel_large','Giant tractor wheel','Deep tractor tread, rusty reinforced rim and animated X-axis rolling. Diameter 1.2 m.')
wheel(.60,.37,True)

part('scrap_axle_2m','Axle with brackets','Two metre shaft with universal brackets and rotating hubs.')
CUR.bone('Axle')
cylinder('Machined axle shaft',.078,2,(0,0,0),'steel',(1,0,0),'Axle',32)
for x in [-.85,.85]:
    cylinder('Hub bearing',.15,.20,(x,0,0),'iron',(1,0,0),'Root',24)
    torus('Bearing brass ring',.124,.024,(x,0,0),'brass',(1,0,0))
    box('Axle mounting upright',(.17,.22,.33),(x,0,.14))
    flange((x,0,.31),(0,0,1),socket=f'Chassis_{x}')
    CUR.socket('Wheel_'+('Left' if x<0 else 'Right'),(x*1.20,0,0),(-1 if x<0 else 1,0,0))
for x in [-.2,.2]: cylinder('Axle collar',.11,.08,(x,0,0),'iron',(1,0,0),'Axle')
anim_rotate('AxleSpin','Axle',(1,0,0))

part('scrap_steering_pivot','Steering swivel','Reinforced turntable with attached lower cross arm and animated steering sweep.')
CUR.bone('Steer')
box('Upper turntable cap',(.44,.35,.12),(0,0,.26))
flange((0,0,.34),(0,0,1),socket='Chassis')
cylinder('Swivel vertical spindle',.10,.42,(0,0,.05),'brass')
torus('Greased turntable ring',.18,.04,(0,0,.07),'iron')
box('Steering cross arm',(.75,.22,.17),(0,0,-.15),bone='Steer')
for x in [-.43,.43]: flange((x,0,-.15),(1 if x>0 else -1,0,0),'Steer',socket=f'Axle_{x}')
anim_swing('Steer','Steer',(0,0,1),35,61,True)

part('scrap_suspension_piston','Spring suspension','Spring-damper with telescoping piston; animated compression.')
CUR.bone('Lower',(0,0,-.35))
cylinder('Suspension casing',.09,.50,(0,0,.10),'iron')
cylinder('Chromed piston rod',.053,.53,(0,0,-.29),'steel',(0,0,1),'Lower')
CUR.bone('Coil',(0,0,.235))
spring((0,0,-.08),.13,.63,7,'Coil')
for z,bone in [(.45,'Root'),(-.59,'Lower')]:
    cylinder('Suspension end bushing',.105,.18,(0,0,z),'brass',(0,1,0),bone)
    flange((0,0,z+.05 if z>0 else z-.05),(0,0,1 if z>0 else -1),bone,socket='Top' if z>0 else 'Bottom')
animate_slide('Compress','Lower',(0,0,.19),61,True)

part('scrap_engine_block','Exposed goblin engine','Patchwork engine with angled manifolds, open exhaust stacks and driven flywheel.')
CUR.bone('Flywheel',(.54,0,0)); CUR.bone('Fan',(0,.49,.23)); CUR.bone('Pistons')
box('Cast engine crankcase',(.82,.75,.50),(0,0,0))
box('Dark sump',(.75,.63,.16),(0,0,-.32),'iron')
for x in [-.30,.30]:
    q=Quaternion((0,1,0),-.25 if x<0 else .25)
    box('V engine cylinder bank',(.32,.69,.31),(x,0,.29),'iron',quat=q)
    box('Chipped painted rocker cover',(.29,.66,.075),(x,0,.48),quat=q)
    for y in [-.24,-.08,.08,.24]:
        tube('Intake manifold',(x,y,.40),(x*.43,y,.68),.035,'steel')
        bolt((x,y,.52))
for z in [-.14,-.03,.08]: box('Cast cooling fin',(.91,.77,.035),(0,0,z),'iron',bevel=.005)
flange((0,-.46,0),(0,-1,0),socket='Rear_power')
flange((0,0,-.44),(0,0,-1),socket='Chassis')
cylinder('Flywheel rotor',.29,.11,(.54,0,0),'rust',(1,0,0),'Flywheel',32)
for i in range(6):
    a=i*math.tau/6; bolt((.61,.22*math.cos(a),.22*math.sin(a)),(1,0,0),'Flywheel')
cylinder('Cooling fan core',.10,.08,(0,.49,.23),'brass',(0,1,0),'Fan')
for i in range(6):
    a=i*math.tau/6; q=Quaternion((0,1,0),a)
    box('Cooling fan paddle',(.08,.035,.24),(.16*math.sin(a),.50,.23+.16*math.cos(a)),'steel','Fan',.006,q)
for x in [-.24,.24]: exhaust((x,-.20,.47),.53)
box('Fuel tank',(.45,.27,.22),(0,-.15,.65),'rust')
cylinder('Fuel cap',.067,.042,(0,-.15,.79),'brass')
curve_tube('Fuel hose',[(-.20,-.19,.76),(-.38,-.28,.67),(-.41,-.25,.14)],.019,'rubber')
anim_rotate('EngineIdle','Flywheel',(1,0,0),2)
# Both moving engine accessories share one clip.
CUR.clips[-1][1]['Fan']=[(1+i*15,(0,1,0),math.tau*3*i/4,(0,0,0)) for i in range(5)]

part('scrap_cab_shell','Driver cage and seat','Open patched goblin roll cage, worn timber seat, hand controls and animated steering wheel.')
CUR.bone('SteeringWheel',(0,.38,.20)); CUR.bone('Lever',(.35,.14,-.10))
box('Driver floor', (1.10,1.23,.09),(0,0,-.44),'iron')
for x in [-.47,.47]:
    tube('Cage front pillar',(x,.50,-.40),(x*.90,.42,.53),.041,'steel')
    tube('Cage rear pillar',(x,-.52,-.40),(x*.86,-.48,.56),.041,'steel')
    tube('Roof side rail',(x*.90,.42,.53),(x*.86,-.48,.56),.047,'iron')
    tube('Lower side protection',(x,-.52,-.35),(x,.50,-.35),.044,'iron')
    tube('Diagonal side brace',(x,-.52,-.35),(x*.9,.42,.53),.027,'steel')
    flange((x*1.17,0,-.38),(1 if x>0 else -1,0,0),socket=f'Side_{x}')
for y,z in [(.42,.53),(-.48,.56)]: tube('Roof cross tube',(-.43,y,z),(.43,y,z),.044,'steel')
box('Roof corner patch',(.96,.20,.065),(0,-.44,.58),'green')
for x in [-.38,-.18,.18,.38]: bolt((x,-.44,.62))
for x in [-.23,0,.23]: box('Worn wooden seat back',(.21,.065,.50),(x,-.25,-.05),'wood',bevel=.015)
box('Seat base',(.69,.43,.11),(0,-.14,-.31),'wood')
for x in [-.23,.23]: tube('Seat bracket',(x,-.3,-.41),(x,-.25,.12),.028,'iron')
box('Chipped dashboard',(.90,.12,.19),(0,.47,-.04))
for x in [-.24,.04]:
    cylinder('Dashboard instrument bezel',.068,.025,(x,.395,-.04),'brass',(0,1,0))
    cylinder('Dashboard dial',.052,.03,(x,.38,-.04),'black',(0,1,0),verts=20)
tube('Steering column',(0,.40,-.12),(0,.38,.20),.031,'iron')
torus('Steering wheel',.20,.022,(0,.38,.20),'brass',(0,1,0),'SteeringWheel')
for a in [0,math.tau/3,math.tau*2/3]: tube('Wheel spoke',(0,.38,.20),(.19*math.cos(a),.38,.20+.19*math.sin(a)),.012,'steel','SteeringWheel')
tube('Gear lever',(.35,.14,-.10),(.35,.10,.26),.021,'steel','Lever')
cylinder('Lever grip',.042,.10,(.35,.10,.27),'wood',(0,0,1),'Lever')
flange((0,0,-.53),(0,0,-1),socket='Floor_mount'); CUR.socket('Roof',(0,0,.62))
anim_swing('SteerWheel','SteeringWheel',(0,1,0),50,61,True)
anim_swing('ShiftLever','Lever',(1,0,0),20,31,False)

part('scrap_armor_plate','Curved riveted armor','Patchwork rounded armor panel with ribs, edge wear and four-bolt joining flanges.')
# True cylindrical panel curvature; front normal +Z, seams and exposed ribs.
for j in range(4):
    aa=-.68+j*.34; bb=aa+.34
    v=[]
    for yy in [-.55,.55]:
        for a in [aa,bb]: v.append((math.sin(a)*.9,yy,math.cos(a)*.9-.67))
    mesh('Rolled patched sheet',v,[(0,1,3,2)],'green',bevel=0)
    obj=CUR.objects[-1]; mod=obj.modifiers.new('Armor sheet thickness','SOLIDIFY'); mod.thickness=.045
for y in [-.55,.55]:
    pts=[(.9*math.sin(a),y,.9*math.cos(a)-.64) for a in np.linspace(-.68,.68,20)]
    curve_tube('Curved reinforced edge',pts,.031,'iron')
for a in [-.68,-.34,0,.34,.68]:
    x=.9*math.sin(a); z=.9*math.cos(a)-.63
    tube('Armor seam rib',(x,-.55,z),(x,.55,z),.018,'rust')
    rivets_line((x,-.47,z+.019),(x,.47,z+.019),5,(math.sin(a),0,math.cos(a)))
for y in [-.69,.69]: flange((0,y,.08),(0,1 if y>0 else -1,0),socket=f'Join_{y}')
CUR.socket('Back',(0,0,0),(0,0,-1))

part('scrap_ram_wedge','Toothed reinforced ram','Broad curved wedge with five reinforced teeth and chassis mounting flange.')
for i in range(5):
    x=(i-2)*.28; y=0
    verts=[(x-.135,-.40,-.15),(x+.135,-.40,-.15),(x-.135,.58,-.15),(x+.135,.58,-.15),
           (x-.135,-.40,.36),(x+.135,-.40,.36),(x-.10,.58,-.08),(x+.10,.58,-.08)]
    mesh('Forged ramp tooth',verts,[(0,2,3,1),(0,1,5,4),(2,6,7,3),(0,4,6,2),(1,3,7,5),(4,5,7,6)],'green',bevel=.018)
    beam('Exposed wedge reinforcement',(x,-.35,.40),(x,.56,-.02),.055,'steel')
    for yy,zz in [(-.27,.35),(.10,.20)]: bolt((x,yy,zz),(0,.35,1))
box('Ram rear cross brace',(1.52,.20,.20),(0,-.44,.06),'rust')
for x in [-.50,.50]: flange((x,-.58,.06),(0,-1,0),socket=f'Chassis_{x}')

part('scrap_saw_disc','Standalone cutter disc','Replaceable toothed circular blade with reinforced hub and spinning animation.')
CUR.bone('Blade')
cylinder('Forged saw blade',.47,.075,(0,0,0),'steel',(1,0,0),'Blade',48)
for i in range(18):
    a=i*math.tau/18; p=Vector((0,.47*math.sin(a),.47*math.cos(a)))
    q=Quaternion((1,0,0),a)
    box('Offset hardened saw tooth',(.11,.085,.14),p,'steel','Blade',.006,q)
for x in [-.065,.065]: cylinder('Saw hub cap',.13,.07,(x,0,0),'rust',(1,0,0),'Blade')
CUR.socket('Shaft',(0,0,0),(1,0,0)); anim_rotate('Cut','Blade',(1,0,0),2)

part('scrap_weapon_mount','Compact weapon cradle','Rotating round turret foot and reinforced two-sided weapon cradle.')
CUR.bone('Yaw',(0,0,.1))
cylinder('Turret steel base',.36,.12,(0,0,0),'iron',verts=32)
torus('Turntable bearing ring',.29,.031,(0,0,.095),'brass')
flange((0,0,-.1),(0,0,-1),socket='Chassis')
for x in [-.20,.20]: box('Turret weapon fork',(.12,.55,.31),(x,0,.26),bone='Yaw')
for y in [-.17,.17]: cylinder('Cradle trunnion',.06,.57,(0,y,.32),'steel',(1,0,0),'Yaw')
flange((0,0,.48),(0,0,1),'Yaw',socket='Weapon')
anim_swing('AimYaw','Yaw',(0,0,1),75,91,True)
print('CORE_GEOMETRY',len(ALL),flush=True)
# --------------------------- EXPANSION KIT ---------------------------
def cone(name,radius,depth,pos,axis=(0,0,1),mat='steel',bone='Root',radius2=0,verts=12):
    bpy.ops.mesh.primitive_cone_add(vertices=verts,radius1=radius,radius2=radius2,depth=depth,location=pos)
    obj=bpy.context.object; obj.name=name; obj.rotation_mode='QUATERNION'; obj.rotation_quaternion=Vector(axis).to_track_quat('Z','Y')
    return finish_mesh(obj,mat,bone,.004)

def mount_gearbox(pos=(0,0,0),bone='Root'):
    p=Vector(pos)
    box('Reinforced drive gearbox',(.48,.46,.42),p,'green',bone)
    for x in [-.27,.27]:
        cylinder('Gearbox bearing cover',.19,.08,p+Vector((x,0,0)),'iron',(1,0,0),bone)
        for i in range(6):
            a=i*math.tau/6; bolt(p+Vector((x+(.045 if x>0 else -.045),.14*math.cos(a),.14*math.sin(a))),(1 if x>0 else -1,0,0),bone,.8)
    flange(p+Vector((0,-.29,0)),(0,-1,0),bone,socket='Chassis')

def blade_disc(center=(0,0,0),bone='Blade',radius=.52):
    c=Vector(center)
    cylinder('Steel cutter disc',radius,.07,c,'steel',(1,0,0),bone,48)
    for i in range(20):
        a=i*math.tau/20
        # Actual directional wedge teeth instead of decorative blocks.
        vs=[]
        for xx in [-.055,.055]:
            for aa,rr in [(a-.055,radius*.90),(a+.05,radius*.96),(a+.11,radius*1.16)]:
                vs.append(tuple(c+Vector((xx,rr*math.sin(aa),rr*math.cos(aa)))))
        mesh('Directional saw tooth',vs,[(0,1,2),(5,4,3),(0,3,4,1),(1,4,5,2),(2,5,3,0)],'steel',bone,.005)
    for xx in [-.095,.095]:
        cylinder('Cutter central hub',.14,.10,c+Vector((xx,0,0)),'rust',(1,0,0),bone)
        for i in range(6):
            a=i*math.tau/6; bolt(c+Vector((xx+(.056 if xx>0 else -.056),.10*math.sin(a),.10*math.cos(a))),(1 if xx>0 else -1,0,0),bone,.7)

part('scrap_crushing_drum','Spiked crushing drum','Wide rotating spiked roller with twin bearing yoke and rear mounting flanges.','Expansion')
CUR.bone('Drum',(0,.32,0))
cylinder('Heavy roller shell',.44,1.62,(0,.32,0),'iron',(1,0,0),'Drum',48)
for x in [-.80,-.40,0,.40,.80]: torus('Drum reinforcement hoop',.443,.021,(x,.32,0),'rust',(1,0,0),'Drum')
for j,x in enumerate(np.linspace(-.67,.67,6)):
    for i in range(9):
        a=(i+j*.5)*math.tau/9; n=Vector((0,math.sin(a),math.cos(a)))
        pos=Vector((float(x),.32,0))+n*.50
        cone('Forged crushing spike',.075,.18,pos,n,'steel','Drum',verts=4)
for x in [-.94,.94]:
    cylinder('Roller outer bearing',.20,.14,(x,.32,0),'brass',(1,0,0))
    beam('Drum bearing yoke',(x,.32,0),(x,-.36,0),.16)
    flange((x,-.44,0),(0,-1,0),socket=f'Chassis_{x}')
    box('Bearing retaining cap',(.06,.40,.40),(x+.08 if x>0 else x-.08,.32,0),'iron')
    for y,z in [(.18,-.15),(.18,.15),(.46,-.15),(.46,.15)]: bolt((x+.12 if x>0 else x-.12,y,z),(1 if x>0 else -1,0,0))
anim_rotate('Crush','Drum',(1,0,0),2)

part('scrap_saw_module','Powered circular saw','Motor-driven cutter with real curved half-guard, geared drive and rear flange.','Expansion')
CUR.bone('Blade',(0,.25,0)); CUR.bone('DrivePulley',(.25,-.20,0))
blade_disc((0,.25,0),'Blade',.49)
# Semicircular safety guard built as a thick annular band.
vs=[]; fs=[]; angles=np.linspace(-math.pi/2,math.pi/2,19)
for xx in [-.10,.10]:
    for rr in [.53,.66]:
        for a in angles: vs.append((xx,.25+rr*math.sin(a),rr*math.cos(a)))
n=len(angles)
for i in range(n-1):
    fs += [(i,i+1,n+i+1,n+i),(2*n+i,3*n+i,3*n+i+1,2*n+i+1),
           (n+i,n+i+1,3*n+i+1,3*n+i),(i,2*n+i,2*n+i+1,i+1)]
fs += [(0,n,3*n,2*n),(n-1,2*n-1,4*n-1,3*n-1)]
mesh('Curved riveted saw shield',vs,fs,'green',bevel=.012)
for a in np.linspace(-1.3,1.3,7): bolt((.12,.25+.60*math.sin(a),.60*math.cos(a)),(1,0,0))
mount_gearbox((.29,-.32,-.02))
cylinder('Saw drive pulley',.20,.11,(.28,-.25,-.02),'rust',(1,0,0),'DrivePulley')
box('Saw electric motor',(.43,.43,.27),(.22,-.57,.20),'iron')
box('Saw motor red primer patch',(.39,.33,.085),(.22,-.57,.38),'rust')
for yy in [-.70,-.56,-.42]: box('Saw motor cooling rib',(.48,.025,.32),(.22,yy,.20),'steel',bevel=.004)
anim_rotate('Cut','Blade',(1,0,0),3)
CUR.clips[-1][1]['DrivePulley']=[(1+i*15,(1,0,0),math.tau*2*i/4,(0,0,0)) for i in range(5)]

part('scrap_battering_fist','Telescoping battering fist','Oversized blunt knuckle ram with sliding twin piston and rear flange.','Expansion')
CUR.bone('Fist',(0,.20,0))
box('Ram piston rear housing',(.57,.49,.48),(0,-.37,0))
flange((0,-.66,0),(0,-1,0),socket='Chassis')
for x in [-.20,.20]:
    cylinder('Piston hydraulic casing',.09,.64,(x,-.07,0),'iron',(0,1,0))
    cylinder('Chromed extending ram',.06,.73,(x,.37,0),'steel',(0,1,0),'Fist')
    torus('Piston collar',.088,.019,(x,.24,0),'brass',(0,1,0))
box('Oversized fist back',(.76,.39,.65),(0,.83,0),'rust','Fist',.055)
box('Welded fist armor plate',(.81,.18,.61),(0,.98,0),'green','Fist',.035)
for x in [-.29,-.095,.095,.29]:
    box('Blunt steel knuckle',(.18,.19,.53),(x,1.12,.01),'steel','Fist',.035)
    bolt((x,1.225,-.16),(0,1,0),'Fist')
box('Folded thumb plate',(.24,.26,.35),(.43,.91,-.22),'iron','Fist',.035,Quaternion((0,1,0),-.35))
animate_slide('Punch','Fist',(0,.55,0),31,False)

part('scrap_hammer_arm','Overhead hammer arm','Heavy pivoted hammer on braced scrap arm with swing animation.','Expansion')
CUR.bone('HammerArm',(0,0,0))
mount_gearbox((0,0,0))
cylinder('Hammer hinge pin',.13,.78,(0,0,0),'brass',(1,0,0))
beam('Hammer lifting arm',(0,0,.05),(0,.82,.96),.21,'green','HammerArm')
beam('Upper hammer boom',(0,.82,.96),(0,1.62,.69),.23,'green','HammerArm')
beam('Hammer diagonal support',(0,.23,.31),(0,1.42,.72),.095,'iron','HammerArm')
for y,z in [(0,.05),(.82,.96),(1.62,.69)]:
    cylinder('Hammer arm hinge cheek',.145,.28,(0,y,z),'iron',(1,0,0),'HammerArm')
    cylinder('Hammer hinge bronze cap',.085,.32,(0,y,z),'brass',(1,0,0),'HammerArm')
box('Heavy square hammer head',(.74,.52,.62),(0,1.70,.37),'iron','HammerArm',.04)
for x in [-.28,0,.28]: box('Hammer impact band',(.10,.56,.65),(x,1.70,.37),'steel','HammerArm',.013)
box('Hammer weathered face',(.71,.075,.59),(0,1.99,.37),'rust','HammerArm')
for x in [-.27,.27]:
    for z in [.15,.59]: bolt((x,1.76,z),(1 if x>0 else -1,0,0),'HammerArm')
CUR.socket('Weapon_base',(0,-.29,0),(0,-1,0))
anim_swing('Smash','HammerArm',(1,0,0),-70,41,False)

part('scrap_auger_drill','Giant auger drill','Tapered spiral drill with gearbox, machined cutting flights and spin animation.','Expansion')
CUR.bone('Drill',(0,.12,0))
mount_gearbox((0,-.23,0))
cylinder('Drill root bearing',.24,.18,(0,.09,0),'rust',(0,1,0))
cone('Tapered drill shaft',.22,1.10,(0,.72,0),(0,1,0),'iron','Drill',.022,32)
# Two-turn conical helicoid cutting flights, not stacked rings.
vs=[]; fs=[]; steps=100
for i in range(steps+1):
    t=i/steps; y=.18+1.10*t; a=t*math.tau*2.7
    outer=.42*(1-t)+.055; inner=.21*(1-t)+.025
    for rr,dy in [(inner,-.020),(outer,-.020),(outer,.020),(inner,.020)]:
        vs.append((rr*math.sin(a),y+dy,rr*math.cos(a)))
for i in range(steps):
    k=i*4; kn=k+4
    for j in range(4): fs.append((k+j,k+(j+1)%4,kn+(j+1)%4,kn+j))
fs += [(3,2,1,0),(steps*4,steps*4+1,steps*4+2,steps*4+3)]
mesh('Continuous forged spiral flights',vs,fs,'steel','Drill',.006)
for i in range(8):
    a=i*math.tau/8; bolt((.26*math.sin(a),.07,.26*math.cos(a)),(0,1,0),'Drill')
anim_rotate('Drill','Drill',(0,1,0),2)

part('scrap_grabber_jaws','Twin grabber jaws','Hydraulic curved pincers with separate articulated left/right jaws and grip animation.','Expansion')
mount_gearbox((0,-.18,0))
for side in [-1,1]:
    bone='JawLeft' if side<0 else 'JawRight'; pivot=(side*.29,.03,0); CUR.bone(bone,pivot)
    cylinder('Grabber hinge',.13,.31,pivot,'brass',(0,0,1))
    # Hook polygon in plan view; forward tips converge to form a grabbing mouth.
    points=[(.22,.02),(.46,.25),(.60,.58),(.53,.91),(.23,1.16),(.08,1.10),(.34,.82),(.38,.59),(.24,.35),(.08,.16)]
    poly=[(side*x,y) for x,y in points]; vv=[]
    for z in [-.11,.11]: vv.extend((x,y,z) for x,y in poly)
    nn=len(poly); ff=[tuple(range(nn-1,-1,-1)),tuple(nn+i for i in range(nn))]
    ff += [(i,(i+1)%nn,(i+1)%nn+nn,i+nn) for i in range(nn)]
    mesh('Curved forged grabbing claw',vv,ff,'iron',bone,.02)
    for xx,yy in [(.33,.35),(.44,.62),(.38,.87)]:
        bolt((side*xx,yy,.135),(0,0,1),bone)
        cone('Grip tooth',.048,.115,(side*(xx-.055),yy,0),(-side,0,0),'steel',bone,verts=4)
    box('Claw armored cheek',(.16,.40,.035),(side*.37,.44,.14),'green',bone,.01,Quaternion((0,0,1),-side*.34))
    tube('Hydraulic claw casing',(side*.16,-.18,-.06),(side*.48,.38,-.06),.07,'iron')
    tube('Chromed claw linkage',(side*.48,.38,-.06),(side*.41,.58,-.06),.038,'steel',bone)
CUR.clip('Grip',{
    'JawLeft':[(1,(0,0,1),0,(0,0,0)),(25,(0,0,1),-.42,(0,0,0)),(49,(0,0,1),0,(0,0,0))],
    'JawRight':[(1,(0,0,1),0,(0,0,0)),(25,(0,0,1),.42,(0,0,0)),(49,(0,0,1),0,(0,0,0))]},False)

part('scrap_forked_ram','Forked goblin tusk ram','Twin curved reinforced steel tusks with a patched rear beam and matching flanges.','Expansion')
box('Ram rear housing',(1.16,.46,.26),(0,-.18,0))
for x in [-.45,.45]:
    flange((x,-.47,0),(0,-1,0),socket=f'Chassis_{x}')
    box('Tusk reinforcement saddle',(.38,.47,.38),(x,.07,0),'iron')
    for y in [-.06,.18]: bolt((x,y,.205))
    vs=[]; fs=[]; rings=12; seg=12
    for i in range(rings):
        t=i/(rings-1); center=Vector((x*(1+.55*t*t),.12+1.13*t,.15*t*t))
        r=.18*(1-t)+.014
        for j in range(seg):
            a=j*math.tau/seg; vs.append(tuple(center+Vector((r*math.cos(a),0,r*math.sin(a)))))
    for i in range(rings-1):
        for j in range(seg):
            k=i*seg+j; kn=i*seg+(j+1)%seg; fs.append((k,kn,kn+seg,k+seg))
    fs += [tuple(range(seg-1,-1,-1)),tuple((rings-1)*seg+j for j in range(seg))]
    mesh('Curved forged ram tusk',vs,fs,'steel',bevel=.004)
    for y,r in [(.20,.175),(.42,.145)]:
        # Squared scars and iron collar bands around the base.
        torus('Tusk reinforcing band',r+.008,.023,(x,y,.005),'rust',(0,1,0))

part('scrap_catapult_basket','Spring catapult basket','Timber basket on hinged spring-steel launch arm with crank and fire animation.','Expansion')
CUR.bone('Launch',(0,-.30,-.08)); CUR.bone('Winch',(.44,-.35,-.17))
for x in [-.44,.44]:
    box('Catapult base side rail',(.18,.99,.17),(x,0,-.29))
    for y in [-.50,.50]: flange((x,y,-.29),(0,1 if y>0 else -1,0),socket=f'Base_{x}_{y}')
    beam('Basket upright brace',(x,-.31,-.25),(x,-.31,.45),.12)
    spring((x,.05,.03),.08,.55,5)
box('Base cross member',(1.03,.17,.17),(0,-.35,-.29))
cylinder('Catapult hinge axle',.075,1.16,(0,-.30,-.08),'steel',(1,0,0))
beam('Basket lever',(0,-.30,-.08),(0,.24,.48),.14,'green','Launch')
# Twelve-sided flared wooden bowl with individual staves and steel hoops.
for i in range(12):
    a=i*math.tau/12; radial=Vector((math.cos(a),math.sin(a),0))
    a0=Vector((0,.20,.36))+radial*.25; a1=Vector((0,.20,.82))+radial*.43
    beam('Worn wooden basket stave',a0,a1,.16,'wood','Launch')
    for t in [.25,.82]:
        pp=a0.lerp(a1,t); bolt(pp+radial*.09,radial,'Launch',.8)
cylinder('Basket timber bottom',.29,.075,(0,.20,.37),'wood',(0,0,1),'Launch',12)
for z,r in [(.48,.29),(.77,.42)]: torus('Basket riveted steel hoop',r,.043,(0,.20,z),'iron',(0,0,1),'Launch')
cylinder('Winch reel',.13,.29,(.44,-.35,-.17),'rust',(1,0,0),'Winch')
beam('Winch handle',(.65,-.35,-.17),(.65,-.35,.09),.035,'steel','Winch')
CUR.clip('Launch',{'Launch':[(1,(1,0,0),0,(0,0,0)),(12,(1,0,0),-.30,(0,0,0)),(17,(1,0,0),1.12,(0,0,0)),(41,(1,0,0),0,(0,0,0))]},False)
anim_rotate('WindUp','Winch',(1,0,0),2)

part('scrap_swivel_turret','Swivel weapon mounting turret','Two-axis structural weapon turret with animated yaw and elevation.','Expansion')
CUR.bone('Yaw',(0,0,.03)); CUR.bone('Pitch',(0,0,.28),'Yaw')
box('Turret base',(.44,.44,.18),(0,0,-.10),'iron')
flange((0,0,-.23),(0,0,-1),socket='Base')
cylinder('Turret yaw bearing',.25,.10,(0,0,.03),'brass')
box('Upper turret block',(.43,.43,.35),(0,0,.24),bone='Yaw')
for x in [-.27,.27]: cylinder('Elevation trunnion',.14,.13,(x,0,.28),'iron',(1,0,0),'Pitch')
for n in [(0,1,0),(0,0,1)]:
    p=Vector((0,0,.28))+Vector(n)*.33
    beam('Turret weapon neck',(0,0,.28),p,.18,'green','Pitch'); flange(p,n,'Pitch',socket='Weapon_'+str(n))
for x in [-.26,.26]:
    flange((x,0,.18),(1 if x>0 else -1,0,0),'Yaw',socket=f'Side_{x}')
torus('Hand adjustment wheel',.12,.018,(.34,0,.23),'brass',(1,0,0),'Yaw')
anim_swing('AimYaw','Yaw',(0,0,1),100,91,True)
anim_swing('AimPitch','Pitch',(1,0,0),45,61,True)

# Track pod includes individually rigged links on an actual closed tread path.
L=.65; R=.32; TRACK_TOTAL=4*L+math.tau*R

def track_path(t):
    s=(t%1)*TRACK_TOTAL
    if s<2*L: return Vector((0,-L+s,R+.34)),0
    s-=2*L
    if s<math.pi*R:
        a=math.pi/2-s/R
        return Vector((0,L+R*math.cos(a),R*math.sin(a)+.34)),math.pi/2-a
    s-=math.pi*R
    if s<2*L: return Vector((0,L-s,-R+.34)),math.pi
    s-=2*L; a=-math.pi/2-s/R
    return Vector((0,-L+R*math.cos(a),R*math.sin(a)+.34)),math.pi/2-a

part('scrap_track_pod','Caterpillar track pod','Independent drive track with circulating rigid links and rotating road wheels.','Expansion')
box('Track side frame',(.22,1.57,.40),(0,0,.34))
for y in [-L,0,L]:
    name='RoadWheel_'+str(y); CUR.bone(name,(0,y,.34))
    cylinder('Track road wheel',.26,.38,(0,y,.34),'iron',(1,0,0),name,32)
    for x in [-.21,.21]:
        torus('Road wheel rim',.23,.029,(x,y,.34),'steel',(1,0,0),name)
        cylinder('Drive hub cap',.10,.052,(x,y,.34),'brass',(1,0,0),name)
        for i in range(6):
            a=i*math.tau/6; bolt((x,y+.15*math.sin(a),.34+.15*math.cos(a)),(1 if x>0 else -1,0,0),name,.7)
flange((-.34,-.25,.36),(-1,0,0),socket='Chassis_rear')
flange((-.34,.25,.36),(-1,0,0),socket='Chassis_front')
link_count=32; clip={}
for i in range(link_count):
    t=i/link_count; pp,angle=track_path(t); bone=f'Link_{i:02d}'; CUR.bone(bone,pp)
    q=Quaternion((1,0,0),-angle)
    box('Articulated track shoe',(.55,.135,.064),pp,'iron',bone,.012,q)
    box('Raised tread grip',(.56,.065,.045),pp+q@Vector((0,0,.050)),'steel',bone,.009,q)
    for x in [-.22,.22]:
        cylinder('Track hinge pin',.028,.07,pp+Vector((x,0,0)),'brass',(1,0,0),bone,12,.003)
    keys=[]
    for f in range(1,62):
        p2,a2=track_path(t+(f-1)/60)
        # Quaternion rotation at the wrap is continuous modulo a full revolution.
        keys.append((f,(1,0,0),-(a2-angle),tuple(p2-pp)))
    clip[bone]=keys
for y in [-L,0,L]:
    clip['RoadWheel_'+str(y)]=[(1+i*15,(1,0,0),-math.tau*2*i/4,(0,0,0)) for i in range(5)]
CUR.clip('TrackDrive',clip,True)

part('scrap_lift_mast','Winch lift mast','Riveted vertical mast, cable drum, guide carriage and animated lift travel.','Expansion')
CUR.bone('Carriage',(0,0,.30)); CUR.bone('Winch',(0,-.10,.18))
for x in [-.20,.20]:
    box('Lift mast rail',(.14,.19,1.75),(x,0,.76))
    for z in [.05,.65,1.45]: bolt((x,-.115,z),(0,-1,0))
box('Lift mast crown',(.55,.27,.17),(0,0,1.67))
box('Lift mast foot',(.66,.49,.17),(0,0,-.10),'iron')
flange((0,0,-.24),(0,0,-1),socket='Base')
flange((0,-.24,.15),(0,-1,0),socket='Rear')
box('Lift carriage bridge',(.53,.30,.24),(0,.05,.30),'iron','Carriage')
beam('Carriage socket neck',(0,.05,.30),(0,.37,.30),.20,'green','Carriage')
flange((0,.40,.30),(0,1,0),'Carriage',socket='Lifted_weapon')
for x in [-.26,.26]:
    for z in [.19,.41]: cylinder('Carriage guide roller',.065,.07,(x,0,z),'brass',(1,0,0),'Carriage')
cylinder('Winch cable drum',.12,.31,(0,-.10,.18),'iron',(1,0,0),'Winch')
for x in [-.18,.18]: cylinder('Winch spool flange',.16,.025,(x,-.10,.18),'rust',(1,0,0),'Winch')
# Closed chain runs, individual visible links mounted beside mast.
for side in [-1,1]:
    for j in range(21):
        torus('Mast winch chain link',.031,.008,(side*.09,-.15,.18+j*.069),'iron',
            (1,0,0) if j%2 else (0,1,0),majorseg=12,minseg=5)
    torus('Upper chain sprocket',.072,.018,(side*.09,-.15,1.60),'brass',(1,0,0))
CUR.clip('Lift',{
    'Carriage':[(1,(0,0,1),0,(0,0,0)),(46,(0,0,1),0,(0,0,1.10)),(91,(0,0,1),0,(0,0,0))],
    'Winch':[(1+i*22,(1,0,0),math.tau*2*i/4,(0,0,0)) for i in range(5)]},True)

part('scrap_outrigger_arm','Braced adjustable outrigger','Offset structural arm with support strut, end swivel and animated adjustment.','Expansion')
CUR.bone('EndSwivel',(.77,0,-.18))
beam('Outrigger primary beam',(-.48,0,.16),(.72,0,.16),.20)
beam('Outrigger diagonal brace',(-.42,0,-.24),(.62,0,.16),.11,'iron')
beam('Outrigger rear vertical',(-.45,0,-.24),(-.45,0,.16),.18)
flange((-.61,0,.16),(-1,0,0),socket='Base')
flange((-.45,0,-.35),(0,0,-1),socket='Lower_brace')
cylinder('Offset arm end swivel',.14,.12,(.77,0,.10),'brass')
beam('End support drop',(.77,0,.10),(.77,0,-.24),.18,'green','EndSwivel')
flange((.77,0,-.33),(0,0,-1),'EndSwivel',socket='Offset_weapon')
for x in [-.35,.12,.57]: bolt((x,-.115,.16),(0,-1,0))
anim_swing('Adjust','EndSwivel',(0,0,1),90,61,True)
print('ALL_GEOMETRY',len(ALL),'objects',sum(len(p.objects) for p in ALL),flush=True)
# -------------------- CLEANUP, UVs, RIGGING, EXPORT --------------------
def planar_uv(obj):
    uv=obj.data.uv_layers.new(name='UVMap') if not obj.data.uv_layers else obj.data.uv_layers[0]
    for poly in obj.data.polygons:
        n=poly.normal; axis=max(range(3),key=lambda i: abs(n[i]))
        dims=[i for i in range(3) if i!=axis]
        for loopid in poly.loop_indices:
            v=obj.data.vertices[obj.data.loops[loopid].vertex_index].co
            uv.data[loopid].uv=(v[dims[0]]*1.25+.17,v[dims[1]]*1.25+.23)

def action_curves(action):
    for layer in action.layers:
        for strip in layer.strips:
            for bag in strip.channelbags:
                for fc in bag.fcurves: yield fc

def make_rig(p):
    # Evaluate bevels once, then consolidate each rigid moving assembly to one mesh.
    for bone,objects in p.groups.items():
        bpy.ops.object.select_all(action='DESELECT')
        for o in objects: o.select_set(True)
        bpy.context.view_layer.objects.active=objects[0]
        bpy.ops.object.convert(target='MESH')
        bpy.ops.object.join()
        joined=bpy.context.object; joined.name=p.file+'__'+bone
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        planar_uv(joined)
        p.groups[bone]=[joined]
    p.objects=[objects[0] for objects in p.groups.values()]
    data=bpy.data.armatures.new(p.file+'_skeleton')
    rig=bpy.data.objects.new(p.file+'_rig',data); p.col.objects.link(rig); rig.parent=p.root
    p.rig=rig; rig.show_in_front=True
    bpy.ops.object.select_all(action='DESELECT'); rig.select_set(True); bpy.context.view_layer.objects.active=rig
    bpy.ops.object.mode_set(mode='EDIT')
    root=data.edit_bones.new('Root'); root.head=(0,0,0); root.tail=(0,0,.2)
    pending=dict(p.pivots)
    for name,(pivot,parent) in pending.items():
        b=data.edit_bones.new(name); b.head=pivot; b.tail=pivot+Vector((0,0,.20))
    for name,(_,parent) in pending.items(): data.edit_bones[name].parent=data.edit_bones[parent]
    bpy.ops.object.mode_set(mode='OBJECT')
    for bone,objects in p.groups.items():
        joined=objects[0]; joined.parent=rig
        vg=joined.vertex_groups.new(name=bone); vg.add(list(range(len(joined.data.vertices))),1,'REPLACE')
        mod=joined.modifiers.new('Rigid mechanical animation skin','ARMATURE'); mod.object=rig
    bpy.context.view_layer.update()
    for socket in p.sockets:
        bone=socket.get('animation_bone','Root')
        socket['rest_position_blender']=list(socket.location)
        socket['rest_normal_blender']=list(socket.rotation_quaternion@Vector((0,0,1)))
        if bone!='Root':
            saved=socket.matrix_world.copy(); socket.parent=rig; socket.parent_type='BONE'; socket.parent_bone=bone
            socket.matrix_world=saved
    p.actions=[]
    for clip,frames,loop in p.clips:
        rig.animation_data_create(); action=bpy.data.actions.new(p.file+'__'+clip)
        action.use_fake_user=True; rig.animation_data.action=action
        for pb in rig.pose.bones:
            pb.location=(0,0,0); pb.rotation_mode='QUATERNION'; pb.rotation_quaternion=Quaternion(); pb.scale=(1,1,1)
        for bone,keys in frames.items():
            pb=rig.pose.bones[bone]
            rest=data.bones[bone].matrix_local.to_quaternion()
            last=None
            for frame,axis,angle,delta in keys:
                q=rest.inverted()@Quaternion(Vector(axis),angle)@rest
                if last and last.dot(q)<0: q=-q
                last=q.copy(); pb.rotation_quaternion=q; pb.location=rest.inverted()@Vector(delta)
                pb.keyframe_insert(data_path='rotation_quaternion',frame=frame,group=bone)
                pb.keyframe_insert(data_path='location',frame=frame,group=bone)
        if p.file=='scrap_suspension_piston' and clip=='Compress':
            pb=rig.pose.bones['Coil']
            for frame,scale in [(1,1),(30,.70),(61,1)]:
                pb.scale=(1,scale,1); pb.keyframe_insert(data_path='scale',frame=frame,group='Coil')
        for curve in action_curves(action):
            for point in curve.keyframe_points: point.interpolation='LINEAR'
        track=rig.animation_data.nla_tracks.new(); track.name=action.name
        strip=track.strips.new(clip,1,action); strip.action_frame_start=min(k[0] for keys in frames.values() for k in keys)
        strip.action_frame_end=max(k[0] for keys in frames.values() for k in keys)
        track.mute=True
        p.actions.append((action,clip,loop,int(strip.action_frame_end)))
    rig.animation_data_create(); rig.animation_data.action=None
    for pb in rig.pose.bones:
        pb.location=(0,0,0); pb.rotation_quaternion=Quaternion(); pb.scale=(1,1,1)
    p.root['animation_clips']=json.dumps([{'name':a.name,'loop':loop,'frames':end} for a,c,loop,end in p.actions])
    bpy.context.view_layer.update()

for p in ALL:
    make_rig(p)
    print('RIGGED',p.file,'bones',len(p.rig.data.bones),'clips',len(p.actions),flush=True)


def glb_json(path):
    data=Path(path).read_bytes()
    if data[:4]!=b'glTF': raise RuntimeError('Invalid GLB magic '+str(path))
    length,kind=struct.unpack_from('<II',data,12)
    return json.loads(data[20:20+length].decode('utf-8'))

def export(p,red=False):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in p.col.objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=p.rig
    p.root['faction']='red' if red else 'green'
    swaps=[]
    if red:
        for obj in p.objects:
            for slot in obj.material_slots:
                if slot.material==MATS['green']: swaps.append(slot); slot.material=MATS['red']
    for track in p.rig.animation_data.nla_tracks: track.mute=False
    path=OUT/('red_faction' if red else '')/(p.file+'.glb')
    bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,
        export_yup=True,export_texcoords=True,export_normals=True,export_materials='EXPORT',
        export_skins=True,export_animations=True,export_animation_mode='NLA_TRACKS',
        export_force_sampling=True,export_frame_range=False,export_frame_step=1,
        export_anim_single_armature=True,export_extra_animations=False,export_extras=True,
        export_cameras=False,export_lights=False,export_apply=False)
    for slot in swaps: slot.material=MATS['green']
    p.root['faction']='green'
    for track in p.rig.animation_data.nla_tracks: track.mute=True
    for pb in p.rig.pose.bones: pb.location=(0,0,0); pb.rotation_quaternion=Quaternion(); pb.scale=(1,1,1)
    SCENE.frame_set(1); bpy.context.view_layer.update()
    doc=glb_json(path)
    animations=doc.get('animations',[])
    if p.actions and len(animations)<len(p.actions):
        raise RuntimeError(f'{p.file}: expected {len(p.actions)} clips, exported {len(animations)}')
    if not doc.get('meshes') or not doc.get('skins'): raise RuntimeError(f'Missing mesh or mechanical rig: {p.file}')
    if any(not a.get('channels') or not a.get('samplers') for a in animations): raise RuntimeError('Empty animation channels')
    if not red:
        p.export_doc=doc
        p.bounds=world_bounds(p)
    print('EXPORTED',path.name,'RED' if red else 'GREEN','meshes',len(doc['meshes']),'animations',len(animations),flush=True)


def world_bounds(p):
    coords=[o.matrix_world@Vector(v) for o in p.objects for v in o.bound_box]
    return [min(v[i] for v in coords) for i in range(3)],[max(v[i] for v in coords) for i in range(3)]

bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'GoblinScraper_RefinedParts.blend'),compress=True)
for p in ALL:
    export(p); export(p,True)

manifest={'kit':'Goblin Scraper refined modular vehicle parts','version':1,'authoring_blender':'5.0',
    'units':'metres','glb_axes':'Y up, forward -Z, wheel shafts X',
    'mount_standard':{'id':'GS-F340-4B','outer_width_m':.340,'square_opening_m':.140,'bolt_centres_m':.250,
        'flange_thickness_m':.065,'usage':'Mate socket origins and oppose their normals; keep world scale 1.'},
    'parts':[]}
for p in ALL:
    doc=p.export_doc
    minb,maxb=p.bounds
    togame=lambda v:[round(float(v[0]),5),round(float(v[2]),5),round(float(-v[1]),5)]
    manifest['parts'].append({'file':p.file+'.glb','red_variant':'red_faction/'+p.file+'.glb',
        'label':p.label,'group':p.group,'description':p.description,
        'meshes':len(doc['meshes']),'bones':len(p.rig.data.bones),
        'triangles':sum(len(poly.vertices)-2 for obj in p.objects for poly in obj.data.polygons),
        'bounds_blender_z_up':[minb,maxb],
        'sockets':[{'name':s.name,'position':togame(s['rest_position_blender']),
            'normal':togame(s['rest_normal_blender']),'connector':'GS-F340-4B','animation_bone':s.get('animation_bone','Root')} for s in p.sockets],
        'animations':[{'name':a['name'],'duration_seconds':max(
            doc['accessors'][sampler['input']].get('max',[0])[0] for sampler in a['samplers']),
            'loop':next((loop for action,c,loop,end in p.actions if action.name==a['name']),False)} for a in doc.get('animations',[])]})
(OUT/'parts-catalog.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')

# Studio for real geometry thumbnails, kept separate from exported asset collections.
studio=bpy.data.collections.new('_Preview Studio'); SCENE.collection.children.link(studio)
def studio_obj(obj):
    for c in list(obj.users_collection): c.objects.unlink(obj)
    studio.objects.link(obj); return obj
bpy.ops.mesh.primitive_plane_add(size=200,location=(0,0,-2)); ground=studio_obj(bpy.context.object); ground.name='Studio Ground'
floor=bpy.data.materials.new('Warm neutral studio floor'); floor.diffuse_color=(.26,.25,.22,1); floor.use_nodes=True
floor.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(.26,.25,.22,1)
floor.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.9
ground.data.materials.append(floor)

for name,pos,energy,size in [('Key',(3,4,7),1100,5),('Fill',(-4,1,4),850,4),('Rim',(1,-5,6),1250,4)]:
    bpy.ops.object.light_add(type='AREA',location=pos); light=studio_obj(bpy.context.object); light.name=name
    light.data.energy=energy; light.data.shape='DISK'; light.data.size=size
    light.rotation_euler=(Vector((0,0,.3))-light.location).to_track_quat('-Z','Y').to_euler()
bpy.ops.object.camera_add(); camera=studio_obj(bpy.context.object); camera.name='Part Preview Camera'; SCENE.camera=camera
camera.data.type='ORTHO'; camera.data.lens=50


def frame_part(p):
    for other in ALL: other.col.hide_render=other is not p
    minb,maxb=world_bounds(p); center=(Vector(minb)+Vector(maxb))*.5
    size=max(maxb[i]-minb[i] for i in range(3))
    camera.location=center+Vector((1.4,1.8,1.25))*max(size,1)
    camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler()
    camera.data.ortho_scale=max(size*1.62,.85)
    ground.location.z=minb[2]-.012
    SCENE.camera=camera

for p in ALL:
    frame_part(p)
    SCENE.render.filepath=str(OUT/'previews'/(p.file+'.png'))
    SCENE.frame_set(1)
    bpy.ops.render.render(write_still=True)
    print('PREVIEW',p.file,flush=True)

# Animation demonstrations from evaluated Blender rigs, not synthetic image motion.
animated_preview=['scrap_crushing_drum','scrap_auger_drill','scrap_grabber_jaws','scrap_track_pod']
(OUT/'previews'/'motion_frames').mkdir(exist_ok=True)
SCENE.render.resolution_x=256; SCENE.render.resolution_y=256
motion=[]
for file in animated_preview:
    p=next(p for p in ALL if p.file==file); frame_part(p)
    action,clip,loop,end=p.actions[0]
    p.rig.animation_data.action=action
    matrix_samples=[]
    for i,f in enumerate(np.linspace(1,end,9)[:-1]):
        SCENE.frame_set(int(f)); bpy.context.view_layer.update()
        matrix_samples.append([tuple(pb.matrix.to_translation())+tuple(pb.matrix.to_quaternion()) for pb in p.rig.pose.bones])
        SCENE.render.filepath=str(OUT/'previews'/'motion_frames'/f'{file}_{i:02d}.png')
        bpy.ops.render.render(write_still=True)
    if all(matrix_samples[0]==sample for sample in matrix_samples[1:]): raise RuntimeError('Animation did not move '+file)
    p.rig.animation_data.action=None
    for pb in p.rig.pose.bones: pb.location=(0,0,0); pb.rotation_quaternion=Quaternion()
    SCENE.frame_set(1)
    motion.append({'part':file,'clip':action.name,'evaluated_motion':True})
    print('MOTION_VERIFIED',file,flush=True)
(OUT/'animation-validation.json').write_text(json.dumps(motion,indent=2),encoding='utf-8')

# Editable source file: every part is in its own collection; animations retained as named Actions.
for i,p in enumerate(ALL):
    p.col.hide_render=False
    p.root.location=((i%5)*4.5,-(i//5)*4.5,1.2)
    p.rig.animation_data.action=None
    for pb in p.rig.pose.bones: pb.location=(0,0,0); pb.rotation_quaternion=Quaternion()
    data=bpy.data.curves.new('Label '+p.label,'FONT'); data.body=p.label; data.size=.24; data.align_x='CENTER'
    obj=bpy.data.objects.new('Label '+p.label,data); studio.objects.link(obj)
    obj.location=(p.root.location.x,p.root.location.y-1.7,.025)
    obj.rotation_euler=(0,0,0)
SCENE.frame_set(1)
ground.location.z=-.08
camera.location=(30,17,34); target=Vector((9,-10,1))
camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler(); camera.data.ortho_scale=39
SCENE.render.resolution_x=1920; SCENE.render.resolution_y=1440
# Show the whole source library in the default viewport when the .blend is opened.
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_distance=35
            area.spaces.active.region_3d.view_location=(9,-10,1)
            area.spaces.active.region_3d.view_rotation=camera.rotation_euler.to_quaternion()
            area.spaces.active.shading.type='MATERIAL'
SCENE['kit_readme']='Each collection is one part. Select its rig; choose a named Action to preview movement. Units metres. All meshes UV mapped with packed PBR textures.'
bpy.ops.object.select_all(action='DESELECT')
ALL[0].root.select_set(True); bpy.context.view_layer.objects.active=ALL[0].root
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'GoblinScraper_RefinedParts.blend'),compress=True)
SCENE.render.filepath=str(OUT/'previews'/'whole-library.png')
bpy.ops.render.render(write_still=True)
print('KIT_COMPLETE',len(ALL),'green and',len(ALL),'red GLBs',flush=True)


