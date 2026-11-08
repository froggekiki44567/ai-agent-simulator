
namespace AgentTown.World;

public class Bank
{
    public bool TryTransfer(
        AgentWallet sender,
        AgentWallet receiver,
        decimal amount)
    {
        ArgumentNullException.ThrowIfNull(sender);
        ArgumentNullException.ThrowIfNull(receiver);

        // Pervedimo suma turi būti teigiama
        if (amount <= 0)
            return false;

        // Negalima pervesti pinigų sau
        if (sender.AgentId == receiver.AgentId)
            return false;

        // Patikriname, ar siuntėjas turi pakankamai pinigų
        if (!sender.TrySpend(amount))
            return false;

        // Pervedame pinigus gavėjui
        receiver.AddMoney(amount);

        return true;
    }
}
