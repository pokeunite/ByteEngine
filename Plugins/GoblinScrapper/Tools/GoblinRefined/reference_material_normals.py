"""Own procedural tangent normals for reference-derived Goblin meshes."""
import bpy,numpy as np
from pathlib import Path

def add_normals(materials,output):
    output=Path(output);(output/'textures').mkdir(exist_ok=True)
    for material in materials:
        if not material.use_nodes:continue
        bs=material.node_tree.nodes.get('Principled BSDF')
        if bs is None or not bs.inputs['Base Color'].links:continue
        tex=bs.inputs['Base Color'].links[0].from_node
        if tex.type!='TEX_IMAGE' or tex.image is None:continue
        albedo=tex.image;w,h=albedo.size;pixels=np.empty(w*h*4,dtype=np.float32);albedo.pixels.foreach_get(pixels)
        colour=pixels.reshape(h,w,4);height=colour[:,:,:3].mean(axis=2)
        dx=(np.roll(height,-1,axis=1)-np.roll(height,1,axis=1))*1.8
        dy=(np.roll(height,-1,axis=0)-np.roll(height,1,axis=0))*1.8
        normal=np.stack((-dx,-dy,np.ones_like(dx)),axis=-1);normal/=np.sqrt((normal*normal).sum(axis=-1))[:,:,None]
        rgba=np.ones_like(colour);rgba[:,:,:3]=normal*.5+.5
        image=bpy.data.images.new(material.name+'_normal',w,h,alpha=True);image.colorspace_settings.name='Non-Color';image.pixels.foreach_set(rgba.ravel())
        image.filepath_raw=str(output/'textures'/(material.name+'_normal.png'));image.file_format='PNG';image.save();image.pack()
        node=material.node_tree.nodes.new('ShaderNodeTexImage');node.image=image
        converter=material.node_tree.nodes.new('ShaderNodeNormalMap');converter.inputs['Strength'].default_value=.45
        material.node_tree.links.new(node.outputs['Color'],converter.inputs['Color']);material.node_tree.links.new(converter.outputs['Normal'],bs.inputs['Normal'])
