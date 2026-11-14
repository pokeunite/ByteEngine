# Session log

Newest first. One entry per work session or hand-off. Keep each entry short:
what changed, what was verified, what is left.

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
