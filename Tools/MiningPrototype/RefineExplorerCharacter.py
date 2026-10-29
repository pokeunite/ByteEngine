import bpy, math
from mathutils import Vector
rig=bpy.data.objects['PolygonSyntyCharacter'];body=bpy.data.objects['HazardPay Explorer - skinned mesh']
# Geometry correction pass, scoped to connected armor islands through group/material selection.
yellow=body.data.materials.find('Explorer / Industrial yellow')
chest_group=body.vertex_groups.get('Spine_02').index
for v in body.data.vertices:
 if any(g.group==chest_group and g.weight>.99 for g in v.groups) and 9.4<v.co.z<15.1 and 107<v.co.y<137 and abs(v.co.x)>8:
  v.co.y=108+(v.co.y-108)*1.16
# Add wraparound yellow thigh armor rather than rectangular patches on white trousers.
for name in ['Industrial yellow','Yellow edge','Yellow shadow']:
 m=bpy.data.materials['Explorer / '+name];p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Metallic'].default_value=.05
m=bpy.data.materials['Explorer / Steel'];m.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value=(.13,.17,.20,1)
for name in ['Visor teal','Visor facet']:
 p=bpy.data.materials['Explorer / '+name].node_tree.nodes.get('Principled BSDF');p.inputs['Roughness'].default_value=.29;p.inputs['Metallic'].default_value=.2
# Selected connected suit patches become yellow to give the limbs the reference's armor coverage.
for poly in body.data.polygons:
 center=sum((body.data.vertices[i].co for i in poly.vertices),Vector())/len(poly.vertices)
 if 55<center.y<83 and 4<abs(center.x)<20 and center.z<5:
  poly.material_index=yellow
scene=bpy.context.scene;scene.world.use_nodes=True
world=scene.world.node_tree.nodes.get('Background');world.inputs['Color'].default_value=(.28,.28,.28,1);world.inputs['Strength'].default_value=.45
scene.view_settings.exposure=-.25
camera=bpy.data.objects['Explorer preview camera'];camera.location=(0,-6,.98);camera.rotation_euler=(Vector((0,0,.93))-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=2.32
scene.render.filepath='C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/explorer-front-refined.png';bpy.ops.render.render(write_still=True)
result={'render':scene.render.filepath,'armature_unchanged':len(rig.data.bones)==51}
