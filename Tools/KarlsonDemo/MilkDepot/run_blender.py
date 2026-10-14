import bpy, sys, json
from pathlib import Path
sys.path.insert(0, 'C:/Users/codex/AppData/Roaming/Blender Foundation/Blender/4.5/scripts/addons')
bpy.ops.preferences.addon_enable(module='vehicle_survivors_blender_mcp')
bpy.context.preferences.addons['vehicle_survivors_blender_mcp'].preferences.allow_python = True
from vehicle_survivors_blender_mcp import runtime
print('MCP_INITIAL_SCENE', json.dumps(runtime._dispatch('get_scene_info', {})), flush=True)
path = Path(__file__).with_name('build_environment.py')
response = runtime._dispatch('execute_python', {'code': 'exec(compile(' + repr(path.read_text()) + ', ' + repr(str(path)) + ', "exec"))'})
print('MCP_BUILD_RESULT', json.dumps(response), flush=True)
