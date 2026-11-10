# Native Windows performance — implementation brief

Target checkout: `C:/Users/codex/.codex/worktrees/52af/ByteEngine` (the one that publishes the editor,
the Windows player and the browser runtime). Written for an implementing agent: no code here, only
decisions, file paths, metrics and acceptance criteria.

Scope: **native GL path** — the Windows player and the editor's Play preview (`ByteEngineApplication`,
`Renderer3D`, `RenderWorld`, `SceneFramebuffer`, `PostProcess3D`). The browser path is already tuned and
must not regress; Core files are shared, so every Core change is verified against both.

Related: `Docs/Engine-Pipeline-Fixes-2026-11-05.md`, `Docs/Browser-Texture-Lifetime-Fix-2026-11-07.md`,
`Docs/Multiplayer-Implementation-Brief.md`.

---

## 1. Goals (numbers, not adjectives)

Reference machine: the current dev box (AMD Vega 10 iGPU class, 8 cores). Scene: Hazard Pay `Main`
(≈3 900 objects, ≈226 colliders, ≈150 draws, 600 k triangles) plus a 30-enemy combat wave.

| Metric | Target | Notes |
| --- | --- | --- |
| 1080p, Balanced, combat | ≥ 60 fps avg, 1 % low ≥ 45 | vsync off for measurement |
| 720p, Balanced, combat | ≥ 90 fps avg | |
| Frame CPU (update + render submit) | ≤ 8.0 ms at 60 fps | excludes swap |
| GPU geometry (existing timer) | ≤ 6.0 ms at 1080p | `GraphicsDiagnostics.GeometryGpuMs` |
| Shadow passes | ≤ 1.5 ms GPU total | directional + point |
| Post chain | ≤ 2.5 ms GPU at 1080p | `GraphicsDiagnostics.PostGpuMs` |
| GL calls per frame | ≤ 1 500 | count in the renderer |
| Managed allocations | ≤ 32 KB/frame steady state, 0 GC0 in combat | `Scene.LastFrameMetrics.AllocatedBytes` |
| Editor Play preview | within 10 % of the standalone player at the same resolution | compare directly |
| Level load hitch | no single frame > 200 ms | |

Every number is a gate in a benchmark test, not a suggestion. If a number turns out to be impossible on
the reference machine, record the achieved value in this document and adjust the gate — never silently
drop it.

---

## 2. Phase 0 — measure before touching anything

The engine already has most of the instrumentation. **Do not build a new profiler; wire up what exists.**

| Already exists | File | What it gives |
| --- | --- | --- |
| `SurfacePerformanceDiagnostics` | `Engine/ByteEngine.Core/Diagnostics/SurfacePerformanceDiagnostics.cs` | frame CPU + swap ms, GPU timer queries |
| `GeometryGpuTimer` | `Engine/ByteEngine.Core/Graphics/3D/GeometryGpuTimer.cs` | main geometry GPU ms + CPU ms |
| Post timing | `Engine/ByteEngine.Core/Graphics/3D/PostEffects3D.cs` | `GraphicsDiagnostics.PostGpuMs` |
| Pass profiler | `Engine/ByteEngine.Core/Graphics/3D/RenderPassProfiler.cs` | per-pass CPU + timestamp pairs |
| Scene frame metrics | `Scene.LastFrameMetrics` (`RuntimeFrameMetrics`) | update / physics / late / animation / allocations |
| World stats | `RenderWorld.LastStats`, `MainTriangles`, `InstancedObjects`, `OccludedMeshes`, `OcclusionMilliseconds` | submissions, cages, instances |
| Editor frame profile | `EditorFrameProfile` (`EditorApplication.OnEngineRender`) | per-label CPU in the editor |
| Component profiler | `Scene.ComponentProfiler` | per-component update cost |

Add only these:

1. `Renderer3D`: public counters `GlCallCount`, `ShaderSwitches`, `TextureBinds`, `UniformUploads`,
   `DrawCalls`, `InstancedDraws`, incremented behind `GraphicsDiagnostics.Enabled` (a bool that is off in
   shipped builds). One `long` per counter, no per-call allocation.
2. `Tests/ByteEngine.Tests/--native-bench <sceneProject> <outCsv>`: launches the real `Renderer3D` in an
   offscreen window at 1280×720 / 1920×1080, runs N warm frames + M measured frames of the given
   project's startup scene, and writes a CSV row per configuration with every metric above
   (p50/p95, GPU ms, GL counters, allocations).
