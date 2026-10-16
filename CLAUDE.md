# Pared — Unity 6 (6000.3.7f1) LAN multiplayer board game

## Workflow
- Quick start: read `docs/RESUMEN_JUEGO.md` (mechanics + current state).
- Read `docs/AGENT_CONTEXT.md` (rules, architecture) and `docs/PROGRESS.md` (task status) before working.
- Tasks live in `docs/tasks/T**.md`. One task per session; apply the closing checklist in AGENT_CONTEXT §9.
- `Assets/DocsCLI/` is a copy of `docs/`; `docs/` is the source of truth.

## Driving Unity (Unity CLI + MCP)
- CLI: `C:\Users\USER\AppData\Local\Unity\bin\unity.exe` (skills in `.claude/skills/unity-cli`, `unity-pipeline`).
- The Editor is usually open with this project, so **do not use `-batchmode`** (project lock). Use the running Editor instead:
  - `unity status` — check the Editor is connected.
  - `unity recompile --json` — compile and get errors/warnings (use this to verify compilation).
  - `unity list` / `unity command <name> ...` — scene/GameObject/prefab/asset edits, play mode, `capture_game_view`, console logs.
  - `unity test` — EditMode/PlayMode tests.
- The `unity` MCP server (`.mcp.json`) exposes the same Editor tools.
- Only fall back to batchmode if `unity status` shows no Editor running.
