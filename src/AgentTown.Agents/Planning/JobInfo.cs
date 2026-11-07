namespace AgentTown.Agents.Planning;

/// <summary>What the planner needs to know about an agent's job.</summary>
/// <param name="ShiftStartHour">0–23. A shift may cross midnight (e.g. 22 → 6).</param>
/// <param name="ShiftEndHour">0–23, exclusive.</param>
public sealed record JobInfo(string JobId, string BuildingId, int ShiftStartHour, int ShiftEndHour)
{
    public bool IsOnShift(int hour) =>
        ShiftStartHour <= ShiftEndHour
            ? hour >= ShiftStartHour && hour < ShiftEndHour
            : hour >= ShiftStartHour || hour < ShiftEndHour;
}
