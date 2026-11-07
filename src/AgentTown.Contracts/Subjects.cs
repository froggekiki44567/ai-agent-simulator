namespace AgentTown.Contracts;

/// <summary>
/// NATS subjects used by AgentTown. See PROJECT_PLAN.md section 2.4.
/// All subjects live in the JetStream stream <see cref="StreamName"/>.
/// </summary>
public static class Subjects
{
    public const string StreamName = "TOWN";
    public const string StreamFilter = "town.>";

    /// <summary>World → Agents, Api. Payload: <see cref="Events.TickOccurred"/>.</summary>
    public const string Tick = "town.tick";

    /// <summary>Agents → World. Payload: <see cref="Events.ActionRequested"/>.</summary>
    public const string ActionsRequested = "town.actions.requested";

    /// <summary>Wildcard for all action results. Payload: <see cref="Events.ActionResult"/>.</summary>
    public const string ActionResultAll = "town.actions.result.*";

    /// <summary>World → Agents, Api. Payload: <see cref="Events.AgentMoved"/>.</summary>
    public const string AgentMoved = "town.agents.moved";

    /// <summary>World → Api, Agents. Payload: <see cref="Events.TransactionCompleted"/>.</summary>
    public const string TransactionCompleted = "town.transactions.completed";

    /// <summary>Agents → Api, Agents. Payload: <see cref="Events.ConversationMessage"/>.</summary>
    public const string ConversationMessage = "town.conversations.message";

    /// <summary>Agents → Api. Payload: <see cref="Events.AgentThought"/>.</summary>
    public const string AgentThought = "town.agents.thought";

    /// <summary>Api → World. Payload: <see cref="Events.WorldEventInjected"/>.</summary>
    public const string EventsInjected = "town.events.injected";

    /// <summary>World → Agents, Api. Payload: <see cref="Events.WorldEventApplied"/>.</summary>
    public const string EventsApplied = "town.events.applied";

    /// <summary>World (every 10 ticks) → Api. Payload: <see cref="Events.WorldSnapshot"/>.</summary>
    public const string WorldSnapshot = "town.world.snapshot";

    /// <summary>Api → World. Payload: <see cref="Events.SimulationControl"/>.</summary>
    public const string Control = "town.control";

    /// <summary>World → Agents, Api. Result of one agent's action. Payload: <see cref="Events.ActionResult"/>.</summary>
    public static string ActionResult(Guid agentId) => $"town.actions.result.{agentId:N}";
}
