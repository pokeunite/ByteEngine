import bpy, math, json, struct
from pathlib import Path
from mathutils import Vector, Matrix, Quaternion

OUT=Path(r'C:/Users/codex/Downloads/byte engine files')
PREVIEW=OUT/'Karlson Gun Previews'
OUT.mkdir(parents=True,exist_ok=True);PREVIEW.mkdir(exist_ok=True)
if (OUT/'Karlson_Guns.blend').exists(): raise RuntimeError('Existing Blender source is preserved; choose a versioned destination.')
print('Initial fresh background Blender scene:',list(bpy.data.objects.keys()))
bpy.ops.wm.read_factory_settings(use_empty=True)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.render.fps=60
scene.render.engine='BLENDER_EEVEE_NEXT';scene.render.resolution_x=1200;scene.render.resolution_y=760;scene.render.resolution_percentage=100
scene.world=bpy.data.worlds.new('Neutral studio');scene.world.use_nodes=True
scene.world.node_tree.nodes['Background'].inputs['Color'].default_value=(.3,.33,.38,1)
scene.world.node_tree.nodes['Background'].inputs['Strength'].default_value=.7
scene.view_settings.view_transform='AgX'
MATS={}
for name,color in [('Body',(.095,.105,.12,1)),('Slide',(.23,.25,.28,1)),('Grip',(.038,.045,.052,1)),('Steel',(.31,.33,.36,1)),('Dark',(.012,.016,.022,1))]:
    mat=bpy.data.materials.new(name);mat.use_nodes=True;mat.diffuse_color=color
    bsdf=mat.node_tree.nodes.get('Principled BSDF');bsdf.inputs['Base Color'].default_value=color;bsdf.inputs['Roughness'].default_value=.75
    MATS[name]=mat

