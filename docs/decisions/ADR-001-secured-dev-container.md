# ADR-001: Secured dev container without Docker access

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** A, B

## Context
We develop in GitHub Codespaces (and run the defense demo locally on a Mac with Docker Desktop). We run AI coding agents (Claude Code, Codex) with shell access inside the dev container. Aspire normally starts NATS and Postgres as Docker containers, which needs either the host Docker socket (= root on the host) or a privileged docker-in-docker container (= easy container escape). Both give an agent far more power than it needs.

## Decision
- The dev container is a Docker Compose service. NATS, Postgres and Ollama run as **sibling compose services**, not started by Aspire.
- The dev container has **no Docker socket and is not privileged**. It only gets `NET_ADMIN`/`NET_RAW` for the firewall.
- A **default-deny outbound firewall** (`init-firewall.sh`) allows only GitHub, NuGet, npm, Anthropic, OpenAI, VS Code and the compose network (NATS, Postgres, Ollama). The allowlist is baked into the image, so changing it needs a rebuild.
- The `vscode` user has **no general sudo**; it can only run the firewall script as root.
- In the AppHost, when `DEVCONTAINER=true`, NATS/Postgres/Ollama are referenced with `AddConnectionString(...)` instead of `AddNats(...)`/`AddPostgres(...)`.

## Alternatives considered
- docker-in-docker – needs `--privileged`.
- docker-outside-of-docker – mounts the host socket; an agent could start a container that mounts the host filesystem.

## Consequences
- Installing system packages needs a Dockerfile change and a rebuild.
- If a CDN changes IPs (NuGet, VS Code gallery), rerun `sudo /usr/local/bin/init-firewall.sh` or rebuild.
- Outside the dev container (or in CI) the AppHost can still start containers itself.
- Testcontainers can't run inside the dev container. Integration tests use the compose NATS/Postgres when `DEVCONTAINER=true` and Testcontainers in CI.
- In Codespaces the threat is mostly token leakage (`GITHUB_TOKEN`, Claude/Codex logins), not reaching a personal computer. GitHub stays allowed, so the firewall reduces but does not remove that risk; branch protection covers the rest.
- Ollama in Codespaces runs on CPU with a 1.5B model; real LLM behaviour is tuned on the Mac with a 7B model.
