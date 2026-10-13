import bpy
result={'guns':[(g.name,len(g.parts),len(g.bones)) for g in bpy.app.driver_namespace.get('cyber',{}).get('guns',[])],'scene_objects':len(bpy.context.scene.objects)}
