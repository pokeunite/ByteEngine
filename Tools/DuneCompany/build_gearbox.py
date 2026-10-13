import bpy,math,json,uuid
from mathutils import Vector
from pathlib import Path
ROOT=Path('C:/Users/codex/Documents/DuneCompany');OUT=ROOT/'Assets/DuneParts';SOURCE=Path('C:/Users/codex/Downloads/byte engine files/Dune Gearbox');SOURCE.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
def material(name,color,metal=.7):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1);p.inputs['Metallic'].default_value=metal;p.inputs['Roughness'].default_value=.62;return m
# Reuse the approved block kit's actual embedded textures and PBR materials.
bpy.ops.import_scene.gltf(filepath=str(OUT/'glb/38_race_engine.glb'))
for o in list(bpy.context.scene.objects):bpy.data.objects.remove(o,do_unlink=True)
steel=bpy.data.materials['Ivory'];orange=bpy.data.materials['Orange'];dark=bpy.data.materials['Rubber'];bolt=bpy.data.materials['Steel'];olive=bpy.data.materials['Olive'];parts=[]
def finish(o,name,mat):
 o.name=name;o.data.materials.append(mat);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);parts.append(o);return o
def box(name,p,size,mat,bevel=.007):
 bpy.ops.mesh.primitive_cube_add(size=1,location=p);o=bpy.context.object;o.dimensions=size;finish(o,name,mat)
 if bevel:mod=o.modifiers.new('Cast edge','BEVEL');mod.width=bevel;mod.segments=2;bpy.ops.object.modifier_apply(modifier=mod.name)
 return o
def cyl(name,p,r,depth,mat,rotation=(math.pi/2,0,0),verts=24):
 bpy.ops.mesh.primitive_cylinder_add(vertices=verts,radius=r,depth=depth,location=p,rotation=rotation);o=finish(bpy.context.object,name,mat)
 mod=o.modifiers.new('Machined rim','BEVEL');mod.width=.0018;mod.segments=2;bpy.ops.object.modifier_apply(modifier=mod.name)
 for p in o.data.polygons:p.use_smooth=len(p.vertices)==4 and verts>8
 return o
box('Fixed_Body',(0,0,.195),(.39,.47,.30),steel,.022)
box('Base dark seam',(0,0,.062),(.49,.54,.012),dark,.003)
for y in [-.19,.19]:
 box('Mount foot',(0,y,.027),(.5,.105,.054),steel)
 for x in [-.212,.212]:cyl('Mount bolt',(x,y,.068),.025,.028,bolt,(0,0,0),6)
for y in [-.25,.25]:
 cyl('End casing',(0,y,.20),.174,.048,steel)
 cyl('Flange trim',(0,y*1.105,.20),.13,.020,bolt)
 cyl('Bearing seal',(0,y*1.16,.20),.087,.022,dark)
 cyl('Drive shaft',(0,y*1.28,.20),.056,.06,bolt)
 for i in range(8):
  a=i*math.tau/8;cyl('Flange bolt',(math.cos(a)*.148,-.355 if y<0 else y*1.10,.20+math.sin(a)*.148),.015,.020,bolt,verts=6)
cyl('Engine mating flange',(0,-.335,.20),.19,.032,steel)
cyl('Bellhousing dark seam',(0,-.308,.20),.175,.014,dark)
for y in [-.165,-.055,.055,.165]:
 box('Housing rib',(0,y,.205),(.414,.020,.32),steel,.003)
for x in [-.206,.206]:
 box('Inspection recess',(x,0,.205),(.012,.31,.195),dark)
 box('Inspection panel',(x*1.035,0,.205),(.016,.29,.175),steel)
 box('Orange identification stripe',(x*1.085,0,.215),(.003,.26,.028),orange,.001)
 for y in [-.12,.12]:
  for z in [.14,.27]:cyl('Inspection bolt',(x*1.10,y,z),.014,.018,bolt,(0,math.pi/2,0),6)
box('Shift tower',(.11,.13,.37),(.10,.12,.07),olive)
box('Shift tower stripe',(.11,.13,.406),(.085,.045,.003),orange,.001)
a=Vector((.11,.13,.40));b=Vector((.11,.195,.455));o=cyl('Shift linkage',(a+b)/2,.012,(b-a).length,bolt,(0,0,0));o.rotation_euler=(b-a).to_track_quat('Z','Y').to_euler()
bpy.ops.mesh.primitive_uv_sphere_add(segments=16,ring_count=8,radius=.026,location=b);finish(bpy.context.object,'Shift knob',dark)
# Embedded kit textures need real UVs; unwrap all pieces before combining material draws.
for o in parts:
 bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o;bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT');bpy.ops.uv.smart_project(island_margin=.02);bpy.ops.object.mode_set(mode='OBJECT')
