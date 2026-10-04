import bpy,math,json
from pathlib import Path
from mathutils import Vector
out=Path(r'C:\Users\codex\ByteEngine\Designs\WorkshopHud\brace');out.mkdir(exist_ok=True)
bpy.ops.wm.read_factory_settings(use_empty=True)
def mat(name,color,metal=0):
 m=bpy.data.materials.new(name);m.diffuse_color=(*color,1);m.use_nodes=True;n=m.node_tree.nodes.get('Principled BSDF');n.inputs['Base Color'].default_value=(*color,1);n.inputs['Metallic'].default_value=metal;n.inputs['Roughness'].default_value=.48;return m
iron=mat('Forged dark iron',(.13,.16,.15),.8);green=mat('Goblin olive enamel',(.27,.36,.13),.5);brass=mat('Brass rivets',(.56,.32,.09),.8)
def cube(name,pos,size,m):
 bpy.ops.mesh.primitive_cube_add(size=1,location=pos);o=bpy.context.object;o.name=name;o.dimensions=size;bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);o.data.materials.append(m);b=o.modifiers.new('Soft forged edges','BEVEL');b.width=.018;b.segments=3;o.modifiers.new('Weighted normals','WEIGHTED_NORMAL');return o
rod=[cube('Brace span',(0,0,0),(.085,1,.085),iron),cube('Olive inset',(0,0,.047),(.042,.8,.01),green)]
ends=[]
for y in [-.5,.5]:
 ends.append(cube('Riveted end plate',(0,y,0),(.21,.18,.13),green))
 ends.append(cube('Steel end strap',(0,y,.079),(.23,.085,.034),iron))
 for x in [-.075,.075]:
  bpy.ops.mesh.primitive_uv_sphere_add(segments=12,ring_count=6,radius=.024,location=(x,y,.104));o=bpy.context.object;o.name='Brass rivet';o.scale=(1,1,.45);o.data.materials.append(brass);ends.append(o)
def export(name,objects):
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.select_set(True)
 bpy.ops.export_scene.gltf(filepath=str(out/(name+'.glb')),use_selection=True,export_apply=True)
export('goblin_brace',rod+ends)
export('goblin_brace_span',rod)
# Reusable end cap centered on its attachment point.
for o in ends[:4]:o.location.y+=.5
export('goblin_brace_end',ends[:4])
for o in ends[:4]:o.location.y-=.5
bpy.ops.object.camera_add(location=(1.8,-2.5,1.9));cam=bpy.context.object;cam.rotation_euler=(-cam.location).to_track_quat('-Z','Y').to_euler();cam.data.type='ORTHO';cam.data.ortho_scale=1.5;bpy.context.scene.camera=cam
for pos,power,size in [((2,-2,4),600,4),((-2,1,2),400,3)]:
 bpy.ops.object.light_add(type='AREA',location=pos);l=bpy.context.object;l.data.energy=power;l.data.shape='DISK';l.data.size=size;l.rotation_euler=(-l.location).to_track_quat('-Z','Y').to_euler()
sc=bpy.context.scene;sc.render.engine='BLENDER_EEVEE';sc.render.resolution_x=640;sc.render.resolution_y=480;sc.render.resolution_percentage=100;sc.render.film_transparent=True;sc.render.image_settings.file_format='PNG';sc.render.filepath=str(out/'goblin_brace.png');bpy.ops.wm.save_as_mainfile(filepath=str(out/'GoblinBrace.blend'));bpy.ops.render.render(write_still=True)
game=Path(r'C:\Users\codex\Documents\Goblin Scraper');import shutil
for p in out.glob('*.glb'):shutil.copy2(p,game/'Assets/ContraptionParts'/p.name)
shutil.copy2(out/'goblin_brace.png',game/'Assets/GarageUI/cutouts/goblin_brace.png')
for dest in [game/'Assets/GarageUI/parts-catalog.json',game/'Assets/ContraptionParts/parts-catalog.json']:
 if not dest.exists():continue
 data=json.loads(dest.read_text());data['parts']=[p for p in data['parts'] if p['file']!='goblin_brace.glb'];data['parts'].append(dict(file='goblin_brace.glb',label='Brace',group='Structure',kind='brace',reference_id=7,mass=.5,description='Two-point rigid reinforcement. Click two connectors to close a structural loop. Do not brace across a joint you want to move.',bounds_blender_z_up=[[-.12,-.6,-.09],[.12,.6,.13]],root_bounds_game=[[-.12,-.09,-.6],[.12,.13,.6]],sockets=[dict(name='SOCKET_Brace_A',position=[0,0,.5],normal=[0,0,1],animation_bone='Root'),dict(name='SOCKET_Brace_B',position=[0,0,-.5],normal=[0,0,-1],animation_bone='Root')]))
 dest.write_text(json.dumps(data,indent=2))
print('BRACE EXPORTED AND INSTALLED')
