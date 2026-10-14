# Drag launch and gearing fix — plugin 0.27.0

- Removed Convoy Breaker and the Combat tab from active plugin code and game scenes. Source, scene and combat-specific art are archived in Docs/ArchivedCombat and the project's Docs/CombatConcepts/Archived. Player saves and earned money are preserved. The two weapon blocks remain.
- Holding the accelerator during the countdown no longer applies a hidden 0.75-second launch penalty. The brakes hold the car until green, then throttle is released immediately.
- Added a sixth, 0.78 overdrive ratio. With a 50 m/s engine limit and a 2:1 tuned final drive, theoretical gear headroom rises from 25 to approximately 32 m/s. Actual speed still depends on weight, engine torque, grip and the build.
- Top gear displays TOP GEAR / OVERDRIVE or MAXIMUM SPEED rather than asking for an impossible further upshift.
- The drag rival retains its preset engine and tyres, without the hidden race-engine, engine-power and tyre-power boosts previously applied to all race opponents. It uses a slightly conservative throttle. Sprint and rally opponent upgrades are unchanged.
- Drag Lane Assist gently stabilizes the starting lane while steering input is neutral. Manual steering overrides it. The Drag Lane Assist option is editable on the Sprint Contract component; recovery, sprint and rally steering are unchanged.

Native private-fixture validation: staged throttle remains held until green and accelerates during the first 0.25 s; a tuned two-heavy-engine rig wins the 400 m race with green-window shifts; all six gears are selectable, overdrive provides additional headroom, and the retired Combat category/scene are absent. The test car reached fourth gear during its winning 400 m run; selecting sixth was verified separately, not claimed to be necessary for every vehicle on this course.

Browser private-fixture validation also passed: throttle held during deployment/countdown, zero launch penalty, driving, keyboard shifts through all six gears, top-gear state/display, quit and resize, without browser/OpenGL errors. Desktop package copies and bundled browser runtime are synchronized at 0.27.0.

No engine rendering, environment indexing, transform or physics-query source was changed for these fixes. Browser runtime publishing shares generated build files with the optimization chat and was run sequentially after a concurrent-build file lock. No distributable game export was made.
