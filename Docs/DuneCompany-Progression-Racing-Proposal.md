# Dune Company: challenge, upgrades and racing

11 October 2026. Category implemented; the mission changes, paid parts and AI below are proposals, not installed gameplay features.

## Contract organization

The four existing jobs belong to **Recovery Operations**: Stranded in the sand, The Well Ate My Truck, Down With the Patient and No Brakes. Available and Completed remain the primary tabs. A category heading and job count appear inside either tab. Completing a job moves that job into Completed; selecting a category or contract never deploys it.

Future racing contracts can have a separate **Desert Motorsport** category once there are actual races. Do not add empty categories now.

## No Brakes: harder decisions, fair handling

Keep the longer 64 m runway, wider upper bend, independent winches, recoverable rolls and instant retry. The player has now learned the catch. Increase the need to control the load after hooking it rather than undoing the fairness fixes.

Recommended first prototype: an offset escape exit with two staggered rock islands. Retain generous road width and recoverable shoulders. The player must steer the coupled vehicles into the first turn, manage cable length while the cargo swings, and countersteer into the second turn before stopping in the yard. Holding both reel keys from the start should make the turn awkward, not automatically win.

Three beats:

1. Catch: the long straight still gives enough time to match speed and connect.
2. Redirect: the escape entrance is offset from the truck's straight downhill trajectory. The anchor helps redirect it, but parking and reel timing affect the swing.
3. Settle: a gentle reverse bend near the braking yard rewards paying out briefly and taking a wider line. Recovery remains possible after a poor entry.

Do not secretly change grip, add random failures, shorten the hook window or make upgraded winches mandatory. Rock placement should visibly explain the path; driving and cable feedback should explain mistakes. No extra tutorial solution text is needed.

If this still feels easy, test a visibly higher cargo stack/center of mass as the next independent change. It makes sharp corrections risky. Do not introduce chassis damage and cargo damage simultaneously before the route change is tested.

Keep the current mission as a standard configuration while testing the revised route in an isolated fixture. If the advanced route becomes unforgiving, offer it as a bonus medal/objective rather than taking away the fair baseline.

Playtest checks: repeat the player's simple successful strategy, confirm it now needs a correction; complete with stock parts; recover a bad swing; inspect both keyboard winch layouts; test native and browser. Aim for an interesting first failure that teaches a better line, then a successful second or third attempt. Do not infer difficulty from automated yard-completion tests.

## Money: own better parts, choose a specialization

Keep current basic parts available. Buy physical advanced parts for a persistent workshop inventory, not temporary boosts or mandatory fuel. A purchased part can be removed and reused between builds. Four upgraded tyres require four owned tyres; duplicating a block does not duplicate purchased stock.

The shop shows the actual difference, weight, quantity and cost before purchase. Blueprint loading reserves owned parts and explains shortages; switching builds returns reservations. Never silently delete a build or charge again. New Game explicitly resets money and owned upgrades after confirmation.

Initially allow full-price sell-back so players can change their mind. Keep all current recovery jobs solvable with starter equipment. Their total first-time rewards are $3,400; the current completion model should not be assumed to provide repeat farming. Avoid permanent purchase mistakes that can strand the player without access to the equipment they need.

These are starting balance targets, relative to default settings, not measured final specifications:

| Part | Proposed benefit | Tradeoff | Proposed price |
| --- | --- | --- | --- |
| Low-gear engine | Around 40% more low-speed drive torque than compact engine | Around 25% lower speed cap; about 15 kg extra | $450 each |
| Long-line winch | 45 m spool versus current 24 m; hook reach 18 m versus 12 m where line of sight is clear | Around 20% slower reeling, about 10 kg extra; no pull-force increase | $650 each |
| Power winch | 24 kN pull cap versus stock 12 kN and current tuned maximum 18 kN | Around 20% slower reeling; about 20 kg extra; standard-length cable | $900 each |
| Sand tyre | Wider footprint; target around 25% less loose-sand rolling resistance | More rotating mass and poorer hard-ground cornering | $125 each / $500 for four |
| Rally suspension | Around 35% more usable travel at equivalent settings; better controlled damping | Taller/heavier assembly can raise center of mass | $175 each / $700 for four |
| Race engine | Higher usable speed cap and modest drive increase; aimed at the compact-engine class | Less suitable low-speed gearing than low-gear engine; heavier than compact | $1,200 each |

