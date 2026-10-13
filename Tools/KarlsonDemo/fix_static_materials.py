from pathlib import Path
p=Path('Tools/KarlsonDemo/CyberpunkImport.cs');s=p.read_text().replace('MaterialReference=string.IsNullOrEmpty(mesh.MaterialKey)?null:new ModelMaterialReference(reference,mesh.MaterialKey)','Material=string.IsNullOrEmpty(mesh.MaterialKey)?new Material():runtime.Assets.GetModelMaterial(reference,mesh.MaterialKey).Clone(),MaterialReference=string.IsNullOrEmpty(mesh.MaterialKey)?null:new ModelMaterialReference(reference,mesh.MaterialKey)')
# Existing static display children are updated as well, preserving their model references.
needle='        runtime.Serializer.Save(scene,Program.Root+"/Scenes/Main.bytescene");'
s=s.replace(needle,'''        foreach(var meshRenderer in scene.GameObjects.Select(o=>o.GetComponent<MeshRenderer>()).OfType<MeshRenderer>())
            if(meshRenderer.MaterialReference is {} materialReference && materialReference.Model.CachedProjectPath?.Contains("Cyberpunk_")==true)
                meshRenderer.Material=runtime.Assets.GetModelMaterial(materialReference.Model,materialReference.SubAssetKey).Clone();
''' + needle);p.write_text(s)
p=Path('Tools/KarlsonDemo/DesignCheck.cs');s=p.read_text().replace('Assets/Models/Karlson_','Assets/Models/Cyberpunk_').replace('<4000','<15000').replace('Bones.Count<=10','Bones.Count==48').replace('Only intended low-poly geometry imports','Bounded cyberpunk gun/arms geometry imports').replace('One intended weapon skeleton','One complete gun/arms skeleton');p.write_text(s)
