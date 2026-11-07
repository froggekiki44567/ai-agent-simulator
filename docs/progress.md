# Progress

> Updated at the end of every session.

## Current phase: 1–3 in parallel (A is ahead on the agent side, waiting for B's infrastructure)

| Phase | Status | Notes |
|---|---|---|
| 1. Foundation | 🔄 In progress | Contracts + ADR-002/003 done (A). AppHost/Messaging/Persistence pending (B) |
| 2. World without LLM | 🔄 In progress | Agents side done in memory (state, needs, planner, loop, telemetry). World side on `b/world-foundation` |
| 3. LLM + UI | 🔄 In progress | LLM client, prompts, parser, scheduler, interesting-moment detector done |
| 4. Economy + memory | 🔄 In progress | A: memory and relationships done (in-memory stores). B: economy/bank pending |
| 5. Events | ⬜ | |
| 6. Quality | ⬜ | |
| 7. Defense | ⬜ | |

**A (Ervis) tasks done: 37 of 72 (~51%).** Goal of 50% (36 tasks) reached.

## Done
- Repo, solution structure, secured dev container (ADR-001).
- A: `AgentTown.Contracts` (envelope, subjects, events, models) + serialization tests; ADR-002, ADR-003. (`a/contracts`)
- A: `AgentState`, `NeedsSystem`, `RuleBasedPlanner` with edge cases (no job, no money). (`a/agent-brain`)
- A: 20 seed agents with loader, validation and tests. (`a/agents-seed`, not merged yet)
- A: `AgentTown.Llm`: `ILlmClient`, `OllamaLlmClient`, `FakeLlmClient`, health check, `PromptBuilder`, `DecisionParser`, `LlmScheduler` (budget, timeout, concurrency), LLM metrics. (`a/llm`)
- A: `InterestingMomentDetector`, `AgentDecisionService` (rules vs LLM, fallback). (`a/llm`)
- A: `AgentLoop` (one active action per agent, ActionResult handling, pending timeout, LLM in background), `AgentRegistry` (in-memory), `TownDirectory`, `AgentTelemetry` (`agents.decisions`, `agents.action_failures`, decision spans), DI wiring, `AgentThought` after LLM decisions. Tests: loop, wiring, 10 agents through one day. (`a/agent-loop`)
- A: Conversations: `ConversationPromptBuilder` + `ConversationReplyParser` (Llm), `ConversationService` (2–4 lines, turns, early end, agreement, tone per speaker, LLM failure ends it cleanly). An accepted `Talk` starts the conversation in the background; both agents are busy until it ends and both gain Social. `AgentLoop.ConversationFinished` event for memory/relationships. (`a/conversations`)
- A: Memory (`a/memory`): `MemoryWriter` (action results, LLM decisions, conversations for both sides, transactions, bankruptcy; embeddings via `EmbedAsync`), importance heuristic (`MemoryImportance`), `MemoryRetriever` (recency + importance + relevance, min-max normalized, configurable weights, top 5), memories in the decision and conversation prompts. `IMemoryStore` with an in-memory store (max 500 per agent) until the `Memories` table exists.
- A: Relationships (`a/relationships`): `RelationshipService` (affinity -100…100 from conversation tone × 2 per side; agent↔agent `Transfer` raises the receiver's affinity and books a debt in `Balance`, paying back evens it out), labels ("a good friend who owes you 20.00 EUR"), in the decision prompt (nearby people + "People you know (not here)") and the conversation prompt, and in `InterestingMomentDetector` (`DebtorNearby`; a friend nearby triggers `SocialChance` at Social < 70). `IRelationshipStore` with an in-memory store until the `Relationships` table exists.
- A: `IAgentMindQuery` + `MemoryView` / `RelationshipView` in `AgentTown.Contracts.Mind` (new, additive); `AgentMindQuery` implementation in Agents over the stores.
- Tests: 230 passing (Contracts 29, Llm 60, Agents 141).

## In progress / next (A)
- Day 17: daily reflections (needs no B work); restore state after restart needs Persistence.
- Day 19: reactions to `town.events.applied` (can be done with the in-memory loop and tests before B's NATS is ready).

## Blockers
- A needs B's `INatsBus` (Messaging) to hook `AgentLoop` to `town.tick` / `town.actions.result.*`.
- A needs B's `Persistence` (`TownDbContext`) for registry DB loading and the `Memories` / `Relationships` tables.
- `IAgentMindQuery` is implemented inside the Agents service (in-memory stores). The Api runs in another process, so for `GET /api/agents/{id}/memories|relationships` B needs either the DB tables (Api implements the interface over them) or an Agents endpoint/NATS request. Decide together.

## Known issues
- No CI: `.github/` (workflows, Dependabot, CODEOWNERS) was never committed, so PRs run no checks. Repo infrastructure is B's area.
- Transaction memories assume World uses agent ids as account ids for agent wallets (`AgentLoop.OnTransaction`). Confirm with B.
- `ConversationService` publishes all lines when the conversation ends (not line by line), so the UI gets them together; B's bubbles can stagger them with `LineIndex`.
- `a/agents-seed` is not merged into the `a/llm` → `a/agent-loop` line yet; merge it when opening PRs.
- Branches `a/contracts`, `a/agent-brain`, `a/llm` are stacked and not yet merged into `main` via PRs.