The existing heavy engine already provides more nominal drive than the compact engine. The proposed engines should create different gearing/speed choices rather than merely duplicate that part.

Traction upgrades must change terrain contact behavior, not just a global grip multiplier. Raising torque alone should not magically solve a poor tyre choice. Part-specific winch settings need to survive save/load and event tuning; existing hard-coded type checks and shared limits must be generalized before adding these catalog entries. Price comparisons must use a consistent baseline, including existing tunable limits.

Recommended first shop prototype: low-gear engine, sand tyres and long-line winch. Build the ownership/reservation/save logic once. Add power winch and racing parts after that prototype works. The concept image shows all six proposals; it is reference art, not meshes or shipped inventory icons.

## First racing content

Start with one opponent and one short circuit, not a full championship.

**Scrapyard Sprint:** about a minute around a dry riverbed with a long straight, one loose-sand corner and a rough shortcut. The shortcut saves distance but can bounce a fast, low-clearance car. The player chooses a fast light build or a stable off-road build.

Later variations:

- **Canyon Duel:** side-by-side bends and an optional wider outside line. Stability and overtaking decisions matter more than top speed.
- **Heavy Haul Hustle:** both competitors tow matching trailers. Suspension, braking distance and tow stability change the best route.

Use ordered checkpoints and physical crossing tests. The UI shows race position, lap/checkpoint and time. Difficulty changes the opponent's equipment, pace margin, reaction time and risk-taking, rather than invisible speed boosts. No rubber-band acceleration in the first prototype.

## Reusable engine AI: adapt UE5's separation of responsibilities

The reviewed UE5 AIController interface has separate brain, perception and path-following responsibilities. StateTree can provide component-based state/task logic. Its path-following interface requests movement through a movement abstraction. ByteEngine can use those boundaries without adopting Unreal's class hierarchy or copying its implementation.

Keep the default user flow: **Add Component > AI Agent > Racer**. Pick a route and a difficulty preset. Advanced controls are collapsed. The agent is the visible facade; route, steering and motor responsibilities remain separate internally. Existing Byte Blueprints save the configured vehicle. Existing events react to AI state and race signals.

| Engine piece | Responsibility | Reuse |
| --- | --- | --- |
| AI Agent / brain | Own goal/state; start, pause, stop and cancel tasks; expose active state and reason | Racer, patrol NPC, chasing enemy, delivery vehicle |
| Route Path | Editable spline/waypoints, direction, corridor width, speed zones and ordered gates | Race circuit, patrol, convoy, scripted transport |
| Path follower | Progress, look-ahead target and desired speed; report arrived/blocked/off-route | Authored vehicle routes; navmesh routes through an adapter |
| Steering / recovery task | Curve-aware braking, obstacle sensing, local avoidance and stuck recovery | Vehicle drivers with different handling |
| Motion adapter | Convert intent into the real character/vehicle motor | CharacterController3D, general rigid body or game-plugin vehicle |
| Optional perception | Nearby objects, opponents or sight/hearing targets | Racing avoidance and later FPS enemy behavior |

Data flow: goal/state -> route/path target -> desired motion -> motor input -> physics. Feedback flows back as progress, speed, blockage and task completion. The AI must not also write a dynamic vehicle's Transform.

For a racer, use authored routes first; a walking navmesh path is not a safe racing line. Choose a look-ahead distance that grows with speed, estimate upcoming curvature and braking distance, and cap speed before corners. Steering uses heading and sideways-error feedback with rate limits. Add a small amount of lane offset for overtaking only where the corridor is wide and obstacle sweeps are clear.

Corrections:

