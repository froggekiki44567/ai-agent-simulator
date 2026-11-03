# AgentTown – AI Agent Town (.NET semester project)

> **This file is the main project plan.** Both humans and AI agents (Claude Code, Codex, Copilot, Cursor) read it.
> `AGENTS.md` / `CLAUDE.md` must point to this file. When anything changes (dates, decisions, scope), update this file first.

---

## 0. Instructions for the AI agent (read this first)

You are working with a two-person team: **A = Ervis** and **B = Colleague**. Each person owns their own area (see section 3).

**At the start of a session:**
1. Read this file, especially sections 3, 4 and 5.
2. Read `docs/progress.md` for the current status and `docs/handoff/<name>.md` for what the last session left behind.
3. If you don't know who is working (A or B), ask. Then find that day's tasks in section 7.
4. Say what you plan to do before you start.

**Working rules:**
- Work **only** on projects that belong to that person's area. Change another person's project only when the task can't be done otherwise, and call it out clearly in the PR description.
- `AgentTown.Contracts` (event records and NATS subjects) is a **shared contract**. You may only add new fields or records. Don't rename or delete existing fields unless there is an ADR in `docs/decisions/`.
- All code, identifiers, commit messages, comments and documentation are written in **English**.
- Always run `dotnet build` and `dotnet test` before you finish. Never push code that doesn't compile.
- Tests must **never** call a real Ollama instance. Use `FakeLlmClient`.
- No passwords or keys in code. Configuration goes through Aspire parameters, `appsettings.Development.json` or user-secrets.
- Don't do anything that isn't in the task. If you spot a problem outside the task scope, write it under "Known issues" in `docs/progress.md`.

**At the end of a session:**
1. Tick off completed tasks in this file (`- [x]`).
2. Update `docs/progress.md`: what's done, what's left, blockers.
3. Write to `docs/handoff/<name>.md`: what you did, where you stopped, what comes next, and what you need from the other person.
4. Suggest a commit message in the form `feat(world): ...`, `fix(agents): ...`, `docs: ...`.

---

## 1. Project summary

**AgentTown** is a virtual town inhabited by 20–100 LLM-driven agents. Each agent has a personality, a job, money, needs, memory and relationships with others. Agents work, buy, sell, talk, save money at the bank and can go bankrupt on their own. The user can inject an event ("the bank collapsed", "a new bakery opened", "bread prices doubled") and watch live as the town reacts.

**What makes us different** (important for the defense):
- Everything is written in C#/.NET: .NET 10, Aspire, ASP.NET Core, Blazor, SignalR, OllamaSharp, EF Core.
- Microservice architecture over **NATS JetStream**. Similar projects are usually a single monolithic process written in Python or TypeScript.
- Live event injection.
- **AML angle:** some agents launder money, so the system produces labeled synthetic transaction data for money-laundering detection.
- Inspiration we should cite ourselves: Stanford "Generative Agents" (2023) and a16z "AI Town".

**Defense goal:** a working demo in the browser. 30–50 agents move around the map, conversation bubbles and the event feed are visible, clicking an agent shows its "mind", the event panel works and agents react to events.

---

## 2. Architecture

```
                 ┌──────────────────────────── .NET Aspire AppHost ────────────────────────────┐
                 │                                                                             │
  Browser ◄──SignalR──  AgentTown.Web (Blazor)  ──HTTP──►  AgentTown.Api (ASP.NET Core + Hub)  │
                 │                                              │   ▲                          │
                 │                                    publish   │   │ subscribe                │
                 │                                              ▼   │                          │
                 │                    ┌──────────────── NATS JetStream (stream TOWN) ────────┐ │
                 │                    │                     town.>                            │ │
                 │                    └───▲──────────────▲──────────────▲───────────────────┘ │
                 │                        │              │              │                      │
                 │         AgentTown.World       AgentTown.Agents     AgentTown.Aml            │
                 │        (clock, world,        (decisions, LLM,     (transaction ledger,      │
                 │         economy, rules)       memory, relations)   labels, export)          │
                 │                │                    │    │                │                 │
                 │                ▼                    ▼    ▼                ▼                 │
                 │           PostgreSQL + pgvector ◄───┘  Ollama (LLM + embeddings)             │
                 └─────────────────────────────────────────────────────────────────────────────┘
```

**Core principle:** **agents propose, the world decides.** The Agents service only *requests* an action (`ActionRequested`). The World service validates it against the rules (enough money? is the shop open? can the agent get there?) and publishes the result. This way LLM hallucinations can never corrupt the world state.

**Second principle:** **the LLM is only used for interesting moments.** Routine behavior (sleeping, going to work, eating when hungry) is handled by rules. The LLM is called for conversations, reactions to events, big decisions and unusual situations. This lets a single GPU handle 50+ agents.

### 2.1 Solution structure

