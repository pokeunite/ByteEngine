from pathlib import Path
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.FreePresentation.cs');s=p.read_text().replace('private UiText? _partPurpose,_connectionFeedback,_machineCount;','private UiText? _partPurpose,_connectionFeedback,_machineCount,_freeDriveLabel;')
s=s.replace('        Rule("Chassis inspector"','        _freeDriveLabel=Text("Test drive label","TEST DRIVE  [B]",new(1110,13),19);_freeDriveLabel.FontReference=title.FontReference;_freeDriveLabel.Color=new(.075f,.07f,.045f,1);_driveButton.Label="";\n        Rule("Chassis inspector"')
s=s.replace('_driveButton!.Label=Building?"TEST DRIVE   [B]":"BACK TO BUILD   [B]";','_freeDriveLabel!.Text=Building?"TEST DRIVE  [B]":"BACK TO BUILD  [B]";_driveButton!.Label="";');p.write_text(s)
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.cs');s=p.read_text()
a=s.index('        for(int side=-1;side<=1;side+=2)\n        {\n            int i=_skidIndex');b=s.index('            mark.Object.Transform.WorldPosition=',a)
s=s[:a]+'''        Vector3[] contacts=FreeBuilding ? Assembly!.Parts.Values.Where(p=>p.File.StartsWith("scrap_wheel_")).Select(p=>
            _assemblyVisuals[p.Id].Transform.WorldPosition-Vector3.UnitY*(p.File=="scrap_wheel_large"?.64f:.4125f)).Where(p=>p.Y<.2f).ToArray() :
            [-1,1].Select(side=>Transform.WorldPosition+Transform.Right*(side*1.02f)-Transform.Forward*.65f).ToArray();
        foreach(var position in contacts)
        {
            int i=_skidIndex++%_skids.Count; var mark=_skids[i];
'''+s[b:]
s=s.replace('_ghost=null; _camera=null; _undo.Clear();','_ghost=null; _camera=null;_workshopPan=Vector3.Zero; _undo.Clear();');p.write_text(s)
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.FreeConstruction.cs');s=p.read_text()
s=s.replace('if(file.StartsWith("scrap_wheel_"))SampleCycle(rig,"Roll",-_rollDistance/(MathF.Tau*(file=="scrap_wheel_large"?.64f:.4125f)));','''if(file.StartsWith("scrap_wheel_"))
            {
                float sign=Vector3.Dot(Vector3.Transform(Vector3.UnitX,Assembly.Parts[pair.Key].Rotation),Vector3.UnitX)<0?-1:1;
                SampleCycle(rig,"Roll",-sign*_rollDistance/(MathF.Tau*(file=="scrap_wheel_large"?.64f:.4125f)));
            }''');p.write_text(s)
