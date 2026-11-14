# Session log

Newest first. One entry per work session or hand-off. Keep each entry short:
what changed, what was verified, what is left.

## 2026-11-14 (tooling) — Markdown→PDF renderer and the review PDF

Changed: added `Tools/MarkdownToPdf.py`, a single-file renderer (Markdown → styled HTML →
headless Chrome/Edge → PDF) with a CLI for output path, cover title/subtitle, `--no-cover`,
`--page-size A4|Letter|Legal`, `--image-height`, accent/heading colours, extra `--css`,
`--browser`, `--keep-html`. Images resolve relative to the Markdown file, and absolute local
paths (`C:\shots\x.png`, `/home/x.png`), raw `<img>` tags and `file:`/`data:`/`http(s)` sources are
handled; unresolvable references are listed as warnings instead of failing the run. Wrote the
user guide `Documentation/Markdown-to-PDF.md` and added the command to `MemoryBank/techContext.md`.
Also reformatted the research doc's header block into bullets so it prints cleanly, and generated
`C:\Users\codex\Downloads\GDevelop-EventsEditor-Proposal-2026-11-14.pdf` for review.

Verified: `python Tools\MarkdownToPdf.py Docs\GDevelop-EventsEditor-Research-2026-11-14.md -o
"%USERPROFILE%\Downloads\GDevelop-EventsEditor-Proposal-2026-11-14.pdf"` → 1,354 KB, 18 pages, A4
(595×842 pt), 13 embedded images; page renders checked with PyMuPDF at 100 dpi (cover, metadata +
§0/§1, final page). Absolute-path image test produced a 1-page PDF with the image embedded; a
Markdown with a missing image still built and printed `Warning: 1 image reference(s) not found`;
`--help` exits 0 and a missing input file exits non-zero. Temporary test files and render scripts were
deleted from `%TEMP%`.

Left: the PDF awaits the user's review; the script is reusable for any `Docs/*.md`.
Requires `python -m pip install markdown` (installed here) and Chrome/Edge; `pypdf` is optional and
only enables the page-count line.


## 2026-11-14 (formation) — horizontal line: 1 centre, 2 right, 3 left

Changed: the party now travels in a single horizontal rank instead of the
species flank zones. `WildlingFollower3D` gained `Formation`
(`Line` default / `Zones` legacy), `SlotIndex`, `LineSpacing` 1.7 and
`LineBehind` 1.6. Slot 1 sits in the middle, slot 2 to the right, slot 3 to the
left, all at equal depth. Slot index resolves automatically from
`TeamManager3D.Resolve(1..3)` so the rank keeps team order; an explicit 1-3 pins
a side. Sides use `cross(travel, up)`, so the rank is tied to real travel
direction, not camera yaw.

Scene: Fire Lizard slot 1, Spriglet slot 2, Zappet slot 3, formation Line,
spacing 1.7, behind 1.6. Blueprints re-extracted with the same values.

Verified: plugin builds clean, installed, `be_runtime/validate` issues [],
`be_scene inspect_object` confirms all three slot indices in the saved scene and
`"slotIndex"` present in each of the three Blueprints.

Trap found: `be_plugin update` and `be_scene apply set_component` in the *same*
MCP request batch silently drops properties added in that same build — the
process still has the previous plugin schema loaded, so `formation` /
`slotIndex` / `lineSpacing` / `lineBehind` were written as absent and the
response echoed defaults. Install in one call, then apply in a *later* call.

Left: no Play-mode test from the shell. Watch the rank collapse in doorways —
the blocked-route fallback still uses the trail point, so a narrow doorway
queues the three in single file and they re-form afterwards.


## 2026-11-14 (companion AI v2, smoothing pass) — no more stop-start or facing flips


Changed: after the first v2 build the trainer reported two visual faults — the
Fire Lizard visibly flipped between "running forward" and "looking back at the
camera", and all three companions shot past their slot, stopped, waited for the
distance to grow, then sprinted again. Root causes were all in
`WildlingFollower3D`:

- The 1.4 m start / 0.6 m stop hysteresis was applied even while the trainer was
  moving, so at 0.6 m error a companion stopped dead until the error reached
  1.4 m. It now applies only to a stationary trainer; while the trainer travels
  the companion holds station at the trainer's own speed.