3. `Tools/CompareNativeBench.ps1`: runs the bench for `before.csv` and `after.csv` and prints a delta
   table; fails if any gate regresses by more than 3 %.
4. Editor: extend the existing performance overlay to show the same counters while playing, plus
   `EditorFrameProfile` labels for "Game view render", "Scene view render", "ImGui build", "Swap".

Benchmark matrix (record each combination; this is the baseline table in the PR):

| Axis | Values |
| --- | --- |
| Resolution | 1280×720, 1920×1080 |
| Mode | standalone player, editor Play preview (Game view only), editor with Scene view also visible |
| Scene moment | idle first frame after load, 30-enemy combat, camera pan |
| Post | on (default look), off |
| Shadows | on, off |

Rule: **no optimization lands without a before/after row from this harness.** Suspected-but-unmeasured
work is allowed only if it also deletes obvious redundant work (e.g. a GL call that provably cannot
change state).

---

## 3. Findings from a code pass (ranked by expected win)

Each finding is a hypothesis to confirm in Phase 0, then an item in the backlog (§4).

### F1 — Per-draw uniform storm (highest CPU cost, lowest risk to fix)

`Renderer3D.Draw` uploads dozens of uniforms per draw: lights, matrices, environment, then
`UploadStandardMaterialMaps` (`Renderer3D.cs:867-880`) sets ~35 uniforms per material *per draw*
(`uUseHeightField`, `uHeightFieldUvTransform`, `uHeightFieldRange/Bias/WorldSize`, `uUvTiling`,
`uUvOffset`, `uNormalStrength`, `uSandSurface`, four wet vectors, water depth/surface/volume block,
sand imprint, `uNormalYSign`, …). `Shader` caches uniform *locations* only
(`Shader.cs:161-175`) — every `SetInt/SetFloat/SetVector4` still issues a `glUniform*` call even when the
value did not change since the last draw.

The browser runtime solved the same problem with `wwwroot/uniform-cache.js`; native has no equivalent.
With ~150 draws this is on the order of 4 000–6 000 redundant GL calls per frame, which is pure driver
overhead (and on AMD/ANGLE-class drivers uniform calls are not free).

**Fix:** value-cache uniforms per program. Store `(location, type, float/double/int payload)` in a
`Dictionary<int, …>` or a small array indexed by location; skip the `glUniform*` call when the cached
value equals the incoming one; invalidate the whole cache on `Use()` of a different program and after
`glLinkProgram`. Applies to `Shader.cs` (single implementation, used by all native shaders).

### F2 — Texture/sampler bind churn

`MaterialTextureSampling.Bind` (`MaterialTextureSampling.cs:21-57`) calls `texture.Bind(slot)`
(`GL.ActiveTexture` + `GL.BindTexture`) and `GL.BindSampler(slot, _sampler)` for every material texture on
every draw, and `Renderer3D` additionally calls `GL.BindSampler(13/14/15, 0)` inside
`UploadStandardMaterialMaps`. Up to 8 textures per draw ⇒ ~24 GL calls per draw, nearly always repeating
the previous draw's state because consecutive draws usually share the same maps.

**Fix:** a small per-renderer texture-unit state table: `activeUnit`, `boundTextures[32]`,
`boundSamplers[32]`, `glActiveTexture` only when the unit changes, `glBindTexture` only when the target
differs, `glBindSampler` only on change. Reset the table at the start of each pass and whenever external
code binds textures (add an explicit `Renderer3D.InvalidateTextureState()`).

### F3 — Directional shadow static cache practically never hits

`RenderDirectionalShadowMap` (`Renderer3D.cs:272-300`) only enables the cache when
`_previousDirectionalView == view.ViewMatrix`, i.e. only when the camera did not move *at all* between
frames. The light matrix is rebuilt from the camera frustum every frame, so any player movement
invalidates the static layer and the full caster set is re-rendered.

**Fix (in order):**
1. Make the shadow projection *stable*: quantize the light-space translation to shadow-map texel size
   and use a fixed shadow extent derived from `ShadowDistance` (no fit-to-frustum every frame). Then a
   moving camera produces the same matrix for many frames, and the static cache hits continuously.
2. Key the static caster cache on (light direction, static caster set + their `Transform` versions),
   **not** on the camera. Static geometry then renders into the shadow map once and is reused while the
   sun and the geometry are unchanged.
3. Only then consider cascades. One well-fitted cascade + texel snapping beats two unstable ones on this
   hardware.

