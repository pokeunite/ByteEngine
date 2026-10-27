"""Fit the CC0 Mesh2Motion spider skeleton to authored anatomy; never auto-weight across legs.
Run inside Blender MCP after importing official spider-animations.glb.
"""
import bpy,json,math
from pathlib import Path
from mathutils import Vector,Matrix
WORK=Path('C:/Users/codex/.codex/worktrees/52af/ByteEngine')
OUT=WORK/'artifacts/spider-models'
source=bpy.data.objects['Mesh2Motion Original Spider Rig']
scene=bpy.data.scenes['Spider Rig Repair'];bpy.context.window.scene=scene
source.animation_data.action=None
for p in source.pose.bones:p.matrix_basis=Matrix.Identity(4)
bpy.context.view_layer.update()
clips={n:bpy.data.actions[n] for n in ['Idle','Walk','Jump','Attack','Death']}
for action in list(bpy.data.actions):
 if action.name.startswith(('PlayerHunter_','DrillBreaker_')):bpy.data.actions.remove(action)
report=[]
for kind in ['PlayerHunter','DrillBreaker']:
 col=bpy.data.collections.get('RIGGED_'+kind)
 if col:
  for o in list(col.objects):bpy.data.objects.remove(o,do_unlink=True)
  bpy.data.collections.remove(col)
 col=bpy.data.collections.new('RIGGED_'+kind);scene.collection.children.link(col)
 arm=source.copy();arm.data=source.data.copy();arm.animation_data_clear();arm.name=kind+' Mesh2Motion rig';col.objects.link(arm)
 bpy.ops.object.select_all(action='DESELECT');arm.select_set(True);bpy.context.view_layer.objects.active=arm
 bpy.ops.object.mode_set(mode='EDIT');eb=arm.data.edit_bones
 # Body and leg roots are not on the floor. This was the principal template mismatch.
 body={'root':((0,0,0),(0,.2,0)), 'hips':((0,0,.80),(0,-.16,.80)),
 'spine_1':((0,-.16,.80),(0,-.36,.80)), 'spine_2':((0,-.36,.80),(0,-.64,.76)),
 'head':((0,-.64,.76),(0,-.87,.67)), 'head_tip':((0,-.87,.67),(0,-.97,.62)),
 'tail_1':((0,.23,.83),(0,.47,.94)), 'tail_2':((0,.47,.94),(0,.98,1.10)),
 'tail_3':((0,.98,1.10),(0,1.52,1.09)), 'tail_tip':((0,1.52,1.09),(0,1.72,1.04))}
 for n,(h,t) in body.items():eb[n].head=h;eb[n].tail=t
 for side in ['l','r']:
  s=1 if side=='l' else -1
  eb['legs_anchor_'+side].head=(0,-.36,.80);eb['legs_anchor_'+side].tail=(s*.1,-.36,.80)
  eb['teeth_'+side].head=(s*.16,-.81,.55);eb['teeth_'+side].tail=(s*.18,-.93,.33)
  eb['teeth_tip_'+side].head=(s*.18,-.93,.33);eb['teeth_tip_'+side].tail=(s*.14,-1,.36)
 anatomy=json.loads((OUT/(kind+'-model.json')).read_text())['legs']
 for leg in anatomy:
  side='l' if leg['side']>0 else 'r';pair=leg['pair'];letter='abcd'[pair-1];points=[Vector(p) for p in leg['points']]
  names=['legs_anchor_'+str(pair)+'_'+side]+['leg_'+letter+'_'+str(i)+'_'+side for i in [1,2,3]]
  for i,n in enumerate(names):eb[n].head=points[i];eb[n].tail=points[i+1]
  tip=eb['leg_'+letter+'_tip_'+side];tip.head=points[-1];tip.tail=points[-1]+(points[-1]-points[-2]).normalized()*.12
 bpy.ops.object.mode_set(mode='OBJECT')
 # Copy editable parts, retaining originals. Each leg is weighted only to its own chain.
 objects=[]
 for src in bpy.data.collections['SPIDER_'+kind].objects:
  if src.type!='MESH':continue
  o=src.copy();o.data=src.data.copy();col.objects.link(o);o.parent=None;o.matrix_world=src.matrix_world.copy();o.name=kind+' rigged '+src.name
  for vg in list(o.vertex_groups):o.vertex_groups.remove(vg)
  if src.get('anatomy')=='walking_leg':
   side='l' if src['side']>0 else 'r';letter='abcd'[src['pair']-1]
   names=['legs_anchor_'+str(src['pair'])+'_'+side]+['leg_'+letter+'_'+str(i)+'_'+side for i in [1,2,3]]
   groups=[o.vertex_groups.new(name=n) for n in names]
   # 8 vertices per authored cross-section; blend only within the short joint membrane.
   # Shaft vertices remain rigid, eliminating rubbery stretches and cross-leg influences.
   for v in o.data.vertices:
    ring=v.index//8;seg=min(ring//4,3);t=[0,.16,.52,.85][ring%4] if ring<16 else 1
    if seg>0 and t==0:
     groups[seg-1].add([v.index],.5,'REPLACE');groups[seg].add([v.index],.5,'REPLACE')
    else:groups[seg].add([v.index],1,'REPLACE')
  else:
   # Keep the chitin shell, armour and eyes rigid together. Flex only the intended leg joints.
   o.vertex_groups.new(name='hips').add(list(range(len(o.data.vertices))),1,'REPLACE')
  mod=o.modifiers.new('Mesh2Motion fitted skeleton','ARMATURE');mod.object=arm;o.parent=arm;objects.append(o)
 # Retain the official bone-local rotations. Remove numerical translation noise and all scale tracks.
 # Refit bone lengths in the bind pose, so no animated scale is needed to reach the mesh.
 arm.animation_data_create()
 for label,original in clips.items():
  source.animation_data.action=original
  action=bpy.data.actions.new(kind+'_'+label);arm.animation_data.action=action
  for f in range(int(original.frame_range[0]),int(original.frame_range[1])+1):
   scene.frame_set(f)
   posed={}
   def setpose(p,matrix):
    posed[p.name]=matrix
    basis=p.bone.matrix_local.inverted() @ p.parent.bone.matrix_local @ posed[p.parent.name].inverted() @ matrix if p.parent else p.bone.matrix_local.inverted() @ matrix
    p.matrix_basis=basis;p.scale=(1,1,1)
   # Transfer rotations in armature space: different fitted bone axes must not turn
   # a source knee bend into a sideways twist. Connected heads follow the new lengths.
   for p in arm.pose.bones:
    sp=source.pose.bones[p.name];rest=p.bone.matrix_local;old=sp.bone.matrix_local
    q=sp.matrix.to_quaternion() @ old.to_quaternion().inverted() @ rest.to_quaternion()
    head=(posed[p.parent.name] @ p.parent.bone.matrix_local.inverted() @ rest.translation) if p.parent else rest.translation.copy()
    if p.name in ['root','hips']:head+=(sp.matrix.translation-old.translation)*1.3
    p.rotation_mode='QUATERNION';setpose(p,Matrix.LocRotScale(head,q,Vector((1,1,1))))
   if label!='Death':
    # Preserve the source gait's alternating contacts with a fitted two-link IK solve.
    # Rotation-only retargeting cannot preserve contact after changing leg proportions.
    for leg in anatomy:
     side='l' if leg['side']>0 else 'r';letter='abcd'[leg['pair']-1];pts=[Vector(v) for v in leg['points']]
     bodypose=posed['hips'] @ arm.data.bones['hips'].matrix_local.inverted()
     start=bodypose @ pts[1];anchor=bodypose @ pts[0]
     tipname='leg_'+letter+'_tip_'+side;sp=source.pose.bones[tipname]
     delta=sp.head-sp.bone.head_local
     foot=pts[4]+Vector((delta.x*.65,delta.y*.65,max(-.005,delta.z)*1.1))
     l1=(pts[2]-pts[1]).length;l2=(pts[3]-pts[2]).length;l3=(pts[4]-pts[3]).length
     ray=foot-start;distance=min(ray.length,l1+l2+l3-.002);direction=ray.normalized();foot=start+direction*distance
     along=(l1*l1-(l2+l3)**2+distance*distance)/(2*max(.001,distance))
     hint=(bodypose @ pts[2])-start;perp=hint-direction*hint.dot(direction)
     if perp.length<.001:perp=Vector((0,0,1))-direction*direction.z
     knee=start+direction*along+perp.normalized()*math.sqrt(max(0,l1*l1-along*along))
     ankle=knee+(foot-knee)*(l2/(l2+l3))
     targets=[anchor,start,knee,ankle,foot]
     names=['legs_anchor_'+str(leg['pair'])+'_'+side]+['leg_'+letter+'_'+str(i)+'_'+side for i in [1,2,3]]
     for i,n in enumerate(names):
      p=arm.pose.bones[n];b=p.bone;q=(b.tail_local-b.head_local).rotation_difference(targets[i+1]-targets[i]) @ b.matrix_local.to_quaternion()
      setpose(p,Matrix.LocRotScale(targets[i],q,Vector((1,1,1))))
     tip=arm.pose.bones[tipname];setpose(tip,Matrix.LocRotScale(foot,posed[names[-1]].to_quaternion(),Vector((1,1,1))))
   for p in arm.pose.bones:
    p.keyframe_insert('rotation_quaternion',frame=f,group=p.name)
    p.keyframe_insert('location',frame=f,group=p.name)
  track=arm.animation_data.nla_tracks.new();track.name=label;strip=track.strips.new(label,1,action);track.mute=True
 arm.animation_data.action=None
 for p in arm.pose.bones:p.matrix_basis=Matrix.Identity(4)
 arm['animation_source']='Mesh2Motion spider CC0; fitted bones and per-leg weights'
 arm['walking_legs']=8;arm['fit_method']='Authored joint centres, independent per-leg chains, rigid shell'
 report.append({'model':kind,'bones':len(arm.data.bones),'meshes':len(objects),'clips':[kind+'_'+n for n in clips]})
source.animation_data.action=None
for p in source.pose.bones:p.matrix_basis=Matrix.Identity(4)
(OUT/'rig-fit-report.json').write_text(json.dumps(report,indent=2))
print(json.dumps(report))
