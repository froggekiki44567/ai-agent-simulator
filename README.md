# AgentTown

A virtual town of LLM-driven agents that work, trade, talk and react to live events. Built with C#/.NET 10, Aspire, NATS JetStream, Blazor and Ollama. Full plan: [`PROJECT_PLAN.md`](PROJECT_PLAN.md).

## Getting started (GitHub Codespaces)

1. On GitHub: **Code → Codespaces → New with options** → machine type **2-core** (enough for coding) or **4-core** (when you work with the LLM).
2. Wait for the first build (5–10 min). NATS, Postgres and Ollama start automatically next to the dev container, and the small model (`qwen2.5:1.5b-instruct`) plus `nomic-embed-text` are pulled.
3. In the terminal, sign in once: `claude`, then `codex` (choose "Sign in with ChatGPT"). Logins live in Docker volumes and survive rebuilds.
4. Run the app: `dotnet run --project src/AgentTown.AppHost`. Codespaces forwards the Aspire dashboard and web ports automatically.

### Save your free hours

Verified students get 180 core-hours per month (GitHub Free: 120). A 2-core codespace uses 2 core-hours per hour, a 4-core one uses 4.

- [ ] github.com → Settings → Codespaces: set **Default idle timeout** to 15 minutes.
- [ ] Stop the codespace when you finish (Codespaces: Stop Current Codespace). Delete codespaces you don't use – they count toward storage.
- [ ] Use 2-core by default; switch to 4-core only for LLM-heavy days. Most tests use `FakeLlmClient` and don't need Ollama at all.

### Running locally on a Mac (for the defense demo)

The same `.devcontainer/` works with Docker Desktop. Ollama inside Docker on a Mac has no GPU, so run Ollama natively and point the container at it. Create `.devcontainer/.env` (gitignored):

```
OLLAMA_CONNECTION=Endpoint=http://host.docker.internal:11434
LLM_MODEL=qwen2.5:7b-instruct
```

Then `ollama pull qwen2.5:7b-instruct && ollama pull nomic-embed-text` on the Mac and **Dev Containers: Reopen in Container**.

## Dev container security

AI agents run with shell access inside the container, so it is locked down (details in [ADR-001](docs/decisions/ADR-001-secured-dev-container.md)):

| Protection | Where |
|---|---|
| Outbound firewall: only GitHub, NuGet, npm, Anthropic, OpenAI, VS Code and the compose services (NATS, Postgres, Ollama) | `.devcontainer/init-firewall.sh`, `allowed-domains.txt` |
| No Docker socket, not privileged | `.devcontainer/docker-compose.yml` |
| No general `sudo` for `vscode` (only the firewall script) | `.devcontainer/Dockerfile` |
| NATS, Postgres and Ollama not exposed outside the compose network | `docker-compose.yml` (no `ports:`) |
| Claude Code cannot read secrets, use curl/sudo, or edit CI/dev container; asks before push/commit | `.claude/settings.json` |
| Codex asks before acting outside the workspace | `.devcontainer/codex-config.toml` |
| Only nuget.org as a package source | `nuget.config` |
| Lock files + vulnerability audit on restore | `Directory.Build.props` |
| Secret scanning, locked restore, vulnerable package check in CI | `.github/workflows/ci.yml` |
| Weekly dependency updates | `.github/dependabot.yml` |

**Need a new domain?** Add it to `.devcontainer/allowed-domains.txt` and rebuild the container. Agents cannot change the allowlist themselves.

**NuGet or VS Code extension downloads failing?** CDN IPs sometimes change. Run `sudo /usr/local/bin/init-firewall.sh` or rebuild.

**Codespace won't connect after the firewall starts?** Open the repo on github.com, temporarily remove the `postStartCommand` line from `.devcontainer/devcontainer.json`, commit, and rebuild the codespace. Then send us the firewall log so the missing domain can be added.

**What the firewall does not stop:** GitHub is allowed, so an agent with the codespace's `GITHUB_TOKEN` could still push a branch or create an issue. Branch protection and code review are the second line of defence.

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

> Every codespace has a `GITHUB_TOKEN` that can push to this repo, so an agent *could* push a branch. Branch protection is what keeps `main` safe – don't skip it.
