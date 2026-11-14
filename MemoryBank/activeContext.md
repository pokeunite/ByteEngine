# Active context

Updated: 2026-11-14 — Hazard Pay engine consolidation

## Maintained source and distribution

- Canonical source: `C:/Users/codex/ByteEngine`, branch `main`.
- Integration branch `codex/hazard-pay-main-consolidation` combines main's engine/export/editor work with the Hazard Pay engine/gameplay history. Promote it only after validation; retire the old separate palclone editor after publishing.
- Active game: `C:/Users/codex/Documents/palclone/palclone.byteproject` (Hazard Pay).
- Canonical gameplay source: `Plugins/KarlsonDemo`; version 3.94.0, minimum engine 0.10.0.
- Windows-first: publishing is not a game export. Browser runtime refresh and game exports remain opt-in.

## Repair evidence

Original fault: main Core was incompatible with the project's newer 3.93 plugin (missing GameWindowMode, ICharacterDebugSource, CombatDamageReceipt and other gameplay APIs). Combined Core compiles and native field/company checks pass. Headless runtime validation now checks missing plugin components in scenes and blueprints; it preserves sky references without requiring OpenGL. Full regression and distribution identity checks are recorded in the consolidation handoff/report.

## Current work

Finish final solution/regression checks, promote main, publish matching Windows editor/player/plugin packages, retire duplicate generated installs and preserve recovery history. Do not delete game assets/saves or authored source during cleanup.
