# Two combat contracts

Keep the machine gun and rocket launcher. No new weapon types.
The promise: build a recovery rig that can fight, create an opening, and escape with something valuable.
These are proposals, not implemented contracts. The generated images show visual direction; their truck bodies and industrial scenery require new assets.

## 1. Convoy Breaker

A stolen recovery truck travels with two escorts. Disable it without destroying the valuable load, hitch it, and bring it home.

- Layout: roughly 650 m S-shaped canyon road, 16 m wide encounter bends, a sheltered 12 m bypass, a 30 m turning yard at extraction. Start has a clear view of the approaching convoy and space to turn.
- Beat 1: intercept at the broad ambush bend. Machine gun disables exposed escort turrets or the target truck's engine. Rockets quickly eliminate an escort, but splash near the valuable truck damages its payout.
- Beat 2: the stopped truck blocks the easy route. Tow through the sheltered bypass; use a rock anchor if the heavy load pulls the rig sideways. Clearing the main road is another valid solution.
- Beat 3: one pursuing buggy forces a choice: keep moving with the load, take cover to fight, or briefly detach and reposition. No infinite spawning.
- Completion: deliver the target truck to extraction. Failure: target destroyed or player disabled. Reward varies with cargo condition; a fast retry restores the initial encounter.
- Player thinking: which threat to disable first, where to stop the target, explosive damage versus precision, front versus rear winch placement, and tow direction.
- First playable slice: one moving target, one escort, one bend and one extraction. Add the second escort and bypass after the recovery loop works.
- Playtest goal: two different successful approaches; the target remains salvageable; failures come from visible threats rather than sudden damage.

## 2. Last Load Out

A valuable disabled cargo truck sits at the bottom of an occupied quarry. Recover it through a defended exit.

- Layout: 100 x 140 m quarry floor, two elevated turret shelves, 14 m ramps, broad turning areas, and two exits: a short barricaded ascent and a longer sheltered switchback.
- Beat 1: survey from the safe entry ridge. Turrets telegraph their firing direction and have real blind spots behind rock and containers.
- Beat 2: descend, disable the turret covering the cargo, then hitch the truck. Machine gun offers precision; rockets remove threats quickly but splash can damage cargo.
- Beat 3: choose the short route by destroying the barricade with rockets, or tow through the longer sheltered route. A winch anchor supports a loaded ascent; neither path demands every optional tool.
- Escape twist: a single raider arrives after cargo starts moving, not during the initial survey. Rock cover and wide corners leave room to recover from a bad approach.
- Completion: cargo truck reaches the upper extraction depot. Failure: cargo destroyed or player disabled. Keep repairs/retry fast and understandable.
- Player thinking: route preparation before hitching, cover versus exposure, explosive ammunition versus cargo safety, rig torque and stability versus speed.
- First playable slice: one turret, one barricade, one towable cargo truck, both exits. Then add the second turret and escape pursuer.
- Playtest goal: players identify both exits, understand why cargo was damaged, and finish through either route without a hidden mandatory loadout.

## Shared implementation needs

- Independently controlled enemy assemblies in the same physics world.
- Disableable engine/turret targets rather than only whole-object hit points.
- Line-of-sight and cover checks against rock/props, not just terrain.
- Cargo damage, recoverable target state and extraction checks.
- Destructible barricade with an intact and broken version.
- Readable hit confirmation, incoming-fire direction, turret wind-up sound and cargo-condition display.
- Existing weapons currently auto-target and may fire automatically. Add appropriate target prioritization/control before claiming precise weak-point combat.

## Image generation

Built-in image generator; separate concept sheets for each mission.
Prompt direction 1: isometric desert S-canyon map with ambush, winch bypass and towing extraction; machine gun/rocket launcher/recovery buggy; beige, olive and orange game art.
Prompt direction 2: two-level quarry map with elevated turrets, cargo recovery, rocket barricade and sheltered switchback escape; same vehicle/material style.
Project images: Docs/CombatConcepts/Convoy-Breaker.png and Last-Load-Out.png.
