# Walking, enemy navigation and falling fireball

## Walking

Normal walk speed is **2.8 m/s**, up from 2.0. Both the starting Character Controller speed and the **Sprint released - walk** Event Sheet use 2.8. Sprint speed is unchanged.

The animation profile retains speed matching with a 2 m/s walk reference, producing **1.4x playback at full walking speed**. Idle, shooting/reload action playback and the existing sprint-strafe cap are preserved. Tune `Assets/Events/Assistant/Rule-ce213848d0654881b44c2dd2026f8cde.byteevents` for walking speed, and the locomotion speed-matching settings in `SyntyDirectionalLocomotion.byteanim` for animation rate.

## Enemy pathfinding

All 11 enemies now have **Use Navigation** enabled and a **Navigation Agent 3D** component. Native A* routes account for walls, cover and pillars rather than relying on direct movement and wall sliding. Agents update their destination as the player moves, repath at a 0.4-second interval, and collision-check movement. Charges remain locked straight-line attacks that stop at obstacles.

The director owns two authored regions:

- **Hunt navigation - courts** covers the lower courts at the ground height.
- **Hunt navigation - Crownback arena** covers the raised arena, using clearance for the larger boss.

Navigation excludes moving actors from the static bake. Nearby blocked start/target cells can snap to a walkable point, helping recovery after knockback. The hunt's Event Sheet explicitly rebuilds navigation after encounter barriers open. If you change static geometry, use the native **Rebuild navigation around current obstacles** action or restart Play to bake it again. Enable Show Debug on a region or agent to inspect surfaces and routes.

These are bounded navigation regions for this prototype; the boss is routed within its arena. Agents are not a full crowd simulation.

## Falling fireball

The violet caster marks the attack point, then drops a **1.6-size fireball from 12 metres above it** over a **1.15-second warning**. It targets the position chosen when the cast starts. The warning clears at impact, the explosion plays and the existing radius damage resolves. Killing or resetting the caster cancels an unfinished cast.

On the caster's **Simple Enemy AI**, edit Falling Fireball, Fireball Height, Fireball Size, Windup Seconds, Hazard Radius and Damage. Fireball Effect and Impact Effect are asset pickers. Edit the visual layers in:

- `Assets/VFX/Enemies/Enemy falling fireball.bvfx`
- `Assets/VFX/Enemies/Enemy fireball impact.bvfx`

The existing hunt and damage rules remain authorable through components and Event Sheets; no project-specific runtime controller was added.

## Verification

The combat regression suite passes, including A* pursuit around cover, recovery from an inflated obstacle cell, fireball descent, no early damage and damage at impact. In a runtime copy of the saved level, agents reached attack range around cover in both courts and around a boss-room pillar. Native rendering captured both the falling fireball and its explosion. Human playtesting is still needed to judge the new walking pace and combat feel.