class Gun:
    def __init__(self,name,bones):
        self.name=name;self.collection=bpy.data.collections.new(name);scene.collection.children.link(self.collection)
        self.parts=[];self.bones=bones;self.actions={}
    def own(self,obj,bone,mat):
        for collection in list(obj.users_collection):collection.objects.unlink(obj)
        self.collection.objects.link(obj);obj.data.materials.append(MATS[mat]);obj['motion_assembly']=bone
        bpy.context.view_layer.objects.active=obj;obj.select_set(True)
        bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
        obj.select_set(False);self.parts.append((obj,bone));return obj
    def box(self,name,center,size,bone='Frame',mat='Body',angle=0,bevel=.0015):
        bpy.ops.object.select_all(action='DESELECT');bpy.ops.mesh.primitive_cube_add(size=1,location=center)
        obj=bpy.context.object;obj.name=self.name+'_'+name;obj.dimensions=size;obj.rotation_euler.x=math.radians(angle)
        bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
        if bevel:
            mod=obj.modifiers.new('Single facet bevel','BEVEL');mod.width=bevel;mod.segments=1
            bpy.ops.object.modifier_apply(modifier=mod.name)
        return self.own(obj,bone,mat)
    def cylinder(self,name,center,radius,length,bone='Frame',mat='Steel',sides=8):
        bpy.ops.object.select_all(action='DESELECT');bpy.ops.mesh.primitive_cylinder_add(vertices=sides,radius=radius,depth=length,location=center,rotation=(math.pi/2,0,0))
        obj=bpy.context.object;obj.name=self.name+'_'+name;return self.own(obj,bone,mat)
    def guard(self,y,z,length,width,height):
        self.box('Guard bottom',(0,y,z-height/2),(width,length,.009),mat='Grip',bevel=.001)
        self.box('Guard front',(0,y+length/2,z), (width,.009,height),mat='Grip',bevel=.001)
    def rig(self):
        data=bpy.data.armatures.new(self.name+'_Skeleton');rig=bpy.data.objects.new(self.name+'_Rig',data);self.collection.objects.link(rig)
        bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
        root=data.edit_bones.new('WeaponRoot');root.head=(0,0,0);root.tail=(0,0,.08)
        for name,head in self.bones.items():
            b=data.edit_bones.new(name);b.head=head;b.tail=Vector(head)+Vector((0,0,.035));b.parent=root
        bpy.ops.object.mode_set(mode='OBJECT');rig.select_set(False)
        for obj,bone in self.parts:
            obj.parent=rig;group=obj.vertex_groups.new(name=bone);group.add(list(range(len(obj.data.vertices))),1,'REPLACE')
            mod=obj.modifiers.new('Rigid assembly skin','ARMATURE');mod.object=rig
        self.rig_object=rig;rig.animation_data_create()
        for pbone in rig.pose.bones:pbone.rotation_mode='QUATERNION'
        self.make_actions()
    def key(self,bone,frame,move=(0,0,0),pitch=0,roll=0):
        pbone=self.rig_object.pose.bones[bone];basis=self.rig_object.data.bones[bone].matrix_local.to_3x3()
        pbone.location=basis.inverted()@Vector(move)
        rotation=Matrix.Rotation(math.radians(pitch),3,'X')@Matrix.Rotation(math.radians(roll),3,'Y')
        pbone.rotation_quaternion=(basis.inverted()@rotation@basis).to_quaternion()
        pbone.keyframe_insert(data_path='location',frame=frame);pbone.keyframe_insert(data_path='rotation_quaternion',frame=frame)
    def make_actions(self):
        for clip,last in [('Idle',60),('Fire',18 if self.name=='Shotgun' else 12),('Reload',100 if self.name=='Shotgun' else 90),('Equip',30)]:
            action=bpy.data.actions.new(self.name+'_'+clip);action.use_fake_user=True;self.rig_object.animation_data.action=action
            for bone in self.rig_object.pose.bones:self.key(bone.name,1);self.key(bone.name,last)
            if clip=='Idle':
                for f in [1,15,30,45,60]:self.key('WeaponRoot',f,move=(0,0,.0015*math.sin((f-1)/59*math.tau)),roll=.3*math.sin((f-1)/59*math.tau))
            elif clip=='Fire':
                self.key('WeaponRoot',3,move=(0,-.018,0),pitch=5 if self.name!='Rifle' else 3)
                self.key('WeaponRoot',8)
                self.key('Trigger',2,pitch=-12);self.key('Trigger',6)
                if self.name=='Pistol':self.key('Slide',3,move=(0,-.032,0));self.key('Slide',7)
                if self.name=='Rifle':self.key('Bolt',3,move=(0,-.035,0));self.key('Bolt',6)
                if self.name=='Shotgun':self.key('Pump',9,move=(0,-.11,0));self.key('Pump',16)
            elif clip=='Reload':
                self.key('WeaponRoot',15,pitch=-16,roll=-12);self.key('WeaponRoot',last-15,pitch=-16,roll=-12)
                if self.name!='Shotgun':
                    for f,amount in [(1,0),(12,0),(28,1),(55,1),(72,0),(90,0)]:self.key('Magazine',f,move=(0,-.04*amount,-.24*amount))
                    if self.name=='Pistol':self.key('Slide',78,move=(0,-.032,0));self.key('Slide',85)
                    else:self.key('Bolt',78,move=(0,-.04,0));self.key('Bolt',85)
                else:
                    for f in [22,44,66]:self.key('LoadingGate',f,pitch=32);self.key('LoadingGate',f+10)
                    self.key('Pump',84,move=(0,-.11,0));self.key('Pump',95)
            else:
                self.key('WeaponRoot',1,move=(0,0,-.24),pitch=-25);self.key('WeaponRoot',20)
            self.actions[clip]=action
        self.rig_object.animation_data.action=None
        for clip,action in self.actions.items():
            track=self.rig_object.animation_data.nla_tracks.new();track.name=clip
            strip=track.strips.new(clip,1,action);strip.name=clip;strip.extrapolation='NOTHING';track.mute=True
        self.rest()
    def rest(self):
        self.rig_object.animation_data.action=None
        for bone in self.rig_object.pose.bones:bone.location=(0,0,0);bone.rotation_quaternion=(1,0,0,0);bone.scale=(1,1,1)
        scene.frame_set(1);bpy.context.view_layer.update()
    def export(self):
        self.rest();bpy.ops.object.select_all(action='DESELECT');self.rig_object.select_set(True)
        for obj,_ in self.parts:obj.select_set(True)
        bpy.context.view_layer.objects.active=self.rig_object
        for track in self.rig_object.animation_data.nla_tracks:track.mute=False
        bpy.ops.export_scene.gltf(filepath=str(OUT/('Karlson_'+self.name+'.glb')),export_format='GLB',use_selection=True,export_animations=True,export_animation_mode='NLA_TRACKS',export_nla_strips=True,export_anim_slide_to_zero=True,export_force_sampling=True,export_skins=True,export_yup=True,export_apply=False)
        for track in self.rig_object.animation_data.nla_tracks:track.mute=True
        self.rest()