- Off route: project onto a nearby reachable segment consistent with the last checkpoint; aim ahead of that projection, not behind the vehicle.
- Hairpin/crossing: bound segment search to prevent jumping to a nearby but wrong section of track.
- Obstacle: compare a few reachable lane offsets using vehicle-sized collision sweeps; brake if none is safe.
- Stuck: low progress over a time window -> brake -> controlled reverse -> steer back onto the corridor -> resume. Limit attempts and report a blocked state.
- Airborne: suppress aggressive steering and avoid treating a jump as being stuck.
- Upside down: request the game's recovery rule; do not silently teleport forward. If reset is enabled, return behind the last valid checkpoint with a visible penalty.

Suggested states: Waiting -> Racing; Racing -> Recovering when stuck/off route; Recovering -> Racing or Blocked; Racing -> Finished. Events expose StartRace, PauseAI, SetRoute, SetGoal, OnStateChanged, OnStuck, OnCheckpoint and OnFinished. Future non-racing agents can use a small Inspector state table or the existing event sheet; no mandatory new graph editor.

Run expensive decisions at a bounded rate (for example 10 Hz) and steering at a suitable fixed rate; the motor participates in the physical simulation tick. Stagger sensors across many agents and profile in browser. Expose the actual goal point, corridor, brake point, route segment and recovery reason in debug view.

## Important Dune Company integration work

Current LandVehiclePhysics explicitly supports a player assembly plus one recovery target. AddRecoveryVehicle sets the target's drive power to zero. It is not already a multi-racer motor API.

Before racing, introduce assembly handles and per-assembly drive inputs inside a shared vehicle physics world. Player and opponent must occupy that same world so their cars can collide, block, overtake and interact with terrain. Creating one isolated physics simulation per AI car would hide a serious collision problem. Keep the existing recovery-target API as a compatibility wrapper; regression-test all four contracts and winches.

Keep reusable AI/path/task types in ByteEngine.Core. Put the adapter for Dune Company's specialized motor in its plugin. The reusable core must not reference Dune part IDs, credit files or mission names. A Race Director owns countdowns, ordered checkpoints, ranking, laps, restart and finish conditions; an AI brain does not decide that it won.

Build order:

1. Shared multi-assembly motor API, keeping existing recovery behavior.
2. One waypoint/spline route, one AI driver, one player; actual motor control and collisions.
3. Curvature braking, corridor corrections and recoverable stuck handling.
4. Race Director, checkpoints and simple HUD.
5. Validate existing contracts, then add overtaking and difficulty presets.

Acceptance checks: finish repeatedly at 30/60/144 render FPS; brake for corners; recover from a deliberate shove; avoid parked vehicles; do not skip crossed/hairpin segments; stop on cancellation; no double transform ownership; measure native and WebGL timing. Repeat on loose sand and with changed tyres/suspension. This is a proposed architecture and test plan, not a claim that UE5 supplies a ready-made vehicle-racing AI.

## UE5 references

Source files downloaded under `D:/ue4 sources/UnrealEngine-5.8`, pinned to `4dced763583b8fa05d214b133c2c21b73da300d4`:

- [AIController responsibilities](https://github.com/EpicGames/UnrealEngine/blob/4dced763583b8fa05d214b133c2c21b73da300d4/Engine/Source/Runtime/AIModule/Classes/AIController.h)
- [Path following and movement interface](https://github.com/EpicGames/UnrealEngine/blob/4dced763583b8fa05d214b133c2c21b73da300d4/Engine/Source/Runtime/AIModule/Classes/Navigation/PathFollowingComponent.h)
- [StateTree AI component](https://github.com/EpicGames/UnrealEngine/blob/4dced763583b8fa05d214b133c2c21b73da300d4/Engine/Plugins/Runtime/GameplayStateTree/Source/GameplayStateTreeModule/Public/Components/StateTreeAIComponent.h)
- [Epic StateTree quick start](https://dev.epicgames.com/documentation/unreal-engine/statetree-quick-start-guide)

Generated concept: `Docs/Concepts/DuneCompany-Upgrade-Parts.png`. Built-in image generation; six-panel concept sheet in the game's beige/olive/orange industrial palette. Prompt asks for low-gear engine, long-line winch, power winch, sand tyre, rally suspension and race engine, isolated in consistent isometric views, labeled as concepts rather than final assets.
