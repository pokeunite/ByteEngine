# Decisions

Append-only. Newest first. One entry per choice that would be expensive to
rediscover. Record what was decided, why, and what was rejected.

## 2026-11-16 — Native Windows recording without an encoder executable

Use Media Foundation sink writing for MP4/H.264/AAC and WASAPI shared loopback for playback audio. Native ABI calls are confined to WindowsMp4Writer and WindowsPlaybackCapture; a real encode/decode test checks them against Windows. Core enables unsafe blocks for the small verified COM vtable adapter. No new NuGet/native redistributable, bundled FFmpeg, microphone access or per-game hardcoded capture component. Capture the final LDR Game View/window; store local user preferences separately from exports. The current feature is Windows-only. A bounded producer queue and worker encoder limit queued frame memory; timestamped held images and silence maintain stream progress when rendering/playback pauses. Calibration handles demonstrably invalid future device timestamps.

## 2026-11-14 — one canonical engine supports Hazard Pay

Integrate both histories into main instead of maintaining a separate palclone engine. Canonical root is C:/Users/codex/ByteEngine; distribution and project plugins are rebuilt together. Archive stale browser runtimes instead of presenting them as compatible with the consolidated Core; explicit browser refresh remains required. Preserve all branch history and retired artifacts outside the active source tree. Headless authoring preserves graphics references without GPU uploads and reports missing runtime components as validation failures.

## 2026-11-13 — MCP wraps shared Core authoring, not editor automation

Decision: `Tools/ByteEngine.Mcp` edits projects through `ProjectSerializer`,
`AssetDatabase`/`AssetManager`, `SceneSerializer`, `BlueprintSerializer`,
`EventModuleSerializer`, the shared `ByteEngine.Core.Authoring.ProjectTemplateFactory`,
`BytePlugin.targets` and the runtime exporters. Starter scenes moved into Core so
the editor and MCP cannot drift. MCP always installs into the same project package
folders the editor reads.

Why: agents need deterministic, reviewable edits with revision tokens. UI mouse
automation and duplicated template/import code would drift and be unsafe. The only
process MCP starts is `dotnet build` for plugin source, with node reuse disabled
(`-nodeReuse:false`, `MSBUILDDISABLENODEREUSE=1`, closed stdin, event-based output)
because reused MSBuild nodes otherwise keep the stdio pipes open and the call hangs.

Rejected: referencing `ByteEngine.Editor` from the MCP host (net9.0-windows, ImGui,
OpenTK, editor-only asset factories); duplicating starter scenes inside the MCP;
shelling out to the editor for imports; running exports implicitly.

## 2026-11-13 — Keep agent memory in a tracked `MemoryBank/` folder

Decision: durable cross-session state lives in `MemoryBank/`, loaded through
`.clinerules/memory-bank.md`, and is committed with the code it describes.

Why: conversation history is lost between chats, and this repository is too large
to rediscover per task. A rules file alone is not enough; the facts have to live
somewhere reviewable.

Rejected: relying on a single growing `AGENTS.md` (unreviewable, mixes standing
instructions with volatile state); gitignoring the memory files (loses continuity
across machines and clones).

## 2026-11-13 — Cache navigation region per agent

Decision: `NavigationAgent3D` caches the resolved `NavigationRegion3D` and
re-resolves only when `RegionObject` changes or after a failed lookup (0.5 s
retry).

Why: the previous implementation projected every component in the scene with
LINQ per agent per frame, producing ~74 bytes of garbage per agent per frame.

Rejected: a scene-level region registry. Correct long term, but a larger change
than the regression required.

## 2026-11-13 — Jitter repaths, keep the first query immediate

Decision: `StaggerRepaths` multiplies the next interval by 0.85–1.15 after the
first query.

Why: `SetDestination` sets the timer to zero, so a spawn wave replanned on one
frame and stayed phase-locked, producing a sawtooth spike proportional to wave
size.

Rejected: randomising the initial destination or delaying the first query, both
of which change gameplay-visible behavior.

## 2026-11-13 — Retain A* scratch, return private path copies

Decision: `NavigationGrid` and `NavigationMesh` keep search dictionaries, parent
maps, closed sets and priority queues between queries, clearing them per query.
Each returned path is still a fresh copy.

Why: per-query allocation scaled with crowd size and produced steady garbage.

Rejected: returning the internal path buffer (aliasing bugs between agents).

## 2026-11-13 — Split physics broad phase out of the solver loop

Decision (corrected during review): prepare candidates at substep start and reuse
them only while collider transforms remain unchanged. Solver position corrections
and joints require a refresh before the next iteration.

Why: the broad phase (gather colliders, refit both bounds trees, query both trees,
sort candidates) was re-run once per solver iteration — eight times per substep at
the default iteration count.

Rejected: lowering iteration count, or unconditional once-per-substep reuse.
Detection results can change during an iteration; a solver-created trigger
overlap is covered by a regression.

## 2026-11-13 — Preserve playback-time crowd quantization

Decision (corrected during review): keep the quantized playback-time key, and
upload immutable cached meshes as static data. Playback already wraps loops.

