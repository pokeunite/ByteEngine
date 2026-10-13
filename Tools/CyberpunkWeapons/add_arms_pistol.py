import bpy
C=bpy.app.driver_namespace['cyber'];g=C['guns'][int(__import__('os').environ.get('CYBER_GUN','0'))];g.add_arms();result={'arms':g.name,'bones':len(g.bones),'rigid_parts':len(g.parts),'continuous_skin_parts':len(g.skinned)}
