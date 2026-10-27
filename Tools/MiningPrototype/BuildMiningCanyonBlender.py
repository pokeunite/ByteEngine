"""Build an editable assembly scene from the same placement plan used by ByteEngine."""
import bpy,json,math
from pathlib import Path
from mathutils import Vector
WORK=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine')
PROJECT=Path('C:/Users/codex/Documents/palclone')
plan=json.loads((WORK/'Docs/Concepts/AlienMining/RockKit/level-layout.json').read_text())
s=bpy.data.scenes.get('Mining Canyon Layout') or bpy.data.scenes.new('Mining Canyon Layout')
bpy.context.window.scene=s
for o in list(s.objects):bpy.data.objects.remove(o,do_unlink=True)
for c in bpy.data.collections:
 if c.name.startswith('ROCKKIT_'):
  root=next(o for o in c.objects if o.get('rockkit_id'))
  c.instance_offset=root.location
  for o in c.objects:o.hide_render=False
for item in plan['items']:
 o=bpy.data.objects.new(item['name'],None);s.collection.objects.link(o);o.instance_type='COLLECTION';o.instance_collection=bpy.data.collections['ROCKKIT_'+item['id']]
 o.location=(item['x'],-item['z'],item['y']);o.scale=(item['scale'][0],item['scale'][2],item['scale'][1]);o.rotation_euler.z=math.radians(item['yaw']);o['asset_id']=item['id'];o['game_group']=item['group'];o['collision']=item['collision']
# Broad floor uses the accepted texture, with the same 12 x 14 metre tiling.
me=bpy.data.meshes.new('Sand floor');me.from_pydata([(-100,-65,-.04),(120,-65,-.04),(120,260,-.04),(-100,260,-.04)],[],[(0,1,2,3)]);me.update();floor=bpy.data.objects.new('Approved sand floor',me);s.collection.objects.link(floor)
uv=me.uv_layers.new()
for loop in me.loops:
 v=me.vertices[loop.vertex_index].co;uv.data[loop.index].uv=(v.x/12,v.y/14)
m=bpy.data.materials.get('Approved orange sand') or bpy.data.materials.new('Approved orange sand');m.use_nodes=True;n=m.node_tree.nodes;n.clear();out=n.new('ShaderNodeOutputMaterial');bs=n.new('ShaderNodeBsdfPrincipled');bs.inputs['Roughness'].default_value=.95;tex=n.new('ShaderNodeTexImage');tex.image=bpy.data.images.load(str(PROJECT/'Assets/Environment/MiningSandbox/OrangeSand-BaseColor.png'),check_existing=True);tex.image.pack();m.node_tree.links.new(tex.outputs['Color'],bs.inputs['Base Color']);m.node_tree.links.new(bs.outputs[0],out.inputs[0]);me.materials.append(m)
for name,file,pos in [('Resource drill','MiningDrill-Massive.glb',(0,3,0)),('Ammo press','AmmoPress.glb',(-9,-12,0)),('Refinery','Refinery.glb',(9,-12,0)),('Company terminal','CompanyTerminal.glb',(0,-13,0))]:
 before=set(s.objects);bpy.ops.import_scene.gltf(filepath=str(PROJECT/'Assets/Models/Mining'/file));imported=set(s.objects)-before
 root=bpy.data.objects.new(name+' assembly',None);s.collection.objects.link(root)
 for o in imported:
  if o.parent not in imported:o.parent=root
 root.location=pos
world=bpy.data.worlds.get('Mining canyon sunset') or bpy.data.worlds.new('Mining canyon sunset');s.world=world;world.use_nodes=True;n=world.node_tree.nodes;n.clear();out=n.new('ShaderNodeOutputWorld');bg=n.new('ShaderNodeBackground');bg.inputs['Strength'].default_value=.6;env=n.new('ShaderNodeTexEnvironment');env.image=bpy.data.images.load(str(PROJECT/'Assets/Environment/MiningSandbox/RingedPlanet-Sunset.png'),check_existing=True);env.image.pack();world.node_tree.links.new(env.outputs['Color'],bg.inputs['Color']);world.node_tree.links.new(bg.outputs[0],out.inputs[0])
sun=bpy.data.objects.new('Warm sunset key',bpy.data.lights.new('Warm sunset key','SUN'));s.collection.objects.link(sun);sun.data.energy=2.2;sun.data.color=(1,.66,.38);sun.rotation_euler=(math.radians(30),math.radians(-20),math.radians(-35));sun.data.angle=.12
fill=bpy.data.objects.new('Cool sky fill',bpy.data.lights.new('Cool sky fill','SUN'));s.collection.objects.link(fill);fill.data.energy=.8;fill.data.color=(.52,.58,1);fill.rotation_euler=(math.radians(15),math.radians(35),math.radians(145))
cam=bpy.data.objects.new('Canyon overview',bpy.data.cameras.new('Canyon overview'));s.collection.objects.link(cam);s.camera=cam;cam.location=(52,-48,42);target=Vector((8,42,0));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.lens=29;cam.data.clip_end=1000
s.render.engine='BLENDER_EEVEE';s.render.resolution_x=1600;s.render.resolution_y=1000;s.render.resolution_percentage=100;s.view_settings.view_transform='AgX';s.render.image_settings.file_format='PNG';s.render.filepath=str(WORK/'artifacts/mining-rock-kit/Blender-canyon-preview.png')
for screen in bpy.data.screens:
 for area in screen.areas:
  if area.type=='VIEW_3D':
   area.spaces.active.region_3d.view_perspective='CAMERA';area.spaces.active.clip_end=1000
result={'scene':s.name,'instances':len(plan['items']),'objects':len(s.objects),'editable_assets':16}
