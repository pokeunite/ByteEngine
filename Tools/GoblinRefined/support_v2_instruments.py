from pathlib import Path
import bpy,json,types,math,struct
from mathutils import Vector,Quaternion
OUT=Path(r'C:\Users\codex\Downloads\parts\refined\standard-flight-v2');bpy.ops.wm.open_mainfile(filepath=str(OUT/'Goblin_Parts_Redesign.blend'));SCENE=bpy.context.scene
lib=Path(__file__).with_name('build_refined.py').read_text();a=lib.index('def glb_json');b=lib.index('bpy.ops.wm.save_as_mainfile',a);exec(compile(lib[a:b],'export','exec'),globals());MATS={k:bpy.data.materials.get('Goblin_'+k,bpy.data.materials['Goblin_green']) for k in ['green','red','iron']};manifest=json.loads((OUT/'parts-catalog.json').read_text());ALL=[]
for item in manifest['parts']:
 f=Path(item['file']).stem;r=bpy.data.objects[f+'_rig'];col=r.users_collection[0];col.hide_viewport=False;p=types.SimpleNamespace(file=f,rig=r,col=col,root=bpy.data.objects[f],objects=[o for o in col.objects if o.type=='MESH'],actions=[(t.strips[0].action,t.strips[0].action.name,False,61) for t in r.animation_data.nla_tracks]);ALL.append(p)
 if item['reference_id'] not in [45,65,66,69,70]:continue
 def bar(a,b,radius):
  a,b=Vector(a),Vector(b);bpy.ops.mesh.primitive_cylinder_add(vertices=16,radius=radius,depth=(b-a).length,location=(a+b)/2);o=bpy.context.object;o.rotation_mode='QUATERNION';o.rotation_quaternion=Vector((0,0,1)).rotation_difference((b-a).normalized())
  for c in list(o.users_collection):c.objects.unlink(o)
  col.objects.link(o);o.data.materials.append(MATS['iron']);bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);o.vertex_groups.new(name='Root').add(list(range(len(o.data.vertices))),1,'REPLACE');o.parent=r;mod=o.modifiers.new('Fixed mechanical support','ARMATURE');mod.object=r;p.objects.append(o)
 i=item['reference_id']
 if i==45:
  bar((-.22,-.15,0),(.22,-.15,0),.025);bar((-.24,.07,.18),(.24,.07,.18),.024)
  for x in [-.22,.22]:bar((x,-.15,0),(x,.07,.18),.024)
 elif i==65:bar((0,.13,.12),(0,.23,.12),.05)
 elif i==69:
  bar((-.21,.18,.06),(.21,.18,.06),.018)
  for x in [-.12,.12]:bar((x,.10,.05),(x,.18,.05),.02)
 else:
  bar((0,.09,.14),(0,.21,.14),.10)
  if i==66:bar((-.18,.01,.05),(-.18,.01,.17),.010)
 export(p)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Goblin_Parts_Redesign.blend'),compress=True)
a=lib.index('# Studio for real geometry thumbnails');b=lib.index('for p in ALL:',a);exec(compile(lib[a:b],'studio','exec'),globals());SCENE.render.resolution_x=256;SCENE.render.resolution_y=192;SCENE.render.film_transparent=True;ground.hide_render=True
for p in ALL:
 if p.file not in ['goblin_rope_winch','goblin_sensor','goblin_timer','goblin_anglometer','goblin_speedometer']:continue
 frame_part(p);SCENE.render.filepath=str(OUT/'previews'/(p.file+'.png'));bpy.ops.render.render(write_still=True)
print('SUPPORTS_COMPLETE',flush=True)
