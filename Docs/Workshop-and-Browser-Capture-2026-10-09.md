# Workshop fixes and browser performance capture

Updated 2026-10-09. Canonical plugin source is Plugins/DuneCompany (0.14.2); game scenes are in C:/Users/codex/Documents/DuneCompany. Desert Terrain 0.8.9 and Core add visual-only terrain visibility so camera cutaways preserve collision and simulation.

## Player changes

- Tutorial docked in the existing right sidebar; part guidance remains on the left. Inspector yields its space while the guide is active.
- Wallet grouped in an opaque header card beside vehicle information.
- Garage floor disappears below its surface; roof sections disappear when the camera crosses their height. Lower walls and storage shelves remain. Original meshes, LOD settings and visibility restore on return; generated cutaway meshes are released on destruction.
- The full-width roof was authored inside the mesh named Workshop storage shelving. Both that roof and the rear roof are handled.
- Contract result card expires after 2.5 seconds. Completion/payment state and return-to-garage controls remain.
- Browser background graphics uploads no longer reactivate the full-screen startup loading splash. This changes presentation, not the underlying upload cost.

## Capture controls

In a browser build using the updated runtime, press F8 to show/hide the panel. It automatically records a bounded recent 60-second window. New capture clears previous data; Pause capture freezes it. Mark hitch annotates an observed stall. Save report downloads a JSON diagnostic report. The 3D and UI switches temporarily disable those rendering passes to compare their costs; leave both on for the normal baseline. They do not disable physics.

For a useful report: New capture, hide the panel with F8, perform the slow garage/contract action or drive for 30–60 seconds, reopen the panel, Pause capture, then Save report. Collect a cold startup report separately if downloading/unpacking is the problem. Existing uploaded games need a user-created update before these controls are present; no user game was exported by this task.

Reports include frame intervals, p50/p95/p99, hitches over 50 ms, managed frame cost, JSON parse cost, audio dispatch, graphics upload time/bytes, 3D/UI CPU time, available geometry GPU timing, pending uploads, draw/UI counts, managed memory and GC counts, scene changes, asset unpack/mount durations, cached downloads/retries and errors. GPU timing is asynchronous and excludes sky, uploads and Canvas UI; null means unavailable/disjoint. Managed time includes simulation and serialization. Render probe states are recorded. Reports omit saves, frame payloads, pixels, URL query strings and account information.

Bounded storage: 7,200 frame samples, recent 60 seconds, 2,000 events, with overwrite counters. Paused reports retain their frozen window. Detailed capture has some overhead; use it for diagnosis, not claims of zero-overhead benchmarking.

## Validation

- Native diagnostic traversed all six tutorial steps, verified highlighted required parts and valid/invalid Next, garage navigation, responsive toolbar, drive/return and tutorial persistence.
- Camera test verified visual cutaways and restoration without disabling heightfield colliders; floor and roof captures were inspected.
- Completion fixture verified the one-time existing victory cue and card expiry after 2.5 seconds. This uses controlled completion state, not a physical delivery run.
- Node checks bounded recording/event storage, percentile calculation, paused retention, reset and rendering probe controls.
- Chrome diagnostic verified F8, report download and contents, measured upload/memory timings, real startup events, 3D probe, garage/settings/contracts/drive/return, persistence, HDR sky and no JavaScript/WebGL errors. This is a local diagnostic fixture, not an itch-hosted performance benchmark.

Logs: .artifacts/workshop-capture-*.log. Private report/screenshot: .artifacts/browser-performance/menu-repair/performance-capture.json and performance-capture-panel.png. Native screenshots: .artifacts/workshop-feedback-fixture/Preview/.
