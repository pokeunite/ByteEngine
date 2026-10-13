import bpy,math,json
from mathutils import Vector,Matrix
C=bpy.app.driver_namespace['cyber'];Gun=C['Gun'];scene=bpy.context.scene

def rig(g):
 bpy.ops.object.select_all(action='DESELECT');data=bpy.data.armatures.new('Cyber '+g.name+' skeleton');obj=bpy.data.objects.new('Cyber_'+g.name+'_Rig',data);g.collection.objects.link(obj);bpy.context.view_layer.update();bpy.context.view_layer.objects.active=obj;obj.select_set(True);bpy.ops.object.mode_set(mode='EDIT')
 root=data.edit_bones.new('WeaponRoot');root.head=(0,0,0);root.tail=(0,0,.05)
 motion=data.edit_bones.new('WeaponMotion');motion.head=(0,0,0);motion.tail=(0,0,.04);motion.parent=root
 for name,(head,tail,parent,connected) in g.bones.items():
  bone=data.edit_bones.new(name);bone.head=head;bone.tail=tail;bone.parent=data.edit_bones.get(parent);bone.use_connect=connected;bone.use_deform=not(name.endswith('Control') or name.endswith('Pole'))
 bpy.ops.object.mode_set(mode='OBJECT');g.rig=obj
 for part,bone in g.parts:
  group=part.vertex_groups.new(name=bone);group.add(list(range(len(part.data.vertices))),1,'REPLACE')
 for part,weights in g.skinned:
  names=set(k for w in weights for k in w);groups={name:part.vertex_groups.new(name=name) for name in names}
  for i,w in enumerate(weights):
   for name,weight in w.items():groups[name].add([i],weight,'REPLACE')
 # Join only by material, keeping every original bone group intact.
 all_parts=[o for o,b in g.parts]+[o for o,w in g.skinned];groups={}
 for part in all_parts:groups.setdefault(part.data.materials[0].name,[]).append(part)
 g.meshes=[]
 for mat,parts in groups.items():
  bpy.ops.object.select_all(action='DESELECT')
  for part in parts:part.select_set(True)
  bpy.context.view_layer.objects.active=parts[0]
  if len(parts)>1:bpy.ops.object.join()
  part=bpy.context.object;part.name='Cyber '+g.name+' '+mat;part.parent=obj;mod=part.modifiers.new('Animated skin','ARMATURE');mod.object=obj;g.meshes.append(part)
 bpy.ops.object.select_all(action='DESELECT')
 for pb in obj.pose.bones:pb.rotation_mode='QUATERNION'
 for side in ['Right','Left']:
  contact=g.contacts[side];p=obj.pose.bones[contact['fore']];ik=p.constraints.new('IK');ik.name='Wrist contact IK';ik.target=obj;ik.subtarget=contact['control'];ik.chain_count=2;ik.use_stretch=False;ik.iterations=64
  # The stable rest bend determines elbow direction; pole avoids inversion across reload poses.
  ik.pole_target=obj;ik.pole_subtarget=contact['pole'];ik.pole_angle=0
  pb=obj.pose.bones[side+'Hand'];rotation=pb.constraints.new('COPY_ROTATION');rotation.name='Glove orientation';rotation.target=obj;rotation.subtarget=contact['control'];rotation.target_space='POSE';rotation.owner_space='POSE'
 obj.animation_data_create();bpy.context.view_layer.update();g.make_actions();g.rest()
Gun.rig_build=rig

