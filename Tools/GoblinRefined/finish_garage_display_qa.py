from pathlib import Path
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.cs');s=p.read_text().replace('        Input.NotifyGameViewPointerAim();','        Input.NotifyGameViewPointerAim();\n        AdaptHudViewport(Input.GameViewSize);');p.write_text(s)
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.GaragePresentation.cs');s=p.read_text().replace('Vector2 point=normalized*Input.GameViewSize/Math.Max(.1f,scale);','var insets=_canvas!.GetComponent<UiCanvas>()!.SafeAreaInsets;\n        Vector2 point=normalized*Input.GameViewSize/Math.Max(.1f,scale)-new Vector2(insets.X,insets.Y);')
needle='    private void HoverMount()';s=s.replace(needle,'''    internal void AdaptHudViewport(Vector2 size)
    {
        if(_canvas==null || size.X<160 || size.Y<90) return;
        float scale=Math.Min(size.X/1280,size.Y/720);
        Vector2 margin=Vector2.Max(Vector2.Zero,(size/scale-new Vector2(1280,720))*.5f);
        _canvas.GetComponent<UiCanvas>()!.SafeAreaInsets=new(margin.X,margin.Y,margin.X,margin.Y);
    }
'''+needle);p.write_text(s)
p=Path('Tests/ByteEngine.Tests/GoblinVehicleDiagnostic.cs');s=p.read_text();s=s.replace('''                    Screenshot(scene,Path.Combine(output,"tracked.png"));''','''                    TickInput(scene,new(930f/1280,622f/720),[],true);
                    TickInput(scene,new(.5f,.5f),[]);
                    Screenshot(scene,Path.Combine(output,"tracked.png"));''')
s=s.replace('Screenshot(scene,Path.Combine(output,"long-base.png"));', '''Screenshot(scene,Path.Combine(output,"long-base.png"));
            Screenshot(scene,Path.Combine(output,"garage-1080p.png"),1920,1080);
            Screenshot(scene,Path.Combine(output,"garage-16x10.png"),1280,800);
            Screenshot(scene,Path.Combine(output,"garage-ultrawide.png"),2560,1080);''')
s=s.replace('private void Screenshot(Scene scene,string file)', 'private void Screenshot(Scene scene,string file,int width=1280,int height=720)')
s=s.replace('        using var fb=new SceneFramebuffer();','        var builder=scene.GameObjects.SelectMany(o=>o.Components).OfType<VehicleBuilder3D>().Single();\n        builder.AdaptHudViewport(new(width,height));\n        using var fb=new SceneFramebuffer();')
s=s.replace('true,1280,720,1280,720,drawGrid3D:false','true,width,height,width,height,drawGrid3D:false').replace('byte[1280*720*4]', 'byte[width*height*4]').replace('Png(file,pixels,1280,720);','Png(file,pixels,width,height);\n        builder.AdaptHudViewport(Input.GameViewSize);')
p.write_text(s)
p=Path('Tests/ByteEngine.Tests/VehicleDriftTests.cs');s=p.read_text().replace('all 28 selectable parts; legacy saves.', '27 attachable parts + 2 bases; exclusive running gear; long-base mounts and saves; legacy migration.');p.write_text(s)