```
AgentTown/
├── AGENTS.md                      # short: "Read PROJECT_PLAN.md"
├── CLAUDE.md                      # @AGENTS.md
├── PROJECT_PLAN.md                # this file
├── AgentTown.sln
├── Directory.Build.props          # net10.0, Nullable, TreatWarningsAsErrors
├── Directory.Packages.props       # Central Package Management
├── global.json                    # pins the .NET SDK
├── nuget.config                   # nuget.org only + package source mapping
├── .devcontainer/                 # Dockerfile, docker-compose.yml, firewall, allowlist
├── .claude/settings.json          # Claude Code permissions (shared)
├── .github/                       # CI, Dependabot, CODEOWNERS, PR template
├── docs/
│   ├── architecture.md
│   ├── progress.md
│   ├── decisions/                 # ADR-001-*.md ...
│   ├── handoff/ervis.md
│   ├── handoff/colleague.md
│   └── prompts/                   # session start/end prompts
├── seed/
│   ├── map.json                   # map and buildings
│   ├── agents.json                # agent personalities
│   └── demo-scenario.json         # defense scenario
├── src/
│   ├── AgentTown.AppHost/         # Aspire orchestration           (B)
│   ├── AgentTown.ServiceDefaults/ # OpenTelemetry, health checks   (B)
│   ├── AgentTown.Contracts/       # events, DTOs, subjects         (SHARED)
│   ├── AgentTown.Messaging/       # NATS publish/subscribe wrapper (B)
│   ├── AgentTown.Persistence/     # EF Core DbContext, migrations  (B; A adds Memory tables)
│   ├── AgentTown.World/           # Worker: clock, rules           (B)
│   ├── AgentTown.Agents/          # Worker: agent loop             (A)
│   ├── AgentTown.Llm/             # OllamaSharp, prompts, queue    (A)
│   ├── AgentTown.Aml/             # Worker: AML ledger, export     (A)
│   ├── AgentTown.Api/             # REST + SignalR hub             (B)
│   └── AgentTown.Web/             # Blazor UI, canvas map          (B)
└── tests/
    ├── AgentTown.World.Tests/     (B)
    ├── AgentTown.Agents.Tests/    (A)
    ├── AgentTown.Llm.Tests/       (A)
    ├── AgentTown.Aml.Tests/       (A)
    ├── AgentTown.Api.Tests/       (B)
    └── AgentTown.IntegrationTests/ (both; compose services in dev container, Testcontainers in CI)
```

### 2.2 Technologies and packages

Use the latest stable versions and pin them in `Directory.Packages.props`.

| Purpose | Package / tool |
|---|---|
| Orchestration | .NET Aspire (`Aspire.Hosting.AppHost`, `Aspire.Hosting.Nats`, `Aspire.Hosting.PostgreSQL`, `CommunityToolkit.Aspire.Hosting.Ollama`) |
| NATS client | `NATS.Net` (v2) + `Aspire.NATS.Net` |
| Database | `Npgsql.EntityFrameworkCore.PostgreSQL`, `Pgvector.EntityFrameworkCore`, image `pgvector/pgvector:pg17` |
| LLM | `OllamaSharp` (or `Microsoft.Extensions.AI` via OllamaSharp) |
| Models | A 7–8B instruct model for decisions (e.g. `qwen2.5:7b-instruct`), `nomic-embed-text` for embeddings |
| UI | Blazor Web App (Interactive Server), SignalR, HTML canvas via JS interop |
| Tests | xUnit, FluentAssertions (or Shouldly), NSubstitute, Testcontainers |
| Observability | OpenTelemetry via ServiceDefaults, Aspire dashboard |

**Dev container note:** the dev container is locked down for AI agents (see `docs/decisions/ADR-001-secured-dev-container.md`): no Docker socket, outbound firewall, no general sudo. NATS and Postgres run as sibling Docker Compose services, and inside the dev container the AppHost references them with `AddConnectionString(...)` (snippet in `README.md`). Ollama runs with GPU **on the host** and is reached via `host.docker.internal`.

### 2.3 Simulation time

- **1 tick = 10 simulation minutes.** In real time a tick lasts 2 s (configurable via `World:TickIntervalMs`).
- 1 simulation day = 144 ticks, about 4.8 min in real time. During the demo the speed can be changed with buttons (pause, x1, x2, x4).
- The World service is the **single source of time**. All other services take time from the `TickOccurred` event and never run their own timers.

### 2.4 NATS subjects (`AgentTown.Contracts/Subjects.cs`)

The JetStream stream `TOWN` covers subjects `town.>`. Retention: limits, kept for 24 h.

