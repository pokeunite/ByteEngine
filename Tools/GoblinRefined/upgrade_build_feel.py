from pathlib import Path
p=Path('Engine/ByteEngine.Core/Construction/VehicleBuilder3D.cs');s=p.read_text().replace('public sealed class VehicleBuilder3D','public sealed partial class VehicleBuilder3D')
s=s.replace('VehicleBuildLayout.Mounts','Layout.ActiveMounts')
s=s.replace('public static (Vector3 Position, Quaternion Rotation) PartPlacement(int mount, string part)','public static (Vector3 Position, Quaternion Rotation) PartPlacement(int mount, string part, string chassis="scrap_frame_2x1")').replace('Vector3 target=Layout.ActiveMounts[mount].Position','Vector3 target=VehicleBuildLayout.GetMounts(chassis)[mount].Position')
s=s.replace('private static void ApplyPlacement(', 'private void ApplyPlacement(').replace('PartPlacement(mount,part);','PartPlacement(mount,part,Layout.Chassis);').replace('PartPlacement(pair.Key,part);','PartPlacement(pair.Key,part,Layout.Chassis);').replace('PartPlacement(21,part);','PartPlacement(21,part,Layout.Chassis);')
for start_marker,end_marker in [('    private void CreateHud()','    private GameObject HudObject'),('    private void ChangePalette(','    private void CreateSkidPool()')]:
 start=s.index(start_marker);end=s.index(end_marker,start);s=s[:start]+s[end:]
s=s.replace('        _palettePage=index/14; RefreshPalette();', '        if (VehicleBuildLayout.PartFiles[index]=="scrap_frame_long") { SetChassis("scrap_frame_long"); return; }\n        _category=Array.FindIndex(Categories,g=>g.Contains(index));\n        _palettePage=Array.IndexOf(Categories[_category],index)/6; RefreshPalette();')
s=s.replace('            if (Input.IsKeyPressed(Key.Z)) ChangePalette(-1);', '            bool control=Input.IsKeyDown(Key.LeftControl) || Input.IsKeyDown(Key.RightControl);\n            if(control && Input.IsKeyPressed(Key.Z)) UndoBuild();\n            else if (Input.IsKeyPressed(Key.Z)) ChangePalette(-1);\n            if(control && Input.IsKeyPressed(Key.Y)) RedoBuild();')
s=s.replace('            _orbit +=', '''            if (!PointerOnHud() && Input.IsGameViewHovered)
            {
                if(Input.IsMouseButtonDown(MouseButton.Middle))
                { var delta=Input.GameViewMouseDelta; _orbit-=delta.X*.008f; _elevation=Math.Clamp(_elevation+delta.Y*.006f,.22f,1.1f); }
                _zoom=Math.Clamp(_zoom-Input.Snapshot.MouseWheel*.6f,4,15);
                HoverMount();
            }
            _orbit +=''')
s=s.replace('        GameObject visual=CreateModelVisual', '        RecordEdit();\n        GameObject visual=CreateModelVisual')
s=s.replace('        if (!Building || !Layout.Remove(mount)) return false;', '        if (!Building || !Layout.Parts.ContainsKey(mount)) return false;\n        RecordEdit(); Layout.Remove(mount);')
s=s.replace('        if (AttachPart(_mount,SelectedPart))', '        if (PlaceAtSelected())').replace('        else _message="Choose an empty compatible mount.";', '        else _message=Layout.PlacementIssue(_mount,SelectedPart);')
s=s.replace('        if (Layout.CanPlace(_mount,SelectedPart)) PlaceSelected();\n        else _message="Mount occupied. Remove its part first.";', '        PlaceSelected();')
s=s.replace('        if (Layout.CanPlace(_mount,SelectedPart))\n        {\n            _ghost=', '        if (Layout.ActiveMounts[_mount].Parts.Contains(SelectedPart))\n        {\n            _ghost=')
s=s.replace('            if (_mount==21) PositionRoofPayload(_ghost,SelectedPart);', '            if (_mount==21) PositionRoofPayload(_ghost,SelectedPart);\n            TintGhost(Layout.PlacementIssue(_mount,SelectedPart));')
s=s.replace('            _markers[i].Active=Layout.ActiveMounts[i].Parts.Contains(SelectedPart);', '            _markers[i].Transform.LocalPosition=Layout.ActiveMounts[i].Position;\n            _markers[i].Active=Layout.ActiveMounts[i].Parts.Contains(SelectedPart);')
s=s.replace('new Vector3(.17f)', 'new Vector3(.10f)')
s=s.replace('Vector3 offset=Building ? new(MathF.Sin(_orbit)*7,4.6f,MathF.Cos(_orbit)*7)', 'Vector3 offset=Building ? new(MathF.Sin(_orbit)*_zoom*MathF.Cos(_elevation),_zoom*MathF.Sin(_elevation),MathF.Cos(_orbit)*_zoom*MathF.Cos(_elevation))')
s=s.replace('            foreach(int mount in Layout.Parts.Keys.ToArray()) RemovePart(mount);\n            foreach(var p in loaded.Parts) AttachPart(p.Key,p.Value);', '            RecordEdit(); RestoreLayout(loaded);')
start=s.index('    private void RefreshHud()');end=s.index('    protected override void OnStop()',start);s=s[:start]+s[end:]
s=s.replace('        _ghost=null; _camera=null;', '        _ghost=null; _camera=null; _undo.Clear(); _redo.Clear(); _cardImages.Clear(); _cardLabels.Clear(); _categoryButtons.Clear(); _part=0; _category=0; _palettePage=0;')
p.write_text(s)
