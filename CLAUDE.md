# CLAUDE.md — Morph C# implementation

Guidance for Claude Code when working in `Morph-CSharp`. The global working agreements (git, C#
style, design process, communication) live in `~/.claude/CLAUDE.md` and are **not** restated here —
this file only adds the project bindings the generic skills (spec-audit, spec-update, build-slice,
…) rely on.

## What this repo is

The **living reference implementation** of the Morph Protocol. The other implementations
(`../Morph-Cpp`, `../Morph-Delphi`, `../Morph-Arduino`) derive from it; Morph-Delphi is unbuilt and
**not** a reference (memory `morph-delphi-not-a-reference`).

## Source of truth (what "correct" means)

The wire format is defined by `../Morph-Definition/Protocol/Morph Protocol.xlsx` — authoritative,
**Peter-edited only, never modify it**. Conformance is audited against the xlsx sheets. How to read
it: memory `morph-reading-the-xlsx-definition` (python + openpyxl).

**No prefixed rule-id families.** The normative "rules" are the xlsx bit-tables; where the generic
skills say "walk the rule ids", here that means "walk the sheets."

## Design docs & ownership map

Under `Morph/Specifications/`:
- **`Backlog.md`** — THE status ledger. Sections: `To do`, `Open design questions (awaiting
  decision)`, `Verified non-issues (do not re-flag)`, `Resolved this session`. Owns every
  implementation-status and known-issue fact — status lives here and nowhere else.
- **`Maui-Demos.md`** — design doc + per-demo status for the MAUI demo conversions (Basic, Booking,
  Clique).

The protocol itself is owned upstream by the xlsx, not by these docs.

## Build & test

- **Core wire-format suite (the audit tests):** `dotnet test Morph/Morph.Tests/Morph.Tests.csproj`
  — currently 86 tests, expected all green. This is the suite to run after any change to the
  `Morph/Morph` library.
- Solutions: `Morph/Morph.sln` (library + tests); demos `MorphDemos/{Basic,Booking,Clique}/*.sln`;
  `Bat.Library/Bat.Library.sln`.
- MAUI demo apps (`*.Maui`) are **build-verified only** — they cannot be run on-device/emulator
  here; say so when reporting. `Test.Bat.Library.Settings` has ~3 pre-existing environmental
  failures (a hard-coded path) — unrelated, per `Backlog.md`.

## Key architectural constraints (also in memory)

- Apartments are **not** owned by connections; a client signs off by disposing its server apartment
  proxy (which sends a Morph `End`) — a socket close alone does not
  (`morph-apartments-not-owned-by-connections`).
- Clients need **no daemon**: daemon-less devices use the `Morph` library directly (register link
  types + `SetThreadCount` + local `IDSeed`); PC apps use `Morph.Daemon.Client`
  (`morph-clients-need-no-daemon`).
- `Morph.Manager` is UI only; the daemon owns all persistent state (`morph-manager-is-ui-only`).
- **Version numbers:** pre-production — do **not** flag version-number issues until Peter says Morph
  is in production (`morph-version-numbers-not-a-concern-preproduction`).
