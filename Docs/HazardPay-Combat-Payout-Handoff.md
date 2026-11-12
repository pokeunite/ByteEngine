# Combat payout / UI concepts — 2026-11-11
Maintained source: C:/Users/codex/.codex/worktrees/52af/ByteEngine. Active project C:/Users/codex/Documents/palclone. Plugin 3.87.0.

User requested generated Audio/Controls and tutorial/countdown concepts; two built-in imagegen sheets delivered inline. They are proposals only, NOT applied to settings/tutorial layouts.

User also reported no exchange funds during combat in exported Windows game. Inspected actual recent save read-only: Games/734f29080bec4dfc872b329b77cb33e8/Builds/7c6bd09d864d4c1aa18e26551738393e/Saves/mining-business-v1.json. Last receipt sold13 ore, gross81, locked50% deposit, company40/camp41; current deposit slider10%, pendingnull, current credits25, rounds1. Actual log confirmed plugin3.86.0. Existing settlement has NO combat phase guard. No basis to claim a combat-blocked payment was reproduced; locked split and poor disclosure explain this receipt. User informed of exact evidence.

Changes: exact non-mutating PreviewSaleIncome respects integer carry and locked percentage. Live exchange gross labelled Sale total with explicit camp/company preview and locked shipment split. Receipt detection uses sale ID rather than observing shipment status transitions. Compact6s combat receipt reveals allocation and spendable balance, avoiding duplicate HUD notices. Insufficient ammo-upgrade feedback gives actual cost/current camp funds. Receipt adds before/after balances and combat flag; one log line at settlement aids future diagnostics. No change to agreed deposit rules or player balances. No save data edited.

Build logs artifacts/combat-payout-build.log, combat-payout-tests-build.log. Company regression extended to start exchange in preparation, change slider after locking, settle in combat, validate immediate500camp/500company payout from1000gross, no double settlement, visible receipt and spend ammo-upgrade proceeds during combat. Existing company interactions remain covered. Exact final test log artifacts/combat-payout-tests.log.

Desktop package/source synchronized with SyncJamPolish.py. No browser rebuild or export. Existing exported EXE must be re-exported by user to include changes.

Separate observed issue (not changed in this task): latest exported diagnostic log repeatedly reports unsupported24bit WAV for Assets/Audio/Mining/Creatures/Spider shell hit.wav. Investigate audio decoding/asset conversion separately; do not claim performance impact without measurements.

Validation complete: plugin and harness builds exit0. Native company suite exit0 including combat payment, locked split, no duplicate payment, receipt display and spending proceeds on ammo production. Existing save untouched.
