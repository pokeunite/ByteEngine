# Active context

Updated: 2026-11-14 (stabilization after the palclone break)

## Current focus

`main` is the single engine. Palclone is deprioritized: its 11/11 plugin needs the branch-only
gameplay layer, and porting that is a 60-file merge (measured), not a patch. Keep the editor, player
and export green on `main`, keep `Dist/ByteEngine` published from `main`, and only revisit the branch
merge if that gameplay layer is wanted again. The Event Sheet UX research stays queued.

## Engine lines (read before republishing anything)

- **`main`** — event sheet work, export/one-exe packaging, Wildlings, DuneCompany. Does **not** load
  the 11/11 KarlsonDemo plugin: the plugin needs `GameWindowMode`, `ICharacterDebugSource`,
  `CombatDamageReceipt` and branch-only members on existing types.
- **`codex/event-sheet-ux-and-engine-updates`** (worktree `C:\Users\codex\.codex\worktrees\52af\ByteEngine`)
  — the engine `palclone` and its plugin were authored against. Verified: the same plugin DLL reports
  `types-loadable=True` against this Core, and the rebuilt editor opens the project with
  `plugins=1`.
- **`C:\Users\codex\ByteEngine-Palclone`** — published branch editor for palclone right now. Do not
  overwrite it with a `main` publish until the lines are merged.


## What changed

- `Docs/GDevelop-EventsEditor-Research-2026-11-14.md` — anatomy of GDevelop's events editor, its
  event/instruction data model, side-by-side comparison with our panel (12,542 UI lines + 6,159
  VisualLogic lines), 8-step redesign proposal, risks and open questions.
- `Docs/Research/GDevelopEventsEditor-2026-11-14/` — 51 documentation screenshots downloaded, 13
  of them embedded in the research doc.
- GDevelop source (rev `a643996`, 2026-11-13) checked out at `D:\ue4 sources\gdevelop source` as a
  sparse blob-filtered clone: `Core/GDCore/{Events,Project,IDE/Events,Extensions}` and
  `newIDE/app/src/{EventsSheet,InstructionOrExpression}`. Widen with
  `git -C "D:\ue4 sources\gdevelop source" sparse-checkout add <paths>`; `sparse-checkout disable`
  pulls the whole (~1 GB) tree.
- `Tools/PullGDevelopEventSource.ps1` (retries the sparse checkout; also sets `http.postBuffer` and
  `http.lowSpeedLimit`, which this slow GitHub link needs) and `Tools/PullGDevelopDocImages.ps1`
  (downloads the doc screenshots).
- `Tools/MarkdownToPdf.py` — Markdown → styled HTML → headless Chrome/Edge → PDF, with cover page,
  print CSS, and local image resolution (relative to the `.md`, absolute Windows/POSIX paths, raw
  `<img>` tags). CLI covers output path, title/subtitle, `--no-cover`, page size, image height,
  accent colours, extra CSS, browser path and `--keep-html`. Guide: `Documentation/Markdown-to-PDF.md`.
- Review PDF: `C:\Users\codex\Downloads\GDevelop-EventsEditor-Proposal-2026-11-14.pdf` (18 pages, A4,
  13 embedded screenshots), regenerated from the research doc.
- **Windows export regression fixed (2026-11-14):** `main` had lost the single-executable packaging
  that only lived on `codex/event-sheet-ux-and-engine-updates`. Ported
  `Engine/ByteEngine.Core/Runtime/SingleExecutablePackage.cs`, the bundled branch in
  `GamePackageExporter`, `SingleExecutablePackage.Prepare(...)` in `Player/Program.cs`, and made
  OpenAL a publish `Content` item in `Player/ByteEngine.Player/ByteEngine.Player.csproj` (the
  one-exe game previously died on `openal32.dll`). `PlayerRuntime/Bundled/ByteEngine.Player.exe` is
  now used whenever present; the folder layout is the fallback.
