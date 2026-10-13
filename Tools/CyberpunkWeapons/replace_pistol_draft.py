import bpy
C=bpy.app.driver_namespace['cyber'];old=C['guns'].pop(0)
for obj in list(old.collection.objects):bpy.data.objects.remove(obj,do_unlink=True)
bpy.data.collections.remove(old.collection);result='Own draft pistol removed for corrected open-guard rebuild'
