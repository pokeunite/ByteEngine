# itch.io performance capture — 2026-10-09

Source: C:/Users/codex/Downloads/ByteEngine-performance-2026-10-09T17-18-39-272Z.json

## Measured result

The 36.63-second capture contains 456 frames from Scrapyard Garage with building=false, an 11-block vehicle, Chrome 155, AMD RX Vega 10 graphics and a 1280x720 rendering viewport. Both rendering probes were disabled.

| Measurement | Mean | Interpretation |
|---|---:|---|
| Frame rate | 12.43 FPS | Severe steady gameplay slowdown |
| Frame interval | Median 83.1 ms; p95 99.8 ms | From capture summary |
| Managed/WASM call | 73.14 ms | Includes game update and frame encoding |
| Physics | 53.90 ms | Largest measured subsystem; diagnostic samples can repeat across frames |
| Frame encoding | 7.11 ms | Secondary CPU cost |
| GPU geometry | 16.68 ms | Asynchronous geometry-only measurement; do not add to CPU totals |
| JavaScript 3D submission | 2.24 ms | Small compared with physics |
| UI submission | 0.99 ms | UI is not the main bottleneck in this run |
| JSON parsing | 0.86 ms | Small compared with physics |
| Upload work | 0.45 ms | Minor average cost, occasional spikes |

387 of 455 visible frames exceeded 50 ms. Physics reported two catch-up ticks throughout the capture. Dropped simulation time increased by 21.10 seconds, demonstrating that the simulation could not keep up with elapsed time. This can affect gameplay pacing as well as visual smoothness.

No asset requests were pending in any captured frame, and there were no download/retry/loading events. This capture therefore establishes a gameplay CPU bottleneck, but cannot diagnose startup download or scene-transition delays.

GC generation-zero collections increased by 139 (about 3.8 per second); generation-two increased by two. This supports investigating allocation overhead, but does not prove that garbage collection is the primary cause.

71 graphics-upload events repeatedly reference nine speedometer dial images; for example dial-06.png appears 21 times. Repeated allocation/upload deserves investigation as a secondary cache issue. The measured upload cost is too small to explain the overall slowdown by itself.

## Source findings and limits

LandVehiclePhysics.Step limits browser catch-up to two ticks. The physics timing currently combines terrain maintenance, wheel/control updates and the Bepu timestep. The solver is configured for eight iterations and four substeps at 60 Hz, and browser physics is single-threaded. Terrain collision meshes can be rebuilt during stepping. These are investigation targets, not individually proven causes from this capture.

Do not raise catch-up limits: that would add more work to already slow frames. Do not reduce physics quality blindly: towing, suspension and winching need stability checks.

## Next targeted debugging pass

1. Split physics timing into terrain rebuild, sand sampling/control updates and solver timestep. Record collider/body/constraint counts, rebuild counts and allocated bytes.
2. Reproduce this exact 11-block drive on the same hardware with an optimized browser runtime. Compare unchanged terrain, deformable terrain, and collision-patch maintenance separately.
3. Test solver iteration/substep profiles against suspension, steering, tow and winch regressions, measuring both frame cost and simulation time lost.
4. Investigate repeated speedometer texture allocations and reduce frame serialization allocations after physics is isolated.
5. Capture from before initial launch and through Accept & Deploy / Return to Garage separately to identify loading stalls. This report contains neither transition.

No gameplay or engine code was changed as part of this capture analysis. The measurements identify where to focus; a browser CPU profile or finer phase timings are required to identify the expensive function within physics.
