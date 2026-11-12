# Combat and field corrections â€” 2026-11-11

Maintained source: C:/Users/codex/.codex/worktrees/52af/ByteEngine (verified against installed .byteengine-build-source.json). Active project: C:/Users/codex/Documents/palclone. Canonical plugin Plugins/KarlsonDemo, version 3.91.0.

## Changes

- ReserveMarketSale and delivery-contract acceptance use CanUseIndustry, allowing new sales/contracts during combat. Permanent research and drill controls retain their combat restrictions. Existing shipment split/payment rules remain unchanged.
- Primary and auxiliary refining can complete at full cargo. Conversion/yield bonuses may temporarily exceed capacity; extraction and purchases still respect capacity. No bonus output is silently discarded.
- Countdown moves into the lower-left vitals/drill status during gameplay. Arrange mode has a compact top-right status strip; other menus do not receive a floating timer overlay.
- Arrange opens and remains active during combat, including wave launch. Combat is not paused. The normal Tab hint is available during combat. Object labels/ranges render only for selected/hovered objects, while the type tray remains available.
- Spawn candidates require an actual upward-facing solid floor; living actors cannot be used as floors. Recovery ignores the recovering actor and validates the exact candidate against the target route, rather than checking one point and choosing another. Once-per-second watchdog lifts shallow buried roots onto solid floor. Last-enemy diamonds now include distance.

## Validation

Build logs: artifacts/combat-flow-build.log and combat-flow-tests-build.log, both successful. Existing warnings retained.

- Director suite: 40 assertions, including full-cargo conversion completion, auxiliary completion, combat sale and floor correction.
- Expedition suite: 44 assertions.
- Company UI/settlement suite: 41 assertions, including combat payout and purchases.
- Actual-level field suite: 29 assertions, including twelve spaced grounded spawn positions, buried-root correction, inactive lost-enemy recovery and keeping arrangement open across wave-transition menu handling.
- Defense placement UX suite: see artifacts/combat-flow-placement.log (final run).
- Actual native screenshots inspected: artifacts/combat-flow-arrange.png and combat-flow-hud.png. Clear arrangement view and compact countdown beside drill status; no label/range flood or floating timer over scene.

Old MiningCampVerify stopped at a stale drill-side reach fixture before arrangement tests; current actual-level field and placement suites cover the changed behavior. Removed obsolete duplicate company-tab coordinate checks from Director suite; current company suite covers them against maintained layout.

Synchronized desktop plugin/package and editable project source with SyncJamPolish.py. Installed SHA256 692E2B0C34EDA23ADBF0B0BD2F5BFF4FCC2CACCEC96CF42842ACFBF75472005E. No scene/save edits, browser refresh, game export or push. Existing exported games require a new export to receive these changes.

Limitations: tests reproduce shallow burial and inactive/fallen enemies, not the user's exact late-wave recording/save. These are defensive corrections, not a claim that every possible navigation stall is impossible. Keep recovery diagnostics StrandedEnemiesRecovered and LastEnemyRecovery available for future reports.

Drone label follow-up:3.91.1 removes numeric maximum from R&D row and deployed confirmation; displays active count and battery only. Scene MaximumDrones and battery settings preserved. Plugin build and native support-shop screenshot verified; synchronized desktop/source. Log artifacts/drone-count-ui-build.log, drone-count-ui-preview.log.

Wave presentation follow-up 3.92.0: drone ShowBatteryBar default false (active SecurityDrone blueprint does not serialize an override); battery drain/AI unchanged. Wave result replaces bottom 540x50 stats strip with upper-centre 376x94 card using shared SupportUiArt typography/icons. Distinguishes wave cleared vs full contract complete and displays actual LastWaveOreSecured/LastWaveOreLost, with drill integrity. Preparation-only rendering prevents stale result over an early-start combat wave. Plugin and harness compile, desktop package synchronized SHA256221BBE04D86E512A921EE2CEB44A0E1330F5B7FB53AE8B4EDB1F3A0064339264. No exports/browser refresh. Visual preview --ui-demo --wave-result-preview log artifacts/wave-result-ui-preview.log.
Balance evidence from user:4–5 sentries with0–1 mortar and barricades already provide substantial base security; user tested8 sentries,1 mortar,80 drones. No balance changes made in this UI-only follow-up.
Native screenshot inspected: artifacts/wave-result-ui.png shows Wave2/6 cleared,+42 raw secured,100 percent drill integrity in upper-centre card; next-wave timer remains lower left. Preview exited0. No overlaps in captured1280x720 gameplay view.
