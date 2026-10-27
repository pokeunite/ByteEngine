# Animated machines and company progression

Installed in palclone with Modular Weapon Gameplay **3.20.0**.

## Models

The four models are original Blender-authored assets with distinct moving assemblies, solid-colour materials and modest polygon budgets. They replace the earlier scene primitives.

| Asset | Triangles | Animation |
|---|---:|---|
| MiningDrill | 2,718 | Rotating auger; only runs during extraction |
| AmmoPress | 1,786 | Rotating flywheel and cycling ram; runs until storage is full |
| Refinery | 1,846 | Processing drum and output motion; runs during a batch |
| CompanyTent | 1,783 | Subtle flap and antenna movement |

Total: **8,133 triangles**, excluding existing level/player assets. The first sparse blockout pass was replaced with more detailed assemblies, fittings, guide rods, brackets, service panels, pipes and structural reinforcement following user feedback.

- Editable sources: `SourceArt/MiningMachines/*.blend` in the game project.
- Game assets: `Assets/Models/Mining/*.glb`.
- All four export a two-second, looping **Operate** clip. Mechanical keyframes use linear interpolation to avoid repeated acceleration/deceleration of wheels and augers.
- Model previews are rendered in Blender; separate game screenshots verify the imported assets and UI.

To edit a machine, open its blend file, edit the mesh/materials while preserving rig bones and vertex groups, then export the mesh and armature to its matching GLB. Keep the clip name Operate or change the animation component's Operating Clip setting. Preserve the existing GLB metadata file so scene references stay valid.

In the scene, each imported model uses **Skeletal Mesh Renderer** plus **Mining Machine Animation**. The latter exposes the operation reference, workshop reference, machine kind, operating clip and playback speed. It starts/stops imported animation according to real production state. No armature or model-name guessing is used for gameplay.

## Access and market

Walk to the company tent's front interaction point and press E. Market and research screens both enforce proximity, including when opened through an Event Sheet action. There is no remote Tab binding. Leaving the allowed range closes the panel.

The market ticks continuously at its configured interval, even between operations. The quantity control is a native draggable slider with stock bounds. Current quotes, history, random state and update clock persist across sessions. Quotes are rounded to two decimals; transaction totals use decimal arithmetic and the displayed whole-credit total, preventing a floating-point rounding loss.

## Refinery

Three independent upgrade tracks:

- **Batch size:** +12 input ore per level by default; base batch 20, base cost 30 credits.
- **Processing:** base 3 seconds, divided by `1 + speed level × 0.2`; base cost 40 credits.
- **Recovery:** +0.10 refined ore per raw ore per level; base cost 55 credits.

Batch input and output are fixed when processing begins. Buying a new speed/yield upgrade affects the next batch. Company recovery research adds to the refinery recovery multiplier. Fractional output rounds down to whole items. Existing cargo and bought upgrades are preserved.

## Permanent company map

Ten nodes, with three branches and one cross-branch prerequisite:

| Node | Prerequisite | Cost | Permanent effect |
|---|---|---:|---|
| Company charter | None | 60 | +5% weapon damage |
| Calibrated tools | Charter | 100 | +8% weapon damage |
| Precision tooling | Calibrated tools | 220 | +12% weapon damage |
| Supply network | Precision tooling and Deep survey | 380 | +25% ammo production |
| Deep survey | Charter | 100 | +10% extraction yield |
| Ore recovery | Deep survey | 200 | +15% refinery output |
| Planetary survey | Ore recovery | 400 | +20% extraction yield |
| Field medicine | Charter | 90 | +15 maximum health |
| Power cells | Field medicine | 180 | +25 maximum energy |
| Field resilience | Power cells | 340 | +30 maximum health |

Effects stack additively within each research category. Nodes can only be bought once; prerequisites and funds are checked again when purchasing. Changes affect actual weapon damage, resource capacity, health, extraction or production. Older company damage levels remain intact.

The **Mining Workshop and Market** component exposes **Research Definitions Json**: stable ID, name, description, price, prerequisite IDs, effect, amount and map coordinates. These are data, not source-code branches. Duplicate IDs, missing prerequisites and cycles are rejected. Keep IDs stable after shipping a save. The current authored map has ten native button objects; when adding nodes, rerun the UI authoring tool to create matching native buttons and Event Sheet bindings (up to 40 nodes supported).

Event Sheets expose panel actions, node selection, purchase, refinery tracks and processing. The control sheet is `Assets/Events/Mining/Mining business controls.byteevents`.

## Verification and backups

252 regression checks and 39 native interaction/import checks passed. The native checks verify actual slider dragging, tentative/locked purchases, live price updates in preparation, tent access, refinery completion, every imported operating clip and a changing live auger bone pose. The installed editor's play/stop/unload check also passed.

Models, scene and plugin backups are retained. No game export or Git push was performed for this work.
