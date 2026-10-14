import bpy,json
from pathlib import Path
OUT=Path('C:/Users/codex/Downloads/byte engine files/Milk Depot Environment')
ASSETS=Path('C:/Users/codex/Documents/karlson demo/Assets/Environment/MilkDepot')
bpy.ops.wm.open_mainfile(filepath=str(OUT/'MilkDepot_Environment.blend'))
library=bpy.data.scenes['01 - Modular asset library'];level=bpy.data.scenes['02 - Milk Depot assembled reference']
bpy.context.window.scene=library
for o in list(library.objects):
    if not o.get('asset_id'):continue
    position=o.location.copy();o.location=(0,0,0)
    bpy.ops.object.select_all(action='DESELECT');o.select_set(True);bpy.context.view_layer.objects.active=o
    bpy.ops.export_scene.gltf(filepath=str(ASSETS/'Models'/f'{o["asset_id"]}.glb'),export_format='GLB',use_selection=True,use_active_scene=True,
        export_animations=False,export_extras=True,export_yup=True)
    o.location=position
bpy.context.window.scene=level;bpy.ops.object.select_all(action='DESELECT')
for o in level.objects:
    if o.type=='MESH':o.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(ASSETS/'MilkDepot_Assembled.glb'),export_format='GLB',use_selection=True,use_active_scene=True,
    export_animations=False,export_extras=True,export_yup=True)
print('CLEAN EXPORT PASS 46 individual assets and one assembled scene')
