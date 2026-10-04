import bpy,math,json,sys
from pathlib import Path
from mathutils import Vector
out=Path(r'C:\Users\codex\ByteEngine\Designs\BattlePrototype\assets');out.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.gltf(filepath=r'C:\Users\codex\ByteEngine\Designs\BattlePrototype\goblin-source.glb')
for o in bpy.data.objects:
 if o.animation_data:o.animation_data_clear()
rigdata=bpy.data.armatures.new('RedGoblinSkeleton');rig=bpy.data.objects.new('RedGoblinRig',rigdata);bpy.context.collection.objects.link(rig);bpy.context.view_layer.objects.active=rig;rig.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
names=['torso','head','arm-l','arm-r','thigh-l','thigh-r','shin-l','shin-r','fore-r'];bones={}
for name in names:
 bone=rigdata.edit_bones.new(name);bone.head=bpy.data.objects[name].matrix_world.translation;bone.tail=bone.head+Vector((0,0,.1));bones[name]=bone
for name,bone in bones.items():
 parent={'head':'torso','arm-l':'torso','arm-r':'torso','thigh-l':'torso','thigh-r':'torso','shin-l':'thigh-l','shin-r':'thigh-r','fore-r':'arm-r'}.get(name)
 if parent:bone.parent=bones[parent]
bpy.ops.object.mode_set(mode='OBJECT')
# Extend the goblin silhouette with pointed ears and a broad hooked nose.
leaf=bpy.data.materials.get('leaf')
for sign in [-1,1]:
 vertices=[(sign*.13,-.01,1.08),(sign*.32,.01,1.2),(sign*.14,-.12,1.15),(sign*.13,.025,1.08),(sign*.32,.035,1.2),(sign*.14,-.09,1.15)]
 data=bpy.data.meshes.new('Pointed ear');data.from_pydata(vertices,[],[(0,1,2),(3,5,4),(0,3,4,1),(1,4,5,2),(2,5,3,0)]);obj=bpy.data.objects.new('Goblin ear',data);bpy.context.collection.objects.link(obj);obj.parent=bpy.data.objects['head'];obj.matrix_parent_inverse=obj.parent.matrix_world.inverted();data.materials.append(leaf)
bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=6,location=(0,-.19,1.06));obj=bpy.context.object;obj.name='Goblin nose';obj.scale=(.065,.105,.055);obj.parent=bpy.data.objects['head'];obj.matrix_parent_inverse=obj.parent.matrix_world.inverted();obj.data.materials.append(leaf)
meshes=[o for o in bpy.data.objects if o.type=='MESH'];groups={'Torso':[],'Head':[],'ArmL':[],'ArmR':[],'LegL':[],'LegR':[]}
for o in meshes:
 ancestor=o.parent
 while ancestor and ancestor.name not in names:ancestor=ancestor.parent
 bone=ancestor.name if ancestor else 'torso';group='Head' if bone=='head' else 'ArmL' if bone=='arm-l' else 'ArmR' if bone in ['arm-r','fore-r'] else 'LegL' if bone in ['thigh-l','shin-l'] else 'LegR' if bone in ['thigh-r','shin-r'] else 'Torso';groups[group].append(o)
 world=o.matrix_world.copy();o.parent=None;o.matrix_world=world;bpy.context.view_layer.objects.active=o;bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.ops.object.transform_apply(location=True,rotation=True,scale=True)
 
 if len(o.data.polygons)>120:
  reduce=o.modifiers.new('Prototype LOD','DECIMATE');reduce.ratio=.3;bpy.ops.object.modifier_apply(modifier=reduce.name)
 vg=o.vertex_groups.new(name=bone);vg.add(list(range(len(o.data.vertices))),1,'REPLACE');mod=o.modifiers.new('Skeleton','ARMATURE');mod.object=rig;o.parent=rig;o.name=group+'_'+o.name
# Clear faction identification: red cloth, retain green skin and existing texture.
for material in bpy.data.materials:
 if any(x in material.name.lower() for x in ['cloth','leather','hood']):
  material.use_nodes=True;node=material.node_tree.nodes.get('Principled BSDF')
  if node:node.inputs['Base Color'].default_value=(.48,.035,.02,1)
red=bpy.data.materials.new('Red faction cloth');red.diffuse_color=(.55,.03,.02,1);red.use_nodes=True;shader=red.node_tree.nodes.get('Principled BSDF');shader.inputs['Base Color'].default_value=(.55,.03,.02,1);shader.inputs['Roughness'].default_value=.9
for obj in meshes:
 if 'strap' in obj.name or 'hair' in obj.name or 'belt' in obj.name:obj.data.materials.clear();obj.data.materials.append(red)