### F4 — Point shadow cost

`RenderPointShadowMaps` renders cube faces for up to `RenderLighting3D.MaxPointShadowLights` lights
(`Renderer3D.cs:507+`), each face a full caster pass, every frame. `_pointDepthCaches` exists but is keyed
per light with the same camera-stability problem as F3.

**Fix:** budget-based point shadows — nearest 1–2 shadow-casting lights only, half-resolution cube maps
in `Balanced`/`Fast`, update alternating faces (2 faces per frame) when the light and casters are static,
skip faces whose direction is outside the camera frustum, and disable point shadows entirely in `Fast`.
Point shadows must never be the reason a frame misses 60 fps.


### F5 — Presentation path always pays for the offscreen target and full post chain

`ByteEngineApplication` render path (`ByteEngineApplication.cs:277-313`): every 3D frame renders into
`_runtimePostProcessTarget` (HDR colour + depth texture), then `Present(...)` runs the post chain and
blits to the window, then `FlushUi()` draws UI directly to the window. `RenderScale` defaults to 1
(`GraphicsLook.cs:13`), so at 1080p that is a full-resolution RGBA16F target plus full-resolution
tonemap/SMAA passes regardless of the quality tier.

Post effects themselves are already sensibly sized (`PostEffects3D.cs:29` – half-res AO, quarter-res
bloom, early-out when AO and bloom are 0). The remaining cost is the mandatory full-res HDR round trip.

**Fix:** a presentation policy that matches the quality tier:
- `Fast`: render straight to the back buffer when AO/bloom/SMAA are off (the code path already exists for
  2D, `ByteEngineApplication.cs:322-325`); no HDR target.
- `Balanced`: keep the HDR target, render at `RenderScale` 1.0 only if a measured frame fits; otherwise
  auto-render-scale (the browser already has this concept, `BrowserPresentationSettings.WorldResolution`)
  with the same 0.5–1.0 clamp.
- `High`/`Ultra`: full res, SMAA; `Fast`/`Balanced` use a single-pass FXAA.
- Never allocate the depth texture when nothing samples scene depth (water off) — `SceneDepthTexture.Create`
  is unconditional today (`RuntimePostProcessTarget3D.cs:155`).

### F6 — Per-frame LINQ and closures

Confirmed per-frame allocations in the render path:
- `RenderWorld.cs:299` `visible.Any(s=>s.Material.WaterSurface)` — enumerator + closure every frame.
- `Scene.cs:463` `_gameObjects.SelectMany(...).OfType<...>().FirstOrDefault() ?? new LoadingScreenStyle()`
  runs **every frame while a loading screen is visible** and allocates a style object per frame.
- `JobRunner.For` (`Runtime/JobRunner.cs`) allocates an `Action<int>` closure plus `ArrayPool` rents per
  call; called from `Scene` pose capture, `PhysicsWorld3D` collider gather and `RenderWorld` visibility.
  `Parallel.For` itself costs roughly 10–30 µs even when it decides not to parallelize; with a 64-item
  threshold and light work items this is plausibly a net loss.

**Fix:** replace the LINQ with plain loops or cached booleans; hoist the loading-screen lookup into a
cached field invalidated by scene changes; raise `JobRunner.DefaultParallelThreshold` after measuring
(try 256) and add a `For<TState>(count, state, static (i, s) => …)` overload so hot callers pass state
instead of capturing. Target ≤ 32 KB/frame allocated during combat (already tracked in
`RuntimeFrameMetrics.AllocatedBytes`).

### F7 — Editor Play preview duplicates work

`GameViewPanel` owns its own `SceneFramebuffer` (HDR + display FBO + `PostProcess3D` +
`RuntimePostProcessTarget3D` + `RenderWorld`, `SceneFramebuffer.cs:26-37`) and renders the full scene
through `_framebuffer.Render(...)` (`GameViewPanel.cs:136`). The Scene view panel renders the same scene
through its own framebuffer. While Play is active in a docked layout **both views render a full 3D frame
per editor frame**, each with its own shadow and post work, and `SceneFramebuffer.Render` calls
`renderer.Resize(width,height)` on every call (`SceneFramebuffer.cs:93`).

ImGui already knows which panel is visible, focused and collapsed — that information is currently unused
for throttling.

**Fix:**
1. Render at most one 3D viewport per editor frame: the focused Game view wins while playing; the Scene
   view renders at 1/4 rate (or not at all) while Play is active and the Game view is visible.