p=Gun('Pistol',{'Frame':(0,0,0),'Slide':(0,0,.02),'Magazine':(0,-.035,-.1),'Trigger':(0,.026,-.035)})
p.box('Frame',(0,.025,-.014),(.036,.22,.034))
p.box('Slide',(0,.028,.026),(.042,.246,.052),'Slide','Slide',bevel=.003)
p.cylinder('Barrel',(0,.091,.023),.011,.146,'Frame')
p.cylinder('Muzzle inset',(0,.151,.024),.006,.001,'Frame','Dark')
for side in [-1,1]:p.box('Grip side '+str(side),(side*.014,-.046,-.085),(.010,.043,.126),mat='Grip',angle=-10)
p.box('Grip back',(0,-.068,-.085),(.033,.007,.126),mat='Grip',angle=-10)
p.box('Grip front',(0,-.024,-.085),(.033,.006,.126),mat='Grip',angle=-10)
p.box('Magazine',(0,-.045,-.086),(.023,.032,.113),'Magazine','Body',angle=-10,bevel=.001)
p.box('Magazine floorplate',(0,-.055,-.145),(.029,.041,.007),'Magazine','Grip',bevel=.001)
p.guard(.03,-.043,.061,.029,.051)
p.box('Trigger',(0,.031,-.043),(.010,.013,.031),'Trigger','Dark',angle=-15,bevel=.001)
p.box('Front sight',(0,.12,.06),(.009,.009,.009),'Slide','Grip',bevel=.0005)
p.box('Rear sight',(0,-.066,.061),(.027,.012,.011),'Slide','Grip',bevel=.001)
p.box('Slide side control',(.023,-.021,.015),(.007,.027,.011),'Frame','Steel',bevel=.001)
p.rig()

s=Gun('Shotgun',{'Frame':(0,0,0),'Pump':(0,.21,-.015),'LoadingGate':(0,-.007,-.029),'Trigger':(0,-.055,-.043)})
s.box('Receiver',(0,-.004,.002),(.050,.205,.082),mat='Slide',bevel=.004)
s.cylinder('Barrel',(0,.285,.028),.014,.50)
s.cylinder('Barrel bore',(0,.536,.028),.009,.001,mat='Dark')
s.cylinder('Tube',(0,.27,-.014),.012,.46,mat='Body')
s.cylinder('Tube end',(0,.502,-.014),.010,.003,mat='Dark')
s.box('Pump',(0,.26,-.014),(.057,.18,.061),'Pump','Grip',bevel=.004)
for i in range(6):s.box('Pump rib '+str(i),(0,.19+i*.025,-.014),(.062,.007,.063),'Pump','Grip',bevel=.001)
s.box('Grip',(0,-.116,-.091),(.038,.048,.133),mat='Grip',angle=-18,bevel=.003)
s.box('Stock neck',(0,-.166,-.019),(.043,.11,.062),mat='Body',angle=10,bevel=.003)
s.box('Stock',(0,-.295,-.037),(.058,.20,.125),mat='Body',angle=8,bevel=.007)
s.box('Butt pad',(0,-.401,-.050),(.064,.018,.137),mat='Grip',angle=8,bevel=.003)
s.guard(-.04,-.049,.060,.032,.052)
s.box('Trigger',(0,-.056,-.048),(.010,.015,.032),'Trigger','Dark',angle=-18,bevel=.001)
s.box('Loading gate',(0,-.006,-.041),(.028,.055,.008),'LoadingGate','Steel',bevel=.001)
s.box('Front sight',(0,.49,.047),(.008,.012,.016),mat='Grip',bevel=.001)
s.box('Rear sight',(0,-.07,.051),(.025,.013,.014),mat='Grip',bevel=.001)
s.rig()

r=Gun('Rifle',{'Frame':(0,0,0),'Magazine':(0,.037,-.1),'Trigger':(0,-.047,-.042),'Bolt':(0,.015,.016)})
r.box('Receiver',(0,-.015,.014),(.057,.20,.072),mat='Slide',bevel=.004)
r.box('Handguard',(0,.185,.016),(.062,.22,.071),mat='Body',bevel=.004)
for side in [-1,1]:
    for i in range(3):r.box('Vent '+str(side)+'_'+str(i),(side*.0315,.117+i*.058,.028),(.0015,.028,.012),mat='Dark',bevel=0)
