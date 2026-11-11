# Hazard Pay judge-feedback implementation — 2026-11-11

Maintained checkout: C:/Users/codex/.codex/worktrees/52af/ByteEngine. Active project: C:/Users/codex/Documents/palclone. Canonical plugin Plugins/KarlsonDemo, version 3.86.0.

## Behaviour
- Opt-in configurable expeditions (enabled in active scene): six automatic waves, 45-second preparation, up to 15% early-start ore. Site forecast is snapshotted on commitment; enemy count grows 12% per wave with concurrent cap unchanged. No free healing/ammo between waves; safe camp/full HP after completion. Cargo overflow loses excess ore.
- Industry/R&D and production upgrades available in combat; panels do not pause simulation. Arrangement remains preparation-only and exits on launch. Permanent research/account closes on combat launch; stock trading retains existing preparation/shipping restrictions.
- Auto-load toggle per refinery, 35% time penalty with unchanged yield; queued batches retain penalty. Extra machines retain independent load/upgrade state.
- Ammo press scene tuning: 3 rounds / 6 seconds, storage35, +2 rounds/payload rank. Existing saves/levels remain.
- Sentry18/.55s/range22; drone12/.35s/range16. Mortar unchanged.
- Mines stay depleted in place; Field R&D recharge action costs10 refined each, no free reset. Depleted state saved in planner placements; destruction still retires to replacement inventory.
- Wave3+ cyan-crested disruptor raiders: configurable1s fixed-position warning,5m pulse,3s disable,7s cooldown. Death cancels wind-up. Existing in-flight mortar shells finish during shutdown.
- Three-rank handling nodes per weapon family (magazine/reload/accuracy), rank-scaled from captured baseline; pistol-only legacy stats scoped to pistol. Current/next values in details. Drill output cap configurable12; explicit MAX feedback.

## Save limitations
Preparation resumes from saved countdown. Combat resumes by restarting the unpaid wave after5 seconds with saved health/ammo; individual enemy state is NOT checkpointed. Restore ammo is delayed two updates to follow weapon initialisation/unlock grants. Additive save schema preserves older company progress. No migration resets user saves.

## Files and install
New partials: MiningOperation3D.Expedition.cs, .Industry.cs, MiningDefense3D.Recharge.cs; new MiningDisruptor3D.cs and MiningExpeditionEvents.cs. Existing operation, contracts, business UI, trade/research, auxiliary refinery, planner, defence visuals/health, drone, plugin registration/catalog updated.
Installer: python Tools/MiningPrototype/InstallExpeditions.py (scene/blueprint backups before edits). Desktop sync: python Tools/MiningPrototype/SyncJamPolish.py. Installer run once; preserve Backups/Judge-feedback-20261111-113444. Future installer runs should avoid resetting scene customization.
User-readable explanation/checklist: Docs/HazardPay-Expedition-Changes.html.

## Checks
Plugin: dotnet build Plugins/KarlsonDemo/KarlsonDemo.Plugin.csproj -c Release --no-restore -m:1 -nodeReuse:false --nologo. Raw log artifacts/judges-build.log.
Harness: dotnet build Tools/MiningPrototype/MiningPrototype.csproj -c Release --no-restore -m:1 -nodeReuse:false --nologo. Raw log artifacts/judges-preview-build.log.
Native regressions: MiningPrototype.exe --verify-expeditions. Raw log artifacts/judges-contract-tests.log. Uses transient scene, disables progression/settings persistence. Full six-wave transitions, UI countdown, no free refill, combat production, automatic penalty across queue, saved state JSON/restart policy, mine expense/no double charge, disruption/death cancellation, rank baselines/MAX tested.
Native visual preview: --drill-ui --expedition-preview --survey-clean; refinery --refinery-ui --ui-demo. Raw logs artifacts/judges-*-preview.log and screenshots artifacts/mining-*.png. Harness now waits for actual capture before closing, avoiding update catch-up ending preview before render.

## Constraints
No FPS/performance gain claimed. Automated transition tests are NOT a finished human balance playtest. More maps/turret families/music deliberately deferred until contract loop tested. Windows package/project source synchronized. Browser runtime intentionally unchanged. No game export or Git push authorized/performed. Preserve unrelated network/PvP/engine/UI work in dirty tree.

Final evidence: 37 expedition assertions passed; 33 existing company-menu assertions passed. Native contract/refinery screenshots visually inspected. Final synchronized Windows package SHA256 CC571E8595BB369F5421A453EF1B0EEC8607D67F5ED3C3B1DAF0273BAF5127A6.
