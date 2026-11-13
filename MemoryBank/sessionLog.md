# Session log

## 2026-11-13 — Review and Git publication of Cline updates

Reviewed allocation counters, crowd phases, shadow cap and network triage.
Corrected repetition counts and multi-mode scheduling in Measure-Performance.ps1.
Kept AdaptiveOcclusion at its previous opt-in default; EnablePackedHdrResolve is
false until image/driver/performance gates pass. Added the review handoff.

Checks (exit 0): dotnet build Tests/ByteEngine.Tests -c Release --no-restore -m:1
-nr:false -p:UseSharedCompilation=false, followed by dotnet run --no-build
-c Release --project Tests/ByteEngine.Tests -- --ai-query, --post-contexts,
--render-pipeline and --native-skinning. dotnet run -c Release --project
Tests/ByteEngine.Network.Tests and Tests/ByteEngine.Network.Integration pass.
Dry-run counts: 16 launches with two modes/eight reps; 12 with three modes/four.
Synthetic paired bootstrap detects a known 10% effect. No actual FPS gate ran.

No branch merge, game export, runtime rebuild or overseas network test performed.
