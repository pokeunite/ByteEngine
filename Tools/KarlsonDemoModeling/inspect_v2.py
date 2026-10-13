import bpy
result={'override':str(bpy.context.view_layer.material_override),'scene_camera':bpy.context.scene.camera.name,'world':str(bpy.context.scene.world),'lights':[(o.name,o.data.energy) for o in bpy.context.scene.objects if o.type=='LIGHT'],'nodes':[(m.name,[ (link.from_node.name,link.to_node.name) for link in m.node_tree.links]) for m in bpy.data.materials if m.name.startswith('Grip')]}
