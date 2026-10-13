import bpy
C=bpy.app.driver_namespace['cyber'];g=C['guns'].pop();C['guns'].insert(0,g);g.add_arms();g.rig_build();result='Corrected pistol and complete arm rig rebuilt'