r.cylinder('Barrel',(0,.345,.021),.012,.17,mat='Steel')
r.box('Muzzle block',(0,.449,.021),(.038,.044,.034),mat='Body',bevel=.003)
r.cylinder('Muzzle inset',(0,.472,.021),.009,.001,mat='Dark')
r.box('Grip',(0,-.113,-.091),(.038,.047,.118),mat='Grip',angle=-17,bevel=.002)
r.box('Magazine',(0,.040,-.106),(.031,.059,.157),'Magazine','Body',angle=-8,bevel=.002)
r.box('Magazine base',(0,.028,-.188),(.036,.065,.010),'Magazine','Grip',angle=-8,bevel=.001)
r.box('Stock upper',(0,-.226,.013),(.040,.223,.043),mat='Body',bevel=.003)
r.box('Stock diagonal',(0,-.243,-.028),(.027,.18,.024),mat='Body',angle=-24,bevel=.002)
r.box('Stock butt',(0,-.330,-.023),(.051,.034,.110),mat='Body',bevel=.003)
r.guard(-.03,-.049,.059,.033,.051)
r.box('Trigger',(0,-.047,-.047),(.010,.013,.030),'Trigger','Dark',angle=-15,bevel=.001)
r.box('Charging handle',(.034,.009,.031),(.014,.029,.012),'Bolt','Steel',bevel=.001)
r.box('Bolt',(.029,.002,.016),(.004,.057,.023),'Bolt','Steel',bevel=.001)
r.box('Front sight',(0,.26,.064),(.011,.014,.036),mat='Grip',bevel=.001)
r.box('Rear sight',(0,-.075,.064),(.025,.017,.034),mat='Grip',bevel=.001)
r.rig()

guns=[p,s,r]
audit=[]
for gun in guns:
    gun.export()
    vertices=[];triangles=0
    for obj,bone in gun.parts:
        vertices.extend([obj.matrix_world@v.co for v in obj.data.vertices]);obj.data.calc_loop_triangles();triangles+=len(obj.data.loop_triangles)
        assert len(obj.vertex_groups)==1 and all(abs(v.groups[0].weight-1)<1e-6 for v in obj.data.vertices)
    low=Vector(tuple(min(v[i] for v in vertices) for i in range(3)));high=Vector(tuple(max(v[i] for v in vertices) for i in range(3)))
    audit.append({'gun':gun.name,'artistic_dimensions_m':list(high-low),'triangles':triangles,'bones':list(gun.rig_object.data.bones.keys()),'clips':list(gun.actions.keys()),'reload_scope':'Weapon-only mechanism test; no character hands. Shotgun gate cycles represent loading, without shells.'})

studio=bpy.data.collections.new('Preview Studio - excluded from exports');scene.collection.children.link(studio)
def studio_obj(name,data):
    obj=bpy.data.objects.new(name,data);studio.objects.link(obj);return obj
camera=studio_obj('Concept camera',bpy.data.cameras.new('Concept camera'));camera.data.type='ORTHO';scene.camera=camera
for name,location,power,size in [('Key',(2,1.5,3),450,3),('Fill',(-2,0,2),230,3),('Rim',(0,-3,2),330,2)]:
    data=bpy.data.lights.new(name,'AREA');data.energy=power;data.shape='DISK';data.size=size
    light=studio_obj(name,data);light.location=location;light.rotation_euler=(Vector((0,0,0))-light.location).to_track_quat('-Z','Y').to_euler()
def render(gun,view,clip=None,frame=1):
    for other in guns:
        for obj in other.collection.objects:obj.hide_render=other!=gun
    gun.rest()
    if clip:gun.rig_object.animation_data.action=gun.actions[clip];scene.frame_set(frame);bpy.context.view_layer.update()
    target=Vector((0,.01,-.03));camera.location={'three-quarter':Vector((1.4,1.3,.85)),'side':Vector((1.8,0,.05)),'top':Vector((.001,0,2)),'front':Vector((0,2,.05))}[view]
    camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=.40 if gun.name=='Pistol' else 1.12
    if clip=='Reload':camera.data.ortho_scale*=1.25
    scene.render.filepath=str(PREVIEW/(gun.name+'_'+(clip+'_'+str(frame) if clip else view)+'.png'));bpy.ops.render.render(write_still=True)
    gun.rest()
for gun in guns:
    for view in ['three-quarter','side','top','front']:render(gun,view)
    render(gun,'three-quarter','Fire',3);render(gun,'three-quarter','Reload',40);render(gun,'three-quarter','Reload',72)
for i,gun in enumerate(guns):
    gun.rest();gun.rig_object.location.x=(i-1)*.65
    for obj in gun.collection.objects:obj.hide_render=False
camera.location=(2.7,2.5,1.8);camera.rotation_euler=(Vector((0,0,0))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=2.3
scene.render.filepath=str(PREVIEW/'All_guns.png');bpy.ops.render.render(write_still=True)
scene.frame_set(1);bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Karlson_Guns.blend'))
(OUT/'Karlson_Guns_validation.json').write_text(json.dumps(audit,indent=2))
print('GUN MODEL EXPORT COMPLETE',json.dumps(audit))
