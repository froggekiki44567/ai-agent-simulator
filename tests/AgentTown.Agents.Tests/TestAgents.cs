using AgentTown.Agents.State;

namespace AgentTown.Agents.Tests;

internal static class TestAgents
{
    public static AgentState Create(
        string? location = "home-1",
        double hunger = 80,
        double energy = 80,
        double social = 80,
        decimal money = 100,
        string? jobId = "job-baker") =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Jonas",
            Personality = new AgentPersonality(["kind", "proud", "calm"], "Bake bread", "Short"),
            JobId = jobId,
            HomeId = "home-1",
            LocationId = location,
            Hunger = hunger,
            Energy = energy,
            Social = social,
            Money = money,
        };
}
