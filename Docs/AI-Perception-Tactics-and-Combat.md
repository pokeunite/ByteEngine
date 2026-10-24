# AI Behaviour — perception, tactics and staged combat

24 October 2026 · native ByteEngine systems · Modular Weapon Gameplay 3.17.0 for gunshot hearing

## What changed

Enemies now use editable perception/tactical trees and a shared attack sub-behaviour. The full legacy **Use enemy combat pattern** task remains compatible, but the upgraded hunt assets no longer depend on it. Their attack sequence is exposed as **approach → reserve attack → choose attack → warn → commit → strike → recover**.

The simpler WHEN/DO editor remains the default. Background services, Memory and Test / Live view are separate tabs. Advanced tree remains available for nesting and interruption policies.

## Set up an enemy

1. Keep Health, collision and Navigation Agent on the enemy. Bind the agent to a baked Navigation Region.
2. Keep the enemy combat component (**Simple Enemy AI**) for its attack implementation, timings, damage and VFX. Enable **Behaviour Controlled**. Its autonomous update is then skipped; individual behaviour tasks drive its stages.
3. Add **AI Behaviour** and choose an asset under `Assets/AI/Enemies`. Set Target Object to the player, or enable Find Nearest Character.
4. Enable **Perception Memory**. Set Detection Range, Field Of View, Eye Height, Sight Mask Bits, Memory Seconds, Hearing Range and Hearing Memory Seconds.
5. Set a **Squad** name, **Maximum Attackers** and **Position Spacing**. Actors sharing a squad and target share attack capacity. The lower configured active limit is respected if squad members disagree.
6. Enter Play. Open the assigned asset and select the enemy in Test / Live view. Check awareness, memory, task status and decision history.
7. Save an asset, then restart that actor from the live debugger to load your changes. Restart cancels owned movement/attacks, releases reservations and restores memory defaults.

Palclone's normal enemies share a two-attacker budget. Crownback uses a separate one-attacker squad. Existing encounter activation still enables/disables the combat component; AI Behaviour waits quietly while its encounter is inactive.

## Sight, hearing and search memory

Sight is a distance limit, horizontal view cone and cover-aware ray. A configured target is a candidate to perceive, not an instruction to see through walls. When visibility is lost, the actor retains the last observed position for Memory Seconds. It does not update that position from the unseen actor's live transform.

Hearing uses explicit noise events. Gunshots report a noise at the muzzle; each gun exposes **Weapon Definition → AI Hearing → Noise Radius** (default 24 metres, zero silent). Hearing is limited by both that radius and the listener's Hearing Range. Occluded Hearing Multiplier reduces effective range through blocking geometry. It is a lightweight noise-event model, not a simulation of recorded audio acoustics.

For footsteps, alarms or other gameplay noises, use **AI / Behaviour → Report AI hearing noise** in Event Sheets. Choose the source object, audible radius and noise kind. An AI ignores its own noise. Events are consumed once per listener and the scene keeps a bounded history.

A heard position can create interest without granting line of sight. **Search last known area** checks the remembered position and nearby reachable points, and ends when its timer/memory expires. A visible target can interrupt search immediately. **Forget AI target and last known position** explicitly clears that information.

Automatically refreshed memory includes:

| Key | Meaning |
| --- | --- |
| HasTarget | A visible or remembered target object is known |
| HasLineOfSight | The current perception test sees the target |
| HasMemory | A sight/hearing interest point has not expired |
| HeardNoise | A new in-range noise was consumed this decision |
| TargetPosition / LastKnownPosition | Observed/heard position, not omniscient tracking |
| TargetDistance | Horizontal distance to the perceived/remembered position |
| HealthFraction | Own health from 0 to 1 |
| EvadedCharges / EvadedSlams | Native adaptive attack counters |
| AttackPhase / IsDodging / IsEnraged | Combat status usable by your own rules |

Do not overwrite perception keys with your own services. Custom memory keys remain separate per enemy even when assets are shared.

## Separate attack stages

The shared asset is `Assets/AI/Enemies/Shared/Attack stages.byteai`. Change it to alter the common stage order, or duplicate it and choose a different sub-behaviour for a specific role.

| Task | Behaviour |
| --- | --- |
| Reserve squad attack | Wait for capacity; timeout prevents permanent waiting. A maximum hold duration protects against stuck owners. |
| Choose attack | Select Automatic, Primary, Charge, Slam or Feint. Slam/Feint require the adaptive boss implementation; Charge requires Charger style. |
| Warn before attack | Run the authored windup and warning VFX without applying strike damage. |
| Commit and lock aim | Mark the attack committed. The previously telegraphed point/direction remains locked. |
| Execute attack | Perform the native melee, bolt, fireball, charge, leap or feint effect. Charge/leap remain running tasks until their execution completes. |
| Recover after attack | Keep the authored recovery/weak-point window, then release attack capacity. |

