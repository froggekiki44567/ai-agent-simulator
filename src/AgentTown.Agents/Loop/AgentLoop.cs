using System.Collections.Concurrent;
using AgentTown.Agents.Conversations;
using AgentTown.Agents.Planning;
using AgentTown.Agents.State;
using AgentTown.Agents.Telemetry;
using AgentTown.Contracts.Events;
using AgentTown.Contracts.Model;
using AgentTown.Llm.Prompts;
using AgentTown.Llm.Scheduling;
using Microsoft.Extensions.Logging;

namespace AgentTown.Agents.Loop;

public sealed class AgentLoopOptions
{
    public const string SectionName = "Agents:Loop";

    /// <summary>
    /// If World hasn't answered an action after this many ticks, give up on it and decide again.
    /// Long enough for a Move across the map (1 cell per tick).
    /// </summary>
    public int PendingTimeoutTicks { get; set; } = 80;
}

/// <summary>
/// The heart of the Agents service. On every <c>TickOccurred</c>: update needs, and for every agent that
/// is not waiting for World, choose an action and send it as <c>ActionRequested</c>.
/// On every <c>ActionResult</c>: update the agent's local state.
/// An agent has at most one active action at a time (ADR-002).
/// </summary>
public sealed class AgentLoop(
    AgentRegistry registry,
    TownDirectory town,
    NeedsSystem needs,
    AgentDecisionService decisions,
    ConversationService conversations,
    LlmScheduler scheduler,
    IAgentEventPublisher publisher,
    AgentTelemetry telemetry,
    AgentLoopOptions options,
    ILogger<AgentLoop> logger)
{
    // Agents whose decision is still running (an LLM call can take several ticks).
    private readonly ConcurrentDictionary<Guid, Task> _deciding = new();

    // Agents in a running conversation (both participants), keyed by agent id.
    private readonly ConcurrentDictionary<Guid, Task> _talking = new();
    private TickOccurred? _lastTick;

    public int DecidingCount => _deciding.Count;

    public bool IsTalking(Guid agentId) => _talking.ContainsKey(agentId);

    /// <summary>Raised after each finished conversation (memories and relationships hook in here).</summary>
    public event Action<ConversationOutcome>? ConversationFinished;

    /// <summary>
    /// Handles one tick. Rule-based decisions finish immediately; LLM decisions keep running in the
    /// background so a slow model never blocks the town. Use <see cref="WaitForDecisionsAsync"/> to wait for them.
    /// </summary>
    public async Task OnTickAsync(TickOccurred tick, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tick);
        scheduler.OnTick(tick.Tick);
        _lastTick = tick;
        var buildings = town.DescribeBuildings(tick.Hour);

        foreach (var agent in registry.All)
        {
            needs.ApplyTick(agent);

            if (agent.PendingAction is { } pending)
            {
                if (tick.Tick - pending.RequestedAtTick < options.PendingTimeoutTicks)
                    continue;

                logger.LogWarning("{Agent}: no result for {Action} after {Ticks} ticks, deciding again.",
                    agent.Name, pending.Type, tick.Tick - pending.RequestedAtTick);
                agent.PendingAction = null;
                agent.LastActionFailed = true;
            }

            if (_deciding.ContainsKey(agent.Id) || _talking.ContainsKey(agent.Id))
                continue;

            var input = new DecisionInput(
                tick.Tick, tick.Day, tick.Hour, tick.Minute,
                town.FindJob(agent.JobId),
                registry.NearbyOf(agent).Select(a => new NearbyAgent(a.Id, a.Name)).ToArray(),
                buildings);

            var work = DecideAndSendAsync(agent, input, cancellationToken);
            if (work.IsCompleted)
            {
                await work;
                continue;
            }

            // Still waiting for the LLM: track it, and remove it from the list as part of the same task,
            // so WaitForDecisionsAsync never sees a finished decision as still running.
            var tracked = new TaskCompletionSource();
            _deciding[agent.Id] = tracked.Task;
            _ = FinishLaterAsync(agent.Id, work, tracked);
        }
    }

    private async Task FinishLaterAsync(Guid agentId, Task work, TaskCompletionSource tracked)
    {
        try
        {
            await work;
        }
        finally
        {
            _deciding.TryRemove(agentId, out _);
            tracked.TrySetResult();
        }
    }

    /// <summary>Waits until all running (LLM) decisions are done. Mostly for tests and shutdown.</summary>
    public async Task WaitForDecisionsAsync()
    {
        // A finished decision can start a conversation, so loop until nothing is running.
        while (!_deciding.IsEmpty || !_talking.IsEmpty)
            await Task.WhenAll(_deciding.Values.Concat(_talking.Values));
    }

    /// <summary>Applies World's answer to the agent's local state.</summary>
    public void OnActionResult(ActionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var agent = registry.Find(result.AgentId);
        if (agent is null)
        {
            logger.LogWarning("Result for unknown agent {AgentId}.", result.AgentId);
            return;
        }

        if (agent.PendingAction is not { } pending || pending.RequestId != result.RequestId)
        {
            logger.LogDebug("{Agent}: ignoring stale result {RequestId}.", agent.Name, result.RequestId);
            return;
        }

        agent.PendingAction = null;
        agent.Apply(result.Delta);

        if (result.Success)
        {
            agent.LastActionFailed = false;
            needs.ApplyActionCompleted(agent, pending.Type);
            agent.ApplyInventoryChange(pending.Type, pending.ItemId, pending.Quantity);
            if (pending.Type == ActionType.Talk)
                StartConversation(agent, pending);
        }
        else
        {
            agent.LastActionFailed = true;
            telemetry.RecordActionFailure(pending.Type, result.FailureReason);
            logger.LogInformation("{Agent}: {Action} failed: {Reason}", agent.Name, pending.Type, result.FailureReason);
        }
    }

    private void StartConversation(AgentState initiator, PendingAction talk)
    {
        if (!Guid.TryParse(talk.TargetId, out var partnerId) || registry.Find(partnerId) is not { } partner || partner.Id == initiator.Id)
        {
            logger.LogWarning("{Agent}: Talk target {Target} is not a known agent.", initiator.Name, talk.TargetId);
            return;
        }

        if (_talking.ContainsKey(initiator.Id) || _talking.ContainsKey(partner.Id))
        {
            logger.LogDebug("{Agent} or {Partner} is already in a conversation.", initiator.Name, partner.Name);
            return;
        }

        var tick = _lastTick ?? new TickOccurred(talk.RequestedAtTick, 1, 0, 0);
        var scene = new ConversationScene(tick.Tick, tick.Day, tick.Hour, tick.Minute, initiator.LocationId);

        var done = new TaskCompletionSource();
        _talking[initiator.Id] = done.Task;
        _talking[partner.Id] = done.Task;
        _ = RunConversationAsync(initiator, partner, talk.Say, scene, done);
    }

    private async Task RunConversationAsync(AgentState initiator, AgentState partner, string? openingLine, ConversationScene scene, TaskCompletionSource done)
    {
        try
        {
            var outcome = await conversations.RunAsync(initiator, partner, openingLine, scene);
            if (outcome.Lines.Count > 1)
                needs.ApplyActionCompleted(partner, ActionType.Talk);
            ConversationFinished?.Invoke(outcome);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Conversation {Initiator} → {Partner} failed.", initiator.Name, partner.Name);
        }
        finally
        {
            _talking.TryRemove(initiator.Id, out _);
            _talking.TryRemove(partner.Id, out _);
            done.TrySetResult();
        }
    }

    private async Task DecideAndSendAsync(AgentState agent, DecisionInput input, CancellationToken cancellationToken)
    {
        using var activity = AgentTelemetry.StartDecision(agent.Id, agent.Name, input.Tick);
        try
        {
            var decision = await decisions.DecideAsync(agent, input, cancellationToken);
            telemetry.RecordDecision(activity, decision);

            if (decision.Source == DecisionSource.Llm)
                await publisher.PublishThoughtAsync(decision.ToThought(), input.Tick, cancellationToken);

            // Idle needs nothing from World, so it isn't sent.
            if (decision.Request.Type == ActionType.Idle)
                return;

            var request = decision.Request;
            agent.PendingAction = new PendingAction(request.RequestId, request.Type, request.TargetId, input.Tick, request.ItemId, request.Quantity, request.Say);
            if (request.Type is not ActionType.Sleep)
                agent.Status = agent.Status == AgentStatus.Sleeping ? AgentStatus.Active : agent.Status;
            await publisher.PublishActionAsync(request, input.Tick, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // One agent's problem must never stop the whole town.
            agent.PendingAction = null;
            logger.LogError(ex, "{Agent}: decision failed.", agent.Name);
        }
    }
}
