# Browser export support

Dune Company and Desert Terrain are statically linked into the .NET browser player. The desktop plugin DLLs are not dynamically loaded in WebAssembly. Unsupported plugins remain blocked explicitly.

To add another plugin: reference its project in ByteEngine.Browser.csproj, register its ID before opening the project, add that ID to the capability list in Tools/PublishBrowser.ps1, and republish the browser runtime. Updating desktop plugins alone does not update the browser player.

Export Web Game now cooks the project and produces an itch.io-ready ZIP with index.html at its root. Cooked model assets retain metadata; duplicated source model bytes are omitted only from the exported copy. The original project is unchanged.

The player requires WebGL 2 and a click to start audio. Blueprint saves persist in localStorage under the project ID plus the exported build identity. Clearing browser storage deletes those browser saves. Desktop saves remain separate.

Browser rendering uses reduced graphics: no desktop shadow, IBL or post-processing parity; decoded image textures are limited to 1024 pixels. Sand has real shader displacement and contact deformation, with a 0.25m near render mesh and unchanged 0.125m simulation samples. Physics uses one worker. Texture uploads are bounded and dirty-region based.

Validation: actual Chrome browser loaded the garage, selected the winch starter, entered driving with physical movement, returned to the garage, opened the winch contract with visible selectors, and restored the nine-part vehicle after reload. No managed or GL errors occurred. Portable export checks and sand regressions passed. Full winch mission completion and live itch.io hosting were not validated in the browser; this is not a browser performance benchmark.

## itch.io download resilience

Web manifest version 3 divides Game.bytepak into 8 MiB .bin chunks (three concurrent downloads, mounted in order), each with its own size and SHA-256. The browser assembles and validates the complete package before mounting. Each interrupted, truncated or corrupt download retries up to four attempts, shows aggregate byte progress and has a 120-second inactivity timeout that pauses while the page is hidden. Completed chunks are cached by verified SHA-256 (256 MiB cache cap); interrupted chunks resume with HTTP Range where supported, or restart cleanly if the host ignores Range. Restricted storage falls back to network. Legacy formats 1 and 2 remain supported.

The supplied itch ZIP contains a 126,454,665-byte single package. The CDN returns HTTP 200 and the matching length; Chrome obtained an HTTP 206 prefix whose SHA-256 matches the uploaded ZIP. The screenshot therefore shows a body-transfer failure rather than a missing asset. The original full-transfer failure was not reproduced, so its particular network cause is unknown.

Validation: C# chunk boundary/reassembly test; Node truncated/corrupt/terminal-failure and legacy compatibility tests; Chrome interrupted HTTP response and corrupt-hash retries, verified reassembly and mounting. The complete updated game has not been uploaded or tested on itch.io. No game exports were made. Browser runtime and editor core were rebuilt and installed for the next user export.

Hosting limits reference: https://itch.io/docs/creators/html5

## Browser runtime performance

Release browser players now AOT-compile the shared engine, linked game plugins and Bepu solver to WebAssembly, with SIMD enabled. Debug builds remain development builds. The physics solver uses the same Bepu version, 60 Hz fixed ticks, eight solver iterations and four substeps as the desktop player. The browser limits catch-up to two ticks per rendered frame and discards whole overdue ticks after a stall, retaining the interpolation remainder. Dropped simulation time is exposed in browser diagnostics; under sustained overload simulation can fall behind real time. `Tools/BrowserPhysicsCompatibility` isolates collision-task float comparison masks behind non-inlined original comparison calls in private generated assembly copies to avoid a .NET 9 Mono/LLVM SIMD compiler assertion; it never modifies desktop DLLs or NuGet packages. Its equivalence checks cover 5,000 comparisons at each of four- and eight-lane widths, including NaN, infinity and signed zero. A successful release publish and actual moving-vehicle test are required; interpreting the solver is not a performance fallback.

