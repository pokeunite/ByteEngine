# Browser export reliability and CPU profiling

## Unity research: scope and provenance

On 2026-10-28 the public Unity-Technologies/UnityCsReference repository was
downloaded at commit `bf6028387a4fc2ea6d41aa7d8998e774655aedf6` into the ignored
local directory `artifacts/Research/UnityCsReference`.

Sources inspected: the repository README and licence, public WebGL player
settings, and the available build pipeline file inventory. The public settings
bind to native engine implementation which is not supplied in that repository.
This is reference-only source, not reusable exporter code. No Unity source has
been incorporated into ByteEngine or distributed with its runtime.

- https://github.com/Unity-Technologies/UnityCsReference
- https://unity.com/legal/licenses/unity-reference-only-license
- https://unity.com/glossary/webgl
- https://docs.unity.com/en-us/engine/7000.0/manual/platform-specific/webgl/develop/performance

The implementation in this change is independent work against concrete
ByteEngine failures and measurements. This is not a port of Unity's web backend.

## Export integrity

`Tools/PublishBrowser.ps1` records SHA-256 fingerprints of runtime resources in
`browser-runtime-integrity.json`. Before creating a game export, the exporter
checks the manifest, required browser modules, boot resource hashes and
untracked framework files. A partially copied runtime or an old JavaScript
renderer paired with newer WASM must fail before an export folder is created.

Plugin freshness now compares the installed DLL fingerprint as well as its
version. A changed plugin with an unchanged version is not silently accepted.
Refresh the browser runtime and installed project package together. This check
can legitimately reject independently rebuilt plugin binaries; it establishes
binary identity, not semantic equivalence.

These checks establish file consistency, not absence of gameplay errors or
acceptable performance. A successful export must not be labelled playtested.

## Shared runtime improvements

Event evaluation reuses per-runtime scratch collections rather than allocating
condition maps, result sets, trigger lists and action maps for every rule on
every frame. Each nested evaluation owns a separate workspace until return;
maps are rebuilt to preserve live Event Sheet edits. Cleared workspaces do not
retain instruction references, and idle pool depth is capped at 64.

Component callback capabilities are discovered once per type. Objects without
an implemented Update, LateUpdate or FixedUpdate callback skip that phase's
clock lookup, sorting and dispatch. Startup still runs, including components
which add update callbacks during startup. Add/remove invalidates the callback
flags. The type cache uses weak keys so it does not independently retain plugin
types. Desktop and browser use these same Core paths.

Packaged desktop/browser hosts disable editor-only Event Sheet trace recording.
Editor Play keeps its existing live highlighting behaviour.

Browser visibility sorting reuses a list and preserves the previous queue,
transparent distance and submission-index ordering. Instance matrix packing
uses indexed loops with the same 16-float layout and independent shadow filter,
avoiding per-matrix temporary arrays and generic SelectMany<float> interpretation.

## Profiling and release checks

`Scene.ComponentProfiler` is opt-in and disabled by default. It aggregates the
current frame by component type and callback phase, with call count, total time
and the slowest object. Its default mode resets on every scene update. Browser diagnostics
expose `ProfileComponents(bool)` and `ComponentTimings()`. Profile a representative
frame to identify costs; disable it when measuring release FPS, because timing
each callback adds overhead.

The downloadable recorder also supports bounded accumulation windows through
`CaptureProfileComponents(bool)`. This retains callback/object/system totals
across frames and transfers them at an adaptive cadence, reducing serialization
overhead. See [Web performance capture](Web-Performance-Capture.md) for tester
controls, scope, limits and interpretation. Normal frame recording is automatic;
F9 enables 30 seconds of deep profiling and F8 opens the download panel.

`Tools/TestMiningCombatBrowser.cjs` can run against an existing exported site's
content with a separately published runtime overlay. This does not export a new
game. It tests fresh-game flow, player movement, actual enemy movement and sane
animation bounds, and writes component timings and screenshots. Set
`BYTEENGINE_PROFILE_IDLE_ONLY=1` for an idle diagnostic only. Scene reloads are
handled by enabling profiling on the active scene at each test tick.

`Tools/TestFullMiningBrowser.cjs` runs the actual browser main loop, captures
steady-state timings, and fails on page errors or failed network requests.
It uses a private browser profile, not existing user saves.

`node Tools/ValidateBrowserCapture.mjs <game.json> [minimum-fps]` writes a separate
validation report and returns a failing exit status below the performance budget,
when uploads remain pending, or when reported runtime errors exist. Defaults:
30 FPS minimum and 50 ms p95 frame interval. It requires at least 30 intervals.
This is a captured-performance gate, not an automatic visual-parity guarantee.

## Remaining architectural work

The browser still uses a separate WebGL renderer and a serialized frame bridge.
This change does not replace them with the desktop renderer, add GPU skinning,
or prove equivalence of every shader, physics interaction or browser. Further
work should be based on whole-frame CPU profiles and matched gameplay/visual
fixtures. No assertion of a perfect or error-free web exporter is justified.

Performance measurements and full-scene test results for this pass are stored
under the ignored `artifacts/browser-hardening-*` directories and logs.

The initial optimized full-scene checks passed and 522 gameplay checks passed.
In sampled idle diagnostic frames, scene allocations fell from 215,392 to
53,160 bytes and Event Sheet update time fell from 6.6 to 2.5 ms. These are
individual profiled frames, not a statistically controlled FPS result.

