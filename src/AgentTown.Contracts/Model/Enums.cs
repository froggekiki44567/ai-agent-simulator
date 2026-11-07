namespace AgentTown.Contracts.Model;

public enum ActionType
{
    Idle,
    Move,
    Work,
    Buy,
    Sell,
    Eat,
    Sleep,
    Talk,
    Deposit,
    Withdraw,
    Transfer,
    TakeLoan,
}

/// <summary>Who made a decision: the rule-based planner or the LLM.</summary>
public enum DecisionSource
{
    Rules,
    Llm,
}

public enum AgentStatus
{
    Active,
    Sleeping,
    Bankrupt,
}

public enum BuildingType
{
    Home,
    Bakery,
    Shop,
    Cafe,
    Bank,
    Factory,
    TownHall,
    Park,
}

public enum SimulationCommand
{
    Pause,
    Resume,
    SetSpeed,
}
