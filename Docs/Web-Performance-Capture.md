# Recording a slow web build

This recorder is bundled with the browser runtime and is copied into future web
exports. An older export must be rebuilt/uploaded to receive it. A runtime publish
is separate from exporting a game.

1. Run the web game on the computer where it slows down. Click inside the game.
2. Play normally first. Basic frame recording runs automatically.
3. Press **F9** immediately before reproducing the problem. Play for 30 seconds.
   This records detailed callback costs and then turns deep instrumentation off.
4. Press **F8** to open the recording panel. It releases the mouse so the buttons
   can be clicked. The game's pause menu may also open when capture is released.
5. Click **Download report** and send the JSON file. **Download trace** saves an
   additional Chrome/Perfetto-compatible timing timeline. Keep the report too:
   the trace does not contain the detailed component/object sample tables.

The panel also has New capture, Pause capture, Mark slowdown, Scene inventory,
and temporary 3D/UI isolation buttons. Keep 3D and UI enabled for normal testing;
any diagnostic probe state is included in the report. F8 hides the panel. Resume
the game and click inside it if the browser requires a fresh mouse-lock gesture.

## What is recorded

- Every retained frame: frame interval, WASM/JSON/audio/video/render submission
  timings, scene update/physics/late-update/animation timings and allocation,
  managed rendering preparation, asynchronous GPU world/shadow timing, upload
  bytes/counts, triangles/instances/material changes, shadows/lights, UI commands,
  quality/resolution, visibility/focus and recorder sampling overhead.
- Periodic samples: managed allocation and GC counters, browser JS heap when
  available, scene/component counts, physics contacts and broad-phase work,
  animation workload, VFX particle/capacity/drop counts, navigation agent/path
  state, render culling/batches, mesh/texture/canvas residency estimates and audio
  source/decoded-buffer activity.
- Deep mode: component type and object callback time/allocation, slowest object,
  physics query calls/time, path-search calls/time, and Event Sheet rule costs.
  The most expensive 256 object rows are retained per detail sample. Internal
  budgets and omitted/dropped counters are included.
- Inventory: authored object identity/parent/component names and active states,
  animation model complexity and clips. This does not force unloaded models to
  load. Deep mode captures an inventory at its start and when scenes change.
- Browser events: resource requests (query strings/fragments removed), long
  tasks, long animation frames and input latency where supported, errors, scene
  changes, recording markers, and exact runtime boot/plugin build fingerprints.

## How to interpret it

The frame ring retains the latest **120 seconds**, up to 18,000 frames. There are
separate bounded rings for 4,096 events, 512 samples and four inventories. This
prevents a recording from growing forever and becoming a new performance problem.
Download soon after the issue; dropped counts disclose overwritten data. JSON
can be large because it contains structured evidence rather than just averages.

Deep profiling adds overhead. Normal and deep frame summaries are separated;
use normal frames for the game's frame rate and deep samples to attribute cost.
Sampling duration is recorded separately. Callback/system costs and allocations
are **inclusive**: a physics query inside a component is also part of that
component's time, so adding them would double-count. Deep callbacks accumulate
across the sample's `firstFrameId`–`frameId` window, including worst callback
times and allocation totals. Transfer cadence adapts between 500 and 3,000 ms,
targeting a sampling cost below 5% of elapsed time; callback instrumentation has
additional cost. This avoids repeatedly serializing large scene tables on every
slow frame. `profiler.frames` gives the denominator for per-frame averages. A
final partial window is collected when deep mode stops.

Browser pages cannot inspect arbitrary CPU function stacks, OS scheduling,
temperatures, actual GPU VRAM or exact GC pause attribution. Unsupported GPU/heap
measurements remain unavailable, never silently treated as zero. GPU timer
results are asynchronous; the trace represents them as counters rather than
inventing precise GPU spans. Memory estimates exclude driver/browser overhead.
For function-level WASM stacks, complement this file with a browser DevTools CPU
profile and the matching private engine symbol map.

Reports stay local until you share the download. They exclude saves, cookies,
account data, typed input, screenshots and asset contents. Object names, error
messages and resource paths can still disclose project information.

For an initial ranked readout:

```text
node Tools/SummarizeWebPerformance.mjs performance-report.json summary.json
```

The summary provides sampled cost rankings and evidence to investigate; it does
not claim a single exact bottleneck or sum nested measurements as exclusive cost.

## Validation evidence

The full-game test exercised actual F9/F8 keys, automatic deep-mode timeout,
inventory, multi-frame callback accumulation, physics-query scope rows, graphics
residency, exact build fingerprints and both downloaded JSON files. The revised
capture contained 369 frames and 46 samples (36 deep), approximately 5.1 MB,
with no recorded browser errors. Its measured recorder work averaged 4.8 ms per
frame, versus 28.6 ms with the initial over-frequent transfer design. These are
local test measurements, not a game FPS claim or guaranteed overhead on another
machine. Callback instrumentation itself has additional cost.

Evidence is retained under the ignored
`artifacts/performance-capture-window-live/` directory. The test uses existing
game content with a runtime overlay; no new game export is produced.