Why: a second modulo by rounded frame count corrupts non-looping terminal poses
and fractional clip endings. The unbounded-key failure was not demonstrated.

Rejected: the added frame-count modulo. Pool capacity remains a separate limit.

---

## Established before 2026-11-13

- **One version source.** `ByteEngineVersion` in `Directory.Build.props` feeds
  assembly, file and informational versions everywhere. Rejected: per-project
  version literals, which drifted.
- **Game projects live outside the engine repository**, under
  `C:/Users/codex/Documents`. Rejected: sample content inside the repo, which
  blurred engine versus game ownership.
- **`Dist/ByteEngine/BrowserRuntime` is the canonical web runtime**, mirrored into
  editor `bin/Debug` and `bin/Release` by `Tools/Sync-BrowserRuntime.ps1`.
  Rejected: building the browser runtime independently per configuration, which
  produced divergent copies.
- **Any plugin change requires `Tools/PublishBrowser.ps1`** because the browser
  runtime is statically linked. Rejected: runtime plugin loading on web.
- **No game exports without explicit user authorization.** Rebuilding the browser
  runtime is not an export.
- **Fixed-step simulation is opt-in per scene.** Rejected: enabling it globally
  while authored scene callbacks still assume variable steps.
- **Tests are a console runner with flag-selected suites**, not xUnit, and hot
  paths assert allocation budgets. Rejected: a test framework migration mid-flight.

- **Performance claims require interleaved repeats and a bootstrap CI.** Two
  identical baseline runs measured 31.77 ms and 49.97 ms, so a single-run A/B is
  indistinguishable from drift. Rejected: one run per condition, which is what the
  2026-11-11 crowd experiments did.
- **Crowd pose sharing stays opt-in and collapses phases onto a shared clock.**
  Buckets above one make actors inside a bucket animate in lockstep. Rejected:
  quantising each actor's own clock, which turns animation into stop-motion, and
  sharing unconditionally, which fixes arbitrary crowds to one phase.
- **`Scene.InvalidateQueryBounds` must keep bumping `QueryStructureRevision`.** A
  bounds-only split breaks collider removal because the incremental refit cannot
  drop an index entry; `AIQueryOptimizationTests` fails on "disabled collider
  disappears immediately". Rejected: the one-line split, reverted after the gate
  caught it.
- **Crowd instancing needs no new code.** `RenderBatcher` groups on
  `(Mesh, Material, Shadows)` by mesh reference and `PrepareInstanceStream`
  instances any batch above one, so shared cached pose meshes instance for free.
  Rejected: a second instancing path for crowd draws.

- **Windows game exports are a single executable whenever PlayerRuntime\Bundled\ByteEngine.Player.exe exists.** The exporter copies that player, appends Game.bytepak behind a 64-byte footer (SingleExecutablePackage), deletes the loose package and hashes the whole EXE in game-package.json. The folder layout (Runtime/ + loose Game.bytepak) remains only as the fallback. Reason: players must receive one file; Tools/PublishPlayer.ps1 and Rebuild-ByteEngine-And-Refresh-Dist.ps1 already build and identity-check the bundled player. Rejected: keeping folder-only exports (the regression this fixed).

- **openal32.dll is a publish Content item in Player\ByteEngine.Player\ByteEngine.Player.csproj.** A post-publish copy alone lands outside the single-file bundle, so the exported EXE failed with Native game dependency could not load: openal32.dll. Rejected: weakening the native probe or shipping the DLL loose beside the EXE.


- **Never publish a distribution across engine lines without loading the project's plugins against it first.** palclone + KarlsonDemo.byteplugin v3.93.0 were built for codex/event-sheet-ux-and-engine-updates; publishing main into Dist dropped every plugin component with only Plugin entry point ... was not found in the log. Rejected: treating plugin load failure as an editor bug and republishing again.

- **Plugin load must bind the host ByteEngine.Core explicitly.** Returning null from ProjectPluginLoadContext.Load delegates the bind to the default context, which rejects a plugin compiled against an older Core assembly version. The loader now returns the loaded Core assembly so type identity stays with the running engine. Rejected: rebuilding every plugin on every engine bump.


- **Asset-cache trimming keeps textures referenced by surviving materials.** TrimCaches(keep) evicted a texture whose material was still in the editor scene (or a kept model material), so the first frame after Play stopped threw ObjectDisposedException: Texture2D. Material.Textures() now feeds a referenced set that both protects cache entries and blocks disposal; MaterialTextureSampling.Bind also skips a disposed texture as a last-resort guard. Rejected: only guarding the sampler, which hides missing textures instead of keeping them alive.

- **Cline workflow is fixed by AGENTS.md (2026-11-14).** One assistant per checkout, work only in C:\Users\codex\ByteEngine, never hand-edit Dist/, Backups/, UpdateBackups/ or the retired 52af worktree, one coherent change per commit with a test, before/after measurements for performance work, then Rebuild-ByteEngine-And-Refresh-Dist.ps1 and push. Engine code stays here; Hazard Pay scenes/assets stay in Documents\palclone. Rejected: ad-hoc publishing or parallel agents against the same tree, which is how the 11/14 palclone break and the swept-up temporary commit happened.

