> Retired at the user's request. This contract is no longer in the active game; its source, scene and art are archived.

# Convoy Breaker — installed contract

Contracts → Combat → Convoy Breaker. Plugin 0.26.0.

Fit a seat, engine, wheels, a winch and at least one of the existing weapons. LMB fires the machine gun; F fires rockets. Winch keys remain editable on each winch block.

Intercept the six-wheel cargo truck and its two independently driven armed escorts. Disable the exposed engine before attaching to the cargo. Use rock cover or the sheltered bypass. Tow the disabled truck to the northern extraction yard and stop both vehicles inside it while the cargo remains secured.

- Cargo and escorts share the player's Bepu simulation: wheel motors, steering joints, contact, and towing remain physical.
- Defeated escorts stop driving and firing. The disabled cargo truck releases its wheel brakes so it can roll under tow.
- Enemy fire has range and terrain/prop occlusion checks, visible tracers, bounded effects and a rig-condition display.
- Rocket splash within six metres of the cargo damages its condition. A successful delivery pays between $1,210 and $2,200 according to remaining cargo health; losing the cargo or player rig fails the job instead.
- R restarts the encounter, including health, weak points, actors and hitch state. Quit returns to the garage. A completed contract pays once and returns automatically after the result card.
- The level's two-metre terrain grid aligns with eight-metre fine-sand tiles to avoid exposed gaps at the moving patch boundary.

## New Blender assets

Editable source: Documents/DuneCompany/Art/RaceKit/DuneCompany-Convoy.blend.

- Armoured cargo deck/body with locked crates, straps, wheel covers and an open engine area.
- Twin-barrel escort turret with bearing, receiver, cooling rings, shield cheeks and ammo box.
- Salvage road barrier with reinforcing feet and hazard stripes.
- Extraction sign, with lettering on both sides.

Each has a high mesh, reduced-distance mesh, shared material atlas and original editable Blender geometry. Existing rocks, containers, depot props and vehicle parts are reused. The level card is an actual game capture. The concept's large truck bodies and extensive industrial detail are simplified to match the game's modular vehicle style.

## Audio

New CC0 effects from Freesound, converted to compact mono WAVs; source/license notes accompany each file:

- Gunshot_002.wav by Brokenphono: https://freesound.org/people/Brokenphono/sounds/344142/
- Massive Explosion by qubodup: https://freesound.org/people/qubodup/sounds/741175/

## Validation

Private native fixture: actual wheel-driven convoy movement, machine-gun damage, engine disable, real winch towing, loss/retry cleanup, secured extraction, condition-based reward, duplicate-payout protection, automatic garage return and native rendering without OpenGL errors.

Private browser fixture: actual contract selection/deployment, asset uploads, two escorts, driving, weapon input, retry, quit and no browser/OpenGL errors. Browser completion/reward was not replayed end-to-end; native validation covers those rules. Headless web rendering remains slower than native, so this is not a frame-rate guarantee.

Project plugin, project PluginLibrary, engine PluginLibrary and project PluginSource synchronized. Release browser runtime rebuilt and copied to Dist and Debug editor. No distributable game export or upload was made.
