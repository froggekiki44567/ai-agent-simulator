using AgentTown.Llm.Prompts;
using FluentAssertions;

namespace AgentTown.Llm.Tests;

public class PromptBuilderTests
{
    private static AgentPromptContext Context(Action<AgentPromptContextBuilder>? change = null)
    {
        var builder = new AgentPromptContextBuilder();
        change?.Invoke(builder);
        return builder.Build();
    }

    [Fact]
    public void System_prompt_lists_every_action_and_the_json_format()
    {
        foreach (var action in PromptBuilder.AllowedActions)
            PromptBuilder.SystemPrompt.Should().Contain($"- {action}", $"{action} must be explained to the model");

        PromptBuilder.SystemPrompt.Should().Contain("\"action\"").And.Contain("\"thought\"").And.Contain("\"importance\"");
    }

    [Fact]
    public void User_prompt_describes_the_agent_and_situation()
    {
        var prompt = new PromptBuilder().Build(Context());

        prompt.User.Should()
            .Contain("You are Jonas, 45 years old.")
            .And.Contain("hard-working, proud")
            .And.Contain("day 2, 07:30")
            .And.Contain("Money: 12.50 EUR")
            .And.Contain("Hunger 25/100")
            .And.Contain("bakery-1 (Bakery, open)")
            .And.Contain("Ona (id 22222222-2222-2222-2222-222222222222), good friend")
            .And.Contain("bread x2")
            .And.Contain("Right now: You are hungry.");
    }

    [Fact]
    public void Unemployed_alone_and_empty_handed_is_described()
    {
        var prompt = new PromptBuilder().Build(Context(b => { b.JobTitle = null; b.Nearby = []; b.Inventory = new Dictionary<string, int>(); }));

        prompt.User.Should().Contain("unemployed").And.Contain("- nobody").And.Contain("Carrying: nothing.");
    }

    [Fact]
    public void Memories_and_events_appear_only_when_present()
    {
        new PromptBuilder().Build(Context()).User.Should().NotContain("What you remember").And.NotContain("What just happened");

        var prompt = new PromptBuilder().Build(Context(b =>
        {
            b.Memories = ["Ona promised to meet me at the cafe."];
            b.Events = ["The bank collapsed."];
        }));

        prompt.User.Should().Contain("What you remember:\n- Ona promised").And.Contain("What just happened in town:\n- The bank collapsed.");
    }

    [Fact]
    public void Known_people_appear_only_when_present()
    {
        new PromptBuilder().Build(Context()).User.Should().NotContain("People you know");

        var prompt = new PromptBuilder().Build(Context(b => b.KnownPeople = ["Petras: an acquaintance who owes you 20.00 EUR (last met day 1, 12:00)"]));

        prompt.User.Should().Contain("People you know (not here):\n- Petras: an acquaintance who owes you 20.00 EUR");
    }

    [Fact]
    public void Numbers_do_not_depend_on_machine_culture()
    {
        var previous = Thread.CurrentThread.CurrentCulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("lt-LT");
            new PromptBuilder().Build(Context()).User.Should().Contain("12.50 EUR");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = previous;
        }
    }

    private sealed class AgentPromptContextBuilder
    {
        public string? JobTitle { get; set; } = "Baker";
        public IReadOnlyList<NearbyAgent> Nearby { get; set; } = [new(Guid.Parse("22222222-2222-2222-2222-222222222222"), "Ona", "good friend")];
        public IReadOnlyDictionary<string, int> Inventory { get; set; } = new Dictionary<string, int> { ["bread"] = 2 };
        public IReadOnlyList<string> Memories { get; set; } = [];
        public IReadOnlyList<string> Events { get; set; } = [];
        public IReadOnlyList<string> KnownPeople { get; set; } = [];

        public AgentPromptContext Build() => new()
        {
            Name = "Jonas",
            Age = 45,
            Traits = ["hard-working", "proud"],
            Goal = "Keep the bakery the best in town.",
            SpeakingStyle = "Short and warm.",
            Day = 2,
            Hour = 7,
            Minute = 30,
            Money = 12.5m,
            Hunger = 25,
            Energy = 70,
            Social = 40,
            LocationId = "home-1",
            JobTitle = JobTitle,
            WorkplaceId = JobTitle is null ? null : "bakery-1",
            HomeId = "home-1",
            Inventory = Inventory,
            NearbyAgents = Nearby,
            NearbyBuildings = [new("bakery-1", "Bakery", true), new("bank-1", "Bank", false)],
            Situation = "You are hungry.",
            Memories = Memories,
            RecentEvents = Events,
            KnownPeople = KnownPeople,
        };
    }
}
