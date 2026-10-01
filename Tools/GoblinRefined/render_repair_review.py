import bpy,sys,json
from pathlib import Path
from mathutils import Vector
out=Path(sys.argv[sys.argv.index('--')+1]);bpy.ops.wm.open_mainfile(filepath=str(out/'GoblinScraper_RefinedParts.blend'));scene=bpy.context.scene;catalog=json.loads((out/'parts-catalog.json').read_text());file='scrap_catapult_basket'
for p in catalog['parts']:bpy.data.collections[p['label']].hide_render=Path(p['file']).stem!=file
root=bpy.data.objects[file];root.location=(0,0,0);rig=bpy.data.objects[file+'_rig'];rig.animation_data.action=bpy.data.actions[file+'__Launch']
cam=bpy.data.objects['Part Preview Camera'];scene.camera=cam;center=Vector((0,.25,.10));cam.location=center+Vector((2.5,3.6,2.4));cam.rotation_euler=(center-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.ortho_scale=2.9;bpy.data.objects['Studio Ground'].location.z=-.55;scene.render.resolution_x=360;scene.render.resolution_y=360
for i in range(12):
 scene.frame_set(1+round(40*i/12));scene.render.filepath=str(out/'previews'/'repair_motion'/f'{file}_{i:02d}.png');bpy.ops.render.render(write_still=True)
print('REPAIR_PREVIEWS_COMPLETE')
