# Ervis – Handoff

> Updated at the end of each session.

## Last session: 2026-11-07

### What I did
- Day 2: `AgentTown.Contracts` + serialization tests, ADR-002, ADR-003 (`a/contracts`).
- Day 3: 20 seed agents with loader and tests (`a/agents-seed`).
- Days 4–7: `AgentState`, `NeedsSystem`, `RuleBasedPlanner`, `AgentLoop`, `AgentRegistry` (in-memory), telemetry (`a/agent-brain`, `a/agent-loop`).
- Days 9–11: `AgentTown.Llm` (Ollama + fake client, health check, prompt builder, decision parser, scheduler, metrics), `InterestingMomentDetector`, `AgentDecisionService` (`a/llm`).
- Day 16: relationships (`RelationshipService`, in-memory store, prompts, detector) and `IAgentMindQuery` (`a/relationships`, stacked on `a/memory`).

### Where I stopped
Day 16 relationships on `a/relationships` (stacked on `a/memory`). Build clean, 230 tests green. 37 of 72 A tasks done (50% goal reached).

### What comes next
- Day 17: daily reflection (LLM summary at night, importance 7, as a `Reflection` memory).
- Day 19: react to `town.events.applied` (memory importance 8–10, forced LLM decision, "What just happened in town").

### What I need from colleague
- UI: conversation lines arrive together at the end of a conversation; show them one after another by `LineIndex`.
- `AgentTown.Messaging` with `INatsBus` so the loop can subscribe to `town.tick` and `town.actions.result.*`.
- `AgentTown.Persistence` (`TownDbContext`) and the `Memories` table with `vector(768)`; columns match `MemoryRecord` (Id, AgentId, Tick, Kind, Text, Importance, Embedding, LastAccessedTick).
- Confirm: are agent wallet account ids the agent ids in `TransactionCompleted`? Relationships also rely on it (agent↔agent `Transfer`).
- `Relationships` table: AgentId, OtherAgentId, Affinity (-100…100), LastInteractionTick, plus `Balance` (decimal, positive = the other owes this agent). Matches `RelationshipRecord`.
- Api agent panel: use `IAgentMindQuery` from `AgentTown.Contracts.Mind` (`GetMemoriesAsync`, `GetRelationshipsAsync`). It is implemented in the Agents process today; we need to agree how the Api reaches it (DB tables or a request to Agents).
- Review the stacked PRs: `a/contracts` → `a/agent-brain` → `a/llm` → `a/agent-loop`, plus `a/agents-seed`.
