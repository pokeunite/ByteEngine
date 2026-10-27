# Sound effects plan — alien mining prototype

The soundscape should communicate a small industrial outpost waking a hostile planet. Machinery gives the camp a steady rhythm; enemy tells interrupt it. The important sounds are those that let the player act without checking a menu.

## Existing files to audition

The project currently has pistol/rifle/shotgun shot and reload files under `Assets/Audio/Weapons/First Hunt`, plus `core-hit.wav` and `armour-hit.wav` under `Assets/Audio/Combat Feedback`. Presence does not establish quality or correct binding. Reuse the pistol and impact recordings only after listening in the current third-person mix; the other handheld weapons are outside this pass.

## Priority 1 — combat readability and the core loop

| Cue | Trigger and purpose | Asset direction / variations |
|---|---|---|
| Pistol shot | Each accepted shot; communicates force | 4–6 short punchy variants, mechanical click plus outdoor tail |
| Pistol reload | Magazine out, magazine in, slide/bolt; follows actual reload stages | Separate short mechanical layers; interrupted reload stops remaining layers |
| Empty trigger | Attempt to fire with no ammunition | One dry click; rate limited |
| Enemy hit | Projectile hits flesh/carapace | 4 short soft-crunch variants; avoid excessive wet gore |
| Metal/rock impact | Missed shots strike the world | 3 variants per surface family |
| Player hurt | Actual player damage | 3 restrained impacts/grunts, with a short cooldown |
| Low health warning | Enters critical health | Subtle pulse loop, stops immediately on recovery; does not stack |
| Player death | Operator defeated | One short defeat sting, separate from enemy sounds |
| Hunter approach | Fast player-targeting enemy gets close | Spatial skittering/footfall loop, individual phase offset |
| Hunter attack tell | Begins committed attack wind-up | Sharp inhalation/chitter before damage; audible from behind |
| Raider approach | Drill-targeting enemy moving nearby | Heavier scraping feet, distinct from hunter |
| Raider drill strike | Tell then actual successful strike | Carapace knock / metal impact; damage sound only on a hit |
| Enemy death | Health reaches zero | 3 variants per role, short enough for crowds |
| Drill startup | Contract accepted | Motor ignition then spin-up into the loop |
| Drill running | Extraction active | Loop with clear beat, quieter at distance |
| Drill hit | Rig takes damage | Hard metal knock, independent of enemy vocal |
| Drill critical | Crosses critical drill health threshold | Local alarm plus brief priority cue; rate limited |
| Drill shutdown | Extraction finished, defeated or cancelled | Distinct spin-down; failure may add a mechanical stall |
| Wave incoming | Contract starts | Short warning horn and first approach rumble |
| Operation complete | Last enemy cleared and ore granted | Brief positive mechanical chime |
| Operation failed | Rig destroyed or operator defeated | Short failure cue; no duplicate sting on simultaneous deaths |
| Ammo production/ejection | Press cycles and rounds land in bucket | Servo stroke plus small metal clinks, pooled/limited |
| Ammo pickup | Player receives rounds from bucket | One compact pickup cue, aggregated per transfer |
| Refinery working | Processing batch | Low turbine/rattle loop while active |
| Refinery complete | Batch output awarded | Valve release plus short completion ping |
| Sale processing | Shipment transaction active | Very subtle pulses; avoid one loud coin per unit |
| Sale complete | Final unit paid | Short credit-confirmation sequence |
| Upgrade purchased | Credits successfully spent | Mechanical latch plus positive tone |
| UI select / confirm / close | Contract or upgrade navigation | Three restrained related cues; selection does not sound like purchase |
| UI unavailable | Invalid/locked action | Soft, brief error cue; no repeated buzz while holding input |

## Priority 2 — make the camp and enemies feel alive

| Cue | Use | Asset direction / variations |
|---|---|---|
| Alien planet wind | Quiet persistent atmosphere | Broad stereo bed, occasional gusts |
| Distant alien calls | Between attacks and during preparation | Rare spatial one-shots, never confused with immediate attack tells |
| Ground movement | Player footsteps and running | 6+ variants per used surface; speed changes cadence |
| Landing / gear movement | Player drops from height or changes pace | Small boot/cloth/tool layers |
| Hunter idle / alert / miss | Patrol, acquires target, misses attack | Three recognisably related vocal families |
| Raider idle / alert / dodge | Repositions or evades fire | Heavier insect-like chitter, short scrape during sidestep |
| Drill casing rattle | Adds life as extraction intensifies | Optional layer, not another full-volume loop |
| Ammo press idle / hopper full | Machine powered but not making rounds | Quiet hum; one full-bin notification |
| Refinery load / discharge | Raw input accepted and refined ore produced | Hopper clatter, chute discharge |
| Company tent ambience | Near the computer | Soft fan, electric hum, occasional relay |
| Terminal open | Interacts with company computer | Short boot/connect sound |
| Ore / supply pickup | Cargo or supplies received | Light tactile pickup family |
| Enemy emergence | Group enters from distant approach lanes | Distant scuttle/ground disturbance, not an in-camp spawn pop |

## Priority 3 — research defences and polish

| Cue | Use | Asset direction / variations |
|---|---|---|
| Defence placement / invalid placement | Deploy or reject a build | Metal set-down / restrained unavailable cue |
| Sentry tracking and firing | Target acquisition and accepted shots | Small servo + short autocannon bursts |
| Rotary gun motor / fire / stop | Sustained repeater use | Spin-up/loop/spin-down with bounded voice count |
| Mortar launch / incoming / impact | Artillery cycle | Thump, descending whistle, controlled explosion |
| Mine arm / trigger / explosion | Mine becomes active and detonates | Click, brief tell if gameplay permits, short blast |
| Supply delivery / landing / collect | Freight reaches camp | Descending motor/chute, impact, pickup |
| Projectile near pass | Shot passes close to player | Very occasional short snap; enemy projectiles only where appropriate |
| Market movement / contract hover | Optional UI polish | Extremely subtle; price graph should not beep every tick |
| Research milestone | Unlocks a defence or a major branch | Brief distinct success cue, not on every minor rank |

## Mix and implementation rules

- Make combat, machines, ambience and UI separate volume groups. UI stays non-spatial; world machinery and enemies use distance attenuation.
- Attack tells, player hurt and drill-critical warnings take priority over decorative ambience. Duck machine loops slightly when these cues play.
- Pool and cap voices: start with 8 enemy vocals, 8 impacts and one loop per machine. Limit nearby bucket clinks; aggregate mass pickups and credits.
- Randomise selection, pitch slightly (about ±4%) and avoid immediately repeating a variant. Do not randomise timings that communicate attack impact.
- No warning sound on a failed attack that never started. Stop attack sounds on death/interruption; stop machine loops on failure, scene unload or pause as appropriate.
- Distant attackers should sound distant. The player's pistol remains clear, but a crowd must not make every creature equally loud.
- Source/licence every imported file. Prefer CC0 or a purchased licence permitting redistribution in a game; keep attribution where required.

## First audio pass to build

Start with pistol/reload/impact quality, role-specific approach and wind-up sounds, drill startup/run/hit/critical/stop, operation success/failure, ammo ejection/pickup, refinery run/complete and UI confirm/error. Play one whole contract with music off before adding more layers.

Acceptance: with eyes briefly off the HUD, the player can recognise a nearby hunter winding up, the drill being attacked, critical health, the press finishing ammunition and the operation ending. The mix remains clear with the maximum allowed enemies alive.

This is an asset and integration checklist. New audio files are not added by this document.