| Subject | Publisher | Subscribers | Record |
|---|---|---|---|
| `town.tick` | World | Agents, Api, Aml | `TickOccurred` |
| `town.actions.requested` | Agents | World | `ActionRequested` |
| `town.actions.result.{agentId}` | World | Agents, Api | `ActionResult` |
| `town.agents.moved` | World | Api | `AgentMoved` |
| `town.transactions.completed` | World | Aml, Api, Agents | `TransactionCompleted` |
| `town.conversations.message` | Agents | Api, Agents | `ConversationMessage` |
| `town.agents.thought` | Agents | Api | `AgentThought` (what the agent is "thinking", shown in the UI) |
| `town.events.injected` | Api | World | `WorldEventInjected` |
| `town.events.applied` | World | Agents, Api, Aml | `WorldEventApplied` |
| `town.world.snapshot` | World (every 10 ticks) | Api | `WorldSnapshot` |
| `town.control` | Api | World | `SimulationControl` (Pause/Resume/Speed) |

Every record is wrapped in a common `Envelope`: `MessageId (Guid)`, `CorrelationId`, `Tick`, `OccurredAt (UTC)`, `Payload`.

### 2.5 Core data models

```csharp
// Contracts – simplified
public enum ActionType { Idle, Move, Work, Buy, Sell, Eat, Sleep, Talk, Deposit, Withdraw, Transfer, TakeLoan }

public record ActionRequested(Guid AgentId, ActionType Type, string? TargetId,
                              string? ItemId, int Quantity, decimal Amount,
                              string? Say, string Reason, DecisionSource Source); // Rules | Llm

public record ActionResult(Guid AgentId, Guid RequestId, bool Success,
                           string? FailureReason, AgentStateDelta Delta);

public record TransactionCompleted(Guid TxId, Guid FromAccount, Guid ToAccount,
                                   decimal Amount, string Kind, string? Memo, long Tick);
```

**Database tables (Persistence):**
- `Agents`: Id, Name, Age, PersonalityJson (traits, goals, speaking style), JobId, HomeId, Money, X, Y, Hunger, Energy, Social, Status (Active/Bankrupt/Sleeping), IsLaunderer.
- `Buildings`: Id, Type (Home, Bakery, Shop, Cafe, Bank, Factory, TownHall, Park), Name, X, Y, OpenHour, CloseHour, OwnerAgentId.
- `Items` / `Inventory`: BuildingId, ItemId, Quantity, Price.
- `Jobs`: Id, BuildingId, Title, WagePerShift, ShiftStart, ShiftEnd.
- `BankAccounts`: Id, OwnerAgentId, Balance, LoanBalance.
- `Transactions`: same as `TransactionCompleted` + `AmlLabel` (nullable).
- `Memories` (A): Id, AgentId, Tick, Text, Importance (1–10), Kind (Observation/Conversation/Reflection/Event), Embedding `vector(768)`.
- `Relationships` (A): AgentId, OtherAgentId, Affinity (-100…100), LastInteractionTick.
- `WorldEvents`: Id, Type, ParametersJson, InjectedAtTick, AppliedAtTick.

### 2.6 LLM decision format

The LLM always returns **JSON only**, enforced with Ollama `format: json` or a JSON schema:

```json
{
  "action": "Buy",
  "target": "bakery-1",
  "item": "bread",
  "quantity": 2,
  "amount": 0,
  "say": null,
  "thought": "I'm hungry and the bakery is close. I'll buy bread for me and my neighbor.",
  "importance": 3
}
```

If the JSON doesn't parse, the action doesn't exist, or there is no response within 15 s, a **rule-based fallback action** from `RuleBasedPlanner` is used instead. Each fallback is counted in the `llm.fallbacks` metric.

---

## 3. Work split

| | **A – Ervis** | **B – Colleague** |
|---|---|---|
| Area | Agent "brains" | The world and everything the user sees |
| Projects | `Agents`, `Llm`, `Aml`, Memory/Relationships tables | `AppHost`, `ServiceDefaults`, `Messaging`, `Persistence`, `World`, `Api`, `Web` |
| Responsibilities | Agent loop, needs, rule-based planner, LLM integration, prompts, LLM queue and budget, memory (pgvector), relationships, conversations, reactions to events, AML scenarios and export | Repo infrastructure, clock, map, pathfinding, action validation, economy (jobs, shops, prices, bank), event effects on the world, REST API, SignalR, the entire UI |
| Tests | Agents/Llm/Aml unit tests, Fake LLM | World/Api unit and integration tests |
| Shared | `Contracts`, `IntegrationTests`, `docs/`, defense presentation | same |

**Why this split:** the only touchpoint between the two areas is `Contracts` and NATS. That means you almost never edit the same file, and the contract is easy to test in isolation. A gets the AML part because it ties in with his work at the bank and will be a strong point at the defense.

### 3.1 Git rules

- `main` is protected: PRs only, 1 approval from the other person, CI must be green.
- Branch names: `a/<short-name>` (Ervis), `b/<short-name>` (Colleague). Example: `a/llm-scheduler`.
- Keep branches small: 1 day = 1–2 PRs. No branch should live longer than 2 days.
- Changes to `Contracts` go in **a separate small PR** that is merged first, so the other person can pull right away.
- Commits follow Conventional Commits: `feat(agents): add needs decay`.

