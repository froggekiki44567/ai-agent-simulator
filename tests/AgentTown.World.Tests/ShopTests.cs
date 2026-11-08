
using AgentTown.World;
using Xunit;

namespace AgentTown.World.Tests;

public class ShopTests
{
    [Fact]
    public void Agent_CanBuyProduct_WhenAtShop()
    {
        var world = new WorldState();
        var agentId = Guid.NewGuid();

        // Sukuriame namus ir parduotuvę
        var home = new Location(
            "Namas", LocationType.Home, 2, 3);

        var shopLocation = new Location(
            "Parduotuvė", LocationType.Shop, 7, 3);

        world.AddLocation(home);
        world.AddLocation(shopLocation);

        // Sukuriame parduotuvę ir prekes
        var shop = new Shop(shopLocation.Id);
        world.AddShop(shop);

        var bread = new Product("Duona", 3m);
        shop.AddStock(bread, 20);

        // Jonas pradeda namuose su 100 €
        world.SetAgentPosition(agentId, 2, 3);
        var wallet = world.GetOrCreateWallet(agentId, 100m);

        // Iš namų pirkti negalima
        Assert.False(
            world.TryBuyProduct(
                agentId, shopLocation.Id, bread.Id, 2));

        // Testui perkeliame Joną į parduotuvę
        world.SetAgentPosition(agentId, 7, 3);

        // Jonas perka 2 duonos vienetus
        bool success = world.TryBuyProduct(
            agentId, shopLocation.Id, bread.Id, 2);

        Assert.True(success);

        // Patikriname pinigus ir prekių likutį
        Assert.Equal(94m, wallet.Balance);
        Assert.Equal(18, shop.GetStock(bread.Id));

        var inventory = world.GetOrCreateInventory(agentId);

        Assert.Equal(2, inventory.GetQuantity(bread.Id));
    }


    [Fact]
    public void Agent_CannotBuyProduct_WhenNotEnoughMoney()
    {
        var world = new WorldState();
        var agentId = Guid.NewGuid();

        // Sukuriame parduotuvę
        var shopLocation = new Location(
            "Parduotuvė", LocationType.Shop, 7, 3);

        world.AddLocation(shopLocation);

        var shop = new Shop(shopLocation.Id);
        world.AddShop(shop);

        // Duona kainuoja 3 €, turime 10 vienetų
        var bread = new Product("Duona", 3m);
        shop.AddStock(bread, 10);

        // Jonas yra parduotuvėje, bet turi tik 5 €
        world.SetAgentPosition(agentId, 7, 3);
        var wallet = world.GetOrCreateWallet(agentId, 5m);

        // Bando pirkti 2 duonas už 6 €
        bool success = world.TryBuyProduct(
            agentId, shopLocation.Id, bread.Id, 2);

        // Pirkimas turi nepavykti
        Assert.False(success);

        // Pinigai ir prekių likutis nepasikeičia
        Assert.Equal(5m, wallet.Balance);
        Assert.Equal(10, shop.GetStock(bread.Id));
    }


    [Fact]
    public void Agent_CannotBuyProduct_WhenNotEnoughStock()
    {
        var world = new WorldState();
        var agentId = Guid.NewGuid();

        // Sukuriame parduotuvę
        var shopLocation = new Location(
            "Parduotuvė", LocationType.Shop, 7, 3);

        world.AddLocation(shopLocation);

        var shop = new Shop(shopLocation.Id);
        world.AddShop(shop);

        // Parduotuvėje yra tik 1 duona
        var bread = new Product("Duona", 3m);
        shop.AddStock(bread, 1);

        // Jonas turi pakankamai pinigų
        world.SetAgentPosition(agentId, 7, 3);
        var wallet = world.GetOrCreateWallet(agentId, 100m);

        // Bando nusipirkti 2 duonas
        bool success = world.TryBuyProduct(
            agentId, shopLocation.Id, bread.Id, 2);

        // Pirkimas turi nepavykti
        Assert.False(success);

        // Pinigai ir atsargos nesikeičia
        Assert.Equal(100m, wallet.Balance);
        Assert.Equal(1, shop.GetStock(bread.Id));


        var inventory = world.GetOrCreateInventory(agentId);

        // Patikriname, ar Jonas gavo 2 duonos vienetus
        Assert.Equal(0, inventory.GetQuantity(bread.Id));

    }

}
