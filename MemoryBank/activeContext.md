# Active context

Updated: 2026-11-14 (Event Sheet UX research vs. GDevelop)

## Current focus

Making the ByteEngine Event Sheet feel as simple as GDevelop's events editor. The research is
finished and written up in `Docs/GDevelop-EventsEditor-Research-2026-11-14.md`; no engine or editor
code has been changed. The user has not approved an implementation plan, so the next step is to pick a
starting slice from §7 of that document (recommended: toolbar/layout trim, then plain event rows, then
instruction chips).

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

- All 13 image links in the research doc resolve to files on disk.
- GDevelop checkout: `git -C "D:\ue4 sources\gdevelop source" log -1` → `a643996`; 150 files under
  `newIDE/app/src/EventsSheet`; `Core/GDCore/Events` present.
- No build was run and no engine/editor source was modified by this task (docs and two PowerShell
  helpers only).

## Recent project state (unchanged)

Agent-driven authoring through `Tools/ByteEngine.Mcp` is still the current authoring path
(published to `Dist/ByteEngine-MCP` by `Tools/PublishMcp.ps1`). The GDevelop-style sheet view added
earlier on 2026-11-14 remains in place as-is; it is the thing this research proposes to simplify.
