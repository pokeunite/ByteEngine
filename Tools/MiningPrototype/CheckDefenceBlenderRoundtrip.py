import bpy,json
from pathlib import Path
from mathutils import Vector
OUT=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine/artifacts/defence-models')
s=bpy.data.scenes.get('Defence export verification') or bpy.data.scenes.new('Defence export verification');bpy.context.window.scene=s
result=[]
for name,clip,frame in [('Sentry','Deploy',46),('Repeater','Fire',2),('Mortar','AimSweep',31),('Mine','Trigger',8),('SupplyCache','Open',46)]:
 before=set(bpy.data.objects);actions=set(bpy.data.actions);bpy.ops.import_scene.gltf(filepath=str(OUT/(name+'.glb')));new=set(bpy.data.objects)-before;new_actions=set(bpy.data.actions)-actions
 arm=next(o for o in new if o.type=='ARMATURE');meshes=[o for o in new if o.type=='MESH'];action=next(a for a in new_actions if a.name.startswith(name+'_'+clip));arm.animation_data.action=action;arm.animation_data.action_slot=action.slots[0];arm.animation_data.use_nla=False
 def points(f):
  s.frame_set(f);bpy.context.view_layer.update();deps=bpy.context.evaluated_depsgraph_get();out=[]
  for o in meshes:
   evaluated=o.evaluated_get(deps);mesh=evaluated.to_mesh();out.extend([o.matrix_world@v.co for v in mesh.vertices]);evaluated.to_mesh_clear()
  return out
 a=points(0);b=points(frame);distance=max((x-y).length for x,y in zip(a,b));assert distance>(.04 if clip=='Fire' else .005),(name,clip,distance)
 entry={'asset':name,'clip':clip,'vertexMotionMetres':distance,'meshes':len(meshes),'bones':len(arm.data.bones)};result.append(entry)
 for o in new:o.hide_render=True;o.hide_set(True)
(OUT/'blender-roundtrip-verification.json').write_text(json.dumps(result,indent=2))