Windup, recovery, damage, charge distance/speed, fireball size/radius and boss settings remain editable on the combat component. This shares the established attack effects and collision logic rather than duplicating them in every tree.

Automatic retains the native adaptive attack chooser. For a wholly authored attack choice, use conditions on EvadedCharges/EvadedSlams, health or custom memory, then an explicit Choose Attack option. The tree controls the sequence; the component supplies the reusable action implementation.

Signals are emitted for AttackChosen, AttackTelegraph, AttackCommitted, AttackStrike and AttackRecovery. Use **AI sent signal** to trigger animations/audio/UI. Do not apply the same damage again in a presentation event.

Committed staged melee checks its locked direction and current cover before dealing damage. Projectile and falling-fireball aim remains at the telegraphed point. Moving out of that point/arc can avoid the hit.

## Tactical positions and squad spacing

**Choose tactical position** offers four queries:

- **Cover:** reachable points hidden from the target.
- **Firing lane:** reachable points with line of sight at the preferred range.
- **Flank:** reachable points to the side of the current approach.
- **Surround:** reachable positions around the target, with separate reservations for nearby allies.

The chosen world position is stored in the task's selected memory key, such as CombatPoint. Follow it with **Move to memory position**. Search Radius, Preferred Target Distance, Minimum Spacing and Hold Position Seconds are editable.

Candidates are checked against the Navigation Region, visibility requirements and existing position reservations. A failed query records why it failed. Positions are held briefly to avoid constant switching, then can be reconsidered when the target moves or the lease expires. Use **Release tactical position** when the sequence no longer needs it.

Attack reservations and position reservations are separate. Death, pause, inactive encounters and disabled actors release or prune their ownership. Reservations are scene-local, so another project/scene cannot inherit them.

This distributes goals; it does not implement a full predictive crowd-avoidance solver. Solid-cover collision remains the navigator's final constraint.

## Interruption policies

Select a priority group or condition and choose:

| Policy | Effect |
| --- | --- |
| Recheck immediately | Higher priorities/failed conditions can cancel the branch at the next decision. |
| Finish this branch | Keep the chosen branch until its running work completes. |
| Finish a committed attack | Recheck normally before commitment; once committed, finish strike/recovery before accepting another branch. |

The hunt uses the committed policy for attack branches. Search remains interruptible. Cancelling releases owned navigation and squad resources and clears native attack warnings. Independent effects launched by your own Event Sheets still need their own cancellation rules.

## Background services

In **Background services**, add an enabled named service with an interval and memory/signal key. Available services:

- Watch a health threshold and update a boolean.
- Update a configured memory value.
- Send a named Event Sheet signal.
- Refresh a tactical-position query.

Services continue while a latent task waits. Services on reusable sub-behaviours run while that sub-behaviour is active. Each actor/call site has its own timing state. Services and perception use configurable intervals, not a full Unreal-style observer scheduler.

## Reusable sub-behaviours

Add **Run reusable behaviour**, then choose another `.byteai` asset. The sub-behaviour shares the calling actor's memory, while its task state is isolated per call site. Defaults fill missing memory keys rather than overwriting existing values.

Use this for common attacks, investigation routines or retreat sequences. Recursive references are rejected at runtime with a diagnostic reason. Nesting is bounded; the root and task budgets remain finite.

## Decision history

Open **Test / Live view → Why the AI did that**. History records perception changes, condition failures, branch status changes, position-query results, squad waiting, services, commitment and cancellation reasons. Filter the history or export it to JSON through **Export history**.

History is per actor, bounded by History Capacity (16–1000 entries) and can be disabled with Record History. It is a recent-decision buffer rather than a permanent telemetry database. Memory is inspectable separately, and running nodes retain their visual status.

## Performance controls

- Decision Interval controls perception/tree decisions; movement and attack stages advance each simulation update.
- Position Query Interval limits retries per actor/query kind.
- Position Candidates is bounded to 8–24; cheap distance/visibility filters run before navigation. Candidates are ranked and the first valid reachable result is accepted.
- Position Paths Per Query bounds route searches per batch (default 3); unsuccessful batches resume on a later query interval. Position Visited Budget bounds each route search.
- A scene-wide position-query budget permits at most one query per 50 ms simulation slot, spreading requests from a newly activated group.
- Position Queries Completed and Last Position Query Milliseconds are exposed as live diagnostics.
- Noise, signal and decision histories are bounded. Tree depth, node count, service count and sub-behaviour nesting are bounded too.

Start with the saved prototype settings. Increase budgets only after profiling a representative fight on the target laptop. These systems supply controllable behaviour; encounter difficulty and perceived fun still need hands-on playtesting.

## Save/export

Save the scene and `.byteai` assets together. Model/VFX/audio assets remain where they already were. This update refreshes the editor and matching Windows player runtime; it does not rewrite an existing exported game. Re-export when you want the new systems in a standalone build.
