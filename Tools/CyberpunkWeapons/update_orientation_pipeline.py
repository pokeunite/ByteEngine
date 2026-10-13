from pathlib import Path
p=Path('Tools/CyberpunkWeapons/refine_rack_contact.py');s=p.read_text().replace('math.pi/2,3,\'Y\'','-math.pi/2,3,\'Y\'').replace('Vector((.012,-.027,.065))','Vector((-.012,-.027,.065))').replace('roll=90','roll=-90').replace('Vector((.044,-.011,.012))','Vector((-.044,-.011,.012))');p.write_text(s)
p=Path('Tools/KarlsonDemo/CyberpunkImport.cs');s=p.read_text().replace('Quaternion.CreateFromYawPitchRoll(MathF.PI+.08f,name=="Pistol"?-.085f:-.14f,0)','Quaternion.CreateFromYawPitchRoll(-.08f,name=="Pistol"?.085f:.14f,0)');p.write_text(s)
p=Path('Tools/KarlsonDemo/CyberpunkReviewApplication.cs');s=p.read_text().replace('Check(View(0).ModelLoaded,"Cyberpunk pistol resolves");','Check(View(0).ModelLoaded,"Cyberpunk pistol resolves");CheckForward(0);');s=s.replace('Check(equipment.SelectedSlot==1,"Shotgun equip event works");','Check(equipment.SelectedSlot==1,"Shotgun equip event works");CheckForward(1);').replace('Check(equipment.SelectedSlot==2,"Rifle equip event works");','Check(equipment.SelectedSlot==2,"Rifle equip event works");CheckForward(2);');a=s.index('    protected override void OnEngineRender()');s=s[:a]+'''    void CheckForward(int slot)
    {
        var renderer=View(slot);renderer.TryGetBoneWorldMatrix("Frame",out var frame);renderer.TryGetBoneWorldMatrix("MuzzleSocket",out var muzzle);
        Check(Vector3.Dot(Vector3.Normalize(muzzle.Translation-frame.Translation),Scenes.ActiveScene!.ActiveCamera!.Transform.Forward)>.9f,"Barrel points downrange: "+Program.Names[slot]);
        renderer.TryGetBoneWorldMatrix("RightHand",out var right);renderer.TryGetBoneWorldMatrix("LeftHand",out var left);
        Check(Vector3.Dot(right.Translation-left.Translation,Scenes.ActiveScene.ActiveCamera.Transform.Right)>0,"Right grip hand is on player right: "+Program.Names[slot]);
    }
''' +s[a:];p.write_text(s)
