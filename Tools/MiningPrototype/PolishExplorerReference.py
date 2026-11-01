import bpy,bmesh,math
from mathutils import Vector
rig=bpy.data.objects['PolygonSyntyCharacter'];body=bpy.data.objects['HazardPay Explorer - skinned mesh']
# Remove the radial glass topology from the joined mesh, retain all armor/weights.
glass_slots={i for i,m in enumerate(body.data.materials) if m and m.name.startswith('Explorer / Visor')}
bm=bmesh.new();bm.from_mesh(body.data);bmesh.ops.delete(bm,geom=[f for f in bm.faces if f.material_index in glass_slots],context='FACES');bm.to_mesh(body.data);bm.free()
bm=bmesh.new();bmesh.ops.create_icosphere(bm,subdivisions=3,radius=1)
bmesh.ops.delete(bm,geom=[v for v in bm.verts if v.co.z<.12],context='VERTS')
for v in bm.verts:v.co=Vector((v.co.x*12.7,166+v.co.y*13.8,9.7+v.co.z*5.7))
data=bpy.data.meshes.new('Nonradial geodesic visor');bm.to_mesh(data);bm.free();data.materials.append(bpy.data.materials['Explorer / Visor teal'])
glass=bpy.data.objects.new('Faceted teal glazing',data);body.users_collection[0].objects.link(glass);glass.parent=rig
group=glass.vertex_groups.new(name='Head');group.add(list(range(len(data.vertices))),1,'REPLACE')
bpy.ops.object.select_all(action='DESELECT');body.select_set(True);glass.select_set(True);bpy.context.view_layer.objects.active=body;bpy.ops.object.join()
for name,color in [('Industrial yellow',(.93,.51,.008)),('Yellow edge',(1,.65,.025)),('Visor teal',(.008,.07,.09))]:
 m=bpy.data.materials['Explorer / '+name];m.diffuse_color=(*color,1);p=m.node_tree.nodes.get('Principled BSDF');p.inputs['Base Color'].default_value=(*color,1)
 if name=='Visor teal':p.inputs['Roughness'].default_value=.29;p.inputs['Metallic'].default_value=.32
scene=bpy.context.scene;scene.view_settings.view_transform='AgX';scene.view_settings.look='AgX - Medium High Contrast';scene.render.resolution_x=1400;scene.render.resolution_y=1400
body['reference_rebuild']=2
scene.render.filepath='C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/explorer-reference-v2-polished.png';bpy.ops.render.render(write_still=True)
result={'render':scene.render.filepath,'vertices':len(body.data.vertices),'bones':len(rig.data.bones)}
