namespace AgentTown.Contracts;

/// <summary>
/// Common wrapper for every message published on NATS.
/// </summary>
/// <param name="MessageId">Unique id of this message (used for de-duplication).</param>
/// <param name="CorrelationId">Id that links related messages (e.g. a request and its result).</param>
/// <param name="Tick">Simulation tick at which the message was produced.</param>
/// <param name="OccurredAt">Real time (UTC) at which the message was produced.</param>
/// <param name="Payload">The event record.</param>
public sealed record Envelope<T>(
    Guid MessageId,
    Guid CorrelationId,
    long Tick,
    DateTimeOffset OccurredAt,
    T Payload)
{
    /// <summary>Creates a new envelope with a fresh <see cref="MessageId"/>.</summary>
    public static Envelope<T> Create(T payload, long tick, Guid? correlationId = null, TimeProvider? clock = null)
    {
        var messageId = Guid.NewGuid();
        return new Envelope<T>(
            messageId,
            correlationId ?? messageId,
            tick,
            (clock ?? TimeProvider.System).GetUtcNow(),
            payload);
    }
}
