# ADR-003: The LLM is used only for interesting moments

- **Status:** Proposed
- **Date:** 2026-11-07
- **Deciders:** A, B

## Context
The demo needs 30–50 agents. A local model (`qwen2.5:7b-instruct` via Ollama) on one consumer GPU
takes roughly 1–5 s per decision and handles only a few calls in parallel. One tick lasts 2 s of real
time. Calling the LLM for every agent on every tick (50 calls every 2 s) is impossible: the queue
would grow without limit and the town would freeze. Most behavior is routine anyway (sleeping at
night, going to work, eating when hungry) and doesn't need a language model.

## Decision
Routine behavior is handled by a deterministic **`RuleBasedPlanner`**. An
**`InterestingMomentDetector`** decides when an agent gets an LLM decision instead: another agent is
nearby and Social is low, money is low, a new situation comes up, or a world event affects the agent.
LLM calls go through an **`LlmScheduler`** with limited concurrency (`Llm:MaxConcurrency`), a
per-tick budget (`Llm:MaxCallsPerTick`) and a 15 s timeout. If the budget is used up, the JSON is
invalid, or the call times out, the rule-based action is used instead, and the `llm.fallbacks`
metric counts it.

## Alternatives considered
- **LLM for every decision:** most "intelligent", but it doesn't scale beyond a handful of agents on
  local hardware.
- **Cloud LLM API:** fast and parallel, but it costs money, needs internet during the defense, and
  sends data off the machine.
- **Rules only:** cheap and fast, but agents wouldn't talk or react to events in interesting ways,
  which is the point of the project.

## Consequences
- 50+ agents can run on one GPU, and the town keeps moving even when Ollama is slow or offline.
- Decisions are labelled with `DecisionSource` (Rules or Llm), so the UI and the metrics can show the
  mix.
- The detector thresholds and the budget need tuning per machine (Day 13 and Day 18 in the plan).
- Two decision paths must produce the same `ActionRequested` shape, so both are tested against the
  same contract.
