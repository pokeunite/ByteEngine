import bpy
C=bpy.app.driver_namespace['cyber'];g=C['guns'][0];g.rig_build();result={'rig':g.name,'meshes':len(g.meshes),'bones':len(g.rig.data.bones)}
