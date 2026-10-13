import bpy,bmesh
C=bpy.app.driver_namespace['cyber'];scene=bpy.context.scene;g=C['guns'][2];g.rest();copies=[]
for original in g.meshes:
 obj=original.copy();obj.data=original.data.copy();scene.collection.objects.link(obj);obj.parent=None;obj.matrix_world=original.matrix_world.copy();names={group.index:group.name for group in obj.vertex_groups};remove=[]
 for v in obj.data.vertices:
  if any(group.weight>.01 and (names[group.group].startswith(('Right','Left')) or names[group.group] in ['ReplacementMagazine','Shell']) for group in v.groups):remove.append(v.index)
 bm=bmesh.new();bm.from_mesh(obj.data);bm.verts.ensure_lookup_table();bmesh.ops.delete(bm,geom=[bm.verts[i] for i in remove],context='VERTS');bm.to_mesh(obj.data);bm.free()
 if len(obj.data.polygons)==0:bpy.data.objects.remove(obj,do_unlink=True);continue
 bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
 for mod in list(obj.modifiers):
  if mod.type=='ARMATURE':bpy.ops.object.modifier_apply(modifier=mod.name)
 obj.vertex_groups.clear();copies.append(obj)
bpy.ops.object.select_all(action='DESELECT')
for obj in copies:obj.select_set(True)
bpy.context.view_layer.objects.active=copies[0]
bpy.ops.export_scene.gltf(filepath=str(C['OUT']/('Cyberpunk_'+g.name+'_Prop.glb')),export_format='GLB',use_selection=True,use_active_scene=True,export_animations=False,export_skins=False)
for obj in copies:bpy.data.objects.remove(obj,do_unlink=True)
result={'static_prop':g.name}
