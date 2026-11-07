namespace AgentTown.Contracts.Model;

public sealed record AgentSnapshot(
    Guid AgentId,
    string Name,
    int X,
    int Y,
    decimal Money,
    double Hunger,
    double Energy,
    double Social,
    AgentStatus Status,
    string? LocationId);

public sealed record BuildingSnapshot(
    string BuildingId,
    BuildingType Type,
    string Name,
    int X,
    int Y,
    int OpenHour,
    int CloseHour,
    bool IsOpen);
