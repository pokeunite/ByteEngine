import bpy
bpy.ops.preferences.addon_install(filepath=r'C:/Users/codex/Desktop/VehicleSurvivors_BlenderMCP_Addon.zip', overwrite=True)
bpy.ops.preferences.addon_enable(module='vehicle_survivors_blender_mcp')
p=bpy.context.preferences.addons['vehicle_survivors_blender_mcp'].preferences
p.allow_python=True
p.host='127.0.0.1'
p.port=9876
bpy.ops.vsbmcp.start_server()