def make_actions(g):
 duration={'Idle':121,'Fire':45 if g.name=='Shotgun' else 15,'Reload':120 if g.name=='Shotgun' else 150,'ReloadEmpty':150 if g.name=='Shotgun' else 180,'Equip':37}
 for clip,last in duration.items():
  action=bpy.data.actions.new('Cyber '+g.name+' '+clip);action.use_fake_user=True;g.rig.animation_data.action=action
  for bone in g.rig.pose.bones:g.key(bone.name,1);g.key(bone.name,last)
  for name in ['Shell','ReplacementMagazine']:
   if name in g.bones:g.key(name,1,scale=.001);g.key(name,last,scale=.001)
  if clip=='Idle':
   for f in [1,31,61,91,121]:g.key('WeaponMotion',f,(0,0,.0011*math.sin((f-1)/120*math.tau)),pitch=.16*math.sin((f-1)/120*math.tau))
  elif clip=='Fire':
   for f,back,pitch in [(1,0,0),(3,.017,3.2 if g.name=='Pistol' else 2.1),(6,.010,1.4),(11,.002,.2),(15,0,0)]:g.key('WeaponMotion',f,(0,-back,0),pitch=pitch)
   g.key('Trigger',3,pitch=-8);g.key('Trigger',7)
   g.key('RightHandIndex1',3,pitch=-5);g.key('RightHandIndex1',7)
   if g.name=='Pistol':g.key('Slide',3,(0,-.028,0));g.key('Slide',7)
   if g.name=='Rifle':g.key('Bolt',3,(0,-.032,0));g.key('Bolt',7)
   if g.name=='Shotgun':
    for f,t in [(8,0),(17,1),(23,1),(34,0),(45,0)]:g.key('Pump',f,(0,-.115*t,0));g.key('LeftWristControl',f,(0,-.115*t,0))
  elif clip.startswith('Reload'):
   # The trigger finger stays indexed while the support hand leaves its grip.
   g.key('RightHandIndex1',12,pitch=-12);g.key('RightHandIndex1',last-8)
   tilt=(0,.035,.015)
   for f in [18,last-22]:g.key('WeaponMotion',f,tilt,pitch=8,roll=-18 if g.name=='Pistol' else -15)
   if g.name!='Shotgun':
    axis=g.mag_axis;clear=axis*g.mag_clear
    base=g.bones['Magazine'][0]+axis*(g.mag_clear*.66)
    # Align support wrist with the magazine base. Hands follow the exact withdrawal axis.
    approach=base+Vector((.036,-.02,0))-g.contacts['Left']['wrist'];off=clear+Vector((.14,-.03,-.07))
    poses=[(1,(0,0,0)),(20,approach),(39,approach+clear),(53,approach+off),(75,approach+off),(92,approach+clear),(112,approach+axis*.012),(119,approach),(135,(0,0,0)),(last,(0,0,0))]
    for f,pos in poses:g.key('LeftWristControl',f,pos)
    for f,pos in [(1,(0,0,0)),(20,(0,0,0)),(39,clear),(53,off),(last,off)]:g.key('Magazine',f,pos,scale=1)
    g.key('Magazine',55,off,scale=1);g.key('Magazine',56,off,scale=.001);g.key('Magazine',last,off,scale=.001)
    for f,pos,size in [(1,off,.001),(73,off,.001),(74,off,1),(92,clear,1),(112,axis*.012,1),(119,(0,0,0),1),(last,(0,0,0),1)]:g.key('ReplacementMagazine',f,pos,scale=size)
    # Retain incoming magazine through end of clip; Idle restores the original at identical placement.
    if clip=='ReloadEmpty':
     mech='Slide' if g.name=='Pistol' else 'Bolt';pull=.028 if g.name=='Pistol' else .032
     # Support hand moves outside the receiver before reaching the charging surface.
     target=Vector((.039,-.006,.033))-g.contacts['Left']['wrist']
     for f,pos in [(125,approach),(140,target),(150,target+Vector((0,-pull,0))),(155,target),(165,(0,0,0)),(last,(0,0,0))]:g.key('LeftWristControl',f,pos)
     g.key(mech,149,(0,-pull,0));g.key(mech,155)
   else:
    gate=Vector((.012,.022,-.072));wrist=g.contacts['Left']['wrist'];grab=gate+Vector((.044,-.041,-.023));off=grab+Vector((.08,-.14,-.18));insert=grab+Vector((0,.025,.035))
    for f,pos in [(1,(0,0,0)),(20,off-wrist),(40,grab-wrist),(65,insert-wrist),(82,insert-wrist),(103,(0,0,0)),(120,(0,0,0))]:g.key('LeftWristControl',f,pos)
    for f,pos,size in [(1,(.15,-.12,-.22),.001),(25,(.15,-.12,-.22),.001),(27,(.15,-.12,-.22),1),(40,(.024,-.033,0),1),(65,(.012,.014,.045),1),(75,(.012,.030,.055),1),(76,(.012,.030,.055),.001),(last,(.012,.03,.055),.001)]:g.key('Shell',f,pos,scale=size)
    g.key('LoadingGate',45,pitch=30);g.key('LoadingGate',78)
    if clip=='ReloadEmpty':
     for f,t in [(110,0),(123,1),(132,1),(145,0),(150,0)]:g.key('Pump',f,(0,-.115*t,0));g.key('LeftWristControl',f,(0,-.115*t,0))
  else:
   g.key('WeaponMotion',1,(0,-.065,-.21),pitch=-22,roll=5);g.key('WeaponMotion',20,(0,0,.008),pitch=1);g.key('WeaponMotion',37)
  # Controlled easing prevents overshoot through the magazine well.
  for layer in action.layers:
   for strip in layer.strips:
    for bag in strip.channelbags:
     for curve in bag.fcurves:
      for key in curve.keyframe_points:key.interpolation='LINEAR' if 'scale' in curve.data_path else 'BEZIER';key.handle_left_type='AUTO_CLAMPED';key.handle_right_type='AUTO_CLAMPED'
  g.actions[clip]=action
 g.rig.animation_data.action=None
 for clip,action in g.actions.items():
  track=g.rig.animation_data.nla_tracks.new();track.name=clip;strip=track.strips.new(clip,1,action);strip.name=clip;strip.extrapolation='NOTHING';track.mute=True
Gun.make_actions=make_actions
result='Contact IK, skinned assemblies and staged animation helpers ready'
