# Itch performance follow-up

Source: `ByteEngine-performance-2026-10-09T21-17-12-279Z.json`, supplied by the user. This is a real itch-hosted capture on Chrome with AMD RX Vega 10 graphics, not the earlier local fixture.

The 60-second capture contains 2,448 frames: 40.81 FPS overall, median 16.7 ms, p95 33.3 ms, p99 50 ms and 19 frame intervals above 50 ms. The canvas was 960x540 within a 640x360 CSS viewport at DPR 1.5.

The 2,381 driving frames averaged 41.15 FPS, 20.39 ms managed work, 8.14 ms reported GPU time and 6.22 ms full physics timestep. Physics caller-thread allocation averaged approximately 49.8 KB per frame. The short garage segment was only 67 frames, averaging 33.32 FPS; it is insufficient for a reliable steady-state garage benchmark. Diagnostic samples repeat between frames, so phase averages are frame-weighted rather than independent timestep measurements.

The uploaded game now reaches approximately the 42 FPS observed in the controlled local driving test. Compared with the earlier supplied capture's 26.3 FPS overall, this is a substantial observed improvement, but the captures differ in gameplay and rendering conditions and are not a controlled A/B experiment.

Remaining work: occasional frame hitches, frame encoding/managed overhead, detailed collision patches in deformed terrain, and a longer garage capture. End-of-capture dropped simulation time is 1.37 seconds; this is cumulative diagnostic state, not necessarily all incurred during this capture. No initial download or complete loading transition is captured, so this file cannot establish startup time improvements. No new gameplay changes are justified solely by these averages.
