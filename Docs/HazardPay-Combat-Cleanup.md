# Hazard Pay combat and progression cleanup

Plugin: Modular Weapon Gameplay 3.52.0. Active project: `C:/Users/codex/Documents/palclone`.

## Installed behaviour

- Directional blend inputs use a 0.10-second local-velocity filter and a small forward dead zone. Brief lateral velocity noise no longer immediately blends into a strafe; intentional sustained lateral movement remains supported. `AnimationController.DirectionBlendSmoothingSeconds` is editable on the component.
- Rearrange mode displays defence reach. Select a defence for exact damage/range. A turret's cone indicates its facing, while its outer ring shows the full 360-degree tracking range. Mortars also show full targeting reach; mines show their trigger and blast rings. `ShowDefenceRanges` and `RangeConeDegrees` are on Camp Layout.
- Defences have real health, default 100, editable as Company Defence / Maximum Health. Enemy-destroyed defences play the explosion effect, leave the scene and retain a replacement entitlement. Placement spends 20 refined ore; the planner's `ReplacementOreCost` controls that price. The ready HUD displays the ore cost. Replacement requires preparation phase.
- A normal mine detonation records its position and yaw separately from destruction. It re-arms at that location for the next operation. Enemy-destroyed mines require replacement ore. Retired placement state is included in the existing defence save, without changing its filename.
- Drill raiders check nearby live defences within six metres, retaining committed attacks before reconsidering targets. Hunters keep their existing player target. The new Drill Raider Priorities component exposes notice radius, reach and reconsideration interval; operation settings configure spawned raiders.
- Drill attacks use an invisible contact target on the physical collider surface. That target forwards damage only within actual surface melee reach. It is not a second drill health pool. All drill contacts share the drill's squad attack-slot budget through `AttackReservationTargetId`; different defences keep independent budgets. The editable Raider AI asset stops one metre from the contact target. Native staged melee no longer adds an undocumented quarter-metre reach allowance.
- The ammo bucket's authored pickup radius is 4.5 metres. Its existing bounded bullet flights show the collection; inventory credit, reserve capacity and flight expiry remain unchanged.
- Mine blast and Mortar impact assets use the new reusable `ToonPuff` sprite, warm expanding layers, smoke and a shock ring. Previous outer size and component size multipliers are retained. Both `.bvfx` assets remain editable; no baked scene-only effect was added.

## Contract target

Mining Operation exposes `TargetEnabled`, `TargetCredits` (30,000), `TargetPlaySeconds` (18,000), `TargetDays` (10) and `TargetFont`. The HUD is top-right, and hides while arranging camp to avoid the Done button.

The goal is credits currently held, not lifetime sales. Spending slows the meter. The clock starts when the first operation successfully launches, advances during active play (including ordinary machine panels), stops in pause/settings/main menu, and does not advance offline. Ten displayed days equal five active hours. Elapsed time and the terminal result are saved in the company progress file. Reaching the goal completes the contract; missing the deadline ends it. A new game starts another contract.

Event Sheet entries added under Mining: set credit target/deadline, target reached, deadline expired, set ammo pickup radius, re-arm mines, and selected defence needs replacement ore. New component tuning is also inspector-editable.

## Upgrade proposal — not applied

Open `Docs/HazardPay-Upgrade-Review.html` locally. Select nodes to read exact effects/prerequisites and edit prices. Use Round Model to inspect cash flow, purchases and expected upkeep. Export reviewed settings using Save reviewed prices as JSON.

The example route prioritises combat and industry instead of buying every optional node. At five credits per refined ore, four minutes per operation/preparation cycle and 0.1 destroyed defences per operation, it funds shotgun after round 3, sentry after round 5 and AR after round 8. It reaches 30,000 held credits around round 61 / 244 minutes. These are design estimates, not measured playtest results. At 3.25 credits, it does not reach the target within 90 operations; heavy losses further delay progress.

The current game's 18-ore baseline, +7 per drill level, 20 starting credits and existing wave schedule informed the model. Later +70 extraction and weapon/ammo changes in the proposal are explicitly new proposals, not claims about current gameplay. The model does not simulate aim skill, kill times, failures, storage bottlenecks or the full market history. Those require playtests before adopting prices.

## Verification and installation

Native regression fixture: `MiningPrototype.exe --verify-cleanup`. It uses actual project assets and preserves scene/progress files. HTML calculation checks: `artifacts/hazard-cleanup/validate-review.cjs`. Native screenshots and logs are in `artifacts/hazard-cleanup`.

The browser tool rejected the local `file:` URL, so the HTML's calculation logic was validated without claiming browser screenshot QA. Open the HTML directly to review its visual layout.

Scene and effect backups: project `SourceArt/Cleanup-Backups/20261030-003553`. No game export was performed. Existing exported games remain unchanged until an explicitly authorized export.
