import bpy,math,json
from pathlib import Path
from mathutils import Vector
OUT=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/defence-models')
names=['Sentry','Repeater','Mortar','Mine','SupplyCache'];work=bpy.data.scenes['Defence Model Workshop'];bpy.context.window.scene=work
for name in names:
 col=bpy.data.collections['DEFENCE_'+name];col.hide_render=False;rig=next(o for o in col.objects if o.type=='ARMATURE');rig.animation_data.action=bpy.data.actions[name+'_Idle'];rig.animation_data.use_nla=False
 for b in rig.pose.bones:b.location=(0,0,0);b.rotation_quaternion=(1,0,0,0);b.scale=(1,1,1)
work.frame_set(1);bpy.context.view_layer.update()
for name in names:
 col=bpy.data.collections['DEFENCE_'+name];s=bpy.data.scenes.get(name+' inspection') or bpy.data.scenes.new(name+' inspection');s.collection.children.link(col) if col not in list(s.collection.children) else None;s.world=work.world;s.render.engine='BLENDER_EEVEE';s.render.fps=30;s.frame_end=121;s.render.resolution_x=1000;s.render.resolution_y=1000;s.render.resolution_percentage=100;s.view_settings.view_transform='AgX';s.view_settings.look='AgX - Medium High Contrast';s.view_settings.exposure=-.25
 pts=[o.matrix_world@Vector(c) for o in col.objects if o.type=='MESH' for c in o.bound_box];lo=Vector(tuple(min(p[i] for p in pts) for i in range(3)));hi=Vector(tuple(max(p[i] for p in pts) for i in range(3)));centre=(lo+hi)/2;span=max(hi-lo)
 cam=bpy.data.objects.new(name+' inspection camera',bpy.data.cameras.new(name+' inspection lens'));s.collection.objects.link(cam);cam.location=centre+Vector((1.35,-2.1,1.15))*span;cam.rotation_euler=(centre-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=span*1.4;s.camera=cam
 for key,d,p,c in [('key',(1,-1.6,2.4),700,(1,.85,.65)),('fill',(-1.5,-.5,1.2),450,(.65,.76,1)),('rim',(.4,1.5,2),800,(1,.88,.65))]:
  lamp=bpy.data.objects.new(name+' '+key,bpy.data.lights.new(name+' '+key,'AREA'));s.collection.objects.link(lamp);lamp.location=centre+Vector(d)*span;lamp.rotation_euler=(centre-lamp.location).to_track_quat('-Z','Y').to_euler();lamp.data.energy=p*span*span/4;lamp.data.size=span*1.6;lamp.data.color=c
 floor=bpy.data.objects['Defence studio floor'].copy();floor.data=floor.data.copy();floor.name=name+' studio floor';floor.location.z=-.015;s.collection.objects.link(floor)
 rig=next(o for o in col.objects if o.type=='ARMATURE');rig['clip_guide']='Action Editor: choose '+name+'_Deploy, '+name+'_Idle and the other named clips. Rigid weights keep attached details together.'
gallery=bpy.data.scenes.get('Defence Collection') or bpy.data.scenes.new('Defence Collection');bpy.context.window.scene=gallery
for o in list(gallery.objects):bpy.data.objects.remove(o,do_unlink=True)
for name,p in zip(names,[(-3.7,0,0),(0,0,0),(3.7,0,0),(-1.6,-3,0),(1.5,-3,0)]):
 o=bpy.data.objects.new(name+' gallery instance',None);gallery.collection.objects.link(o);o.instance_type='COLLECTION';o.instance_collection=bpy.data.collections['DEFENCE_'+name];o.location=p
gallery.world=work.world;gallery.render.engine='BLENDER_EEVEE';gallery.render.fps=30;gallery.render.resolution_x=1600;gallery.render.resolution_y=1000;gallery.render.resolution_percentage=100;gallery.view_settings.view_transform='AgX';gallery.view_settings.look='AgX - Medium High Contrast';gallery.view_settings.exposure=-.25
floor=bpy.data.objects['Defence studio floor'].copy();floor.data=floor.data.copy();floor.name='Gallery floor';floor.location.z=-.015;gallery.collection.objects.link(floor)
cam=bpy.data.objects.new('Defence collection camera',bpy.data.cameras.new('Defence collection camera'));gallery.collection.objects.link(cam);gallery.camera=cam;cam.location=(10,-17,11);centre=Vector((0,-.2,1));cam.rotation_euler=(centre-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=12.6
for name,p,power,color in [('Collection key',(2,-6,10),2800,(1,.85,.65)),('Collection fill',(-7,-2,6),2000,(.65,.76,1)),('Collection rim',(3,6,9),3200,(1,.88,.65))]:
 lamp=bpy.data.objects.new(name,bpy.data.lights.new(name,'AREA'));gallery.collection.objects.link(lamp);lamp.location=p;lamp.rotation_euler=(centre-lamp.location).to_track_quat('-Z','Y').to_euler();lamp.data.energy=power;lamp.data.size=8;lamp.data.color=color
gallery.render.filepath=str(OUT/'DefenceCollection-preview.png');gallery.frame_set(1)
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA'
result={'gallery':gallery.name,'inspectionScenes':[name+' inspection' for name in names]}