Two normal-loop captures still measured only 8.6–8.9 FPS, failing the default
30 FPS / 50 ms p95 release gate. Detailed steady-state capture also exposed
substantial GPU cost (about 77 ms in one capture). Disabling shadows in a private
diagnostic reduced that cost but did not resolve the CPU ceiling. Therefore the
remaining bottleneck is not exclusively CPU or exclusively GPU.

After direct instance-matrix packing and the GPU timing correction, the final
normal-loop capture measured **17.6 FPS**, **83.1 ms p95**, about **48.4 ms managed
work**, and **6.2 ms browser render submission work** across 120 frames. The
current GPU timing averaged about **23.1 ms**; Auto resolution had selected
1024×576 for a 1280×720 viewport, with full-resolution UI. Earlier captures used
a different Auto scale and the old GPU timing selection, so this is not an
identical-resolution GPU benchmark. The final capture still **fails the 30 FPS
release gate**. No runtime errors were recorded in that capture, and the final
full-scene movement/combat check passed. This does not establish error-free
behaviour across all gameplay, browsers or machines.

Final evidence: `artifacts/browser-matrix-realtime/game.json.validation.json`,
`artifacts/browser-matrix-realtime/runtime-errors.json`,
`artifacts/browser-matrix-combat.log`, and `artifacts/browser-matrix-tests.log`.

The publisher archives exact-binary CPU symbol maps under the ignored
`artifacts/BrowserSymbols/<native-wasm-SHA256>.symbols`. They are not game assets.
Set `BYTEENGINE_CPU_PROFILE=1` when running the full-browser test for a five-second
CPU sample; `Tools/SummarizeBrowserCpuProfile.mjs` resolves function numbers using
the matching private map. New captures record the binary hash and the summarizer
rejects a differently named map. No private symbol map is added to a game ZIP.

## Component lookup and callback scheduling pass

Game objects now cache repeated component lookups (including misses), and reuse
stable callback schedules instead of copying and sorting components every phase.
Adding, removing or reordering components invalidates both caches. Mutable update
priorities are checked each phase; enabled state remains checked at execution.
Callbacks changing membership retain the current phase's snapshot, while later
phases see the change. Collectible plugin types are not stored as lookup keys.

Portable regression tests cover lookup inheritance, first-match order, negative
cache invalidation, callback priority changes, disabling, and membership changes
during Update. The full-scene browser movement/combat fixture passed.

Matched Balanced / fixed 1280x720 captures using the existing game export:

| Runtime | FPS | p95 interval | Managed work |
| --- | ---: | ---: | ---: |
| Previous runtime, initial baseline | 21.88 | 66.3 ms | 38.92 ms |
| Cached lookup/schedules | 23.14 | 50.0 ms | 36.33 ms |
| Previous runtime, repeat baseline | 22.42 | 50.2 ms | 37.69 ms |

The measured gain is modest (about 3–6%), with visible machine/run variation.
The candidate still fails the 30 FPS gate. Rendering quality and game settings
were unchanged between these captures. No runtime errors were recorded; this
does not prove all gameplay or browser combinations error-free. CPU skinning,
component queries and transform evaluation remain substantial sampled costs.

Evidence: `artifacts/next-component-realtime/game.json.validation.json`,
`artifacts/next-optimization-baseline/recheck/game.json.validation.json`,
`artifacts/next-component-combat.log`, and `artifacts/next-component-tests.log`.
No new game export was produced for this pass.

## Browser input and VFX parity checks

Mouse button state must use per-button `mousedown` / `mouseup` transitions:
`pointerdown` only signals the first held mouse button, so it loses aim/fire
chords. Touch still uses pointer events. Browser-reserved Escape is forwarded
from `pointerlockchange` as one game Escape press, retaining the capture state
for that frame. Menu-requested unlock does not create a second pause press.

VFX assets use generated typed JSON metadata and explicit four-component colour
reads, preserving alpha and vector properties under WASM AOT instead of invoking
SIMD setters reflectively. Desktop and browser use the same serializer.

`Tests/BrowserRuntime/Input.test.mjs` executes the shipped event listeners with
chorded buttons, independent release, Escape, intentional unlock and focus loss.
`Tools/TestBrowserInputVfx.cjs` exercises actual mouse/keyboard input against the
complete existing game, and records pause, aim/fire and particle diagnostics.
Use it in addition to simulated combat checks: feeding keys directly to the
managed loop cannot validate DOM event wiring or browser capture behaviour.

The full-game DOM input check passed with both mouse buttons held, independent
release, and Escape pause/resume. The active transparent particle draws changed
2,309 framebuffer pixels when compared with the same frame with those draws
removed. The captured weapon effect was active and rendered, and machine VFX
also reported active particles. This establishes sampled visible output, not
visual equivalence of every effect. Evidence:
`artifacts/browser-input-vfx-parity-pixels/particle-pixels.json`, `pause.png`,
`aim-and-fire.json`, and `errors.json` in the same directory.

A later fixed-720p capture including the input/VFX fixes measured 14.41 FPS,
57.52 ms managed work and 83.3 ms p95. An immediate repeat using the archived
original runtime also slowed to 13.75 FPS, 61.36 ms managed and 99.6 ms p95.
This supports a modest improvement under those conditions, but also demonstrates
substantial machine/run variation. Do not present the earlier 23.14 FPS as a
guaranteed final frame rate. Both runtimes still fail the 30 FPS gate. Evidence:
`artifacts/browser-parity-final-realtime/game.json.validation.json` and
`artifacts/browser-parity-final-baseline/game.json.validation.json`.
