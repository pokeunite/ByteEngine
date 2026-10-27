# Metro station and zombie setup

The game project is `C:/Users/codex/Documents/palclone`. Open `palclone.byteproject`; the station is in `Scenes/Main.bytescene`. The blockout follows the approved architecture-only image using native editable primitives. The image was a concept, so the scene uses explicit measured dimensions and physically connected openings rather than copying perspective distortions.

## Station
Entry safe room -> ticket hall -> platform -> utility crossing -> pump through-route -> electrical hall. The medical waiting room and dogleg storage room are optional. The upper platform passage rejoins at both ends. The bottom return passage opens after restoring power. Water recesses are protected by railings. No enemies have been placed in the scene.

Main floors are at Y=0; entry and return floors are at Y=-0.8. Four 0.2-metre steps have actual collision at each transition. All walkable floor slabs use layer 20. Keep this floor layer when moving/duplicating slabs so the navigation region can sample their heights. Water and empty space have no walkable floor on that layer.

The generated hierarchy is under **Metro Station**. Player, weapons, main HUD and combat feedback remain separate. A scene backup is made before each authoring pass under `.byteengine/Backups/metro-build-*`.

## Interactions and rewards
Open `Assets/Events/Metro/Station interactions.byteevents`.
- E near the breaker opens the return shutter, activates sector lights, rebuilds navigation and changes the objective text.
- E near the green waiting-room cache heals 35 HP once.
- E near the deeper storage cache adds 24 rounds to the existing pistol once.
All distances, keys, rewards, targets and actions are editable in that sheet. The cache objects and breaker displays are parented so collection removes their whole visual. The prototype cache uses the project's pistol object; change that target if replacing the pistol.

## Place a zombie
1. Find `Assets/Blueprints/Enemies/Zombie Shambler.byteblueprint` in the asset browser.
2. Drag it onto a floor and place its root at the floor's top surface: Y=0 in most rooms; Y=-0.8 in the lower entry/return passage. Duplicate as required.
3. No player ID or navigation-region ID needs assigning. The blueprint finds a character and the enabled scene Navigation Region automatically.
4. Enter Play. A zombie wanders within about 3 metres of its placed start, sees nearby players, investigates gunshots, searches remembered positions and attacks with a warning and recovery.

The model is an intentionally simple green block humanoid placeholder; animation and finished character art are not included. Replace the visual children later while retaining the root components.

## Editable components
- **Health:** 65 HP, destroys actor on death.
- **Capsule Collider:** radius 0.35, height 1.8, feet-based root.
- **Navigation Agent:** collision avoidance enabled; automatically finds region; behaviour owns movement.
- **Simple Enemy AI:** Behaviour Controlled ON; old autonomous update is disabled. Native Rusher-style melee executor is used for a close strike, not a charging attack. Damage 12, wind-up 0.65 s, recovery 0.85 s and cooldown 1.1 s. Predatory Rush is OFF.
- **AI Behaviour:** `Assets/AI/Metro/Zombie Shambler.byteai`; sight 10 m, retained sight range 14 m, FOV 220 degrees, hearing cap 24 m, wall attenuation 0.3, memory/search 7 s, three simultaneous attackers per shared Metro infected squad.

Edit the **Shamble toward visible player**, **Investigate last sound or sight position** and **Wander close to placed starting position** tasks for movement speeds. Wander defaults to 0.65 m/s; pursuit 1.65; investigation 1.8. The Wander task exposes radius, speed, destination interval and memory key in the AI editor.

The shared `Assets/AI/Metro/Zombie attack.byteai` contains reserve -> choose -> telegraph -> commit -> strike -> recover. Keep that sequence together. Actor death stops/removes its collision and behaviour. All asset settings are data; no dispatch uses enemy names.

## Sound and placement
Gunshot noise already comes from weapon definitions via the shared AI hearing system. Actual audible radius is limited by both the weapon's Noise Radius and the zombie's Hearing Range. Walls attenuate it. Infected remember the sound position rather than continuously tracking the player through walls. Do not place every group within one weapon's unobstructed audible radius. Put the first group in view and a neighbouring group just beyond a doorway to teach the consequence of firing.

## Verification
Native runtime checks cover room-to-room paths, both floor heights, open and closed return routes, physical player stair ascent/descent, blueprint instantiation, local wandering, sound-position memory, telegraphed melee damage, death, and actual power/cache action execution. Test instances are spawned only in a temporary scene copy and never saved into Main.
