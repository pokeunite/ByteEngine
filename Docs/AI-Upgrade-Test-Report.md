# AI upgrade verification — 24 October 2026

## Result

The requested perception/memory, staged combat, tactical positions, squad reservations, services, interruption policies, reusable sub-behaviours and decision history are implemented and assigned to the eleven hunt enemies.

This is a lightweight native ByteEngine implementation. It is not a full Unreal Engine AI stack or a direct integration of Unity's A* package.

## Automated regression suite

Command: `dotnet run --project Tests/ByteEngine.WeaponEvents.Tests/ByteEngine.WeaponEvents.Tests.csproj --no-restore -p:UseAppHost=false`.

The final run completed successfully with **166 reported PASS checks/groups**, including existing gun, inventory, projectile, animation binding, HUD, encounter and boss tests. New advanced tests additionally cover:

- Sight range/cone, cover occlusion and loss of visibility without tracking the unseen target.
- Hearing, one-time event consumption, self-noise rejection and reduced hearing through cover.
- Memory expiry and bounded searching of the last-known area.
- Background service intervals during latent tasks, including services in sub-behaviours.
- Isolated sub-behaviour call state and safe rejection of recursive references.
- Rechecking and finish-branch interruption policies.
- Reachable cover, visible firing lanes, flank offsets and separately reserved surround points.
- Attack capacity, pause/inactivity/death cleanup and scene isolation.
- **40 agents over 100 simulated steps**: simultaneous attack reservations never exceeded the configured limit of two, and Stop released them.
- Separate warning/commit/strike/recovery signals and single-hit damage timing.
- Locked melee direction, cover introduced during windup, expired leases after a long frame, and finishing committed charge/recovery despite loss of sight.
- Bounded decision history and JSON reason export.

Detailed output: engine workspace `artifacts/ai-v2-optimized-tests.log`.

## Native level integration

Loaded the saved palclone scene through GameProjectRuntime and ran temporary runtime copies with the actual components, navigation regions, sub-assets and rendered VFX. The test isolated one representative of each role, used an invulnerable stationary target and accelerated only the test copy to 3x simulation. It did not save those test positions or timings into Main.

| Role | Observed phases / attacks |
| --- | --- |
| Crownback | Windup, charge, recovery, slam windup and leap; Charge, Slam and Feint selections |
| Rusher | Windup, strike and recovery |
| Spitter | Bolt-caster windup, strike and recovery |
| Charger | Windup, charge and recovery |
| Hexer | Falling-fireball windup, strike and recovery |

All roles emitted AttackStrike and AttackRecovery and finished without an AI binding error. The native check caught and fixed the old Externally Driven navigation flags during migration.

Performance review found approximately 20 ms sampled position searches when all candidates ran full route searches. The query implementation was changed to cheap filtering/ranking first, early acceptance of a valid candidate and bounded route-search batches. The optimized native run's **last sampled queries** were approximately **2.02 ms (rusher), 2.65 ms (spitter), 3.24 ms (hexer)** in Debug. These samples are not a worst-case profile or a promised frame rate.

Detailed output: `artifacts/ai-v2-native-optimized.log`.

## Editor verification

Rendered the actual native rules view, background-service controls and per-actor decision history. Screenshots were inspected for readable controls and functioning layouts. Export history uses a user-chosen JSON file. Source-save checks and component serialization are covered separately by the regression suite.

Output: `artifacts/ai-v2-ui-verified.log`.

## Scope of confidence

Functional regression and integration checks passed. The test target did not play like a human, and only one representative role was active at a time in the native role check. The 40-agent test checks reservation correctness, not rendering performance with forty active monsters. Human playtesting is still needed to tune pressure, pacing, query budgets and whether the upgraded fights feel better.

No game export was performed. The installed Windows runtime is refreshed with the editor; existing exported games need a new export to include the updated scene, assets and plugin.
