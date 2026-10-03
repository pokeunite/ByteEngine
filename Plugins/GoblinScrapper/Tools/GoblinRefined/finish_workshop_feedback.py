from pathlib import Path
import sys,subprocess
sys.path.insert(0,str(Path('Tools/Inspection/python').resolve()))
import imageio_ffmpeg
sound_dir=Path(r'C:\Users\codex\Documents\Goblin Scraper\Assets\GarageUI\sounds');sound_dir.mkdir(exist_ok=True)
for src,dst in [('click-a','select'),('switch-a','place')]:
    subprocess.run([imageio_ffmpeg.get_ffmpeg_exe(),'-y','-i',str(Path('output/goblin-scraper/ui-sources/Kenney/ui-pack/Sounds')/(src+'.ogg')),'-ac','1','-ar','44100','-c:a','pcm_s16le',str(sound_dir/(dst+'.wav'))],check=True,stdout=subprocess.DEVNULL,stderr=subprocess.DEVNULL)
p=Path('Engine/ByteEngine.Core/Construction/VehicleAssembly.cs');s=p.read_text();block='''        var parentBlock=Parts[parent];
        var ownBounds=Catalog[file].Bounds.Transform(position,rotation);var parentBounds=Catalog[parentBlock.File].Bounds.Transform(parentBlock.Position,parentBlock.Rotation);
        Vector3 separation=Vector3.Max(Vector3.Zero,Vector3.Max(parentBounds.Min-ownBounds.Max,ownBounds.Min-parentBounds.Max));
        if(separation.Length()>.32f)return "Part must touch its supporting structure";
''';s=s.replace(block,'').replace('        foreach(var other in Parts.Values)',block+'        foreach(var other in Parts.Values)');p.write_text(s)
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.FreeConstruction.cs');s=p.read_text().replace('using ByteEngine.Core.Assets;','using ByteEngine.Core.Assets;\nusing ByteEngine.Core.Assets.Importers;\nusing ByteEngine.Core.Audio;');s=s.replace('    public bool FreeBuilding','    private AudioSource3D? _selectSound,_placeSound;\n    public bool FreeBuilding',1)
s=s.replace('        Assembly=new(_catalog);','        RegisterSteeringClips();StartWorkshopSounds();\n        Assembly=new(_catalog);',1)
s=s.replace('ApplyAssemblyPose();_selectedBlock=id;','ApplyAssemblyPose();_placeSound?.Play();_selectedBlock=id;')
a=s.index('                if(parent.File=="scrap_steering_pivot"&&p.ParentBone=="Steer")');b=s.index('                {\n                    var bone=',a)
s=s[:a]+'''                if(p.ParentBone!="Root"&&_assemblyRigs.TryGetValue(parent.Id,out var rig)&&rig.TryGetBoneModelMatrix(p.ParentBone,out var current))
'''+s[b:]
s=s.replace('SampleCycle(rig,"Steer",Math.Abs(steering)*.5f)','SampleCycle(rig,steering>=0?"SteerRight":"SteerLeft",Math.Abs(steering)*.5f)')
s=s.replace('    private void SaveAssembly()', '''    private void StartWorkshopSounds()
    {
        AudioSource3D Sound(string name)
        {
            var sound=Own("Workshop "+name+" sound").AddComponent(new AudioSource3D {Spatial=false,Volume=.22f,PlayOnStart=false,ClipReference=new("Assets/GarageUI/sounds/"+name+".wav")});
            if(AudioClip.TryLoadWave(Path.Combine(ProjectRoot,"Assets","GarageUI","sounds",name+".wav"),out var clip))sound.SetClip(clip);
            return sound;
        }
        _selectSound=Sound("select");_placeSound=Sound("place");
    }
    private void RegisterSteeringClips()
    {
        var model=Assets!.LoadModel(new AssetReference(PartsDirectory+"/scrap_steering_pivot.glb"));
        var source=model.Animations.First(a=>a.Name.EndsWith("__Steer"));
        foreach(bool mirrored in new[]{false,true})
        {
            string suffix=mirrored?"SteerRight":"SteerLeft";
            var channels=source.Channels.Select(c=>
            {
                ImportedQuaternionTrack? track=c.Rotation;
                if(mirrored&&track is {Keys.Count:>0})
                {
                    Quaternion rest=track.Keys[0].Value;
                    track=new ImportedQuaternionTrack {Interpolation=track.Interpolation,Keys=track.Keys.Select(k=>k with {Value=Quaternion.Normalize(rest*Quaternion.Inverse(Quaternion.Inverse(rest)*k.Value))}).ToList()};
                }
                return new ImportedAnimationChannel {NodeName=c.NodeName,Translation=c.Translation,Rotation=track,Scale=c.Scale};
            }).ToList();
            model.RegisterRuntimeAnimation(new ImportedAnimation {Key="goblin-workshop-"+suffix,Name="scrap_steering_pivot__"+suffix,Duration=source.Duration,Channels=channels});
        }
    }
    private void SaveAssembly()''');p.write_text(s)
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.cs');s=p.read_text().replace('{ item.Click(); clickedUi=true; break; }','{ if(FreeBuilding)_selectSound?.Play();item.Click(); clickedUi=true; break; }');p.write_text(s)
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.GaragePresentation.cs');s=p.read_text().replace('new(.3f,.9f,.62f,.35f)','new(.86f,.67f,.26f,.30f)');p.write_text(s)
