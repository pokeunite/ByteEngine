# Memory Bank protocol (ByteEngine)

Durable project memory lives in `MemoryBank/` at the repository root. Conversation
history is disposable; those files are the source of truth for project state.
Read them before work and update them before you finish.

## Session start — do this before editing anything

1. Read `MemoryBank/activeContext.md` and `MemoryBank/progress.md`. Do this for
   every new task, including one-line changes.
2. Read `MemoryBank/systemPatterns.md` before changing engine, editor, player or
   browser code you have not already inspected in this conversation.
3. Read `MemoryBank/decisions.md` before changing architecture, build/publish
   flow, plugin packaging, versioning, or anything `AGENTS.md` constrains.
4. Read `MemoryBank/projectBrief.md` and `MemoryBank/productContext.md` when the
   request is about scope, direction, or "should we build X".

If the request contradicts recorded state, say so instead of silently overriding
a documented decision.

## Trigger phrases

| The user says | You do |
| --- | --- |
| "update memory bank" | Review every file in `MemoryBank/`, correct what is stale, prepend a `sessionLog.md` entry. |
| "follow your custom instructions", "catch up", "where were we", "continue" | Read `activeContext.md` and `progress.md`, restate current state and the next step in two or three sentences, then continue the work. |
| "initialize memory bank" | Only when `MemoryBank/` is missing. Otherwise treat it as "update memory bank". |

## During work

- Memory can be stale. Verify claims against the code; when they disagree, trust
  the code and fix the memory file in the same task.
- Record durable facts as you learn them: the command that actually works, which
  file owns a system, a pitfall that cost time.
- Do not copy whole documents into memory. Link `Docs/...` or `Documentation/...`
  instead.
- Do not repeat instructions that already exist in `AGENTS.md`; reference them.

## Session end — required when the task changed files or decisions

Update in this order:

1. `MemoryBank/activeContext.md` — current focus, recent changes with real file
   paths, next step, open questions. Replace stale content instead of appending.
   Keep it under ~150 lines.
2. `MemoryBank/sessionLog.md` — prepend one dated entry: what changed, what was
   verified (exact command and result), what is left.
3. `MemoryBank/progress.md` — only when a capability moved between works,
   in progress, or known gap.
4. `MemoryBank/decisions.md` — only when a non-obvious choice would be expensive
   to rediscover. Append: decision, reason, rejected alternative.
5. `MemoryBank/systemPatterns.md` and `MemoryBank/techContext.md` — only when
   structure, commands, dependencies or conventions changed.

Memory files are tracked by git. Include them in the same commit as the change
they describe, and delete entries that are no longer true rather than letting
them rot.

Never store secrets, tokens, credentials, or machine-specific absolute paths
beyond the ones already documented in `AGENTS.md`.

## Maintenance

- Terse, factual, present tense, dated `YYYY-MM-DD`.
- One topic per file. A memory file over ~200 lines means detail belongs in a
  dated document under `Docs/`.
- Stale memory is worse than missing memory: prune aggressively.
