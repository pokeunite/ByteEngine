# Assigned company terminal and massive drill

Both new models are assigned in palclone's Main scene:

- Animated drill model → Assets/Models/Mining/MiningDrill-Massive.glb.
- Company tent → Assets/Models/Mining/CompanyTerminal.glb.

Editable sources are in SourceArt/MiningMachines. The terminal blend preserves separate mesh parts, packed canvas textures and the earlier machine scenes. The approved reference determined the visible design; tiny labels, surface wear and unseen construction are simplified.

The tent has olive canvas, a yellow frame, rolled front door, upgrade and market displays, keyboard, trackball, radio, computer tower, cables, warm worklight and supply crate. Its four-second Operate loop adds subtle idle motion. The world display graphics are presentation; E opens the existing live company upgrade and market interfaces.

The massive drill keeps its full authored scale, long auger and enlarged hopper. Its existing operation, health and enemy target bindings are retained. Version 3.33.0 measures interaction range from the drill compound body, allowing E from every reachable side. The earlier control-cabinet anchor remains authored for reference; the prompt no longer depends on standing next to it.

Collision follows the drill's base sectors, central shaft, hydraulic columns, feet and hopper. The open front remains available for approach. The tent has side/rear walls, entrance posts, desk/screen/crate collision and a walkable mat. A low-cost unshadowed point light illuminates its interior. Enemy navigation is baked from these authored colliders at scene load.

Assets and scene assignments were backed up under .byteengine/Backups. Gameplay verification disables persistence and preserves authored scene and company progress. No game export is performed.
