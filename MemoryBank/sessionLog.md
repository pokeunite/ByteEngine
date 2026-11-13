# Session log

Newest first. One entry per work session or hand-off. Keep each entry short:
what changed, what was verified, what is left.

## 2026-11-13 — Memory Bank initialized

Changed:
- Added `.clinerules/memory-bank.md` (the protocol Cline follows each session).
- Added `MemoryBank/` with `projectBrief.md`, `productContext.md`,
  `systemPatterns.md`, `techContext.md`, `activeContext.md`, `progress.md`,
  `decisions.md`, `sessionLog.md`.
- Seeded content from `README.md`, `AGENTS.md`, `Docs/`, the solution layout and
  the uncommitted crowd-performance work.
- Added a memory pointer to `AGENTS.md` so non-Cline agents use the same files.

Verified: repository structure and script parameters read directly
(`Directory.Build.props`, `Tools/*.ps1`, `Tests/ByteEngine.Tests/Program.cs`,
`Plugins/DuneCompany/plugin.json`); `git status` shows the uncommitted crowd work.
No build or test run was performed as part of this setup.

Left: confirm the uncommitted crowd-performance edits build and pass
`CheckNavigationAllocation`; then commit them with
`Docs/EnginePerformance/crowd-animation-pathfinding.md`.
