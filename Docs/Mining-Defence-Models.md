# Rigged mining defence kit

The five approved concepts have been recreated as editable low-poly mechanical models. The source matches their visible silhouettes, yellow/graphite palette, stabilizer legs, receiver armour, sensors, hollow barrels and supply-case details. The reference renders do not specify unseen surfaces or exact dimensions; those are reconstructed. Surface wear in the concept images is not reproduced as a painted texture.

## Files

Source: `C:/Users/codex/Documents/palclone/SourceArt/DefenceModels/DefenceKit.blend`.

Animated GLBs: `C:/Users/codex/Documents/palclone/Assets/Models/Mining/Defences/`.

Blender opens on **Defence Collection**. Choose **Sentry inspection**, **Repeater inspection**, **Mortar inspection**, **Mine inspection** or **SupplyCache inspection** to inspect one asset. Select its Rig, then choose a named action in the Action Editor. Clips are also retained as muted NLA tracks; normal source playback uses the active action. The meshes remain separate and named by mechanical assembly. Exports merge the geometry into one skinned mesh with rigid, full bone weights.

The original Blender Scene is preserved. A verification scene retains the reimported GLBs for comparison, hidden from renders.

| Model | Triangles | Bones | Clips |
|---|---:|---:|---|
| Sentry | 8,440 | 17 | Deploy, Retract, Idle, AimSweep, Fire |
| Repeater | 9,260 | 18 | Deploy, Retract, Idle, AimSweep, Fire |
| Mortar | 7,524 | 17 | Deploy, Retract, Idle, AimSweep, Fire |
| Mine | 1,236 | 6 | Deploy, Retract, Idle, Arm, Trigger |
| SupplyCache | 2,212 | 5 | Deploy, Retract, Idle, Open, Close |

Animation names are prefixed with the model name, for example `Mortar_Fire` or `SupplyCache_Open`. Source frame rate is 30 fps. Turret clips include stabilizer folding, scan/aim pivots and recoil. The mortar recoils along its inclined barrel. The mine has moving anchors, an arming ring and a trigger crown. The cache has a hinged lid, moving clasp and a modelled interior.

The mortar correction adds a continuous bolted mounting bridge over the swivel, overlapping yoke feet and aligned side bearing pins. Both support arms connect to this bridge; the assembly rotates together on the yaw pivot. The launch tube has an actual open bore rather than a capped front collar.

## Validation and integration status

All five GLBs passed structural checks for finite animation keys, ordered times, normalized weights, valid joint indices and exactly five correctly scoped clips. Reimporting them into Blender confirmed real mesh deformation for deployment, firing, aiming, mine activation and lid opening. ByteEngine's native importer loaded every material section, skeleton and clip with valid node bindings.

The five models are now assigned to the in-game Defence Placement component and replace the procedural visuals for newly placed and restored saved defences. Plugin version 3.35.0 drives deployment, turret tracking, recoil, mine arming/activation and supply-cache opening. Muzzle markers provide shot and mortar origins. The turret rotates its Yaw/Pitch bones while support feet retain their placement rotation. No game export was made.

The planner exposes five model asset pickers and Defence Model Scale. Each live Company Defence exposes its model, scale, animation prefix, clip names, yaw/pitch/muzzle bones, visual-ready status and errors. Event Sheets expose Play defence animation and Animated defence model is ready under Company Defences. Saved defence identities, licences and placements remain in the existing save format.

Native scene checks cover all five model replacements, loaded skeletal meshes, actual combat firing and damage, fixed feet during target tracking, delayed mine removal, single supply collection and post-opening cleanup. The installed editor play check and existing gameplay regression executable passed.

Build source, model manifest, previews and verification records accompany the Blender file. `BuildDefenceModels.py` is the authoring script executed through the user's Blender MCP; `StageDefenceWorkshop.py` creates the inspection scenes. Do not rerun the generator over manual refinements without a backup.
