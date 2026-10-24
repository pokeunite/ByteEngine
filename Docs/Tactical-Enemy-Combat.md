# Tactical enemy combat and performance pass

## Fireball hitch

Profiling the previous sprite atlases measured about 369 ms for the falling fireball and 121 ms for the impact on this test host. Effects were destroyed after every cast, losing their atlas leases and rebuilding on the next attack.

The caster now retains and reuses its fireball and impact VFX players, buffers and atlas leases. They prepare during scene startup. Atlas tile resolution is 64 and particle capacities are bounded; combined atlas data fell from 10 MiB to 2.5 MiB. Cold preparation measured approximately 117 ms + 40 ms after the change, at startup rather than every cast. Three in-game casts verified identical particle-batch references and a stable object count. VFX release their GPU resources when the scene stops. This addresses the measured rebuild hitch; it is not a guarantee of constant frame rate on every machine.

## Faster kills and shotgun

| Enemy | HP |
|---|---:|
| Rusher | 42 |
| Spitter | 55 |
| Ground caster | 70 |
| Charging brute | 160 |
| Crownback | 900 |

A 25-damage pistol normally takes two hits for a rusher and three for ranged enemies. Recovery damage remains doubled on chargers and the boss. These health values are in each enemy's Health component.

Shotgun: 12 damage per pellet, 115 RPM, 3.5-degree spread, per-shell reload .4 sec / speed multiplier 1.25 = .32 sec. The first shell from empty takes .4 sec. A full eight-shell empty reload takes about 2.64 seconds. Firing can interrupt a per-shell reload as soon as ammunition is available. Auto-reload remains enabled. Edit Weapon Definition on the Shotgun pickup to tune these values.

## Dodges and ranged tactics

Normal enemies react to a hit with a collision-checked sidestep when their dodge cooldown permits. They take the triggering hit and have no added dodge invulnerability. Dodge Enabled, Dodge Cooldown, Dodge Speed and Dodge Seconds are editable on Simple Enemy AI. The Event Sheet action **Request enemy dodge** is available for other triggers.

Spitters and ground casters retreat when the player gets too close. While an active rusher is near the player, they also search for actual occluding cover using raycasts and reachable navigation paths. They briefly hold cover, then leave it to peek and fire. The frontline condition prevents ranged enemies from relying indefinitely on a dead frontline. Retreat Enabled, Seek Cover, Preferred Range, Cover Search Radius, Cover Hold Seconds and Cover Interval expose the behaviour. Current Tactic displays the runtime decision.

## Adaptive Crownback

This is lightweight game logic, not a cloud AI or a machine-learning model.

1. Introduce the charge. A missed charge increments Evaded Charges.
2. After the first evaded charge, introduce the leap slam. The landing point locks when its area warning appears; it does not track the player during the leap.
3. After the player evades a slam, introduce a short rush feint followed by a separately warned area slam.
4. Once introduced, weighted choices mix charge, leap and feint. The boss does not simply repeat a fixed three-attack loop.

Charges remain straight committed attacks and respect the arena navigation boundary. Slams show their area before impact. Recovery still creates a damage opportunity. The feint is not an instant damaging rush: its danger is the follow-up slam. Boss reset/checkpoint retry clears the learned counters and restores its attack state.

Adaptive Boss, Jump Height, Jump Seconds, Slam Warning Seconds, Slam Radius, Slam Damage, Feint Distance and Feint Warning Seconds are saved on Simple Enemy AI. Boss Attack, Attack Phase, Current Tactic, Evaded Charges and Evaded Slams expose runtime state. **Boss has learned from evaded attacks** is available as an Event Sheet condition.

## Validation

The native combat regression suite passed. Added checks exercise hit-triggered displacement, real cover selection, retreat increasing player distance, charge-miss learning, leap-miss learning, feint-to-slam transition and reset. Fireball tests verify object reuse and cancellation when the caster dies. Runtime checks passed three repeated sky attacks without recreating VFX buffers, and the complete hunt including boss death/retry and victory without event warnings.

Manual playtesting is still needed to judge pressure, dodge frequency, shotgun feel and boss fairness. Ground material, encounter enemy count and normal player movement settings were preserved.
