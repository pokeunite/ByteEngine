import bpy, math, json
from pathlib import Path
from mathutils import Vector
OUT=Path('C:/Users/codex/Downloads/byte engine files/Milk Depot Environment')
ASSETS=Path('C:/Users/codex/Documents/karlson demo/Assets/Environment/MilkDepot')
bpy.ops.wm.open_mainfile(filepath=str(OUT/'MilkDepot_Environment_v2.blend'))
library=bpy.data.scenes['01 - Modular asset library'];level=bpy.data.scenes['02 - Milk Depot assembled reference']
data=json.loads((OUT/'asset_catalog.json').read_text())
bpy.context.window.scene=level
# Keep one deck mesh per landing: overlapped top surfaces create self-shadowing.
duplicates=[(-23,3,2.5),(-27,-7,2.5),(5,1,2.5)]
for o in list(level.objects):
    if o.get('asset_id')=='Landing_2m' and any((o.location-Vector(p)).length<.01 for p in duplicates):
        for p in data['placements'][:]:
            if p['asset']=='Landing_2m' and (Vector(p['position'])-o.location).length<.01:data['placements'].remove(p);break
        bpy.data.objects.remove(o,do_unlink=True)
        next(a for a in data['assets'] if a['id']=='Landing_2m')['placements']-=1
master=next(o for o in library.objects if o.get('asset_id')=='Landing_2m')
landing=master.copy();landing.data=master.data;level.collection.objects.link(landing);landing.location=(-3,-13,2.5);landing.rotation_euler=(0,0,0)
data['placements'].append({'asset':'Landing_2m','position':[-3,-13,2.5],'rotation_z':0})
next(a for a in data['assets'] if a['id']=='Landing_2m')['placements']+=1

# A tangible objective display supports the milk bottle instead of suspending it.
bpy.context.window.scene=library
parts=[]
def piece(radius,depth,z,material,vertices=32):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices,radius=radius,depth=depth,location=(0,0,z))
    o=bpy.context.object;o.data.materials.append(bpy.data.materials['MD_'+material])
    mod=o.modifiers.new('Display edge machining','BEVEL');mod.width=.009;mod.segments=3;bpy.ops.object.modifier_apply(modifier=mod.name)
    for face in o.data.polygons:face.use_smooth=True
    mod=o.modifiers.new('Display face normals','WEIGHTED_NORMAL');mod.keep_sharp=True;bpy.ops.object.modifier_apply(modifier=mod.name)
    parts.append(o)
piece(.48,.10,.05,'Steel',8)
piece(.37,.72,.46,'Concrete',8)
piece(.38,.06,.80,'Orange',8)
piece(.34,.05,.855,'Cyan',32)
piece(.38,.08,.92,'Steel',32)
piece(.32,.045,.9825,'White',32)
for angle in [0,math.pi/2,math.pi,math.pi*1.5]:
    bpy.ops.mesh.primitive_cube_add(size=1,location=(math.cos(angle)*.373,math.sin(angle)*.373,.45))
    o=bpy.context.object;o.dimensions=(.02,.20,.5);o.rotation_euler.z=angle
    o.data.materials.append(bpy.data.materials['MD_Dark']);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
    mod=o.modifiers.new('Inset control face','BEVEL');mod.width=.007;mod.segments=3;bpy.ops.object.modifier_apply(modifier=mod.name);parts.append(o)
bpy.ops.object.select_all(action='DESELECT')
for o in parts:o.select_set(True)
bpy.context.view_layer.objects.active=parts[0];bpy.ops.object.join();pedestal=bpy.context.object;pedestal.name='Milk_Objective_Pedestal'
library.cursor.location=(0,0,0);bpy.ops.object.origin_set(type='ORIGIN_CURSOR')
pedestal['asset_id']='Milk_Objective_Pedestal';pedestal['category']='Gameplay props';pedestal.asset_mark();pedestal.asset_data.description='Octagonal extraction plinth with inset control faces and illuminated neck.'
pedestal.data.calc_loop_triangles()
entry={'id':'Milk_Objective_Pedestal','category':'Gameplay props','dimensions_m':[.96,.96,1.005],
       'description':'Octagonal extraction display with inset control faces, steel collar and cyan neck; supports the objective bottle',
       'collision':'compound cylinders or box','triangles':len(pedestal.data.loop_triangles),'file':'Milk_Objective_Pedestal.glb','placements':1,'pivot':'floor-centred'}
data['assets'].append(entry)
bpy.ops.export_scene.gltf(filepath=str(ASSETS/'Models/Milk_Objective_Pedestal.glb'),export_format='GLB',use_selection=True,use_active_scene=True,export_animations=False,export_extras=True,export_yup=True)
pedestal.location=(49,40,0)
instance=pedestal.copy();instance.data=pedestal.data;level.collection.objects.link(instance);instance.location=(23,15.9,0)
data['placements'].append({'asset':'Milk_Objective_Pedestal','position':[23,15.9,0],'rotation_z':0})
for o in level.objects:
    if o.get('asset_id')=='Milk_Bottle':
        o.location.z=.985
        for p in data['placements']:
            if p['asset']=='Milk_Bottle':p['position'][2]=.985
data['unique_assets']=len(data['assets']);data['instances']=len(data['placements']);data['source']='MilkDepot_Environment.blend'
for p in [OUT/'asset_catalog.json',ASSETS/'asset_catalog.json']:p.write_text(json.dumps(data,indent=2))
bpy.context.window.scene=level;bpy.ops.object.select_all(action='DESELECT')
for o in level.objects:
    if o.type=='MESH':o.select_set(True)
bpy.ops.export_scene.gltf(filepath=str(ASSETS/'MilkDepot_Assembled.glb'),export_format='GLB',use_selection=True,use_active_scene=True,export_animations=False,export_extras=True,export_yup=True)
level.camera=bpy.data.objects['Overview']
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'MilkDepot_Environment.blend'))
for name,file,size in [('Overview','MilkDepot_Overview.png',(1680,1200)),('Courtyard player view','MilkDepot_Courtyard.png',(1600,900))]:
    level.camera=bpy.data.objects[name];level.render.resolution_x=size[0];level.render.resolution_y=size[1];level.render.filepath=str(OUT/'Previews'/file)
    bpy.ops.render.render(write_still=True)
level.camera=bpy.data.objects['Overview']
print('LAYOUT POLISH PASS',data['unique_assets'],'assets;',data['instances'],'placements')
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'MilkDepot_Environment.blend'))
