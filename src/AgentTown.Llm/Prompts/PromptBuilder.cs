using System.Globalization;
using System.Text;
using AgentTown.Contracts.Model;
using AgentTown.Llm.Client;

namespace AgentTown.Llm.Prompts;

/// <summary>
/// Builds the decision prompt: a fixed system prompt (rules, actions, JSON format)
/// plus a user prompt describing the agent's current situation.
/// </summary>
public sealed class PromptBuilder
{
    /// <summary>Actions the LLM may choose. Idle is always allowed.</summary>
    public static IReadOnlyList<ActionType> AllowedActions { get; } = Enum.GetValues<ActionType>();

    public static string SystemPrompt { get; } = $$"""
        You are the mind of a person living in a small town simulation called AgentTown.
        You decide the person's NEXT single action. Stay in character: follow their personality, goal and needs.

        Rules of the town:
        - Time moves in steps of 10 minutes. You choose one action per step.
        - Needs go from 0 (terrible) to 100 (great). Low Hunger means hungry, low Energy means tired, low Social means lonely.
        - You can only act on places and people listed in the situation. Use their exact ids.
        - Shops only sell while they are open. You cannot spend money you do not have.
        - The world may reject an impossible action. Then you will be asked again.

        Allowed actions:
        - Idle: do nothing for now.
        - Move: walk to a building. target = building id.
        - Work: work at your workplace during your shift. target = workplace id.
        - Buy: buy an item. target = shop id, item = item id, quantity >= 1.
        - Sell: sell an item you carry. target = shop id, item, quantity.
        - Eat: eat an item you carry. item, quantity = 1.
        - Sleep: sleep at home. target = home id.
        - Talk: talk to a nearby person. target = person id, say = what you say (one or two sentences).
        - Deposit: put money into your bank account. target = bank id, amount > 0.
        - Withdraw: take money out of your bank account. target = bank id, amount > 0.
        - Transfer: give money to a person. target = person id, amount > 0.
        - TakeLoan: borrow money from the bank. target = bank id, amount > 0.

        Answer with ONE JSON object and nothing else:
        {"action":"<one action>","target":"<id or null>","item":"<item id or null>","quantity":0,"amount":0,"say":"<text or null>","thought":"<one short sentence: why, in first person>","importance":<1-10, how memorable this moment is>}
        """;

    public LlmPrompt Build(AgentPromptContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var text = new StringBuilder();
        var money = context.Money.ToString("0.00", CultureInfo.InvariantCulture);

        text.AppendLine($"You are {context.Name}, {context.Age} years old.");
        text.AppendLine($"Personality: {string.Join(", ", context.Traits)}.");
        text.AppendLine($"Goal: {context.Goal}");
        text.AppendLine($"Speaking style: {context.SpeakingStyle}");
        text.AppendLine();

        text.AppendLine($"Now: day {context.Day}, {context.Hour:00}:{context.Minute:00}.");
        text.AppendLine($"Location: {context.LocationId ?? "outside on the street"}. Home: {context.HomeId}.");
        text.AppendLine(context.JobTitle is null
            ? "Job: none (unemployed)."
            : $"Job: {context.JobTitle} at {context.WorkplaceId}.");
        text.AppendLine($"Money: {money} EUR.");
        text.AppendLine($"Needs: Hunger {Need(context.Hunger)}, Energy {Need(context.Energy)}, Social {Need(context.Social)}.");
        text.AppendLine(context.Inventory.Count == 0
            ? "Carrying: nothing."
            : "Carrying: " + string.Join(", ", context.Inventory.Select(i => $"{i.Key} x{i.Value}")) + ".");
        text.AppendLine();

        text.AppendLine("Places:");
        if (context.NearbyBuildings.Count == 0)
            text.AppendLine("- (none known)");
        foreach (var building in context.NearbyBuildings)
            text.AppendLine($"- {building.Id} ({building.Type}, {(building.IsOpen ? "open" : "closed")})");

        text.AppendLine("People nearby:");
        if (context.NearbyAgents.Count == 0)
            text.AppendLine("- nobody");
        foreach (var agent in context.NearbyAgents)
            text.AppendLine($"- {agent.Name} (id {agent.Id}){(agent.Relationship is null ? "" : $", {agent.Relationship}")}");

        if (context.Memories.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("What you remember:");
            foreach (var memory in context.Memories)
                text.AppendLine($"- {memory}");
        }

        if (context.RecentEvents.Count > 0)
        {
            text.AppendLine();
            text.AppendLine("What just happened in town:");
            foreach (var townEvent in context.RecentEvents)
                text.AppendLine($"- {townEvent}");
        }

        if (context.Situation is not null)
        {
            text.AppendLine();
            text.AppendLine($"Right now: {context.Situation}");
        }

        text.AppendLine();
        text.Append("What do you do next? Answer with the JSON object only.");

        return new LlmPrompt(SystemPrompt, text.ToString());
    }

    private static string Need(double value) =>
        $"{Math.Round(value).ToString(CultureInfo.InvariantCulture)}/100";
}
