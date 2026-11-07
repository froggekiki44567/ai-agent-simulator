using AgentTown.Contracts.Model;

namespace AgentTown.Agents.Memory;

/// <summary>
/// Importance heuristic (1–10) for things that happen without an LLM. 0 means "not worth remembering".
/// LLM decisions use the model's own <c>importance</c> field instead.
/// </summary>
public static class MemoryImportance
{
    public const int Skip = 0;
    public const int Purchase = 2;
    public const int Conversation = 4;
    public const int ConversationWithAgreement = 5;
    public const int Bankruptcy = 9;
    public const int FailedAction = 3;

    /// <summary>Importance of a finished action. Routine actions (Move, Work, Sleep, Idle, Talk) are skipped:
    /// wages come as transactions and conversations are remembered separately.</summary>
    public static int ForAction(ActionType action, bool success)
    {
        if (!success)
            return action is ActionType.Idle ? Skip : action is ActionType.Move ? 2 : FailedAction;

        return action switch
        {
            ActionType.Buy => Purchase,
            ActionType.Sell => 2,
            ActionType.Eat => 1,
            ActionType.Deposit or ActionType.Withdraw => 2,
            ActionType.Transfer => 3,
            ActionType.TakeLoan => 5,
            _ => Skip,
        };
    }

    /// <summary>
    /// Importance of a transaction for one agent. Only money the agent didn't cause itself is remembered here
    /// (its own Buy, Deposit, ... are already remembered as action results).
    /// </summary>
    public static int ForTransaction(string kind, bool received) => (kind, received) switch
    {
        (TransactionKinds.Wage, true) => 2,
        (TransactionKinds.Sale, true) => 2,
        (TransactionKinds.Transfer, true) => 3,
        (TransactionKinds.LoanInterest, false) => 2,
        _ => Skip,
    };
}
