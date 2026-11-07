using AgentTown.Contracts.Model;

namespace AgentTown.Contracts.Events;

/// <summary>
/// Agents → World: an agent <i>asks</i> to do something. World validates it and replies with
/// <see cref="ActionResult"/> on <see cref="Subjects.ActionResult(Guid)"/> (ADR-002).
/// </summary>
/// <param name="RequestId">Id echoed back in <see cref="ActionResult.RequestId"/>.</param>
/// <param name="TargetId">Building id or other agent id, depending on <paramref name="Type"/>.</param>
/// <param name="Say">Spoken text for <see cref="ActionType.Talk"/>.</param>
/// <param name="Reason">Short explanation (logged and shown in the UI).</param>
public sealed record ActionRequested(
    Guid RequestId,
    Guid AgentId,
    ActionType Type,
    string? TargetId,
    string? ItemId,
    int Quantity,
    decimal Amount,
    string? Say,
    string Reason,
    DecisionSource Source);

/// <summary>World → Agents, Api: outcome of an <see cref="ActionRequested"/>.</summary>
public sealed record ActionResult(
    Guid AgentId,
    Guid RequestId,
    bool Success,
    string? FailureReason,
    AgentStateDelta Delta);

/// <summary>World → Api: an agent moved one cell.</summary>
public sealed record AgentMoved(Guid AgentId, int FromX, int FromY, int ToX, int ToY);

/// <summary>World → Aml, Api, Agents: any movement of money.</summary>
/// <param name="Kind">See <see cref="TransactionKinds"/>.</param>
public sealed record TransactionCompleted(
    Guid TxId,
    Guid FromAccount,
    Guid ToAccount,
    decimal Amount,
    string Kind,
    string? Memo,
    long Tick);