- Regression harness: `Tests/ByteEngine.Tests --windows-export-single <PlayerRuntime>` builds a slim
  identity-aligned runtime and validates the exported EXE end to end; `WindowsGameExportTests` now
  accepts both layouts and checks payload tampering.

## Key findings

- GDevelop has one flat two-column events view: conditions left, actions right, one sentence per
  instruction, parameters edited in popovers. No graph, no wires, no card headers, no right-hand
  inspector. Order is the model (`EventsList` vector); sub-structure is indentation.
- GDevelop's UI is ~31.9k JS lines, but almost all of that is per-parameter field components and
  mobile handling — the event/instruction renderer itself is `EventsTree/index.js` +
  `Instruction.js` + `InstructionsList.js` (~1,400 lines).
- Our panel's complexity comes from being graph-first: `EditorX/EditorY`, `NextActionId`,
  `TrueActionId`/`FalseActionId`, `ConditionInputIds` all need editor UI, and we ship two views of the
  same data plus an outline and a details pane.
- Our registry already has GDevelop's sentence concept (`SentenceTemplate` +
  `VisualArgumentDefinition`), and `EventSheetProjection` is the right layer for a list renderer, so
  the redesign is mostly deletion and re-layout, not new data plumbing.

## Verification

- `main` build 0 errors; default test suite passes; `Dist/ByteEngine` republished 17:26 (editor, Core),
  17:28 (bundled player) with identical loose/bundled Core hashes.
- Wildlings opens on the published editor with `plugins=1`, no missing components or exceptions.
- Single-executable Windows export test passes on the published runtime.
- Play-stop `Texture2D` crash: trim now keeps textures referenced by surviving materials
  (`Material.Textures()`), and the sampler skips disposed textures as a guard.

- Palclone restore: rebuilt `codex/event-sheet-ux-and-engine-updates` in its worktree, published the
  editor, copied the distribution to `C:\Users\codex\ByteEngine-Palclone`, opened
  `Documents\palclone\palclone.byteproject`; log shows
  `Loaded ByteEngine plugin 'Modular Weapon Gameplay' (bytebard.karlsondemo) v3.93.0` and
  `plugins=1`, with no missing-component or unknown-visual-logic warnings.
- Plugin compatibility probe: the same plugin DLL reports `types-loadable=True` against the branch
  Core and fails on `main` Core with `GameWindowMode`, `ICharacterDebugSource` and
  `CombatDamageReceipt` missing.
- `main` builds with 0 errors after `65c04a5` (which removes registrations for components that exist
  only on the branch).

- Export fix: `dotnet build ByteEngine.sln` → 0 errors; `Tools/PublishPlayer.ps1` and
  `Tools/PublishEditor.ps1` (Release) refreshed `Dist/ByteEngine` and printed "Matching Release
  Windows player and editor core refreshed"; loose and bundled players report the same Core MVID
  (`e801fdb5-bc0e-4d43-a347-c838bd30b88d`); `dotnet run -c Release --project Tests/ByteEngine.Tests --
  --windows-export-single "…\Dist\ByteEngine\PlayerRuntime"` printed "Single-executable Windows export
  (runtime and assets inside the EXE) passed." — one EXE, no `Runtime/`, no `Game.bytepak`, tampered
  payload rejected, exported EXE `--validate` exit 0.
- All 13 image links in the research doc resolve to files on disk.
- GDevelop checkout: `git -C "D:\ue4 sources\gdevelop source" log -1` → `a643996`; 150 files under
  `newIDE/app/src/EventsSheet`; `Core/GDCore/Events` present.

## Recent project state (unchanged)

Agent-driven authoring through `Tools/ByteEngine.Mcp` is still the current authoring path
(published to `Dist/ByteEngine-MCP` by `Tools/PublishMcp.ps1`). The GDevelop-style sheet view added
earlier on 2026-11-14 remains in place as-is; it is the thing this research proposes to simplify.
