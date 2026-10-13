from pathlib import Path
p=Path('Tools/KarlsonDemo/CyberpunkReviewApplication.cs');s=p.read_text().replace('    public CyberpunkReviewApplication():base(1280,720,"Cyberpunk weapons - native verification"){}','    readonly bool _posesOnly;\n    public CyberpunkReviewApplication(bool posesOnly=false):base(1280,720,"Cyberpunk weapons - native verification"){_posesOnly=posesOnly;}');s=s.replace('        if(_frame is 60 or 550 or 1470','        if(_posesOnly){PoseReview(scene,equipment,raw);return;}\n        if(_frame is 60 or 550 or 1470');a=s.index('    void CheckForward(int slot)');s=s[:a]+'''    void PoseReview(Scene scene,FpsGunLab3D equipment,RawInputSnapshot raw)
    {
        typeof(Input).GetMethod("UpdatePortable",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[raw,true]);InputActions.Update(raw,true);typeof(Time).GetMethod("Update",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[1.0/60]);
        if(_frame<50)return;
        foreach(var controller in scene.GameObjects.Select(o=>o.GetComponent<AnimationController>()).OfType<AnimationController>())controller.Enabled=false;
        equipment.UseBuiltInFireInput=false;
        int index=(_frame-50)/4;if(index>=18){File.WriteAllText(Program.Root+"/Preview/cyberpunk-pose-validation.json",JsonSerializer.Serialize(_checks,new JsonSerializerOptions{WriteIndented=true}));Close();return;}
        if((_frame-50)%4!=0)return;
        int slot=index/6;int pose=index%6;equipment.Select(slot);var renderer=View(slot);
        (string Clip,int Frame)[] poses=[("Idle",1),("Fire",slot==1?18:3),("Reload",39),("Reload",92),("Reload",119),("ReloadEmpty",slot==1?65:150)];
        var sample=poses[pose];Check(renderer.Play(sample.Clip,false,0)&&renderer.Seek((sample.Frame-1)/60f),"Pose samples "+Program.Names[slot]+" "+sample);renderer.Pause();
        foreach(string side in new[]{"Right","Left"})
        {
            renderer.TryGetBoneModelMatrix(side+"Hand",out var hand);renderer.TryGetBoneModelMatrix(side+"WristControl",out var target);
            Check(Vector3.Distance(hand.Translation,target.Translation)<.001f,"Baked "+side+" wrist contact "+Program.Names[slot]+" "+sample);
        }
        Check(renderer.TryGetCurrentModelBounds(out var bounds)&&bounds.IsValid,"Finite sampled skin "+Program.Names[slot]+" "+sample);
        if(pose==0)CheckForward(slot);
        _capture="Pose_"+Program.Names[slot]+"_"+sample.Clip+"_"+sample.Frame+".png";
    }
''' +s[a:];p.write_text(s)
p=Path('Tools/KarlsonDemo/Program.cs');s=p.read_text().replace('if(args.Contains("--cyber-check"))','if(args.Contains("--cyber-poses")){using var review=new CyberpunkReviewApplication(true);review.Run();return;}if(args.Contains("--cyber-check"))');p.write_text(s)
p=Path('Tools/KarlsonDemo/CyberpunkImport.cs');s=p.read_text();needle='        runtime.Database.Scan();File.WriteAllText(Program.Root+"/Preview/cyberpunk-import.json"';a=s.index(needle);s=s[:a]+'''        var player=scene.GameObjects.Single(o=>o.GetComponent<FpsGunLab3D>()!=null);var descendants=scene.GameObjects.Where(o=>IsUnder(o,player)).ToArray();
        serialized=runtime.Serializer.Serialize(scene);
        var character=new BlueprintDefinition{Name="FPS Character with cyberpunk guns and arms",Type=BlueprintType.Character,Root=serialized.GameObjects.Single(o=>o.Id==player.Id),Children=serialized.GameObjects.Where(o=>descendants.Any(d=>d.Id==o.Id)&&o.Id!=player.Id).ToList()};
        Backup("Assets/Blueprints/FPS Character.byteblueprint");new BlueprintSerializer().Save(character,Program.Root+"/Assets/Blueprints/FPS Character.byteblueprint");
''' +s[a:];s=s.replace('        void Backup(string path)', '        bool IsUnder(GameObject obj,GameObject root){for(GameObject? current=obj;current!=null;current=current.Parent)if(current==root)return true;return false;}\n        void Backup(string path)');p.write_text(s)
