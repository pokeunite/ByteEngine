# Sand driving correction

Installed plugins 0.8.4 in the Documents DuneCompany project; editor and player runtime rebuilt. Saved vehicle and scenes preserved.

- Dust emitter resume now checks emission, rather than whether old particles are still alive. Transparent particle batches use the transparent render queue.
- Tyre feedback uses the cylinder collision centre rather than visual bounds. Terrain support supplements missing sleeping/speculative contact reports, without snapping or moving the chassis. Airborne dust regression passes.
- Tracks follow actual tyre width, deform a bounded local soil grid, raise shoulders and apply a stronger imprint normal/colour response. This is a bounded heightfield soil simulation, not individual grain simulation.
- Per-frame render worlds were discarding all their buffers. Framebuffers and the player now retain them; scene snapshots, render ordering, shadow caster lists and light uniform names reuse storage.
- Isolated native run with the user's 21-block vehicle: sustained tyre wake, bounded 528-particle total capacity, ~50m drive, drifting, braking and mission transitions passed. Collection counts dropped from 55 to 15 in the final 240-frame run (12 before the last soil-depth adjustment). GPU timings vary substantially on this laptop; this does not establish a stable 60 FPS.
- Effects, terrain, vehicle-physics and native rendering regressions pass. The broad suite encountered a separate failing character-grounding test (V07RegressionTests: Character grounds inside finite collider bounds); full-suite success is not claimed.

The actual Windows executable smoke test exited 0; packed-content terrain/collision, menu transition and 15.6m driving checks passed.

Latest Windows build: Builds/Windows/Dune_Company-Windows-20261006-232354-1aa301.

Use F10 to export a fresh driving log. The old Windows export does not receive runtime updates automatically; use the newly exported build.
