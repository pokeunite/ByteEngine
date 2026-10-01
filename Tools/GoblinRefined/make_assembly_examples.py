import bpy,math,sys,json
from pathlib import Path
from mathutils import Vector,Quaternion
OUT=Path(sys.argv[sys.argv.index('--')+1])
bpy.ops.wm.open_mainfile(filepath=str(OUT/'GoblinScraper_RefinedParts.blend'))
scene=bpy.context.scene
catalog=json.loads((OUT/'parts-catalog.json').read_text())
source_cols={Path(p['file']).stem:bpy.data.collections[p['label']] for p in catalog['parts']}
for col in source_cols.values(): col.hide_render=True; col.hide_viewport=True
studio=bpy.data.collections['_Preview Studio']
for obj in studio.objects:
    if obj.type=='FONT': obj.hide_render=True; obj.hide_viewport=True
camera=bpy.data.objects['Part Preview Camera']; ground=bpy.data.objects['Studio Ground']
ground.location.z=-.03
scene.camera=camera; scene.render.resolution_x=1280; scene.render.resolution_y=960


def clone(file,destination,pos,quat=None):
    source=source_cols[file]; mapping={}
    for old in source.objects:
        new=old.copy()
        if old.type=='ARMATURE': new.data=old.data.copy()
        destination.objects.link(new); mapping[old]=new
        new.hide_render=False; new.hide_viewport=False
    for old,new in mapping.items():
        new.parent=mapping.get(old.parent)
        if old.parent_type=='BONE': new.parent_type='BONE'; new.parent_bone=old.parent_bone
        if new.type=='MESH':
            for modifier in new.modifiers:
                if modifier.type=='ARMATURE': modifier.object=mapping.get(modifier.object,modifier.object)
    root=mapping[bpy.data.objects[file]]; root.location=pos
    if quat: root.rotation_mode='QUATERNION'; root.rotation_quaternion=quat
    root['assembly_example']=destination.name
    return root

def collection(name):
    col=bpy.data.collections.new(name); scene.collection.children.link(col); return col
buggy=collection('Example 1 - armored ram buggy')
clone('scrap_frame_2x1',buggy,(0,0,.75))
for yy in [-.75,.75]:
    clone('scrap_axle_2m',buggy,(0,yy,.60))
    for x in [-1.03,1.03]: clone('scrap_wheel_large',buggy,(x,yy,.60))
clone('scrap_engine_block',buggy,(0,-.56,1.19))
clone('scrap_cab_shell',buggy,(0,.47,1.34))
clone('scrap_ram_wedge',buggy,(0,1.38,.85))
for x in [-.69,.69]: clone('scrap_armor_plate',buggy,(x,0,1.17),Quaternion((0,1,0),math.pi/2 if x>0 else -math.pi/2))

tower=collection('Example 2 - six wheel hammer tower')
clone('scrap_frame_long',tower,(0,0,.75))
for yy in [-1.5,0,1.5]:
    clone('scrap_axle_2m',tower,(0,yy,.60))
    for x in [-1.03,1.03]: clone('scrap_wheel_large',tower,(x,yy,.60))
clone('scrap_engine_block',tower,(0,-1.1,1.19))
for x in [-.50,.50]:
    clone('scrap_beam_2m',tower,(x,-.60,2.06),Quaternion((1,0,0),math.pi/2))
clone('scrap_frame_2x1',tower,(0,-.60,3.26))
clone('scrap_cab_shell',tower,(0,-.13,3.85))
clone('scrap_hammer_arm',tower,(0,2.08,1.00))
clone('scrap_forked_ram',tower,(0,1.78,.76))
clone('scrap_outrigger_arm',tower,(1.25,-.60,1.25))
clone('scrap_lift_mast',tower,(-.92,-.8,1.16))


def render(col,filename):
    buggy.hide_render=col is not buggy; tower.hide_render=col is not tower
    bpy.context.view_layer.update()
    coords=[o.matrix_world@Vector(v) for o in col.objects if o.type=='MESH' for v in o.bound_box]
    low=Vector([min(v[i] for v in coords) for i in range(3)]); high=Vector([max(v[i] for v in coords) for i in range(3)])
    center=(low+high)*.5; size=max(high-low)
    camera.location=center+Vector((1.35,1.9,1.05))*size
    camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler(); camera.data.ortho_scale=size*1.65
    scene.render.filepath=str(OUT/'previews'/filename); scene.frame_set(1); bpy.ops.render.render(write_still=True)
render(buggy,'example-ram-buggy.png')
render(tower,'example-hammer-tower.png')
# Place the two demonstrations beside each other for the editable assembly file.
for obj in tower.objects:
    if obj.parent is None: obj.location.x+=5
buggy.hide_render=False; tower.hide_render=False
camera.location=(13,12,9); center=Vector((2.5,.5,1.8))
camera.rotation_euler=(center-camera.location).to_track_quat('-Z','Y').to_euler(); camera.data.ortho_scale=13
for screen in bpy.data.screens:
    for area in screen.areas:
        if area.type=='VIEW_3D':
            area.spaces.active.region_3d.view_location=center
            area.spaces.active.region_3d.view_distance=13
            area.spaces.active.region_3d.view_rotation=camera.rotation_euler.to_quaternion()
scene['purpose']='Assembly look checks from the actual exported part geometry. Example assemblies are illustrative, not physics prefabs.'
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'GoblinScraper_AssemblyExamples.blend'),compress=True)
print('ASSEMBLY_EXAMPLES_COMPLETE',flush=True)