# Author in Blender Z-up; glTF converts to engine Y-up. Engine import adds a yaw but bounds remain symmetric.
bpy.ops.object.select_all(action='DESELECT')
lever_parts=[o for o in parts if o.name.startswith(('Shift linkage','Shift knob'))];body_parts=[o for o in parts if o not in lever_parts]
for o in body_parts:o.select_set(True)
bpy.context.view_layer.objects.active=body_parts[0];bpy.ops.object.join();body=bpy.context.object;body.name='Fixed_Body'
bpy.ops.object.select_all(action='DESELECT')
for o in lever_parts:o.select_set(True)
bpy.context.view_layer.objects.active=lever_parts[0];bpy.ops.object.join();lever=bpy.context.object;lever.name='Shift_Lever';bpy.ops.object.transform_apply(location=False,rotation=True,scale=True);scene=bpy.context.scene;scene.cursor.location=a;bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
bpy.ops.object.armature_add();rig=bpy.context.object;rig.name='Gearbox_Rig';bpy.ops.object.mode_set(mode='EDIT');bone=rig.data.edit_bones[0];bone.name='GearboxBody';bone.head=(0,0,0);bone.tail=(0,0,.1);moving=rig.data.edit_bones.new('GearboxLever');moving.head=a;moving.tail=a+Vector((0,0,.1));moving.parent=bone;bpy.ops.object.mode_set(mode='OBJECT')
for obj,name in [(body,'GearboxBody'),(lever,'GearboxLever')]:
 group=obj.vertex_groups.new(name=name);group.add(list(range(len(obj.data.vertices))),1,'REPLACE');modifier=obj.modifiers.new('Gearbox rig','ARMATURE');modifier.object=rig;obj.parent=rig
for name,angle in [('Idle',0),('ShiftUp',.32),('ShiftDown',-.32)]:
 action=bpy.data.actions.new(name);rig.animation_data_create();rig.animation_data.action=action;pose=rig.pose.bones['GearboxLever'];pose.rotation_mode='XYZ'
 for f,value in [(1,0),(7,angle),(14,0)]:pose.rotation_euler=(value,0,0);pose.keyframe_insert(data_path='rotation_euler',frame=f)
 track=rig.animation_data.nla_tracks.new();track.name=name;track.strips.new(name,1,action)
rig.animation_data.action=None;scene.render.fps=60
bpy.ops.object.select_all(action='DESELECT')
for obj in [body,lever,rig]:obj.select_set(True)
path=OUT/'glb/39_gearbox.glb';bpy.ops.export_scene.gltf(filepath=str(path),export_format='GLB',use_selection=True,export_animations=True,export_animation_mode='NLA_TRACKS',export_force_sampling=True)
for track in rig.animation_data.nla_tracks:track.mute=True
rig.pose.bones['GearboxLever'].rotation_euler=(0,0,0);scene.frame_set(1)
def meta(path,kind):Path(str(path)+'.meta').write_text(json.dumps(dict(guid=str(uuid.uuid5(uuid.NAMESPACE_URL,'dune/gearbox/'+str(path))),type=kind,**({'modelImporter':dict(importScale=1,generateNormals=False,preferEmbeddedMaterials=True,importAnimations=True,rigType='Generic')} if kind=='Model3D' else {})),indent=2))
meta(path,'Model3D')
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.samples=24;scene.render.resolution_x=640;scene.render.resolution_y=640;scene.render.resolution_percentage=100;scene.render.film_transparent=True;scene.view_settings.view_transform='AgX';scene.world=bpy.data.worlds.new('Studio');scene.world.use_nodes=True;scene.world.node_tree.nodes['Background'].inputs[0].default_value=(.20,.20,.20,1)
bpy.ops.object.camera_add(location=(1.15,-1.25,.9));camera=bpy.context.object;camera.rotation_euler=(Vector((0,0,.20))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.type='ORTHO';camera.data.ortho_scale=.87;scene.camera=camera
for p,power,size in [((1,-1,2),130,2),((-1,-.3,1.3),100,1.8),((0,1,1),90,1.5)]:
 bpy.ops.object.light_add(type='AREA',location=p);o=bpy.context.object;o.data.energy=power;o.data.shape='DISK';o.data.size=size;o.rotation_euler=(Vector((0,0,.2))-o.location).to_track_quat('-Z','Y').to_euler()
scene.render.filepath=str(OUT/'cutouts/39_gearbox.png');bpy.ops.render.render(write_still=True);meta(OUT/'cutouts/39_gearbox.png','Texture2D');bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE/'Gearbox.blend'))
print('GEARBOX ASSET PASS')
