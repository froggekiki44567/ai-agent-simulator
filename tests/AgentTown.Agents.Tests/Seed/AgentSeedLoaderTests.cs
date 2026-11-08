using System.Text;
using AgentTown.Agents.Seed;
using FluentAssertions;
using Xunit;

namespace AgentTown.Agents.Tests.Seed;

public class AgentSeedLoaderTests
{
    private static readonly string SeedPath = Path.Combine(AppContext.BaseDirectory, "seed", "agents.json");

    [Fact]
    public async Task Real_seed_file_has_20_valid_agents()
    {
        var agents = await AgentSeedLoader.LoadAsync(SeedPath);

        agents.Should().HaveCount(20);
        agents.Select(a => a.Id).Should().OnlyHaveUniqueItems();
        agents.Should().OnlyContain(a => a.Traits.Count >= 3 && a.Traits.Count <= 5);
    }

    [Fact]
    public async Task Real_seed_file_uses_8_homes_and_has_unemployed_agents()
    {
        var agents = await AgentSeedLoader.LoadAsync(SeedPath);

        agents.Select(a => a.HomeId).Distinct().Should().HaveCount(8);
        agents.Should().Contain(a => a.JobId == null, "the planner must handle agents with no job");
    }

    [Fact]
    public async Task Loads_all_fields()
    {
        const string json = """
            [{ "id": "a0000000-0000-0000-0000-000000000001", "name": "Jonas", "age": 45,
               "traits": ["kind", "proud", "calm"], "goal": "Bake", "speakingStyle": "Short",
               "jobId": "job-baker", "homeId": "home-1", "money": 12.5 }]
            """;

        var agents = await LoadFromString(json);

        agents.Should().ContainSingle().Which.Should().BeEquivalentTo(new AgentSeed(
            Guid.Parse("a0000000-0000-0000-0000-000000000001"), "Jonas", 45,
            ["kind", "proud", "calm"], "Bake", "Short", "job-baker", "home-1", 12.5m));
    }

    [Theory]
    [InlineData("\"traits\": [\"kind\"]", "traits")]
    [InlineData("\"age\": 5", "age")]
    [InlineData("\"money\": -1", "money")]
    [InlineData("\"homeId\": \"\"", "homeId")]
    public async Task Rejects_invalid_agent(string override_, string expectedError)
    {
        var fields = new Dictionary<string, string>
        {
            ["id"] = "\"id\": \"a0000000-0000-0000-0000-000000000001\"",
            ["name"] = "\"name\": \"Jonas\"",
            ["age"] = "\"age\": 45",
            ["traits"] = "\"traits\": [\"kind\", \"proud\", \"calm\"]",
            ["goal"] = "\"goal\": \"Bake\"",
            ["homeId"] = "\"homeId\": \"home-1\"",
            ["money"] = "\"money\": 10",
        };
        fields[expectedError] = override_;
        var json = "[{" + string.Join(",", fields.Values) + "}]";

        var act = () => LoadFromString(json);

        (await act.Should().ThrowAsync<InvalidDataException>()).WithMessage($"*{expectedError}*");
    }

    [Fact]
    public async Task Rejects_duplicate_ids()
    {
        const string agent = """{ "id": "a0000000-0000-0000-0000-000000000001", "name": "NAME", "age": 30, "traits": ["a","b","c"], "goal": "g", "speakingStyle": "s", "homeId": "home-1", "money": 1 }""";
        var json = "[" + agent.Replace("NAME", "A") + "," + agent.Replace("NAME", "B") + "]";

        var act = () => LoadFromString(json);

        (await act.Should().ThrowAsync<InvalidDataException>()).WithMessage("*Duplicate id*");
    }

    private static Task<IReadOnlyList<AgentSeed>> LoadFromString(string json) =>
        AgentSeedLoader.LoadAsync(new MemoryStream(Encoding.UTF8.GetBytes(json)));
}
