# Dune Company — three motorsport contracts

Installed plugin version: **0.23.0**. Game content remains in `C:/Users/codex/Documents/DuneCompany`. No distributable game export or itch upload is part of this update.

| Contract | Layout and rules | Reward |
| --- | --- | --- |
| Dust Bowl Drift | Quarry oval, central rock island and optional figure-eight. Two minutes to bank 6,000 points. Grounded forward slides build a multiplier; a clean exit banks the chain, a collision discards it. | $900 |
| Dead Straight | Two lanes over 400 m, a separate 160 m braking area and parallel return road. Animated launch tree; early throttle causes a 0.75 s launch delay. Independently controlled physical rival stays in its lane. | $1,200 |
| Red Rock Rally | Approximately 899 m, 32 m climb, broad opening sweeps, rock pass, controlled crest and upper hairpin. Six ordered blue gates; 227 separate AI waypoints. Rival and player share the physics world. | $1,500 |

The contract browser separates four recovery jobs from four motorsport events. Completion history and one-time rewards are independent for every contract. Results return automatically to the garage.

## Assets

20 props were built and corrected through the installed Blender MCP bridge: tyre barrier, loose tyre, cone, chevron board, route flag, finish gantry, launch tree, shipping container, workshop shed, service canopy, horizontal fuel tank, barrel, utility crate, conveyor, silo, service mast, three outcrop variants and dry bush. Manufactured parts have authored UVs, a shared weathered PBR atlas and separate distance meshes. The outcrops were remodelled from the existing CC0 Poly Haven rock scan in Blender; its photographed UVs and licensing are preserved.

Editable source and contact sheet: `Art/RaceKit/DuneCompany-RaceKit.blend` and `Art/RaceKit/RaceKit-contact-sheet.png` in the game project. Game models and road textures: `Assets/RaceKit`. Course heightmaps and packing masks: `Assets/ContractLevels`. Repeatable course authoring: `Tools/DuneCompany/author_motorsport.py`.

Road meshes and physical heightfields share the same sampled surface. These courses retain full terrain detail to prevent distant terrain triangles cutting through the road. Prop distance meshes, shared materials and mesh reuse remain enabled. Each prop submits its selected detail mesh directly in Edit and Play, avoiding six extra scene objects per prop. Native material sampling now uses anisotropic filtering when supported, keeping angled road markings legible.

The concepts guide layout, landmarks and palette; final engine rendering and the existing modular cars are not a pixel-identical reproduction of the generated illustrations.

## Checks

- Native build, drift scoring tests and all three contract integration checks passed.
- Straight driving, reversing, spins and airborne motion do not farm drift points. Banking, multiplier growth, collision loss and reset were checked.
- Actual native drifting earned points. The target/reward integration test separately feeds valid drift samples to exercise completion without a manual two-minute run.
- Physical rivals completed drag and rally. Drag peaked at 27.7 m/s; rally peaked at 18.0 m/s. Ordered gates, retry, one reward and garage return passed.
- Browser checks passed for all three: deployment, transitions, actual driving, drift points, race state, quitting and resizing. Browser full race wins remain a player playtest; native tests cover finish and payout logic.
- The private browser fixture initially omitted an upgraded engine's cooked model. Correcting all 120 model entries resolved that test failure. Failed vehicle/rival setup now rolls back to build mode instead of leaving a partial race running. Browser errors retain their managed stack for diagnosis.

Test evidence is under `.artifacts/motorsport-*.log`. The release plugin package and statically linked browser runtime must have matching Dune Company version and assembly hash. Installation synchronization includes both editor BrowserRuntime copies and all three plugin package locations.