- `predictiveLead` (0.2 s) shifted the slot forward by up to 1.2 m against a
  0.7 m `behind` offset, literally placing slots *ahead* of the trainer. Lead is
  now clamped to `behind`.
- No braking: desired speed could exceed what deceleration could shed. Added
  `arriveLimit = sqrt(2 * deceleration * error)` plus an `overtakeLimit` of
  `leaderSpeed + clamp(error, 0.6, 3)`.
- Slot error only updated on the 0.3 s destination refresh, so braking reacted
  late; it is now recomputed every frame.
- Facing used `Transform.EulerAngles` per frame and idle-glanced at the trainer
  at 72 deg/s regardless of angle, so an overtaken companion turned 180 deg to
  the camera and back. Facing is now quaternion yaw (`Transform.WorldRotation`),
  follows the route rather than the separation vector, holds for
  `facingHoldSeconds` after stopping, and idle glances are capped at
  `idleGlanceMaxAngle` 75 deg at 36 deg/s.

New fields: `faceTurnSpeed` 540, `idleGlanceMaxAngle` 75, `facingHoldSeconds`
0.6.

Verified: plugin builds clean, installed, scene + the three Blueprints carry all
28 v2 fields, `be_runtime/validate` issues [].

Left: foot speed still does not match travel speed — the animation profiles have
`matchPlaybackToSpeed: false` with `runReferenceSpeed` 5.5/5/6, so a 6.6 m/s
chase slides. Enabling playback matching is a data change, not yet applied.



## 2026-11-14 (companion AI v2) — proactive catch-up, state ladder, debug overlay
*(superseded by the smoothing pass above for speed/facing behaviour)*


Changed: rewrote `WildlingFollower3D` to the Companion Exploration AI v2 brief.
Speed model is now proactive: caps 6.6 normal / 9.0 catch-up, 14 m/s²
acceleration, 18 m/s² deceleration, 0.2 s predictive lead, and desired speed
`min(cap, trainerSpeed + (error - 0.6) * 2.2)` so companions outrun a sprinting
trainer instead of trailing behind. Slot hysteresis moved to 1.4 m start / 0.6 m
stop with catch-up at 3 m and urgent regroup at 6 m. Added the deterministic
state ladder `Combat > Regroup > Navigate > Accompany > Settle > Observe` with
dwell, settle retention (stay put when 1.5-4 m from the trainer), settle
cooldown, separation steering (1.2 m radius), 0.5 m timestamped breadcrumbs in a
40-point ring buffer with 8 s lifetime, no-progress detection at 1.3 s, and an
attentive idle glance. `WildlingHud3D` gained `ShowFollowerDebug`, which prints
each companion's zone, state, follow error and desired/actual speed.

Verified: plugin builds with 0 errors; installed and validated in the wildlings
project (181 objects, 3 blueprints, 1 event module, issues []); `be_catalog`
reports every v2 default (6.6/9.0 caps, 1.4/0.6 hysteresis, 0.5 m trail, 1.2 m
separation); scene and the three Wildling Blueprints carry the new fields.

Left: no in-editor play test from the shell. Personality actions (tail swish,
hop, spark) still need clips; observation currently reuses the idle glance.


## 2026-11-14 (event sheet UX) — GDevelop events-editor research, no engine changes

Changed: pulled GDevelop's events system for reference. `D:\ue4 sources\gdevelop source` is a sparse
blob-filtered clone (`--depth 1 --filter=blob:none --sparse`) of rev `a643996`, carrying
`Core/GDCore/{Events,Project,IDE/Events,Extensions}` and
`newIDE/app/src/{EventsSheet,InstructionOrExpression}`; `Tools/PullGDevelopEventSource.ps1` re-applies
the checkout with retries (plain `git clone` stalls on this slow link, and promisor fetches need
`http.postBuffer`/`http.lowSpeedLimit`). `Tools/PullGDevelopDocImages.ps1` downloads the 51 screenshots
in `Docs/Research/GDevelopEventsEditor-2026-11-14/`. New write-up
`Docs/GDevelop-EventsEditor-Research-2026-11-14.md`: GDevelop's one-view/two-column/sentence-per-row
anatomy, its `BaseEvent`/`StandardEvent`/`Instruction` list model, comparison with our panel
(`EventWorkspacePanel*` = 12,542 lines, VisualLogic = 6,159), and an 8-step proposal (drop the
Event sheet/Graph switch from the default UI, plain rows with a 10 px handle instead of cards,
clickable sentence parameters in popovers, sub-condition rendering for `logic.and/or/not`, labelled
sub-lists for `flow.branch`, search strip, keyboard pass).

