import bpy,sys,json, pathlib
bpy.ops.wm.open_mainfile(filepath=r'C:\Users\codex\Downloads\parts\refined\GoblinScraper_RefinedParts.blend')
print('CAMERAS',[(o.name,tuple(o.location)) for o in bpy.data.objects if o.type=='CAMERA'],flush=True)
print('COLLECTIONS',[(c.name,len(c.objects),c.hide_render) for c in bpy.data.collections],flush=True)
