# Crash compatibility correction - plugin 0.4.5

Removed the Goblin-only animation-distance thresholds and the direct dependency on AnimationUpdateInterval. Refreshed the primary published editor and added loaded core build identity to startup diagnostics.

Engine-wide component/physics allocation fixes, research, raw benchmarks and priorities are documented in Docs/EnginePerformance/ENGINE-PERFORMANCE-AUDIT.md. Batching, GPU skinning, shared animation budgets and a physics broadphase remain planned systems. Earlier 0.4.4 FPS measurements do not describe this corrected package.

Use Dist/ByteEngine/ByteEngine.Editor.exe or the refreshed development editor. Restart after updating. No model reimport is needed.
