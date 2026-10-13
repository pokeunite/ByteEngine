import bpy,math
from mathutils import Vector,Matrix
C=bpy.app.driver_namespace['cyber']
for g in C['guns']:
 if g.name=='Shotgun':continue
 g.rest();g.rig.animation_data.action=g.actions['ReloadEmpty'];wrist=g.contacts['Left']['wrist'];offset=g.contacts['Left']['palm']-wrist
 if g.name=='Pistol':
  rotation=Matrix.Rotation(-math.pi/2,3,'X');target=Vector((-.039,-.027,.097))-rotation@offset-wrist;roll=0
 else:target=Vector((-.044,-.011,.012))-offset-wrist;roll=0
 pull=.028 if g.name=='Pistol' else .032
 for f,pos,r in [(125,(0,0,0),0),(140,target,roll),(150,target+Vector((0,-pull,0)),roll),(155,target,roll),(165,(0,0,0),0),(180,(0,0,0),0)]:g.key('LeftWristControl',f,pos,roll=r,pitch=-90 if g.name=='Pistol' and f in [140,150,155] else 0)
 g.rest()
result='Empty reload support hand now contacts the slide/charging handle, with pistol overhand orientation'
