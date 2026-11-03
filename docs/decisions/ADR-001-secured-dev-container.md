# ADR-001: Secured dev container without Docker access

- **Status:** Accepted
- **Date:** 2026-11-03
- **Deciders:** A, B

## Context
We develop locally with Docker Desktop. We run AI coding agents (Claude Code, Codex) with shell access inside the dev container. Aspire normally starts NATS and Postgres as Docker containers, which needs either the host Docker socket (= root on the host) or a privileged docker-in-docker container (= easy container escape). Both give an agent far more power than it needs.

## Decision
- The dev container is a Docker Compose service. NATS and Postgres run as **sibling compose services**, not started by Aspire. Ollama runs natively on the host for GPU access.
- The dev container has **no Docker socket and is not privileged**. It only gets `NET_ADMIN`/`NET_RAW` for the firewall.
- A **default-deny outbound firewall** (`init-firewall.sh`) allows only GitHub, NuGet, npm, Anthropic, OpenAI, VS Code the compose network (NATS, Postgres) and the host (Ollama). The allowlist is baked into the image, so changing it needs a rebuild.
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
- GitHub stays allowed and VS Code forwards Git credentials, so an agent could still push a branch; branch protection covers that.
- AI CLIs must be run inside the container; run on the host they have no protection.
