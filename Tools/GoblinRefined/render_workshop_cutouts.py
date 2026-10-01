import bpy,json,pathlib
from mathutils import Vector
bpy.ops.wm.open_mainfile(filepath=r'C:\Users\codex\Downloads\parts\refined\GoblinScraper_RefinedParts.blend')
scene=bpy.context.scene; camera=scene.camera
catalog=json.loads(pathlib.Path(r'C:\Users\codex\Downloads\parts\refined\parts-catalog.json').read_text())
out=pathlib.Path(r'C:\Users\codex\Documents\Goblin Scraper\Assets\GarageUI\cutouts');out.mkdir(exist_ok=True)
scene.render.engine='BLENDER_EEVEE';scene.render.resolution_x=256;scene.render.resolution_y=192;scene.render.resolution_percentage=100
scene.render.film_transparent=True;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA'
for obj in bpy.data.collections['_Preview Studio'].objects:
    if obj.type not in {'LIGHT','CAMERA'}:obj.hide_render=True
scene.frame_set(1)
for item in catalog['parts']:
    collection=bpy.data.collections[item['label']]
    for other in bpy.data.collections:
        if other.name!='_Preview Studio':other.hide_render=other!=collection
    root=next(o for o in collection.objects if o.type=='EMPTY' and o.name.startswith(item['file'].removesuffix('.glb')))
    root.location=(0,0,0)
    bpy.context.view_layer.update()
    points=[obj.matrix_world@Vector(corner) for obj in collection.objects if obj.type=='MESH' for corner in obj.bound_box]
    low=Vector(tuple(min(p[i] for p in points) for i in range(3)));high=Vector(tuple(max(p[i] for p in points) for i in range(3)))
    center=(low+high)/2;size=max(high-low)
    camera.location=center+Vector((1.4,1.8,1.25))*max(size,1)
    camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler();camera.data.ortho_scale=max(size*1.35,.7)
    scene.render.filepath=str(out/item['file'].replace('.glb','.png'))
    bpy.ops.render.render(write_still=True)
    print('CUTOUT',item['file'],flush=True)
