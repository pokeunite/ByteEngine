from pathlib import Path
p=Path('Tools/KarlsonDemo/CyberpunkImport.cs');s=p.read_text().replace('renderer.PlayOnStart=weapon;','renderer.SkeletonKey="";renderer.PlayOnStart=weapon;')
s=s.replace('foreach(var obj in scene.GameObjects)\n', 'foreach(var obj in scene.GameObjects.ToArray())\n')
needle='            if(obj.GetComponent<AnimationController>() is {} controller)'
a=s.index(needle)
s=s[:a]+'''            if(!weapon)
            {
                var reference=renderer.Model;var prop=runtime.Assets.LoadModel(reference);obj.RemoveComponent(renderer);
                obj.AddComponent(new ModelHierarchyInstance { Model=reference });
                foreach(var mesh in prop.Meshes)
                {
                    var child=scene.CreateGameObject(name+" display mesh "+mesh.Name);child.SetParent(obj,false);
                    child.AddComponent(new MeshRenderer { MeshReference=new ModelMeshReference(reference,mesh.Key),MaterialReference=string.IsNullOrEmpty(mesh.MaterialKey)?null:new ModelMaterialReference(reference,mesh.MaterialKey) });
                }
                continue;
            }
''' +s[a:]
p.write_text(s)
