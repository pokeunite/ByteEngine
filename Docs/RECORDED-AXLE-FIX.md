# Recorded vehicle axle correction — 0.2.3

Input: user construction debug captured 2026-10-03. Both exact authored builds were recovered, including large rear powered tires, smaller front passive tires, outer beam mounts, and optional front steering hinges.

Cause reproduced: hinge constraints allowed significant tire axle rotation and pivot separation under drive/contact loads. Previous mixed-wheel tests did not assert those errors. Reducing motor torque alone masked rather than corrected this defect.

Changes: wheel-bearing constraint spring frequency 30→120 Hz; other rigid hinge constraints 30→60 Hz. Solver 12 iterations/4 substeps→16 iterations/8 substeps. Wheel drive remains at 60 maximum base motor force, and torque distribution between inside/outside wheels allows physical hinge steering under power. Physical wheel spin, collision and separate rigid bodies remain; no pose snapping or vehicle translation overrides were added.

Exact recorded builds at 45/60 Hz: rest axle misalignment ≤0.31°, hub displacement ≤0.6 mm; loaded misalignment ≤1.92°, hub displacement ≤13.4 mm; straight travel about 6.5–10 m in four seconds, negligible lateral drift. These are measured regression results, not a promise for every arbitrary machine. Full focused contraption suite passes including wheel attachment, loaded hinge steering, powered/free gears, mixed wheels at 30/60/120 Hz, reverse/braking, projectiles and selected parts. Previous broader character grounding failure is outside this fix.

Regression fixtures: Tests/ByteEngine.Tests/TestData/GoblinRecordedBuild0.json and GoblinRecordedBuild1.json. RecordedBuildTests asserts rest/load error limits and uprightness. Diagnostics retain wobbleDeg, hubGap, attachmentGap and solver settings. Updated plugin 0.2.3 and matching editor are installed; restart the editor to reload them.
