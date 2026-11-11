# Field Systems HUD corrections and machine menus — 2026-11-10

Source: C:/Users/codex/.codex/worktrees/52af/ByteEngine (installed routing verified). Game: C:/Users/codex/Documents/palclone. Plugin version 3.82.0.

Completed: resource panel narrowed with a content-sized credit column; combined HP/drill panel 430 -> 350 units; company panel 250 -> 210. Original arrangement widget is inactive outside edit mode, leaving the correct bottom-right hint and preventing native widget restoration over credits.

Refinery and ammo press now use a shared 480x580 right-side Field Systems renderer, Inter fonts, original factory/ammo/clock/recycle/info/gear artwork, clipped-corner panel/row/price borders, live progress and current/next upgrade values. Additional refineries use exactly the same renderer and updated hitboxes while retaining independent processing. Upgrade arrows are line geometry to avoid missing-font glyphs. Existing gameplay costs/production tuning are unchanged.

Build: plugin Release succeeded (0 errors; existing nullable warnings, NuGet vulnerability-feed unavailable warning). Preview harness built with current dependencies; final plugin DLL copied from the final Release output into its unchanged harness and hash identity confirmed before final captures. Desktop package/project PluginSource synchronized using SyncJamPolish.py; final package SHA256 3035132FBF4D06A053170B42D52068993247FBD97B5CDEA6DD2BC71EE7BC59AF. New icons/borders + metadata installed under Assets/UI/Mining/FieldHud; source SVGs in SourceArt/Gameplay HUD.

Passed: --verify-field-machines (all upgrades, actual load/return, active-process guard, X hitbox, scene unchanged); --verify-support-interactions (local refinery upgrade/X hitboxes for both machines, independent timers and upgrades, menu click-through prevention); --verify-context-hud. Full legacy --verify-machine-menus gets through both changed machine menus then fails on obsolete research id company; focused flag explicitly scopes to current requested menus. Do not claim that legacy full suite passed.

Visual QA: final native gameplay, refinery processing and ammo press captures inspected at 1280x720. No missing arrows or observed overlaps in these captures. Logs artifacts/field-machines-*.log, artifacts/field-compact-hud-*.log; previews Docs/Concepts/HazardPay-UI-Overhaul/Gameplay-Hud-Assets/index.html. No exports, browser runtime refresh, save edits or git push performed.
