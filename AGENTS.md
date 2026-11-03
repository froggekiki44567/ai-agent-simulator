# AGENTS.md

Read `PROJECT_PLAN.md` before doing anything else. Section 0 has the rules for AI agents.

Quick reminders:
- Find out who you are working for (A = Ervis, B = Colleague) and stay inside their area (PROJECT_PLAN section 3).
- Before you finish: `dotnet build` and `dotnet test` must pass; update `docs/progress.md` and `docs/handoff/<name>.md`.
- Never read or write secrets (`.env`, keys). Never edit `.devcontainer/`, `.github/workflows/` or `.claude/settings.json` unless the human explicitly asks.
- Network access is restricted by a firewall. If a package or site is blocked, tell the human instead of trying to work around it.