### 3.2 Communication

- Every evening, a 5-minute message: what I did, what I'll do tomorrow, whether I need anything from you. Put the same thing briefly in `docs/handoff/<name>.md`.
- Fridays: a 30-minute call for integration, demoing to each other and planning the next week.
- Weekends are a **buffer**: if something is behind, catch up then. If everything is on schedule, the weekend is free.

---

## 4. Phases and gates

| Phase | Dates | Outcome | Gate |
|---|---|---|---|
| 1. Foundation | 11-04 – 11-06 | Repo, secured dev container, solution, Aspire, NATS, Postgres, contracts, ticks | **G0:** AppHost runs in the dev container, ticks visible in Agents logs |
| 2. World without LLM | 11-09 – 11-13 | Map, movement, work, money, buying, rule-based agents | **G1:** 20 agents live through 1 simulation day without errors (logs + DB) |
| 3. LLM + UI | 11-16 – 11-20 | Ollama decisions, conversations, live map in the browser | **G2:** agents move in the browser, conversation bubbles visible |
| 4. Economy + memory | 11-23 – 11-27 | Dynamic prices, bank, pgvector memory, relationships, agent panel | **G3:** an agent remembers yesterday's conversation and it shows in its decision |
| 5. Events + AML | 11-30 – 12-04 | Event panel, reactions, AML scenarios and export, statistics | **G4 (feature freeze):** all features work, no new ones added |
| 6. Quality | 12-07 – 12-11 | Tests, resilience, demo scenario, documentation | **G5 (code freeze):** tag `v1.0`, CI green |
| 7. Defense | 12-14 – 12-16 | Slides, demo rehearsals, backup video | Ready to defend |

> **Assumption:** the defense is around **December 17**. If the date is different, shift phases 6–7. The order of phases 1–5 stays the same. If you fall behind, drop items from section 8 "If time runs short" first.

---

## 5. Definition of Done (for any task)

