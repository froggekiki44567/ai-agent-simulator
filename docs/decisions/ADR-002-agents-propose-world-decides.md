# ADR-002: Agents propose, the World decides

- **Status:** Proposed
- **Date:** 2026-11-07
- **Deciders:** A, B

## Context
Agent decisions come from two sources: a rule-based planner and an LLM. The LLM can hallucinate:
it may pick an action that doesn't exist, target a closed shop, spend money the agent doesn't have,
or walk through a wall. Several services (Agents, Api, Aml) also need a consistent view of the town.
If more than one service could change the world state, the services would drift apart and bugs would be
hard to trace.

## Decision
The **World service is the single owner of the world state** (positions, money, inventory, buildings,
time). The Agents service never changes the world directly. It only publishes an `ActionRequested`
on `town.actions.requested`. World validates the request against the rules (money, opening hours,
stock, reachable path, ...), executes it if it's valid, and always answers with an `ActionResult` on
`town.actions.result.{agentId}`. Side effects are published as their own events (`AgentMoved`,
`TransactionCompleted`). The Agents service keeps only a local copy of an agent's state, updated
from `ActionResult.Delta`.

## Alternatives considered
- **Agents change the state themselves (shared database):** simpler at first, but every LLM mistake goes
  straight into the world, and two services writing the same rows need locking.
- **One monolithic process:** no messaging overhead, but this is what similar projects (Generative
  Agents, AI Town) already do. It also loses independent restarts and makes it harder to add new
  subscribers such as Aml.
- **Validating in Agents before sending:** the rules would be duplicated in two places, and the
  check would still be based on a possibly stale local copy.

## Consequences
- An invalid LLM decision can never corrupt the world. It ends as `Success=false` with a
  `FailureReason`, and the agent tries something else on the next tick.
- Every action takes a round trip over NATS, so an agent acts at most once per tick. That is fine
  for 1 tick = 10 simulation minutes.
- The agent loop must handle "waiting for a result" and failed results.
- All rules live in one place (World), which is easy to unit test.
