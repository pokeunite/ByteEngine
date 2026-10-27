import bpy,json,math
from pathlib import Path
from mathutils import Vector
OUT=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/spider-models');work=bpy.data.scenes['Spider Modelling Workshop']
names=['PlayerHunter','DrillBreaker']
for name in names:
 col=bpy.data.collections['SPIDER_'+name];col.hide_render=False
 s=bpy.data.scenes.get(name+' spider inspection') or bpy.data.scenes.new(name+' spider inspection')
 if col not in list(s.collection.children):s.collection.children.link(col)
 s.world=work.world;s.render.engine='BLENDER_EEVEE';s.render.resolution_x=1200;s.render.resolution_y=1200;s.render.resolution_percentage=100;s.view_settings.view_transform='AgX';s.view_settings.look='AgX - Medium High Contrast';s.view_settings.exposure=-.65
 cam=bpy.data.objects.new(name+' spider camera',bpy.data.cameras.new(name+' spider lens'));s.collection.objects.link(cam);cam.location=(0,-4.4,10);cam.rotation_euler=(Vector((0,.1,.65))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=5.2;s.camera=cam
 for src in ['Spider key','Spider fill','Spider rim','Spider studio floor']:
  o=bpy.data.objects[src].copy();o.data=o.data.copy();o.name=name+' '+src;s.collection.objects.link(o)
g=bpy.data.scenes.get('Spider Pair') or bpy.data.scenes.new('Spider Pair');bpy.context.window.scene=g
for o in list(g.objects):bpy.data.objects.remove(o,do_unlink=True)
for name,x in [('PlayerHunter',-2.65),('DrillBreaker',2.65)]:
 o=bpy.data.objects.new(name+' gallery',None);g.collection.objects.link(o);o.instance_type='COLLECTION';o.instance_collection=bpy.data.collections['SPIDER_'+name];o.location=(x,0,0)
g.world=work.world;g.render.engine='BLENDER_EEVEE';g.render.resolution_x=1800;g.render.resolution_y=1000;g.render.resolution_percentage=100;g.view_settings.view_transform='AgX';g.view_settings.look='AgX - Medium High Contrast';g.view_settings.exposure=-.65
cam=bpy.data.objects.new('Spider Pair camera',bpy.data.cameras.new('Spider Pair lens'));g.collection.objects.link(cam);g.camera=cam;cam.location=(4,-9,13);target=Vector((0,0,.7));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=11.5
floor=bpy.data.objects['Spider studio floor'].copy();floor.data=floor.data.copy();floor.name='Spider pair floor';g.collection.objects.link(floor)
for name,p,power,color in [('Pair key',(2,-5,9),1800,(1,.9,.78)),('Pair fill',(-6,-1,7),1400,(.73,.79,1)),('Pair rim',(3,6,7),1900,(1,.9,.8))]:
 o=bpy.data.objects.new(name,bpy.data.lights.new(name,'AREA'));g.collection.objects.link(o);o.location=p;o.rotation_euler=(target-o.location).to_track_quat('-Z','Y').to_euler();o.data.energy=power;o.data.size=7;o.data.color=color
g.render.filepath=str(OUT/'SpiderPair-preview.png')
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.shading.color_type='MATERIAL'
result={'scene':g.name,'inspectionScenes':[name+' spider inspection' for name in names]}