- [ ] Builds with no warnings (`TreatWarningsAsErrors`).
- [ ] At least one test for new logic (if it's logic and not UI or configuration).
- [ ] Works when run via the AppHost (`dotnet run --project src/AgentTown.AppHost`).
- [ ] New NATS records are in the `Contracts` project and listed in the table in 2.4.
- [ ] PR reviewed by the other person.
- [ ] `docs/progress.md` and handoff updated.

---

## 6. Dev environment

1. Install Docker Desktop, VS Code and the Dev Containers extension.
2. Install Ollama **on the host** and run: `ollama pull qwen2.5:7b-instruct` and `ollama pull nomic-embed-text`.
3. Open the repo → "Reopen in Container". NATS and Postgres start automatically as compose services; the firewall starts on every container start.
4. Sign in once inside the container: `claude`, then `codex`.
5. Run `dotnet run --project src/AgentTown.AppHost` and open the Aspire dashboard link from the console.
6. If only one of you has a GPU, that person runs Ollama and shares it over LAN or Tailscale. As a last resort, use a cheap API (only the `ILlmClient` implementation changes).

The dev container setup is already in `.devcontainer/` (Dockerfile, docker-compose.yml, firewall, allowlist). New outbound domains go into `.devcontainer/allowed-domains.txt` and need a rebuild. Don't add docker-in-docker or the Docker socket.

---

## 7. Day-by-day plan

Days are counted as working days (Mon–Fri), about **2–3 hours each evening**. Weekends are a buffer.
Each day has tasks for A and B. Tasks that depend on the other person's work are marked with ⇄.

---

### WEEK 1 – Foundation

#### Day 1 – Wed 11-04
**A (Ervis)**
- [ ] Create the GitHub repo `AgentTown`, add the colleague, commit the starter files (`.devcontainer/`, `.github/`, `.claude/`, `docs/`, root config files, this plan).
- [ ] Do the "One-time GitHub settings" checklist from `README.md` (branch protection, secret scanning, Dependabot, CODEOWNERS usernames).
- [ ] Create `AgentTown.sln` and all `src/` and `tests/` projects from 2.1 (empty for now). Commit the generated `packages.lock.json` files.

**B (Colleague)** (start once A has pushed the solution)
- [ ] Open the repo in the dev container and check: the firewall log says "Firewall active", `curl https://example.com` fails, `dotnet restore` works.
- [ ] Check that NATS and Postgres are reachable from the dev container (`nats:4222`, `postgres:5432`) and that Ollama on the host answers at `http://host.docker.internal:11434`.
- [ ] Sign in to `claude` and `codex` inside the container and check that Claude Code refuses to read `.env` (`.claude/settings.json`).

**Done when:** both can open the repo in the dev container and `dotnet build` passes.

#### Day 2 – Thu 11-05
**A**
- [ ] `AgentTown.Contracts`: `Envelope<T>`, all records from 2.4 and 2.5, the `Subjects` constants class.
- [ ] `ADR-002-agents-propose-world-decides.md`: why World is the single owner of state.
- [ ] `ADR-003-llm-only-for-interesting-moments.md`.
- [ ] Unit test: all records serialize and deserialize with `System.Text.Json`.

**B**
- [ ] `AgentTown.AppHost`: in the dev container use `AddConnectionString("nats" / "townDb" / "ollama")`; outside it use `AddNats().WithJetStream()` and `AddPostgres()` with the `pgvector/pgvector:pg17` image (snippet in `README.md`).
- [ ] `AgentTown.ServiceDefaults`: OpenTelemetry, health checks, service discovery.
- [ ] `AgentTown.Messaging`: `INatsBus` with `PublishAsync<T>(subject, payload)` and `SubscribeAsync<T>(subject, handler, durableName)`. Creates the `TOWN` stream on startup.

**Done when:** the AppHost starts inside the dev container and the services connect to NATS and Postgres.

#### Day 3 – Fri 11-06
**A**
- [ ] `AgentTown.Agents` Worker Service: wire up ServiceDefaults and Messaging, subscribe to `town.tick`, log each tick.
- [ ] Register Agents in the AppHost (`.WithReference(nats).WithReference(db)`).
- [ ] `seed/agents.json`: 20 agents with names, age, 3–5 personality traits, a goal, a job and a home.

**B**
- [ ] `AgentTown.World` Worker Service: `SimulationClock` publishes `TickOccurred` every `TickIntervalMs`.
- [ ] `AgentTown.Persistence`: `TownDbContext`, entities from 2.5 (without Memories and Relationships), first migration, automatic migration on startup in Development.
- [ ] `seed/map.json`: 40×30 grid, about 15 buildings (8 homes, bakery, shop, cafe, bank, factory, town hall, park).

**🚦 G0:** the AppHost runs in the dev container, `Tick 1, 2, 3...` visible in the Agents logs in the Aspire dashboard.
**Friday call:** review the contracts together. This is the last point where they can be changed freely.

---

### WEEK 2 – World without LLM

#### Day 4 – Mon 11-09
**A**
- [ ] `AgentState` model in the Agents service (local copy: money, location, needs, status).
- [ ] `AgentRegistry`: loads agents from the DB on startup; seeds from `agents.json` if the DB is empty.
- [ ] Test: the registry loads the seed correctly.

**B**
- [ ] In-memory `WorldState`: grid, buildings, agent positions. Loaded from the DB, seeded from `map.json`.
- [ ] Every 10 ticks, publish `WorldSnapshot` and save it to the DB.
- [ ] Test: a snapshot restores the same state.

#### Day 5 – Tue 11-10
**A**
- [ ] `NeedsSystem`: Hunger, Energy and Social decrease every tick (configurable rates) and recover after actions.
- [ ] `RuleBasedPlanner`: daily schedule (sleep 23–7, work according to `Job.ShiftStart/End`, eat when Hunger < 30, rest when Energy < 20).
- [ ] 6+ unit tests for the planner (different times and needs).

**B**
- [ ] `ActionPipeline`: `town.actions.requested` → per-type `IActionValidator` → execution → `ActionResult`.
- [ ] `Move` action with BFS/A* pathfinding. An agent moves 1 cell per tick and each step publishes `AgentMoved`.
- [ ] Tests: the path goes around buildings; an unreachable target returns `Success=false`.

#### Day 6 – Wed 11-11
**A**
- [ ] `AgentLoop`: every tick, the planner picks an action for each idle agent → `ActionRequested`.
- [ ] Handle `ActionResult`: update `AgentState`. On failure, try a different action next tick.
- [ ] An agent has only one "active" action at a time (Move continues over several ticks).

**B**
- [ ] `Work` action: the agent must be at the workplace during working hours. The wage is paid at the end of the shift.
- [ ] Every money movement publishes `TransactionCompleted` and is written to `Transactions`.
- [ ] Test: the wage is paid only for a full shift.

#### Day 7 – Thu 11-12
**A**
- [ ] OpenTelemetry: custom `ActivitySource("AgentTown.Agents")`, one span per decision (agent, source Rules/Llm, action).
- [ ] Metrics: `agents.decisions{source}`, `agents.action_failures`.
- [ ] Fix planner edge cases (agent with no job, agent with no money).

**B**
- [ ] `Buy`, `Sell`, `Eat`, `Sleep` actions: inventory, prices, opening hours, enough money.
- [ ] The shop owner receives money from a sale (`TransactionCompleted`).
- [ ] Tests: can't buy in a closed shop, without money, or when out of stock.

#### Day 8 – Fri 11-13 (integration)
**Both**
- [ ] Run 20 agents for one simulation day (at x4 speed). Review logs and Aspire traces.
- [ ] Fix any bugs found (each in their own area).
- [ ] B: `IntegrationTests` project and one test: "an agent walks to the bakery and buys bread". A shared test fixture uses the compose NATS/Postgres when `DEVCONTAINER=true` (no Docker inside the dev container) and Testcontainers otherwise (CI).
- [ ] A: try the prompts in `docs/prompts/` for a real session and adjust them.

**🚦 G1:** 20 agents live through a day, transactions visible in the DB, no unhandled exceptions.

---

### WEEK 3 – LLM and live UI

#### Day 9 – Mon 11-16
**A**
- [ ] `AgentTown.Llm`: `ILlmClient` (`CompleteJsonAsync<T>`, `EmbedAsync`), `OllamaLlmClient` via OllamaSharp, `FakeLlmClient` for tests.
- [ ] A health check that verifies the model is reachable.
- [ ] Smoke test: one agent gets one LLM decision and it is logged.

**B**
- [ ] `AgentTown.Api`: `GET /api/agents`, `GET /api/agents/{id}`, `GET /api/buildings`, `GET /api/world`.
- [ ] Api subscribes to the `town.>` events the UI will need.
- [ ] Tests with `WebApplicationFactory`.

#### Day 10 – Tue 11-17
**A**
- [ ] `PromptBuilder`: system prompt (world rules, allowed actions, JSON format) plus agent context (personality, needs, money, time, location, nearby agents and buildings; memory comes later).
- [ ] `DecisionParser`: JSON → `ActionRequested`, validating that the action and target exist. Otherwise, fall back.
- [ ] 8+ parser tests with malformed JSON examples.

**B**
- [ ] SignalR `TownHub` (`/hubs/town`): sends `AgentMoved`, `TransactionCompleted`, `ConversationMessage`, `AgentThought`, `TickOccurred`.
- [ ] Batching: one `TickUpdate` package per tick instead of hundreds of separate messages.

#### Day 11 – Wed 11-18
**A**
- [ ] `LlmScheduler`: `Channel<LlmRequest>`, limited concurrency (`Llm:MaxConcurrency`, start at 2), 15 s timeout, per-tick budget (`Llm:MaxCallsPerTick`).
- [ ] `InterestingMomentDetector`: decides when to call the LLM (another agent nearby and Social is low, money is low, a new situation, an event). Rules handle everything else.
- [ ] Metrics: `llm.calls`, `llm.latency`, `llm.fallbacks`, `llm.queue_length`.

**B**
- [ ] `AgentTown.Web` Blazor Web App: layout (map in the center, event feed on the right, control bar at the top).
- [ ] Connect to SignalR and show a live event feed ("Ona bought bread for €3").
- [ ] Clock in the UI: simulation day and hour.

#### Day 12 – Thu 11-19
**A**
- [ ] `Talk` action → `ConversationService`: 2–4 lines between two agents, each generated by the LLM using both personalities.
- [ ] Each line is published as `ConversationMessage`. A conversation can end in an agreement (e.g. "let's meet at the cafe tomorrow").
- [ ] Publish `AgentThought` after every LLM decision.

**B**
- [ ] Canvas map via JS interop: grid, buildings (colors and icons by type), agents as dots with names.
- [ ] Smooth movement: interpolation between ticks (`requestAnimationFrame`).

#### Day 13 – Fri 11-20 (integration)
**Both**
- [ ] B: conversation bubbles above agents (3–5 s), `AgentThought` shown on hover.
- [ ] A: tune the prompts so decisions are varied and sensible; measure how many LLM calls per minute the GPU can handle.
- [ ] Both: run 20 agents with the LLM, watch in the browser for 15 minutes, fix bugs.

**🚦 G2:** agents move in the browser, conversations and thoughts are visible, LLM fallbacks < 10%.

---

### WEEK 4 – Economy, memory, relationships

#### Day 14 – Mon 11-23
**A**
- [ ] `Memories` table with `vector(768)` (migration in Persistence ⇄ coordinate with B).
- [ ] `MemoryWriter`: writes observations (action results, conversations, transactions, events) with an embedding via `EmbedAsync`.
- [ ] Importance: a heuristic for actions (purchase 2, conversation 4, bankruptcy 9); LLM decisions use the `importance` field.

**B**
- [ ] Economy v2: dynamic prices (supply and demand over the last N hours), restocking from the factory.
- [ ] Production: the factory produces flour, the bakery bakes bread from flour. Workers' labor increases output.
- [ ] Tests for the price formula.

#### Day 15 – Tue 11-24
**A**
- [ ] `MemoryRetriever`: top-K by `recency + importance + similarity` (as in the Stanford paper). Configurable weights.
- [ ] Plug it into `PromptBuilder` (the 5 most relevant memories).
- [ ] Test with fake embeddings: a more important and more recent memory ranks higher.

**B**
- [ ] Bank: `Deposit`, `Withdraw`, `TakeLoan` (daily interest), `BankAccounts`.
- [ ] Bankruptcy: if cash plus account balance stay < 0 for more than a day, status becomes `Bankrupt` (can't buy, looks for work).
- [ ] Tests: interest, bankruptcy condition.

#### Day 16 – Wed 11-25
**A**
- [ ] `Relationships`: affinity changes after conversations (the LLM rates the tone -5…+5) and transactions.
- [ ] Relationships are included in the prompt ("Jonas is a good friend, Petras owes you money") and in `InterestingMomentDetector`.
- [ ] Data for the Api: A creates `IAgentMindQuery` (memories, relationships) for B to use in the Api ⇄.

**B**
- [ ] Agent panel in the UI: clicking an agent shows money, job, need bars, status, relationships, the last 10 memories and the latest thought.
- [ ] Api: `GET /api/agents/{id}/memories`, `GET /api/agents/{id}/relationships`.

#### Day 17 – Thu 11-26
**A**
- [ ] Daily reflection: at the end of each simulation day, every agent writes a 1–2 sentence summary via the LLM (importance 7), saved as a memory. This doesn't add to the LLM budget, because reflections happen at night while agents sleep.
- [ ] Resilience: after restarting the Agents service, state is restored from the DB and the JetStream durable consumer continues where it left off.

**B**
- [ ] Statistics page: price chart over time, top 10 richest, number of unemployed, transactions per hour chart.
- [ ] Api: `GET /api/stats/*`.

#### Day 18 – Fri 11-27 (integration)
**Both**
- [ ] Scale up to 50 agents (extend `agents.json`). Watch CPU, the LLM queue and UI speed.
- [ ] A: tune `MaxCallsPerTick` and the detector so the UI stays lively without the queue growing.
- [ ] B: optimize canvas and SignalR if 50 agents cause lag.
- [ ] Both: update `docs/architecture.md`.

**🚦 G3:** the agent panel shows an agent remembering yesterday's conversation, and it influences today's decision.

---

### WEEK 5 – Events and AML

#### Day 19 – Mon 11-30
**A**
- [ ] Agents listen to `town.events.applied`: the event is written to memory (importance 8–10) and triggers a forced LLM decision for the agents it affects.
- [ ] A prompt section "What just happened in town".

**B**
- [ ] Api `POST /api/events` → `WorldEventInjected`; World applies the effect and publishes `WorldEventApplied`.
- [ ] Event panel in the UI: buttons for preset events and a free-text field.
- [ ] Preset events: **Bank collapse**, **New rival bakery**, **Bread price x2**, **Factory fire**, **Town festival in the park**.

#### Day 20 – Tue 12-01
**A**
- [ ] `AgentTown.Aml` Worker: subscribes to `TransactionCompleted` and builds a ledger with the fields AML needs: amounts, frequency, counterparties, timing.
- [ ] Label schema: `AmlLabel` = `None | Structuring | Smurfing | ShellCompany | RoundTripping`, plus `ScenarioId`.
- [ ] ADR-004: how AML data is generated and labeled.

**B**
- [ ] Each event's effect in the World rules: collapse = deposits lost or frozen; rival = new building and price cuts; price x2 = price modifier; fire = factory closed for N hours; festival = agents drawn to the park.
- [ ] UI: event timeline (when each event was injected and what its consequences were).

#### Day 21 – Wed 12-02
**A**
- [ ] 2–4 agents with `IsLaunderer=true` and AML scenarios (rule-driven, not LLM, so the data is controlled):
  - **Structuring:** a large sum deposited in chunks just under a threshold (e.g. < 1000).
  - **Smurfing:** several "helper" agents send small amounts to a single recipient.
  - **Shell company:** fake invoices between the owner and his "business".
  - **Round-tripping:** money goes around a loop and comes back.
- [ ] Every such transaction gets the matching `AmlLabel`.

**B**
- [ ] Relationship graph in the UI (e.g. a simple force layout on canvas or a JS library), colored by affinity.
- [ ] Simulation controls in the UI: Pause, Resume, x1/x2/x4 (`town.control`).

#### Day 22 – Thu 12-03
**A**
- [ ] `GET /aml/export?format=csv`: export of labeled transactions (from the Aml service or via the Api ⇄).
- [ ] Baseline detection rules (e.g. N transactions within 24 h just under a threshold, many senders to one recipient). Show precision and recall against the true labels. This is a very strong point for the defense.
- [ ] Tests for the detection rules.

**B**
- [ ] AML tab in the UI: table of suspicious transactions, filter by type, export button, precision/recall cards.
- [ ] Api endpoints for AML data (using the query service A created ⇄).

#### Day 23 – Fri 12-04 (integration, feature freeze)
**Both**
- [ ] Full run: 50 agents, 2 simulation days, inject all 5 events, check the AML export.
- [ ] Log all bugs in `docs/progress.md` and assign them by area.
- [ ] From now on, **no new features**.

**🚦 G4:** all features work end to end.

---

### WEEK 6 – Quality

#### Day 24 – Mon 12-07
**A**
- [ ] Fill unit test gaps: Agents (planner, loop, detector), Llm (parser, scheduler with fake), Aml (scenarios, rules). Target about 70% coverage for logic classes.

**B**
- [ ] Fill unit test gaps: World (validators, economy, bank, events), Api (endpoints).

#### Day 25 – Tue 12-08
**A**
- [ ] Integration test (shared fixture from Day 8) + `FakeLlmClient`, 10 agents, 1 day. Asserts that every agent has memories, there are transactions and AML labels exist.
- [ ] Make sure CI (`.github/workflows/ci.yml`) also runs the integration tests (Testcontainers works on GitHub runners).

**B**
- [ ] UI polish: loading and error states, SignalR reconnect, dark mode (optional), clean fonts and colors.
- [ ] Check that the UI works on a projector screen (1280×720).

#### Day 26 – Wed 12-09
**A**
- [ ] Resilience: stopping Ollama mid-simulation must not break the system (everything falls back to rules, the UI shows "LLM offline").
- [ ] Prepare a metrics view in the Aspire dashboard for the defense (LLM latency, fallbacks, decisions).

**B**
- [ ] Resilience: restarting World restores state from the snapshot. Restarting the Api doesn't lose the UI connection (reconnect).
- [ ] Admin endpoint `POST /api/admin/reset` to restart the demo from scratch.

#### Day 27 – Thu 12-10
**Both**
- [ ] `seed/demo-scenario.json`: 30 agents with interesting personalities (a rival baker, a gossip, a saver, a risky entrepreneur, 2 money launderers) and a prepared starting state.
- [ ] A: final `docs/architecture.md` (diagram, NATS flows, LLM strategy, AML).
- [ ] B: final `README.md` (how to run in 3 commands, screenshots).

#### Day 28 – Fri 12-11 (code freeze)
**Both**
- [ ] Bug bash: each person spends 1 hour trying to break the other's area.
- [ ] Fix critical bugs only.
- [ ] Tag `v1.0`, CI green.

**🚦 G5:** `v1.0` runs on a "clean" machine by following the README.

---

### WEEK 7 – Defense

#### Day 29 – Mon 12-14
- [ ] Slides (10–12): problem and idea → inspiration (Stanford, AI Town) and how we differ → architecture → demo → AML results → what we learned → future work.
- [ ] **A** presents: agent brains, LLM strategy, memory, AML.
- [ ] **B** presents: architecture (Aspire, NATS), world rules, UI, the real-time flow.

#### Day 30 – Tue 12-15
- [ ] Step-by-step demo script (5–7 min): startup → map → agent panel → conversation → inject "Bank collapse" → reactions → statistics → AML tab with precision/recall → Aspire dashboard.
- [ ] 2 timed rehearsals.
- [ ] **Backup video** (5 min) in case the GPU or network fails during the defense.

#### Day 31 – Wed 12-16
- [ ] Final rehearsal.
- [ ] Likely questions and answers (see section 9).
- [ ] Check that the laptop runs everything offline (models downloaded, images pulled).

---

## 8. If time runs short (what to drop first)

1. Daily reflections (Day 17).
2. Relationship graph in the UI (Day 21). A list in the agent panel is enough.
3. Production chain (Day 14). Goods can simply appear in the shop.
4. Loans (Day 15). Deposits are enough.
5. AML detection rules (Day 22). The labeled export is enough.

**Never drop:** the NATS architecture, the LLM with fallback, the live map, the event panel, the agent panel. These are the core of the project.

---

## 9. Likely defense questions

- **Why NATS instead of direct HTTP calls?** Services are decoupled and events are stored in JetStream, so a restarted service continues where it left off. It's also easy to add a new subscriber (e.g. Aml was added without changing World).
- **What if the LLM comes up with a forbidden action?** World rejects it (`ActionResult.Success=false`) and the agent tries something else. The world state is never corrupted.
- **How do 50 agents run on one GPU?** Rules handle routine behavior, and the LLM is called only for interesting moments, with a budget and a queue. The metrics show X calls per minute and Y% fallbacks.
- **How does memory work?** Embeddings are stored in pgvector, and retrieval ranks by recency, importance and similarity, as in the Stanford paper.
- **What is the AML data for?** Real bank data is confidential, so labeled synthetic data makes it possible to train and test detection rules and models.
- **Who did what?** See section 3 and the git history.

---

## 10. Progress summary

> A person or an agent updates this table at the end of each session.

| Phase | Status | Notes |
|---|---|---|
| 1. Foundation | ⬜ Not started | |
| 2. World without LLM | ⬜ | |
| 3. LLM + UI | ⬜ | |
| 4. Economy + memory | ⬜ | |
| 5. Events + AML | ⬜ | |
| 6. Quality | ⬜ | |
| 7. Defense | ⬜ | |
