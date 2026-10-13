import bpy
C=bpy.app.driver_namespace['cyber'];g=C['guns'][2];g.add_arms();result={'arms':g.name,'bones':len(g.bones),'parts':len(g.parts),'skin':len(g.skinned)}
