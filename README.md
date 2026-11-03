# AgentTown

A virtual town of LLM-driven agents that work, trade, talk and react to live events. Built with C#/.NET 10, Aspire, NATS JetStream, Blazor and Ollama. Full plan: [`PROJECT_PLAN.md`](PROJECT_PLAN.md).

## Getting started (local, Docker Desktop)

1. Install **Docker Desktop**, **VS Code** and the **Dev Containers** extension.
2. Install [Ollama](https://ollama.com) **on your computer** (not in Docker – natively it uses the GPU / Apple Silicon) and pull the models:
   ```bash
   ollama pull qwen2.5:7b-instruct
   ollama pull nomic-embed-text
   ```
   - Weaker machine (no GPU, < 16 GB RAM)? Use `qwen2.5:3b-instruct` and create `.devcontainer/.env` with `LLM_MODEL=qwen2.5:3b-instruct`.
   - Linux: make Ollama listen on the Docker bridge (`OLLAMA_HOST=0.0.0.0` for the Ollama service).
   - Only one of you has a good machine? The other sets `OLLAMA_CONNECTION=Endpoint=http://<ip>:11434` in `.devcontainer/.env`, and that IP must be added to the firewall allowlist.
3. Open the repo in VS Code → **Dev Containers: Reopen in Container**. NATS and Postgres start automatically next to the dev container; the log shows whether Ollama and the models were found.
4. Inside the container terminal, sign in once: `claude`, then `codex` (choose "Sign in with ChatGPT").
5. Run the app: `dotnet run --project src/AgentTown.AppHost`.

> **Run `claude` and `codex` only in the dev container terminal**, not in your Mac's own terminal. Inside the container they can't touch your computer; outside it they can.

## Dev container security

AI agents run with shell access inside the container, so it is locked down (details in [ADR-001](docs/decisions/ADR-001-secured-dev-container.md)):

| Protection | Where |
|---|---|
| Outbound firewall: only GitHub, NuGet, npm, Anthropic, OpenAI, VS Code, the compose services (NATS, Postgres) and Ollama on your computer | `.devcontainer/init-firewall.sh`, `allowed-domains.txt` |
| No Docker socket, not privileged | `.devcontainer/docker-compose.yml` |
| No general `sudo` for `vscode` (only the firewall script) | `.devcontainer/Dockerfile` |
| NATS and Postgres not exposed to your network | `docker-compose.yml` (no `ports:`) |
| Claude Code cannot read secrets, use curl/sudo, or edit CI/dev container; asks before push/commit | `.claude/settings.json` |
| Codex asks before acting outside the workspace | `.devcontainer/codex-config.toml` |
| Only nuget.org as a package source | `nuget.config` |
| Lock files + vulnerability audit on restore | `Directory.Build.props` |
| Secret scanning, locked restore, vulnerable package check in CI | `.github/workflows/ci.yml` |
| Weekly dependency updates | `.github/dependabot.yml` |

**Need a new domain?** Add it to `.devcontainer/allowed-domains.txt` and rebuild the container. Agents cannot change the allowlist themselves.

**NuGet or VS Code extension downloads failing?** CDN IPs sometimes change. Run `sudo /usr/local/bin/init-firewall.sh` or rebuild.

**Container won't start because of the firewall?** Temporarily remove the `postStartCommand` line from `.devcontainer/devcontainer.json`, rebuild, and check the firewall log to see which domain is missing.

**What the firewall does not stop:** GitHub is allowed and VS Code passes your Git credentials into the container, so an agent could still push a branch. Branch protection and code review are the second line of defence.

### AppHost inside the dev container

The dev container cannot start Docker containers, so the AppHost uses the compose services through connection strings:

```csharp
var builder = DistributedApplication.CreateBuilder(args);
var inDevContainer = builder.Configuration["DEVCONTAINER"] == "true";

IResourceBuilder<IResourceWithConnectionString> nats, townDb, ollama;
if (inDevContainer)
{
    // Values come from ConnectionStrings__* in .devcontainer/docker-compose.yml
    nats   = builder.AddConnectionString("nats");
    townDb = builder.AddConnectionString("townDb");
    ollama = builder.AddConnectionString("ollama");
}
else  // e.g. CI or someone running without the dev container
{
    nats   = builder.AddNats("nats").WithJetStream();
    townDb = builder.AddPostgres("postgres").WithImage("pgvector/pgvector", "pg17").AddDatabase("townDb");
    ollama = builder.AddConnectionString("ollama"); // Ollama always runs on the host
}

builder.AddProject<Projects.AgentTown_World>("world").WithReference(nats).WithReference(townDb);
builder.AddProject<Projects.AgentTown_Agents>("agents").WithReference(nats).WithReference(townDb).WithReference(ollama);
// ...
builder.Build().Run();
```

## One-time GitHub settings (do these by hand)

- [ ] **Branch protection on `main`:** require a PR, 1 approval, review from Code Owners, the `Build & test` and `Secret scan` checks; block force pushes and deletions.
- [ ] **Secret scanning + push protection** (Settings → Code security).
- [ ] **Dependabot alerts and security updates** on.
- [ ] **2FA** on both GitHub accounts.
- [ ] Replace `ERVIS_USERNAME` / `COLLEAGUE_USERNAME` in `.github/CODEOWNERS`.
- [ ] Actions → General → Workflow permissions: **Read repository contents**.

> VS Code passes your Git credentials into the container, so an agent *could* push a branch. Branch protection is what keeps `main` safe – don't skip it.