Verified: all 13 image links in the doc resolve; `git -C "D:\ue4 sources\gdevelop source" log -1` shows
`a643996` and 150 files are checked out under `newIDE/app/src/EventsSheet`. No build was run; no
engine, editor or project code was modified.

Left: no implementation started and no option chosen yet — §9 of the doc lists the open questions
(graph removal, real `Else` event, `DisplayName` visibility, inline vs popover argument editors).

## 2026-11-14 (exploration) — camera authoring preview, object pickers, v0.3 followers

Changed: `CameraBoom3D` gained `ApplyAuthoringPose()` (applies authored
yaw/pitch/arm/pivot to its camera object) plus `SeedControlAnglesFromAuthoring()`
so authored angles become the PlayerController3D starting orientation;
`ComponentPropertyRenderer` calls the preview while editing in `EditorMode.Edit`,
which is what makes yaw/pitch/arm changes visible in the scene and game views
before Play. Added `SceneObjectPickerAttribute` (`ByteEngine.Core.Authoring`) and
a renderer branch that draws a scene-object dropdown for attributed string
properties, then applied it to the Wildlings name fields. Replaced the plugin's
`WildlingCompanion3D` with `WildlingFollower3D`: stable travel-heading slots
(1.8 m left/right, Zappet 1.8 m back), 1.8 m start / 0.7 m settle hysteresis,
5.0 / 7.0 m/s follow and catch-up, 0.3 s destination refresh, 0.4 s heading
smoothing, 0.4 m breadcrumb trail with blocked-route fallback, idle settle with a
slow head turn, and combat hand-off through `WildlingCombat3D.CombatEnabled`.
`WildlingStats3D.ApplyMoveSpeedOnly()` restored after follower catch-up speeds.

Verified: editor and plugin build with 0 errors; editor published to
`Dist/ByteEngine` (byte scan finds `ApplyAuthoringPose` and
`SceneObjectPickerAttribute`); the wildlings project validates with 181 objects,
3 blueprints, 1 event module and no issues; scene re-read shows arm 6.5 / pivot
2.5 / pitch 20 on the trainer boom, `WildlingFollower3D` on all three Wildlings
and zero `WildlingCompanion3D` left; Blueprints re-extracted against the new
components.

Left: no in-editor visual check from the shell. Idle personality actions (tail
sway, hops) still need animation clips; navigation uses the breadcrumb trail
rather than a navmesh path query.


## 2026-11-14 (event sheet) — restored the GDevelop-style Event Sheet view on main

Changed: the sheet view (`Event sheet` / `Graph` switch) only existed on the
divergent branch `codex/event-sheet-ux-and-engine-updates` (13 Nov 11:35);
`main`'s refactor 2137bf2 split that monolith into Graph/Interaction/Layout/
Values partials and dropped the sheet partials plus the core fields it used.
Ported onto main: `EventWorkspacePanel.Sheet.cs`, `.SheetReorder.cs`,
`.SheetTools.cs`, `EventSheetProjection.cs` (from the branch), the core members
`VisualArgumentDefinition.IsObjectReference`,
`VisualConditionDefinition`/`VisualActionDefinition.MenuGroup` +
`SentenceTemplate`, `EventRuleDefinition.FunctionName` + `EditorHeaderColor`,
`EventGraphGroupDefinition.SheetGroup`, `ByteEnginePluginRegistry.
GetVisualLogicMenuPath`, and a local `IsCapturingKey` shim. Wired the switch into
`DrawGraphWorkspace` (`Event sheet` / `Graph` / `View options`, sheet first),
routed `DrawGraphOutline` to `DrawSheetOutline`, and hid Arrange/Frame/zoom/View
while the sheet is showing.

