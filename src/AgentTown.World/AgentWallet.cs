
namespace AgentTown.World;

public class AgentWallet
{
    public Guid AgentId { get; }
    public decimal Balance { get; private set; }

    public AgentWallet(Guid agentId, decimal startingBalance = 0)
    {
        if (startingBalance < 0)
            throw new ArgumentOutOfRangeException(nameof(startingBalance));

        AgentId = agentId;
        Balance = startingBalance;
    }

    // Pridedame pinigų
    public void AddMoney(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        Balance += amount;
    }

    // Bandome išleisti pinigus
    public bool TrySpend(decimal amount)
    {
        if (amount <= 0)
            throw new ArgumentOutOfRangeException(nameof(amount));

        if (Balance < amount)
            return false;

        Balance -= amount;
        return true;
    }
}
