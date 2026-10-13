import bpy,bmesh
from mathutils import Vector
C=bpy.app.driver_namespace['cyber'];scene=bpy.context.scene
for g in C['guns']:
 g.rest()
 def flip(p):p=Vector(p);p.x=-p.x;return p
 for mesh in g.meshes:
  for v in mesh.data.vertices:v.co.x=-v.co.x
  bm=bmesh.new();bm.from_mesh(mesh.data);bmesh.ops.reverse_faces(bm,faces=list(bm.faces));bmesh.ops.recalc_face_normals(bm,faces=list(bm.faces));bm.to_mesh(mesh.data);bm.free();mesh.data.update()
 for name,(head,tail,parent,connected) in list(g.bones.items()):g.bones[name]=(flip(head),flip(tail),parent,connected)
 g.grip=tuple(flip(g.grip));g.support=tuple(flip(g.support))
 for values in g.contacts.values():
  values['palm']=flip(values['palm']);values['wrist']=flip(values['wrist'])
 bpy.ops.object.select_all(action='DESELECT');g.rig.select_set(True);bpy.context.view_layer.objects.active=g.rig;bpy.ops.object.mode_set(mode='EDIT')
 for name,(head,tail,parent,connected) in g.bones.items():
  bone=g.rig.data.edit_bones[name];bone.use_connect=False;bone.head=head;bone.tail=tail;bone.roll=-bone.roll
 for name,values in g.bones.items():g.rig.data.edit_bones[name].use_connect=values[3]
 bpy.ops.object.mode_set(mode='OBJECT')
 for track in list(g.rig.animation_data.nla_tracks):g.rig.animation_data.nla_tracks.remove(track)
 g.actions={}
# Reload geometry helpers with mirrored handoff/tilt controls; source bone motion remains +Y forward.
from pathlib import Path
code=Path(r'C:/Users/codex/ByteEngine/Tools/CyberpunkWeapons/rig_and_animations.py').read_text()
code=code.replace('Vector((.036,-.02,0))','Vector((-.036,-.02,0))').replace('Vector((.14,-.03,-.07))','Vector((-.14,-.03,-.07))').replace("roll=-18 if g.name=='Pistol' else -15","roll=18 if g.name=='Pistol' else 15").replace('Vector((.044,-.041,-.023))','Vector((-.044,-.041,-.023))').replace('Vector((.08,-.14,-.18))','Vector((-.08,-.14,-.18))').replace('gate=Vector((.012,.022,-.072))','gate=Vector((-.012,.022,-.072))').replace('(.15,-.12,-.22)','(-.15,-.12,-.22)').replace('(.024,-.033,0)','(-.024,-.033,0)').replace('(.012,.014,.045)','(-.012,.014,.045)').replace('(.012,.030,.055)','(-.012,.030,.055)').replace('(.012,.03,.055)','(-.012,.03,.055)')
exec(compile(code,'mirrored_animation_helpers','exec'))
for g in C['guns']:g.make_actions();g.rest()
result='Geometry, winding, rest skeletons and reload handedness corrected for native -Z view'
