# Refinery and defence placement update

The editable refinery is `C:/Users/codex/Documents/palclone/SourceArt/MiningMachines/Refinery.blend`. The active Refinery Reference scene preserves separate mechanical parts; the earlier press scene remains in the source file. The runtime GLB contains only the refinery, with one Operate animation, 40 bones and approximately 9,500 triangles across eight materials.

The material path is now explicit: hopper/feed gate → processing chamber → actual rectangular opening through the right end plate → segmented moving conveyor → bulk bin. The bin centre and conveyor share the same centreline. The discharge lip extends 0.515 m inside the bin's near edge, leaving clearance above its rim. These changes address the blocked outlet and sideways bin position in the first Blender pass.

The feed gate, drive rotor, motor fan, rollers and 26 conveyor slats animate. The Mining Refinery Output component delivers completed batch visuals to a bounded ingot pile backed by actual Refined Ore. Selling cargo reduces the pile. It does not create an extra stock balance. Processing continues animating while output is still being delivered and stops afterward.

Output and bin marker names, conveyor speed, delivery/drop timing, visual budget, units per visible ingot, pile footprint, layer height, ingot size and colour are exposed on the component. The model uses OutputStart, OutputEnd and BucketFloor markers. Source-only reference ingots are excluded from the runtime GLB.

Defence HUD selection accepts either Alt key when the binding is Alt. Selecting an icon immediately disables HUD hit testing while returning to placement, preventing the same click from becoming a world shot. A selected build is retained through temporary cursor/capture transitions. The editor now shows a temporary UI cursor without releasing gameplay capture, and restores camera capture after selection without requiring an extra click. F1–F5 and B remain available.

Native checks cover the imported animation, moving belt/rotor, marker alignment and overhang, real batch delivery, bounded bin pile and reductions on sale; defence checks cover both Alt keys, selection/capture handoff, actual world deployment and shot suppression. An installed-editor check exercises the real Game View cursor release/restore path. Tests disable persistence. The asset and scene installers retain backups and asset GUIDs. No game export is performed.
