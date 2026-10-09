# Browser physics investigation and first optimisation — 2026-10-09

The original itch capture was 12.43 FPS. It established a physics-heavy CPU bottleneck but did not isolate terrain maintenance from the solver.

## New diagnostics

F8 reports now include physicsPhases: terrainMs, controlsMs, solverMs, allocatedBytes, terrainRebuilds, fineRebuilds, bodies, contacts, staticColliders, sandSampleMs, sandSamples, iterations and substeps. Phase timings are accumulated across the physics ticks for the current frame. solverMs measures the full Bepu timestep, including collision detection, constraint solving and integration; it is not a constraint-solver-only timer. Sand-sampling time overlaps controls and some presentation work; do not add it as a separate total. Diagnostic values are sampled every 250 ms and can repeat across frames. Allocation counts cover the physics caller thread, not all application allocation or worker threads.

The instrumented 11-block heavy-recovery driving fixture reproduced the issue on the same AMD RX Vega 10 GPU and Chrome at 1280x720: 14.34 FPS, average solver 48.53 ms, terrain 1.39 ms and sand samples 0.82 ms. Solver allocations were approximately 956 KB per captured frame. Terrain is not the main steady bottleneck, although rebuild spikes exist.

A Chrome CPU profile is also saved by the diagnostic harness. The published WASM function indices do not match the pre-optimisation symbol map, so this report does not claim an individual hot method from that profile.

## Controlled solver comparison

Both runs below used the same new browser binary (including bounded texture residency), the same 11-block vehicle, the same hardware and viewport, and a 20-second held-throttle capture with Chrome CPU profiling enabled. They are local-host measurements, not a claim about the current itch upload.

| Profile | FPS | Average solver | p95 frame interval | Dropped simulation time during captured frames |
|---|---:|---:|---:|---:|
| 8 iterations / 4 substeps | 18.27 | 36.14 ms | 83.0 ms | 8.43 seconds |
| 6 iterations / 2 substeps | 21.91 | 27.75 ms | 66.5 ms | 5.68 seconds |

The lighter profile was about 20% faster in this comparison; solver time fell about 23%. Native physics checks passed with that profile: continuous contact, drive, steering, brake, suspension, fixed-step catch-up and hitch/reset behaviour. The scene-based winch diagnostic also passed actual approach, attach/release/reattach, reeling, extraction, one-time payout, second deployment after resetting the private test campaign, and reset/return.

Browser defaults now use six iterations and two substeps at the existing 60 Hz simulation rate. Desktop defaults retain eight iterations and their existing substep rate. Catch-up limits remain unchanged. Diagnostic PhysicsProfile can compare profiles for the active vehicle; environment overrides DUNE_SOLVER_ITERATIONS and DUNE_SOLVER_SUBSTEPS allow native validation.

## Texture residency correction

The browser previously removed every texture unused for one frame, causing speedometer frames to upload repeatedly. It now retains recently unused textures with a 16 MiB / 128-texture limit and 600-frame expiry. Active textures are preserved. Pending partial uploads continue for retained textures so hiding and showing an image cannot leave it incomplete. In both new captures, every captured dial image allocated once (four distinct images/four allocations and three/three respectively).

## Reproducing

Tests/BrowserSmoke/dune_physics.py uses an existing private Dune fixture and a built browser runtime; it does not export a game. Provide the fixture directory and BrowserRuntime directory, optionally followed by iteration and substep counts. Without profile arguments it verifies the installed browser default. Use Python 3.12 with the existing .artifacts/web-test-tools Playwright installation, or supply compatible Playwright dependencies. It stores JSON captures and Chrome CPU profiles in the private fixture directory.

## Earlier remaining limitation (superseded by the follow-up below)

21.9 FPS is still below 30 FPS and far below desktop performance. The solver remains expensive and allocation-heavy. This first change improves the measured bottleneck without claiming that web performance is solved. The next investigation should isolate collision detection versus constraint solving and symbolicate the final optimised WASM or record managed allocation stacks. This capture does not explain download/loading stalls; those require a capture covering startup and scene transitions.

Final installed-runtime check: automatic six-iteration/two-substep selection passed without calling PhysicsProfile. The 11-block drive measured 20.54 FPS, with no JavaScript or GL errors. DuneCompany 0.14.4 source and package were synchronized to the Documents project and engine libraries; package hashes matched. Browser runtime boot manifests matched between Dist and the Debug editor.

## Final follow-up: collision, allocations and loading

New phase timers isolated collision detection as the largest physics cost. The contact cache now reuses dictionary storage, with locking for desktop collision callbacks. Fine sand collision tiles merge planar regions within a 1 mm tolerance and restore detailed cells where ruts or authored depressions require them. Browser-only cosmetic 18 mm geometric ripples use material detail instead; physical tyre ruts and the winch pit remain. Keeping those cosmetic ripples initially prevented any triangle merging on the real field.

Controlled local Chrome captures used the same 11-block vehicle, 1280x720 viewport and 20-second throttle test with CPU profiling. The contact-cache-only run measured 26.98 FPS, p95 49.9 ms and 10 frames above 50 ms. The final run measured 41.69 FPS, p95 33.3 ms and four frames above 50 ms. Mean full physics timestep fell from 22.13 ms to 6.31 ms; final collision detection averaged 2.31 ms. Physics caller-thread allocation fell from approximately 843 KB to 23 KB per frame. The final run reported no JavaScript or GL errors. This is a local comparison, not a measurement of the current itch upload or a claim of desktop parity. Some dropped simulation time remains.

Chunk downloads now use three continuously active workers, so a retry in one chunk cannot stall unrelated chunks. Chunk and whole-package integrity checks remain. Loading uses an accessible progress bar and loading wording. A regression verifies bounded concurrency and correct reassembly while the first chunk is blocked.

Future web exports prepare oversized LDR textures before shipping, using the same nearest-sampled pixels as the existing browser downsampling. HDR, 16-bit PNG and referenced heightmaps are preserved. A read-only audit of 227 fixture textures changed 22 and reduced encoded texture bytes from 52.44 MiB to 43.09 MiB. This is a 9.35 MiB reduction in encoded image data, not a measured ZIP reduction. Original project assets are untouched.

Native driving, steering, braking, suspension, hitch and adaptive terrain checks passed. The winch flow passed approach, attachment, extraction, one-time payment, reset and garage return. Loader and texture/save regression checks passed. Release and Debug editors and the browser runtime were updated. DuneCompany 0.14.5 and DesertTerrain 0.8.10 packages and sources were synchronized to the Documents project and engine libraries; package hashes matched. No game was exported or uploaded during this work.
