import bpy,math,json
from pathlib import Path
from mathutils import Vector
W=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine');OUT=W/'artifacts/camp-decorations';library=bpy.data.scenes['Hazard Pay Decoration Library'];review=bpy.data.scenes['Hazard Pay Decorated Camp REVIEW'];bpy.context.window.scene=library
assets=sorted([c for c in bpy.data.collections if c.name.startswith('HPD ') and c.get('asset_name')],key=lambda c:c.name);report=[]
for col in assets:
 library.collection.children.link(col);bpy.ops.object.select_all(action='DESELECT')
 for o in col.objects:o.select_set(True)
 bpy.context.view_layer.objects.active=next(iter(col.objects));dest=OUT/(col['asset_name']+'.glb');bpy.ops.export_scene.gltf(filepath=str(dest),export_format='GLB',use_selection=True,export_apply=True,export_animations=False)
 library.collection.children.unlink(col);report.append({'asset':col['asset_name'],'file':str(dest),'parts':len(col.objects)})
library.world=bpy.data.worlds.new('Decoration neutral studio');library.world.use_nodes=True;library.world.node_tree.nodes['Background'].inputs[0].default_value=(.11,.14,.19,1);library.world.node_tree.nodes['Background'].inputs[1].default_value=.65;library.render.engine='BLENDER_EEVEE';library.render.resolution_x=1600;library.render.resolution_y=1100;library.render.resolution_percentage=90;library.view_settings.view_transform='AgX';library.view_settings.look='AgX - Medium High Contrast'
def aim(o,target):o.rotation_euler=(Vector(target)-o.location).to_track_quat('-Z','Y').to_euler()
for p,power,size in [((0,-6,16),2400,15),((20,6,18),2700,14)]:
 bpy.ops.object.light_add(type='AREA',location=p);o=bpy.context.object;o.data.energy=power;o.data.size=size;aim(o,(10,6,0))
bpy.ops.mesh.primitive_plane_add(size=1,location=(10,6,-.07));floor=bpy.context.object;floor.name='Decoration gallery floor';floor.scale=(29,22,1);floor.data.materials.append(bpy.data.materials['HPD charcoal'])
labels=['Tripod work light','Spare auger rack','Pipe storage rack','Gas cylinder rack','Shade canopy','Cargo crate','Supply cases','Cargo pallet','Tool trolley','Cable reel','Generator','Comms mast','Power cable','Cable ramp','Floor panel']
for i,text in enumerate(labels):
 bpy.ops.object.text_add(location=((i%5)*5-1.7,(i//5)*6-1.55,.03));o=bpy.context.object;o.name='Gallery label '+text;o.data.body=f'{i+1:02}  {text}';o.data.size=.24;o.data.extrude=0;o.data.materials.append(bpy.data.materials['HPD gold edge'])
bpy.ops.object.camera_add(location=(28,-29,29));cam=bpy.context.object;aim(cam,(10,6,1));cam.data.type='ORTHO';cam.data.ortho_scale=34;library.camera=cam;library.render.filepath=str(OUT/'Decoration-Library.png');bpy.ops.render.render(write_still=True)
bpy.context.window.scene=review;review.camera=bpy.data.objects['Workshop detail'];review.render.filepath=str(OUT/'Workshop-Detail.png');bpy.ops.render.render(write_still=True)
review.camera=bpy.data.objects['Camp overview'];review.render.filepath=str(OUT/'Decorated-Camp-Overview.png')
(OUT/'export-manifest.json').write_text(json.dumps(report,indent=2));print(json.dumps({'assets':len(report),'scene':review.name,'output':str(OUT)}))
