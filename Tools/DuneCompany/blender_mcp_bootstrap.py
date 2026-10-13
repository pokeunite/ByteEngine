"""Start an isolated, unsaved Blender scene and the existing local MCP add-on."""
import sys,bpy
from pathlib import Path
addons=Path('C:/Users/codex/AppData/Roaming/Blender Foundation/Blender/4.5/scripts/addons')
sys.path.insert(0,str(addons))
import vehicle_survivors_blender_mcp as addon
# Enable only for this user-authorized modelling session; do not change saved preferences.
prefs=bpy.context.preferences.addons.get('vehicle_survivors_blender_mcp')
if prefs is None:
    bpy.ops.preferences.addon_enable(module='vehicle_survivors_blender_mcp')
prefs=bpy.context.preferences.addons['vehicle_survivors_blender_mcp'].preferences
prefs.allow_python=True
addon.runtime.start_server('127.0.0.1',19876,'')
bpy.context.scene.name='Dune Company Race Kit'
