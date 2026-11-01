# Current explorer integration

The active Hazard Pay project is `C:/Users/codex/Documents/palclone/palclone.byteproject`. Player identity, events, inventory and save bindings are preserved.

## Active assets

- Model: `Assets/Characters/Explorer/Explorer.glb`, GUID `70728830-03aa-4a34-a9a3-c3b9452d5969`.
- Profile: `Assets/Animations/Explorer.byteanim`, GUID `45398fe8-98af-4d5a-a3d3-6d31acb642d4`.
- Sockets: `.byteengine/ModelSockets/7072883003aa4a34a9a3c3b9452d5969.sockets.json`.
- Source folder: `C:/Users/codex/Documents/Hazard Pay Explorer Turnaround`.
- Clean animated source: `Explorer-Animated.blend`. Current segmented authoring source: `Explorer-Refined-Authoring.blend`. Earlier source: `Explorer-Rigged.blend`. Static source: `Explorer-Turnaround.blend`.

The geometry is newly authored from the approved turnaround. The supplied Synty GLB provides 51 bone names/hierarchy and 76 clips, not the visible mesh. Joint translations and inverse binds are fitted to the explorer's proportions. Rotational samples are preserved except deliberate finger-curl adjustments in weapon clips. Source animation licensing still applies.

The game asset contains one skinned mesh with 11 material primitives. Soft suit regions have normalized blended weights; rigid equipment follows its intended attachment. Boot treads bind to the feet. Shoulder caps blend clavicle and upper-arm influences. The profile retains existing unarmed/SH/DH blendspaces and upper-body aiming/action layers. Turn-in-place stays disabled. The capsule is 2 m high; camera pivot is 1.58 m.

## Pistol

`PistolGrip` follows `Hand_R`, without inheriting bone scale. Its offset is fitted around the new palm, with socket scale one. The weapon definition chooses this named socket; no new hard-coded runtime attachment rule was added. It remains editable in the model socket editor. Existing inactive rifle/shotgun sockets are retained, but this pass visually fits the active pistol.

## Evidence

- Reopened the final GLB in Blender and sampled 19 representative clips at five times, including locomotion, strafes, firing, reloads, two-handed poses and death: 95 samples. Source folder contains reports and renders.
- `MiningPrototype.exe --verify-explorer-character`: 123 checks across seven clips, including native import, finite pose bounds, default pistol, animated grip and usable muzzle. Results and runtime posed geometry: `artifacts/explorer-native`.
- `MiningPrototype.exe --verify-third-person`: 28 checks covering movement, aiming, hitscan/projectile hits, menu control restoration and vertical torso aiming.
- Rendered actual runtime hand/pistol geometry for visual attachment review, rather than relying only on socket lookup success.

Eleven unreferenced old character models/profiles and their metadata were removed from the project asset tree after reference scanning. Recoverable copies and a manifest are in the source folder's `Archived-Game-Characters`. Original downloads were retained.

No game export was performed. Existing exported builds require a separately authorized export to include these project asset changes.

## Surface refinement

The installed explorer now uses rounded suit and glove contours, smoother helmet/visor normals, softened boot forms and bevelled equipment edges. Backpack vents, fasteners and a pressure gauge add construction detail beside the camp machines. Custom corner normals preserve hard equipment edges while smoothing curved surfaces. The fitted skeleton, animation profile, player identity and pistol socket are unchanged.

The refined model retains 51 bones, 76 clips and 11 material primitives. It has about 39,000 exported vertices and 59,500 triangles: fewer split skin vertices than the previous export, but more triangles for the curved contours. This is a visual refinement, not a measured performance improvement. Recoverable earlier files are in `Surface-Refinement-Backups` in the source folder.

Native camp screenshots are `artifacts/explorer-native/style-before.png` and `style-after.png`. The refinement passed 123 native animation/attachment checks and 95 Blender pose samples. The mesh uses material colours without UV textures.