scene=bpy.context.scene;scene.render.fps=24
for clip,end in [('Idle',48),('Walk',24),('Attack',24)]:
 action=bpy.data.actions.new(clip);rig.animation_data_create();rig.animation_data.action=action
 for f in range(1,end+2,4):
  t=(f-1)/end*math.tau
  for pb in rig.pose.bones:
   pb.rotation_mode='XYZ';pb.rotation_euler=(0,0,0)
   if clip=='Walk':
    if pb.name in ['thigh-l','thigh-r','arm-l','arm-r']:pb.rotation_euler.x=math.sin(t)*(.42 if 'thigh' in pb.name else .3)*(1 if pb.name.endswith('-l') else -1)
    if 'shin' in pb.name:pb.rotation_euler.x=max(0,math.sin(t+(math.pi if pb.name.endswith('-r') else 0)))*.5
   elif clip=='Idle':pb.rotation_euler.x=math.sin(t)*(.025 if pb.name=='torso' else .012)
   elif pb.name in ['arm-r','fore-r']:pb.rotation_euler.x=-.7-.65*math.sin(t)
   pb.keyframe_insert(data_path='rotation_euler',frame=f)
 track=rig.animation_data.nla_tracks.new();track.name=clip;track.strips.new(clip,1,action);rig.animation_data.action=None
for track in rig.animation_data.nla_tracks:track.mute=True
for pb in rig.pose.bones:pb.rotation_euler=(0,0,0)
scene.frame_set(1);bpy.context.view_layer.update();bpy.ops.object.select_all(action='DESELECT');rig.select_set(True)
for o in meshes:o.select_set(True)
for track in rig.animation_data.nla_tracks:track.mute=False
bpy.ops.export_scene.gltf(filepath=str(out/'red-goblin.glb'),export_format='GLB',use_selection=True,export_skins=True,export_animations=True,export_animation_mode='NLA_TRACKS',export_force_sampling=True,export_frame_range=False,export_extras=True)
for track in rig.animation_data.nla_tracks:track.mute=True
rig.animation_data.action=None
for pb in rig.pose.bones:pb.rotation_euler=(0,0,0)
scene.frame_set(1);bpy.context.view_layer.update()
manifest=[];game=lambda v:[v.x,v.z,-v.y]
for group,items in groups.items():
 points=[o.matrix_world@v.co for o in items for v in o.data.vertices];lo=Vector(tuple(min(v[i] for v in points) for i in range(3)));hi=Vector(tuple(max(v[i] for v in points) for i in range(3)));centre=(lo+hi)/2;temp=[]
 for o in items:
  copy=o.copy();copy.data=o.data.copy();bpy.context.collection.objects.link(copy);copy.modifiers.clear();copy.parent=None;copy.matrix_world=o.matrix_world.copy();copy.location-=centre;temp.append(copy)
 bpy.ops.object.select_all(action='DESELECT')
 for o in temp:o.select_set(True)
 bpy.ops.export_scene.gltf(filepath=str(out/('goblin-'+group+'.glb')),export_format='GLB',use_selection=True,export_skins=False,export_animations=False)
 for o in temp:bpy.data.objects.remove(o,do_unlink=True)
 size=hi-lo;bone={'Torso':'torso','Head':'head','ArmL':'arm-l','ArmR':'arm-r','LegL':'thigh-l','LegR':'thigh-r'}[group]
 manifest.append({'name':group,'bone':bone,'centre':game(centre),'size':[max(.08,size.x),max(.08,size.z),max(.08,size.y)],'pivot':game(bones[bone].head) if False else game(rigdata.bones[bone].head_local)})
# Combine the living character to reduce skinned draw calls; fragment exports stay separate.
bpy.ops.object.select_all(action='DESELECT')
for o in meshes:o.select_set(True)
bpy.context.view_layer.objects.active=meshes[0];bpy.ops.object.join();alive=bpy.context.object;alive.name='RedGoblinBody'
rig.select_set(True)
for track in rig.animation_data.nla_tracks:track.mute=False
bpy.ops.export_scene.gltf(filepath=str(out/'red-goblin.glb'),export_format='GLB',use_selection=True,export_skins=True,export_animations=True,export_animation_mode='NLA_TRACKS',export_force_sampling=True,export_frame_range=False)
for track in rig.animation_data.nla_tracks:track.mute=True
(out/'goblin-parts.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8');bpy.ops.wm.save_as_mainfile(filepath=str(out/'RedGoblin.blend'))
print('GOBLIN_READY',flush=True)
