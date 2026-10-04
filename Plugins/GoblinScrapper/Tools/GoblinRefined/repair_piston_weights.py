"""Repair the piston's elastic shaft weights without changing its shape or materials."""
import bpy,json,sys
from pathlib import Path
from mathutils import Vector
out=Path(sys.argv[sys.argv.index('--')+1]).resolve();out.mkdir(parents=True,exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=r'C:\Users\codex\Downloads\parts\refined\standard-flight-v3\Goblin_Standard_Flight_v3.blend')
rig=bpy.data.objects['goblin_piston_rig'];objects=[o for o in bpy.data.objects if 'goblin_piston' in o.name];col=rig.users_collection[0]
for track in rig.animation_data.nla_tracks:track.mute=True
rig.animation_data.action=None
for bone in rig.pose.bones:bone.location=(0,0,0);bone.rotation_quaternion=(1,0,0,0);bone.scale=(1,1,1)
bpy.context.scene.frame_set(1);bpy.context.view_layer.update()
mesh=bpy.data.objects['goblin_piston__Moving'];root=mesh.vertex_groups.get('Root') or mesh.vertex_groups.new(name='Root');moving=mesh.vertex_groups.get('Moving')
lo=-.5651179;hi=.048
for v in mesh.data.vertices:
 t=max(0,min(1,(v.co.y-lo)/(hi-lo)))
 root.add([v.index],1-t,'REPLACE');moving.add([v.index],t,'REPLACE')
# The lower accordion/shaft stretches between the fixed mounting foot and moving head.
# It must not be treated as one rigid mesh translating away from the mounting foot.
for o in bpy.data.objects:o.hide_render=o not in objects
for c in bpy.data.collections:c.hide_viewport=False;c.hide_render=False
for o in objects:o.hide_set(False);o.hide_viewport=False
bpy.ops.object.select_all(action='DESELECT')
for o in objects:o.select_set(True)
bpy.context.view_layer.objects.active=rig
for track in rig.animation_data.nla_tracks:track.mute=False
bpy.ops.export_scene.gltf(filepath=str(out/'goblin_piston.glb'),export_format='GLB',use_selection=True,export_yup=True,export_texcoords=True,export_normals=True,export_materials='EXPORT',export_skins=True,export_animations=True,export_animation_mode='NLA_TRACKS',export_force_sampling=True,export_frame_range=False,export_anim_single_armature=True,export_extras=True,export_cameras=False,export_lights=False)
for track in rig.animation_data.nla_tracks:track.mute=True
bpy.data.libraries.write(str(out/'Goblin_Piston_Repaired.blend'),{col},fake_user=True)
# Evaluate physical-driver-like translations; fixed lower vertices remain fixed, head moves .4 m.
def points(travel):
 rig.pose.bones['Moving'].location=(0,travel,0);bpy.context.view_layer.update();obj=mesh.evaluated_get(bpy.context.evaluated_depsgraph_get());return [v.co.copy() for v in obj.data.vertices]
a=points(0);b=points(.4);lower=[i for i,v in enumerate(mesh.data.vertices) if v.co.y<=lo+.001];upper=[i for i,v in enumerate(mesh.data.vertices) if v.co.y>=hi]
assert lower and upper
assert max((b[i]-a[i]).length for i in lower)<.002
assert min((b[i]-a[i]).length for i in upper)>.398
print('PASS elastic piston:',len(lower),'fixed anchor vertices;',len(upper),'moving head vertices; maximum travel .4m',flush=True)
# Top rigid head collider; spring/shaft visuals no longer collide as a floating full-length box.
verts=[v.co for v in mesh.data.vertices if v.co.y>=hi]
game=lambda v:(v.x,v.z,-v.y)
bounds=[[min(game(v)[i] for v in verts) for i in range(3)],[max(game(v)[i] for v in verts) for i in range(3)]]
(out/'piston-collider.json').write_text(json.dumps(bounds))
# Actual Blender renders at retracted and extended positions.
scene=bpy.context.scene;scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=640;scene.render.resolution_y=720;scene.render.resolution_percentage=100
scene.world.color=(.17,.17,.17)
bpy.ops.object.camera_add(location=(2,-2,1.5));cam=bpy.context.object;cam.rotation_euler=(Vector((0,.1,0))-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=1.9;scene.camera=cam
for loc,power in [((2,-3,4),700),((-2,1,3),500)]:
 bpy.ops.object.light_add(type='AREA',location=loc);lamp=bpy.context.object;lamp.data.energy=power;lamp.data.shape='DISK';lamp.data.size=3;lamp.rotation_euler=(Vector((0,0,0))-lamp.location).to_track_quat('-Z','Y').to_euler()
for travel,name in [(0,'retracted'),(.4,'extended')]:
 points(travel);scene.render.filepath=str(out/(name+'.png'));bpy.ops.render.render(write_still=True)
print('PISTON_REPAIR_COMPLETE',flush=True)