Verified: editor builds with 0 errors; `Tools/PublishEditor.ps1` published
`Dist/ByteEngine` (11:29) and byte-level scans of `ByteEngine.Editor.dll`
contain `Event sheet`, `ReadableEventSheet`, `CONDITIONS`, `Events: Always`;
`Tools/PublishMcp.ps1` republished the MCP against the same Core;
`be_runtime/validate` on the wildlings project still reports 181 objects, 3
blueprints, issues [].

Left: no in-editor visual check from the shell — the editor must be restarted.
The branch's extra creator/function toolkit (CreatorSceneServices, AIBehaviour,
registry `Creator*` families, runtime functions) was NOT ported; only what the
sheet itself needs. A stray empty `Assets/Logic/New Event Module.byteevents`
(one blank rule) appeared during testing and can be deleted.


## 2026-11-14 (editor) — Event Sheet EVENTS list no longer disappears in narrow docks

Changed: `EventWorkspacePanel.Graph.cs` drew the EVENTS outline (the Event Sheet
event list) only at >=1050 px, so a docked panel showed a graph with no list.
The outline now appears from 760 px, its width scales (160-210 px), and a
persisted "Events Auto / On / Off" control in the toolbar (plus the `...` menu,
so it is reachable when the toolbar collapses) forces it on or off. The details
pane now yields when the graph would fall below 320 px. New preference
`EventOutlineMode` (0 auto, 1 always, 2 never) lives in
`%LOCALAPPDATA%\ByteEngine\Editor\preferences.json`.

Verified: editor builds with 0 errors (2 pre-existing AssetsPanel warnings);
`Tools/PublishEditor.ps1` published to `Dist/ByteEngine` and byte-level scans of
the published `ByteEngine.Editor.dll` contain every new string ("EVENTS list",
"needs 640", "Events: Never", "Events Off").

Left: no in-editor visual check was run from the shell; the editor must be
restarted to pick up the new DLL.


## 2026-11-14 (event logic) — Event Sheet nodes, Wildling Event Module, no GUIDs

