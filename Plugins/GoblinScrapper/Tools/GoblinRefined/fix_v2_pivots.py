from pathlib import Path
import bpy,json,types,math,struct
from mathutils import Vector,Quaternion
OUT=Path(r'C:\Users\codex\Downloads\parts\refined\standard-flight-v2');bpy.ops.wm.open_mainfile(filepath=str(OUT/'Goblin_Parts_Redesign.blend'));SCENE=bpy.context.scene
lib=Path(__file__).with_name('build_refined.py').read_text();a=lib.index('def glb_json');b=lib.index('bpy.ops.wm.save_as_mainfile',a);exec(compile(lib[a:b],'own_export','exec'),globals());MATS={k:bpy.data.materials.get('Goblin_'+k,bpy.data.materials['Goblin_green']) for k in ['green','red','iron']}
fix={5:(0,.20,0),28:(0,.20,0),44:(0,.10,0),50:(0,.18,0),86:(0,.18,0),88:(0,.16,0),17:(0,.15,0),45:(0,.07,.18),75:(0,.07,.18),66:(0,.23,.17),70:(0,.23,.17),69:(0,.18,.18),27:(.11,.13,0),77:(.07,-.08,0)}
manifest=json.loads((OUT/'parts-catalog.json').read_text());ALL=[]
for item in manifest['parts']:
 file=Path(item['file']).stem;rig=bpy.data.objects[file+'_rig'];col=rig.users_collection[0];col.hide_viewport=False;acts=[(t.strips[0].action,t.strips[0].action.name,False,61) for t in rig.animation_data.nla_tracks];p=types.SimpleNamespace(file=file,rig=rig,col=col,root=bpy.data.objects[file],objects=[o for o in col.objects if o.type=='MESH'],actions=acts);ALL.append(p)
 if item['reference_id'] not in fix and item['reference_id']!=13:continue
 sockets=[(o,o.matrix_world.copy()) for o in col.objects if o.parent==rig and o.parent_type=='BONE']
 if item['reference_id'] in fix:
  bpy.ops.object.select_all(action='DESELECT');rig.select_set(True);bpy.context.view_layer.objects.active=rig;bpy.ops.object.mode_set(mode='EDIT');bone=rig.data.edit_bones['Moving'];v=Vector(fix[item['reference_id']]);bone.head=v;bone.tail=v+Vector((0,0,.2));bpy.ops.object.mode_set(mode='OBJECT');bpy.context.view_layer.update()
  for o,m in sockets:o.matrix_world=m
 if item['reference_id']==13:
  action=acts[0][0];rig.animation_data.action=action;pb=rig.pose.bones['Moving'];rest=rig.data.bones['Moving'].matrix_local.to_quaternion()
  for f in range(1,62):pb.rotation_quaternion=rest.inverted()@Quaternion(Vector((0,1,0)),math.radians(-45+(f-1)*1.5))@rest;pb.keyframe_insert(data_path='rotation_quaternion',frame=f,group='Moving')
  rig.animation_data.action=None;pb.rotation_quaternion=(1,0,0,0)
 if item['reference_id']==77:
  bpy.ops.mesh.primitive_cylinder_add(vertices=16,radius=.035,depth=.26,location=(0,-.06,0));o=bpy.context.object;o.rotation_euler[1]=math.pi/2
  for c in list(o.users_collection):c.objects.unlink(o)
  col.objects.link(o);o.data.materials.append(MATS['iron']);bpy.context.view_layer.objects.active=o;bpy.ops.object.transform_apply(location=True,rotation=True,scale=True);o.vertex_groups.new(name='Root').add(list(range(len(o.data.vertices))),1,'REPLACE');o.parent=rig;mod=o.modifiers.new('Fixed jaw hinge shaft','ARMATURE');mod.object=rig;p.objects.append(o)
 export(p);print('PIVOT_FIXED',file,flush=True)
bpy.ops.wm.save_as_mainfile(filepath=str(OUT/'Goblin_Parts_Redesign.blend'),compress=True)
a=lib.index('# Studio for real geometry thumbnails');b=lib.index('for p in ALL:',a);exec(compile(lib[a:b],'studio','exec'),globals());SCENE.render.resolution_x=256;SCENE.render.resolution_y=192;SCENE.render.film_transparent=True;ground.hide_render=True
p=next(p for p in ALL if p.file=='goblin_metal_jaw');frame_part(p);SCENE.render.filepath=str(OUT/'previews'/(p.file+'.png'));bpy.ops.render.render(write_still=True)
print('PIVOT_REPAIR_COMPLETE',flush=True)
