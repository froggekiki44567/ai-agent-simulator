
using AgentTown.World;
using Xunit;

namespace AgentTown.World.Tests;

public class AgentWalletTests
{
    [Fact]
    public void Wallet_CanReceiveAndSpendMoney()
    {
        var wallet = new AgentWallet(Guid.NewGuid(), 100);

        wallet.AddMoney(50);
        Assert.Equal(150m, wallet.Balance);

        bool success = wallet.TrySpend(20);

        Assert.True(success);
        Assert.Equal(130m, wallet.Balance);
    }

    [Fact]
    public void Wallet_CannotSpendMoreThanBalance()
    {
        var wallet = new AgentWallet(Guid.NewGuid(), 100);

        bool success = wallet.TrySpend(150);

        Assert.False(success);
        Assert.Equal(100m, wallet.Balance);
    }

    [Fact]
    public void WorldState_ReturnsSameWalletForAgent()
    {
        var world = new WorldState();
        var agentId = Guid.NewGuid();

        var home = new Location(
            "Namas", LocationType.Home, 2, 3);

        world.AddLocation(home);
        world.SetAgentPosition(agentId, 2, 3);

        var wallet1 = world.GetOrCreateWallet(agentId, 100);
        wallet1.AddMoney(50);

        var wallet2 = world.GetOrCreateWallet(agentId);

        Assert.Same(wallet1, wallet2);
        Assert.Equal(150m, wallet2.Balance);
    }
}
