"""Background product renders for UI; does not touch the user's open Blender file."""
import bpy,math
from pathlib import Path
from mathutils import Vector
W=Path(__file__).resolve().parents[2];out=W/'artifacts/research-ui';out.mkdir(parents=True,exist_ok=True)
for name in ('SecurityDrone','FieldBarricade'):
 bpy.ops.wm.read_factory_settings(use_empty=True);s=bpy.context.scene;s.render.engine='BLENDER_EEVEE';s.render.resolution_x=512;s.render.resolution_y=320;s.render.resolution_percentage=100;s.render.film_transparent=True;s.view_settings.view_transform='AgX';s.view_settings.look='AgX - Medium High Contrast'
 bpy.ops.import_scene.gltf(filepath=str(Path('C:/Users/codex/Documents/palclone/Assets/Models/Mining/Research')/(name+'.glb')))
 for o in s.objects:
  if o.animation_data:o.animation_data.action=None
 s.frame_set(1);bpy.context.view_layer.update();points=[o.matrix_world@Vector(c) for o in s.objects if o.type=='MESH' for c in o.bound_box];lo=Vector(tuple(min(p[i] for p in points) for i in range(3)));hi=Vector(tuple(max(p[i] for p in points) for i in range(3)));center=(lo+hi)/2;span=max(hi-lo)
 # glTF import restores Blender Z-up; front is -Y.
 cam=bpy.data.objects.new('UI camera',bpy.data.cameras.new('UI camera'));s.collection.objects.link(cam);s.camera=cam;cam.location=center+Vector((1,-2.5,1.4))*span;cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=span*1.25
 s.world=bpy.data.worlds.new('UI studio');s.world.use_nodes=True;s.world.node_tree.nodes['Background'].inputs[1].default_value=.35
 for nameLight,offset,power in [('Key',(1,-2,3),1000),('Fill',(-2,-1,1),650),('Rim',(1,2,2),1100)]:
  light=bpy.data.objects.new(nameLight,bpy.data.lights.new(nameLight,'AREA'));s.collection.objects.link(light);light.location=center+Vector(offset)*span;light.rotation_euler=(center-light.location).to_track_quat('-Z','Y').to_euler();light.data.energy=power*span*span/4;light.data.size=span*1.6
 s.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