2. Skip entirely for collapsed/hidden panels.
3. Cap the preview resolution: default the Game view target to 1280×720 (or the panel size, whichever is
   smaller) with a setting "Match window". On a 150 % DPI 1440p window this alone removes 2–3× the pixels.
4. Reuse the framebuffer when the size is unchanged (assert no `GL.GenFramebuffer` per frame), and stop
   calling `renderer.Resize` unless the size actually changed.
5. In play mode, run `prepareEnvironmentLighting3D` only when the sky/environment actually changes (it
   currently re-runs per frame at `SceneFramebuffer.Render`'s call site).
6. `NativeEditorDocumentWindow` hardcodes `VSyncMode.On` (`NativeEditorDocumentWindow.cs:77`); make it
   follow `EditorPreferences.EditorVSync` like the main window so benchmarking and multi-window setups
   are not silently capped.

### F8 — Object/visibility CPU work per frame

`Scene.RenderInternal` (`Scene.cs:461-475`) calls `SkeletalAttachmentService.UpdateScene(this)` and
`GetRenderOrder()` every frame. `GetRenderOrder` compares every entry each frame
(`Scene.cs:573+`) — O(n) with a reference and order compare per object. With ~3 900 objects this is
hundreds of microseconds of pure bookkeeping. The browser path already has `RenderMembers` and a version
counter; the native path still walks the full list on every frame.

**Fix:** drive the render order list off a version (`RenderHierarchyVersion` / membership version) instead
of an element-wise comparison; keep the O(n) compare only for the editor path where objects move between
reparents constantly. Measure `SkeletalAttachmentService.UpdateScene` and skip it when no attachment
dirty flags exist.


### F9 — Main-pass ordering and state grouping on native

`RenderWorld` sorts submissions with `PortableRenderOrdering.Compare` and batches with
`RenderBatcher.Builder` without `MaterialAffinity`, while the browser sink enables it
(`Player/ByteEngine.Browser/Program.cs:302`). Native therefore orders opaque draws front-to-back but does
not group by material, so consecutive draws re-set the same material uniforms and re-bind the same
textures — it multiplies F1 and F2.

**Fix:** enable `MaterialAffinity` for the native main pass (it keeps front-to-back order inside a material
group) and verify with `RenderOptimizationTests` plus a screenshot diff.

### F10 — Skinned-mesh and animation CPU

GPU skinning and distance-sampled animation are already in. Remaining native cost: pose evaluation,
`SkeletalAttachmentService.UpdateScene` every frame, and bone palette uploads when the palette did not
change (the browser sink dedupes palettes with `RetainPacket`; native does not). Measure with
`Scene.LastFrameMetrics.AnimationMs`; target ≤ 2.5 ms with a 30-enemy wave, then dedupe palette uploads by
palette version and keep crowd poses behind the renderer's distance budget.

### F11 — Physics

`PhysicsMs` of 6–18 ms in the browser capture of a 226-collider scene is the largest update-side cost, and
native runs the same code. Measure first; then the cheap wins are: skip static-static pairs in broadphase,
cache collider world bounds while the owning `Transform` version is unchanged, key the contact cache on
collider pair + body versions, and only solve awake islands. Never change solver iteration counts without
running the physics fixtures (`--render-pipeline`, vehicle and ragdoll tests).

### F12 — Dynamic texture uploads

`Texture2D.UpdatePixels` uploads whole images; `UpdateRegion` exists but is barely used. Video
(`Media/VideoPlayer.cs:109`) and any per-frame generated map should use regions, or a persistent PBO with
`GL.FenceSync` for genuinely async uploads. Do this after the render-path work.

### F13 — Startup and load hitches

`GameProjectRuntime` loads synchronously; the browser spreads work with `ProgressiveSceneFactory`. Native
should keep the progressive path, add background file IO and decoding on a worker pool (decode off-thread,
upload on the GL thread with a per-frame texture budget), and warm every shader program once at startup
(compile, link, draw into a 1×1 viewport) so first-use compile stutter never reaches gameplay.

---

## 4. Backlog (execute in this order)

One item at a time, with a bench row each. "Gate" = the acceptance test.

### P1 — Uniform value cache (CPU, biggest single win)
- Change: cache the last uploaded value per uniform per program in `Shader`; skip redundant `glUniform*`.
- Files: `Engine/ByteEngine.Core/Graphics/Shader.cs`.
- Gate: `UniformUploads` down ≥ 70 %, frame CPU down ≥ 1.5 ms, identical pixels.

### P2 — Texture unit and sampler state cache
- Change: per-renderer state table; no redundant `ActiveTexture`/`BindTexture`/`BindSampler`.
- Files: `Graphics/3D/MaterialTextureSampling.cs`, `Graphics/3D/Renderer3D.cs`, `Graphics/Texture2D.cs`.
- Gate: `TextureBinds` down ≥ 60 %, CPU down ≥ 0.5 ms, anisotropic road/terrain screenshot diff clean.

### P3 — Stable directional shadow matrix + transform-keyed static cache
- Change: texel-snapped fixed-extent light matrix; static casters cached on light direction and caster
  transform versions instead of camera equality.
- Files: `Graphics/3D/Renderer3D.cs` (`RenderDirectionalShadowMap`, `BuildDirectionalShadowMatrix`),
  `Graphics/3D/ShadowDepthCache.cs`.
- Gate: directional shadow ≤ 1.0 ms GPU at 1080p while panning; static casters re-rendered at most once
  per second when nothing moves; no shimmer/crawl in the screenshot diff.

### P4 — Point shadow budget
- Change: nearest 1–2 shadow lights, half-res in Balanced, alternating face updates, frustum-facing faces
  only, off in Fast.
- Files: `Graphics/3D/Renderer3D.cs` (`RenderPointShadowMaps`), `Graphics/3D/RenderLighting3D.cs`.
- Gate: point shadow ≤ 0.5 ms GPU total; torch/lamp fixture unchanged.

### P5 — Presentation policy
- Change: tier-driven path; auto render scale with hysteresis; FXAA in Fast/Balanced; skip the HDR target
  when no post is active; no depth texture when nothing samples depth.
- Files: `ByteEngine.Core/ByteEngineApplication.cs`, `Graphics/3D/RuntimePostProcessTarget3D.cs`,
  `Graphics/3D/PostProcess3D.cs`, `Graphics/GraphicsLook.cs`.
- Gate: 1080p Balanced ≥ 60 fps avg, `Fast` 1080p ≥ 90 fps, post ≤ 2.5 ms GPU, Balanced pixels unchanged.

### P6 — Editor Play preview throttling and resolution cap
- Change: one 3D viewport per editor frame, throttled Scene view, collapsed-panel skip, preview
  resolution cap (default 1280×720 or panel size, whichever is smaller), no per-frame resize,
  environment lighting only on change, VSync follows the preference.
- Files: `Editor/ByteEngine.Editor/Panels/GameViewPanel.cs`, the Scene view panel,
  `Editor/ByteEngine.Editor/SceneFramebuffer.cs`, `EditorApplication.cs`,
  `NativeEditorDocumentWindow.cs`.
- Gate: preview within 10 % of the standalone player at 1280×720; editor frame CPU down ≥ 35 % with both
  views open; zero framebuffer reallocations while playing.

### P7 — Native material-affinity ordering
- Change: `MaterialAffinity = true` for the native main batch builder.
- Files: `Graphics/3D/RenderWorld.cs`.
- Gate: material uploads and shader switches down ≥ 30 %; screenshot diff clean.

### P8 — Per-frame allocation cleanup
- Change: F6 items — LINQ removal, cached loading-screen style, `JobRunner` state overload and retuned
  threshold.
- Files: `Graphics/3D/RenderWorld.cs`, `Scene/Scene.cs`, `Runtime/JobRunner.cs`,
  `Physics/PhysicsWorld3D.cs`.
- Gate: ≤ 32 KB/frame allocated in combat, zero GC0 over a 60 s run.

### P9 — Occlusion culling quality and cost
- Change: keep the software culler (it already exists, `RenderWorld.EnableOcclusionCulling`), but feed it
  the previous frame's depth instead of re-rasterizing occluders at full detail; skip it entirely when
  `OcclusionMilliseconds` exceeds 0.6 ms or when the scene has no large occluders; use it to also reject
  shadow casters outside the light frustum.
- Files: `Graphics/3D/SoftwareOcclusionCuller.cs`, `Graphics/3D/RenderWorld.cs`.
- Gate: culling CPU ≤ 0.6 ms, `OccludedMeshes` unchanged or better, no popping on the camera-pan capture.

### P10 — Async asset loading and shader warmup
- Change: F13 — background decode, one upload per frame budget, shader warmup at startup.
- Files: `Runtime/GameProjectRuntime.cs`, `Runtime/RuntimeAssetPrewarmer.cs`, `Graphics/Texture2D.cs`,
  `Graphics/3D/Renderer3D.cs` (warmup entry point), `Editor/.../GameExportPanel` unaffected.
- Gate: no frame > 200 ms during startup and scene load; first-use shader compile absent from gameplay
  (log must show compiles only during warmup).

### P11 — Physics cost work (only after measurement)
- Change: F11 — broadphase pair pruning, transform-version bounds cache, contact cache keys, awake-island
  solver.
- Files: `Physics/PhysicsWorld3D.cs`, `Physics/PhysicsBroadPhase*`, collider bounds helpers.
- Gate: physics ≤ 4 ms at 226 colliders with 30 enemies; all physics fixtures pass unchanged.



---

## 5. Guardrails (read before writing code)

1. **Core is shared with the browser.** `RenderWorld`, `RenderBatcher`, `Scene`, `PhysicsWorld3D`,
   `JobRunner` and `Texture2D` are used by the wasm runtime. Any change there must keep the browser
   bench and the browser test suite green (`Tools/TestBrowserRuntime.cjs`, `TestBrowserRetainedGpu.cjs`,
   `TestBrowserEngineFixes.cjs`), and must not add native-only GL calls behind a browser code path.
   Two proven ways to stay safe: gate by `OperatingSystem.IsBrowser()`, or make the change purely a
   *dedupe* of redundant work (uniform/texture caches, LINQ removal) which is correct everywhere.
2. **No GL from background threads.** All state caching in P1/P2 is per-context and must reset when the
   context changes (`GraphicsContextScope.CurrentContextId` already exists and is used by
   `ShadowDepthCache`).
3. **Two GL contexts exist in the editor** (main window + document windows share a context). Caches keyed
   by program or texture id must survive/refresh correctly when the current context changes; add an
   explicit test that opens a second window and renders.
4. **Quality tiers are a contract.** `Fast`/`Balanced`/`High`/`Ultra` must keep their meaning across the
   player and the editor preview. Do not change a tier's look without recording it in this doc.
5. **No visual regressions.** Every render-path item ends with a screenshot comparison against the
   pre-change build on: default scene, water scene, sand/terrain scene, night scene with point lights,
   UI-heavy scene. `Tests/ByteEngine.Tests --render-pipeline` and the quiet "24 randomized image
   comparisons" suite already exist — extend rather than replace them.
6. **No speculative rewrites.** Do not move the renderer to a separate thread, do not replace the
   render graph, do not switch to bindless/DSA, do not "optimize" the physics solver. Everything in this
   brief is measured, local and reversible.
7. **Keep the public API stable** for plugins (`Renderer3D.Draw`, `RenderWorld.Submit`, `Material`).
   Internal caches only; no signature churn.
8. **Document each item** in this file as it lands: measured before/after from the bench CSV, files
   touched, and any tier/look change.

---

## 6. Definition of done

1. `--native-bench` exists, runs unattended, and writes the matrix in §2; `CompareNativeBench.ps1` fails a
   regression > 3 %.
2. Every gate in §1 is met on the reference machine, or the achieved value is recorded here with the
   reason.
3. Play preview is within 10 % of the standalone player at the same resolution, with both editor views
   open.
4. Zero GC0 during a 60-second combat run; ≤ 32 KB/frame allocated.
5. GL call counters show ≥ 70 % fewer uniform uploads and ≥ 60 % fewer texture binds than the baseline.
6. Browser runtime rebuilt from the same commit, its perf capture shows no regression (frame p50, GPU ms,
   `draws`), and `browser-runtime-integrity.json` validates.
7. Windows player and editor binaries republished (`Tools/PublishPlayer.ps1`,
   `Tools/PublishEditor.ps1`) so the gains exist in the installed build, not just in source.

---

## 7. Suggested first week (highest value per hour)

| Order | Item | Why first |
| --- | --- | --- |
| 1 | Phase 0 bench harness | Without the CSV every later claim is anecdote |
| 2 | P1 uniform cache | Largest CPU win, isolated to one file, zero visual risk |
| 3 | P2 texture state cache | Same class of win, complements P1 |
| 4 | P6 preview throttling | "Play preview feels slow" is the user-visible complaint; mostly editor-side, no shader risk |
| 5 | P3 shadow stability | Usually the single largest GPU win once the camera moves |
| 6 | P5 presentation policy | Makes 1080p playable on the reference iGPU |

Then P4, P7, P8, P9 and finally P10/P11, re-running the full matrix each time.