Changed: `WildlingsEvents.cs` registers 12 Event Sheet nodes for the systems
(`studio.wildlings.team.*` and `studio.wildlings.creature.*`, all target by object
name, team nodes fall back to the scene's manager) so rules can read and command
the team instead of duplicating the per-frame runtime in graphs. Added
`TeamManager3D.CommandSlot` for scripted switching and created
`Assets/Logic/WildlingTeamRules.byteevents` (Combat banner / Explore banner /
Fire Lizard downed) attached to `Wildling Manager`, with an `Event Message` UiText
under the HUD canvas. Removed the last GUID properties from plugin components:
`WildlingProjectile3D.OwnerId`/`OwnerLayer` became a runtime `SetOwner(...)` and
`WildlingStats3D.SpeciesId` became `Species`; the three Wildling Blueprints were
re-extracted so they carry the renamed property.

Verified: plugin builds with 0 warnings; `be_runtime/validate` reports 181
objects, 3 blueprints, 1 event module and no issues; `be_logic(inspect)` shows the
three rules resolving `studio.wildlings.*` conditions; a scene scan finds zero
GUID-like values in any plugin component.

Left: still no Play test from the shell; capture, enemies and Synergy are next.


## 2026-11-14 (mode split) — explore/combat control, fixed facing, Wildling Blueprints

Changed: `TeamManager3D` gained an explore/combat control split
(`trainerObjectName`, `autoCombat`, `combatTriggerRange`, `combatExitDelay`,
`hostileLayer`, `toggleCombatWithKey`, `InCombat`, `Leader`). Outside combat the
player drives `Trainer` and the three Wildlings follow passively; combat hands
input, camera and HUD to the active Wildling and the trainer falls back to
`TrainerFollower3D`. `WildlingCombat3D.CombatEnabled` gates firing,
`WildlingCompanion3D` follows `TeamManager3D.Leader`, and the HUD prints
EXPLORE/COMBAT. The Fire Lizard `Model` rotation returned to identity: the
importer already bakes a 180° `Import Space` yaw for every model, so the extra
180° was the backwards-facing bug. Extracted `FireLizard`, `Spriglet` and
`Zappet` `.byteblueprint` assets from the scene.

Verified: plugin builds with 0 warnings; `be_runtime/validate` reports 180
objects, 3 blueprints and no issues; `be_blueprint/list` shows all three assets;
`be_catalog` confirms the new property names.

Left: still no Play test from the shell; the scene objects remain standalone (not
Blueprint instances); capture, enemies and Synergy are next.


## 2026-11-14 (later) — Wildling party rework: real Fire Lizard, name-based refs

Changed: the trainer avatar is no longer party slot 1. `player` was renamed to
`Trainer` and is now a noncombat follower (`TrainerFollower3D` also switches off
its `PlayerController3D` and `CameraBoom3D`). A new `Wildling Manager` object owns
`TeamManager3D` + `WildlingHud3D`, and a new `Fire Lizard` object uses a cleaned
`Assets/Characters/Wildlings/FireMander.glb` (studio floor, six fire-VFX planes
and seven icospheres unlinked by `tools/clean_creature_glb.py`) with a new
`fire_lizard.byteanim` (`EmberCub • Idle/Walk/Run/Tackle/Fireball/Hurt`).
Plugin Guid properties were replaced with scene-object names (`teamObjectName`,
`slotOneName`..`slotThreeName`), and the greybox Mushlings were removed.

Verified: plugin builds with 0 warnings; `be_runtime/validate` reports 180 objects
and no issues; every authored Fire Lizard clip name resolves against the imported
model (`tools/verify_animation_clips.py`).

Left: still no Play test from the shell; capture, essence, enemies and Synergy are
next.


## 2026-11-14 — Wildling Adventures prototype systems (project `Documents/wildlings`)

Changed: `Plugins/Wildlings` grew from the scaffold into eight gameplay
components — `TeamManager3D`, `WildlingStats3D`, `WildlingCombat3D`,
`WildlingCompanion3D`, `WildlingStatus3D`, `WildlingProjectile3D`,
`WildlingHud3D`, `TrainerFollower3D` — with GDD v0.2 species numbers in
`Species.cs` (Fire Lizard / Spriglet / Zappet). Built and installed from the MCP
server. The user project `Scenes/Main.bytescene` now carries `HealthComponent`,
stats, combat, status, team manager and HUD on the `player` object, two greybox
Mushlings on the Enemy layer, and a `Wildling HUD` UiCanvas.

Verified: plugin builds with 0 warnings; `be_runtime/validate` reports 172
objects and no issues; `be_plugin/list` shows `studio.wildlings` enabled and
loaded. No Play test was possible from the shell (MCP has no headless Play).

Left: capture, essence/charm, Fangling/Moss Golem, Synergy and sanctuary work are
not started. The three-member party uses `Mannequin.glb` placeholders with two
authored `.byteanim` profiles until creature art lands.

## 2026-11-13 — Expanded the ByteEngine MCP authoring server

Changed: MCP grew from 9 read/basic-edit tools to 11 tools covering project
templates and creation, asset import (`import`/`import_folder`), model sub-asset
inspection, import settings and reimport, scene transform/duplicate/tags/variables
edits, Blueprint extraction, plugin scaffold/build/install/update, whole-project
validation, explicit Windows/Web export plus web analysis, and editor launch.
`ProjectTemplate`/`ProjectTemplateFactory` moved from the editor to
`Engine/ByteEngine.Core/Authoring` so both hosts create identical projects.
`AssetDatabase` now exposes `DetectAssetType` and full model import settings.
Added `Tools/PublishMcp.ps1`; the server is published to `Dist/ByteEngine-MCP`
and registered with Codex and Cline.

Verified: `dotnet build ByteEngine.sln` succeeds; the default test suite passes
(v0.7–v0.8-d regressions and Phase 1/2/3); published
`ByteEngine.Mcp.exe --self-test` covers creation/import/scene/Blueprint/plugin
paths; the stdio handshake lists all 11 tools; a disposable plugin scaffold built
in 10.7 s, installed, and exposed `smoke.mcp.McpSmokeSystem3D` after reopen.

Pitfall: `dotnet build` launched from the stdio server must disable MSBuild node
reuse `-nodeReuse:false`, clear `MSBUILDDISABLENODEREUSE`, close stdin and read
output via events. Otherwise reused nodes hold the pipes open and the MCP call
never completes.

Left: no headless Play/screenshots; editor-only templates still need the Project
Browser; exports require matching published runtimes. Next task: create the first
game project with the MCP and author with engine components.

## 2026-11-13 — Reviewed Cline engine updates before Git publication

Changed: navigation cache lifetime and path invalidation; safe physics candidate
reuse after solver/joint movement; configured fixed callback delta; removed unsafe
crowd frame modulo; corrected README, memory and performance claims. Secondary
render experiments stay opt-in; harness repetitions/multi-mode scheduling fixed.
See Docs/Cline-Update-Review-2026-11-13.md.

Main checks: dotnet build Tests/ByteEngine.Tests -c Release --no-restore -m:1
-nr:false -p:UseSharedCompilation=false; then dotnet run --no-build -c Release
--project Tests/ByteEngine.Tests with --engine-optimization, --roadmap-physics,
--roadmap-integration, --web-cpu-regressions and no flag for the default suite.
Secondary: same test-project build flags; --ai-query, --post-contexts,
--render-pipeline, --native-skinning; dotnet run -c Release --project
Tests/ByteEngine.Network.Tests and Tests/ByteEngine.Network.Integration.
All successful validation uses current assemblies. Build logs retain warnings.
Harness dry runs verify 16 launches for 8 repetitions per arm, and 12 for 4
repetitions across three modes. Raw logs: .artifacts/cline-review-20261113.

Limitations: no controlled FPS speedup claim, actual overseas test, game export,
runtime publication or branch merge. Keep both branches separate when pushing.

## 2026-11-13 - Performance harness, crowd phase buckets, multiplayer triage

Changed (worktree `52af`, branch `codex/event-sheet-ux-and-engine-updates`):
- Measurement: `Tools/Performance/Measure-Performance.ps1` (interleaved ABBA/BAAB)
  and `Aggregate-Performance.ps1` (paired medians, 4000-seed bootstrap, CI gate).
- Instrumentation: per-phase allocation bytes in `RuntimeFrameMetrics` and `Scene`;
  `queryFullIndexRefreshes`, `queryIncrementalBoundsUpdates` and `allocatedPerFrame`
  now appear in the stress artifact.
- Crowd: shared crowd clock plus `CrowdPosePhaseBuckets` collapses pose keys, and the
  existing `RenderBatcher`/`PrepareInstanceStream` then instances the draw batches.
- Rendering: `AdaptiveOcclusion` defaults on, R11G11B10F resolve, opt-in shadow
  resolution cap.
- Multiplayer: pre-accept timeout 15 to 60 s, inbound signals re-arm it, lobby polls
  and signals use independent in-flight slots, ICE diagnostics added.

Verified: `MiningPrototype` builds with 0 errors and the DLL contains the new
symbols; `--ai-query` exit 0; network suites 4/4 and 3/3; harness statistics checked
against synthetic data with a known effect. No real repeat sweep ran, because the
stress scene does not load in that worktree.

Left: commit with the three docs; enable `ShareCrowdPoses` on drones; settle the
merge direction between main and the multiplayer branch.

## 2026-11-13 — Memory Bank initialized

Changed:
- Added `.clinerules/memory-bank.md` (the protocol Cline follows each session).
- Added `MemoryBank/` with `projectBrief.md`, `productContext.md`,
  `systemPatterns.md`, `techContext.md`, `activeContext.md`, `progress.md`,
  `decisions.md`, `sessionLog.md`.
- Seeded content from `README.md`, `AGENTS.md`, `Docs/`, the solution layout and
  the uncommitted crowd-performance work.
- Added a memory pointer to `AGENTS.md` so non-Cline agents use the same files.

Verified: repository structure and script parameters read directly
(`Directory.Build.props`, `Tools/*.ps1`, `Tests/ByteEngine.Tests/Program.cs`,
`Plugins/DuneCompany/plugin.json`); `git status` shows the uncommitted crowd work.
No build or test run was performed as part of this setup.

Left: confirm the uncommitted crowd-performance edits build and pass
`CheckNavigationAllocation`; then commit them with
`Docs/EnginePerformance/crowd-animation-pathfinding.md`.
