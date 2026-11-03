# AgentTown

A virtual town of LLM-driven agents that work, trade, talk and react to live events. Built with C#/.NET 10, Aspire, NATS JetStream, Blazor and Ollama. Full plan: [`PROJECT_PLAN.md`](PROJECT_PLAN.md).

## Getting started

1. Install Docker Desktop, VS Code and the **Dev Containers** extension.
2. Install [Ollama](https://ollama.com) **on your host** and pull the models:
   ```bash
   ollama pull qwen2.5:7b-instruct
   ollama pull nomic-embed-text
   ```
   On Linux, make Ollama listen on the Docker bridge: set `OLLAMA_HOST=0.0.0.0` for the Ollama service.
3. Open the repo in VS Code → **Reopen in Container**. NATS and Postgres start automatically next to the dev container.
4. Inside the container, sign in once: run `claude`, then `codex` (choose "Sign in with ChatGPT").
5. Run the app: `dotnet run --project src/AgentTown.AppHost`.

## Dev container security

AI agents run with shell access inside the container, so it is locked down (details in [ADR-001](docs/decisions/ADR-001-secured-dev-container.md)):

| Protection | Where |
|---|---|
| Outbound firewall: only GitHub, NuGet, npm, Anthropic, OpenAI, VS Code and the host (Ollama) | `.devcontainer/init-firewall.sh`, `allowed-domains.txt` |
| No Docker socket, not privileged | `.devcontainer/docker-compose.yml` |
| No general `sudo` for `vscode` (only the firewall script) | `.devcontainer/Dockerfile` |
| NATS and Postgres not exposed to the host network | `docker-compose.yml` (no `ports:`) |
| Claude Code cannot read secrets, use curl/sudo, or edit CI/dev container; asks before push/commit | `.claude/settings.json` |
| Codex asks before acting outside the workspace | `.devcontainer/codex-config.toml` |
| Only nuget.org as a package source | `nuget.config` |
| Lock files + vulnerability audit on restore | `Directory.Build.props` |
| Secret scanning, locked restore, vulnerable package check in CI | `.github/workflows/ci.yml` |
| Weekly dependency updates | `.github/dependabot.yml` |

**Need a new domain?** Add it to `.devcontainer/allowed-domains.txt` and rebuild the container. Agents cannot change the allowlist themselves.

**NuGet or VS Code extension downloads failing?** CDN IPs sometimes change. Run `sudo /usr/local/bin/init-firewall.sh` or rebuild.

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
else
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