Content mounts as bytes rather than base64 strings. Chunk size/hash checks, retries and whole-package verification remain enabled. Dynamic mesh and texture uploads also use binary interop; frame command metadata uses a source-generated JSON serializer. The renderer caches tinted font atlases with a 64 MiB limit and reuses consecutive material state. Framebuffers preserve aspect ratio and are capped at 1280x720 on high-DPI screens. Publishing removes obsolete fingerprinted runtime binaries before installing the bundle.

Browser diagnostics: `byteEnginePerf.frames` keeps the latest 600 CPU frame samples, `byteEngineDebug()` reports vehicle state and loop/encoding/physics times, and `byteEngineGraphics()` reports the graphics device, resident resources and GL error. F8/F10 reach the browser game. The exported diagnostic CSV resides in the browser's virtual filesystem; desktop file dialogs are not provided by the browser.

The local performance harness serves an existing export's content with the rebuilt engine runtime. The later diagnostic fixture recooks existing content into the new portable model format. It uses a fresh Chrome context and a recovery vehicle fixture, verifies actual movement under throttle, and captures frame timings and screenshots. This is not an itch.io network benchmark, mission-completion test or guarantee of 60 FPS. No game ZIP is generated by the harness.

Relevant engine approaches: [.NET WASM AOT](https://github.com/dotnet/runtime/blob/main/src/mono/wasm/features.md), [Unity progressive asset loading](https://docs.unity.com/en-us/engine/6000.7/manual/platform-specific/webgl/building-distribution/web-optimization/progressive-asset-loading), and [Godot web export](https://docs.godotengine.org/en/4.5/tutorials/export/exporting_for_web.html). Progressive scene streaming is still a separate task; current content is downloaded and verified before startup.

### Local driving measurements (8 October 2026)

On this laptop's Radeon Vega 10 in Chrome, the existing 126,454,665-byte content package initially took roughly 41 seconds to prepare plus 24 seconds to start the scene. Repeated rebuilt-runtime runs took about 5–9 seconds to prepare plus 6–7 seconds to start. These local HTTP timings exclude itch.io/CDN download latency. The payload size has not changed.

The original browser driving path measured roughly 795–891 ms of CPU work per frame. The native physics build with binary uploads and generated serialization measured 33–78 ms in repeated moving-vehicle runs; rendering itself was about 1.5–2 ms. The vehicle moved about 38–42 metres during ten seconds of throttle. Variability remains substantial: this does not meet a consistent 60 FPS budget.

A Chrome CPU profile identifies remaining interpreter work in Bepu's shared value-type generic solver, collision and bounding-box paths. LLVM-only Mono AOT forces this sharing mode; `--optimize=-gsharedvt` did not remove it and is not shipped. Lower jiterpreter thresholds also did not demonstrate an improvement and are not shipped. Physics timestep, solver iteration/substep counts, traction and sand behavior were not reduced to improve the measurements.

Validation in this pass covers startup, a loaded eleven-part recovery vehicle, driving movement, JavaScript/GL errors, package retries/integrity, and SIMD mask equivalence. Full towing/winch completion, live itch.io performance and user blueprint persistence were not retested in this pass. Browser physics optimization and smaller/progressively loaded content remain outstanding work.

Final installed-bundle check: a fresh high-DPI Chrome drive ran with a 1280x720 framebuffer at devicePixelRatio=2. It prepared content in 5.1 seconds, started the scene in 6.0 seconds, and measured a 51.8 ms CPU-frame median (rendering 1.6 ms). The eleven-part vehicle moved over 40 metres. There were no JavaScript errors or GL errors. All 53 runtime resource SHA-256 checks passed, obsolete fingerprints/symbol sidecars were absent, and installed JavaScript matched the source. This remains below the desired smooth-driving performance budget.


### Export/save/loading repair (8 October 2026)

The user's selected **B — Open Frame** supplies the editor/player/launcher icon, embedded 40-frame Windows startup GIF, browser favicon and animated browser loading screen. A retry button is available on loading failure. The original three drafts remain under Designs/EngineIdentity; canonical assets are Resources/Branding.

New Windows and browser exports receive `RuntimeSaveId` in the exported project copy, preserving the source ProjectId and leaving the authoring project unchanged. Same-build restarts retain saves; different builds start independently. Dune Company New Game has an authored confirmation dialog, transactional campaign reset with backup, and preserves blueprints/settings. Company balance is loaded when entering the workshop and displayed in garage/driving UI. Source scene/event changes are in Documents/DuneCompany; plugin source and package are synchronized to the main engine and Documents copies.

Following [Godot's tracked preloader](https://github.com/godotengine/godot/blob/master/platform/web/js/engine/preloader.js) and [parallel engine initialization](https://github.com/godotengine/godot/blob/master/platform/web/js/engine/engine.js), ByteEngine overlaps runtime initialization with content downloads and reports byte progress. This adapts the loading design; it does not replace ByteEngine with Godot or provide renderer parity.

Portable models now store geometry as binary arrays (BYTMOD03) plus small metadata. Model-embedded textures are stored once under SHA-256 content keys and reuse GPU textures. Skeletons, animation, node transforms and material properties are retained; old JSON cooked models can still load. Existing content recooked in a private fixture changed from **126,454,665 to 67,780,197 bytes**, sixteen chunks to nine, with 35 models. The source game's model files were not changed and no game ZIP was exported.

Validation: save identity/reset and binary-model roundtrips; actual native New Game/cancel/confirm -> both available contracts and zero wallet; launcher relocation, quoted arguments, working directory and exit code; embedded animation startup/dismissal; actual Chrome late-chunk interruption resumed with Range and a second load fetched no content chunks (559 mounted files); blocked-cache/corrupt-cache and terminal retry unit tests. Chrome loader-only cold interrupted run: 6.58 s; cached reload: 3.20 s. These are local timings without itch.io network latency or scene startup.

An experimental 40 Hz contact cadence with 240 Hz solver substeps passed native vehicle tests but did not establish a browser performance improvement; the installed default retains 60 Hz. Native tests verify that ordinary-rate frames drop no simulation time, a 200 ms stall executes only two configured catch-up ticks, and the next frame resumes without backlog. Driving, steering, braking, suspension, terrain contact, hitching/detachment/reset and different configurations pass.

The wider legacy test run fails `V07RegressionTests: Character grounds inside finite collider bounds`. This is outside the tested Dune vehicle flow and is not reported as a passing engine-wide regression run. Browser full mission completion and live itch.io performance remain unverified.


Final installed build: an actual high-DPI Chrome drive used a 1280x720 framebuffer on Radeon Vega 10. Local content/runtime preparation was 4.74 s and scene startup 4.79 s (9.53 s combined), versus 9.60 s + 12.45 s in the control. Moving-vehicle CPU-frame median was **39.7 ms**, renderer 1.7 ms, versus 156.1 ms CPU in the control. Mean presentation interval over the final 60 samples was 41.28 ms (~24 FPS); this remains below 60 FPS. The vehicle moved over 33 m under ten seconds of throttle, with no JavaScript/GL errors. Catch-up limits dropped about 2.28 s of simulation time during that overloaded interval: responsiveness improves, but sustained CPU overload still slows simulation relative to real time. The remaining shared-generic interpreter work in the Mono/Bepu browser path has not been eliminated. All 53 runtime resource hashes pass. No user game exports, uploads or Git pushes were performed.

Actual Chrome main-menu validation also passed: seeded completed tow/winch jobs and $750, cancelled New Game without mutation, confirmed a fresh one-block build with both jobs available and zero wallet, retained the saved blueprint, and reloaded/continued the reset campaign successfully. The animated loader, confirmation and available-contract screenshots were visually inspected.
