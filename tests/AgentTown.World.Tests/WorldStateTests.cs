
using AgentTown.World;
using Xunit;

namespace AgentTown.World.Tests;

public class WorldStateTests
{
    [Fact]
    public void AddLocation_RejectsLocationOutsideMap()
    {
        // Sukuriame miestelį
        var world = new WorldState();

        // Bandome pridėti parduotuvę už žemėlapio ribų
        var shop = new Location(
            "Parduotuvė",
            LocationType.Shop,
            20, 5);

        // Tikimės, kad programa atmes šią vietą
        Assert.Throws<ArgumentOutOfRangeException>(
            () => world.AddLocation(shop));
    }


    [Fact]
    public void SetAgentPosition_SavesAndUpdatesPosition()
    {
        var world = new WorldState();
        var agentId = Guid.NewGuid();

        world.AddLocation(new Location(
            "Namas", LocationType.Home, 2, 3));

        world.Map.AddRoad(2, 4);

        // Agentas atsiranda namuose
        world.SetAgentPosition(agentId, 2, 3);

        var firstPosition = world.GetAgentPosition(agentId);

        Assert.NotNull(firstPosition);
        Assert.Equal((2, 3), (firstPosition.X, firstPosition.Y));

        // Atnaujiname agento poziciją
        world.SetAgentPosition(agentId, 2, 4);

        var updatedPosition = world.GetAgentPosition(agentId);

        Assert.NotNull(updatedPosition);
        Assert.Equal((2, 4), (updatedPosition.X, updatedPosition.Y));
    }


    [Fact]
    public void TryMoveAgent_AllowsOneStepButPreventsTeleporting()
    {
        var world = new WorldState();
        var agentId = Guid.NewGuid();

        // Sukuriame kelią šalia namo
        world.Map.AddRoad(2, 4);


        world.AddLocation(new Location(
            "Namas", LocationType.Home, 2, 3));


        // Agentas pradeda namuose
        world.SetAgentPosition(agentId, 2, 3);

        // Leidžiame žengti į gretimą kelio langelį
        bool moved = world.TryMoveAgent(agentId, 2, 4);

        Assert.True(moved);

        // Bandome teleportuotis į kitą žemėlapio pusę
        bool teleported = world.TryMoveAgent(agentId, 7, 3);

        Assert.False(teleported);

        // Agentas turi likti paskutinėje teisėtoje pozicijoje
        var position = world.GetAgentPosition(agentId);

        Assert.NotNull(position);
        Assert.Equal((2, 4), (position.X, position.Y));
    }


    [Fact]
    public void SetAgentPosition_RejectsSpawnOnGrass()
    {
        var world = new WorldState();
        var agentId = Guid.NewGuid();

        // (0, 0) yra žolė – ten nėra nei kelio, nei pastato
        Assert.Throws<ArgumentException>(() =>
            world.SetAgentPosition(agentId, 0, 0));

        // Po nesėkmingo bandymo agentas neturi būti sukurtas
        Assert.Null(world.GetAgentPosition(agentId));
    }


    [Fact]
    public void AdvanceAgentRoutes_MovesAgentToDestination()
    {
        var world = new WorldState();
        var agentId = Guid.NewGuid();

        // Sukuriame namą ir parduotuvę
        var home = new Location(
            "Namas", LocationType.Home, 2, 3);

        var shop = new Location(
            "Parduotuvė", LocationType.Shop, 7, 3);

        world.AddLocation(home);
        world.AddLocation(shop);

        // Sukuriame kelią tarp pastatų
        for (int x = 2; x <= 7; x++)
        {
            world.Map.AddRoad(x, 4);
        }

        // Agentas atsiranda namuose
        world.SetAgentPosition(agentId, 2, 3);

        // Suplanuojame kelionę į parduotuvę
        bool planned = world.TryPlanRoute(agentId, shop.Id);

        Assert.True(planned);

        // Tikimės vieno žingsnio per kiekvieną tick
        var expectedPositions = new (int X, int Y)[]
        {
            (2, 4),
            (3, 4),
            (4, 4),
            (5, 4),
            (6, 4),
            (7, 4),
            (7, 3)
        };

        foreach (var expected in expectedPositions)
        {
            world.AdvanceAgentRoutes();

            var position = world.GetAgentPosition(agentId);

            Assert.NotNull(position);
            Assert.Equal(expected, (position.X, position.Y));
        }

        // Pasiekus tikslą agentas turi sustoti
        world.AdvanceAgentRoutes();

        var finalPosition = world.GetAgentPosition(agentId);

        Assert.NotNull(finalPosition);
        Assert.Equal((7, 3), (finalPosition.X, finalPosition.Y));
    }


    [Fact]
    public void TravelStatus_ChangesFromIdleToTravelingToArrived()
    {
        var world = new WorldState();
        var agentId = Guid.NewGuid();

        // Sukuriame namą ir parduotuvę
        var home = new Location(
            "Namas", LocationType.Home, 2, 3);

        var shop = new Location(
            "Parduotuvė", LocationType.Shop, 7, 3);

        world.AddLocation(home);
        world.AddLocation(shop);

        // Sukuriame kelią
        for (int x = 2; x <= 7; x++)
        {
            world.Map.AddRoad(x, 4);
        }

        // Agentas atsiranda namuose
        world.SetAgentPosition(agentId, 2, 3);

        Assert.Equal(
            TravelStatus.Idle,
            world.GetTravelStatus(agentId));

        // Suplanuojame kelionę
        Assert.True(world.TryPlanRoute(agentId, shop.Id));

        Assert.Equal(
            TravelStatus.Traveling,
            world.GetTravelStatus(agentId));

        // Atliekame 7 judėjimo ciklus
        for (int i = 0; i < 7; i++)
        {
            world.AdvanceAgentRoutes();
        }

        // Patikriname, ar agentas atvyko
        Assert.Equal(
            TravelStatus.Arrived,
            world.GetTravelStatus(agentId));

        var position = world.GetAgentPosition(agentId);

        Assert.NotNull(position);
        Assert.Equal((7, 3), (position.X, position.Y));
    }


    [Fact]
    public void TryPlanRoute_WhenNoPathExists_SetsFailedStatus()
    {
        var world = new WorldState();
        var agentId = Guid.NewGuid();

        var home = new Location(
            "Namas", LocationType.Home, 2, 3);

        var shop = new Location(
            "Parduotuvė", LocationType.Shop, 7, 3);

        world.AddLocation(home);
        world.AddLocation(shop);

        // Agentas atsiranda namuose
        world.SetAgentPosition(agentId, 2, 3);

        // Kelių nesukuriame
        bool planned = world.TryPlanRoute(agentId, shop.Id);

        Assert.False(planned);

        // Nesėkminga kelionė turi būti pažymėta Failed
        Assert.Equal(
            TravelStatus.Failed,
            world.GetTravelStatus(agentId));
    }


    [Fact]
    public void Salary_IsPaidOnlyAtWorkplace_OncePerDay()
    {
        var world = new WorldState();
        var agentId = Guid.NewGuid();

        // Sukuriame namus ir biurą
        var home = new Location(
            "Namas", LocationType.Home, 2, 3);

        var office = new Location(
            "Biuras", LocationType.Workplace, 7, 8);

        world.AddLocation(home);
        world.AddLocation(office);

        // Jonas pradeda namuose
        world.SetAgentPosition(agentId, 2, 3);

        // Sukuriame ir priskiriame darbą
        var job = new Job("Programuotojas", office.Id, 100m);

        world.AddJob(job);
        Assert.True(world.TryAssignJob(agentId, job.Id));

        // Pradinis balansas – 20 €
        var wallet = world.GetOrCreateWallet(agentId, 20m);

        // Namuose atlyginimo gauti negalima
        Assert.False(world.TryPaySalaryForShift(agentId));
        Assert.Equal(20m, wallet.Balance);

        // Testui perkeliame Joną į biurą
        world.SetAgentPosition(agentId, 7, 8);

        // Pirmas atlyginimas sėkmingas
        Assert.True(world.TryPaySalaryForShift(agentId));
        Assert.Equal(120m, wallet.Balance);

        // Antrą kartą tą pačią dieną mokėti negalima
        Assert.False(world.TryPaySalaryForShift(agentId));
        Assert.Equal(120m, wallet.Balance);
    }


    [Fact]
    public void ProcessSalaries_PaysAgentAtWorkplaceOnlyOncePerDay()
    {
        var world = new WorldState();
        var agentId = Guid.NewGuid();

        // Sukuriame namus ir darbovietę
        var home = new Location(
            "Namas", LocationType.Home, 2, 3);

        var office = new Location(
            "Biuras", LocationType.Workplace, 7, 8);

        world.AddLocation(home);
        world.AddLocation(office);

        // Jonas pradeda namuose
        world.SetAgentPosition(agentId, 2, 3);

        // Įdarbiname Joną
        var job = new Job("Programuotojas", office.Id, 100m);
        world.AddJob(job);
        Assert.True(world.TryAssignJob(agentId, job.Id));

        var wallet = world.GetOrCreateWallet(agentId, 50m);

        // Namuose atlyginimo negauna
        world.ProcessSalaries();
        Assert.Equal(50m, wallet.Balance);

        // Testui perkeliame Joną į darbovietę
        world.SetAgentPosition(agentId, 7, 8);

        // Sistema automatiškai išmoka atlyginimą
        world.ProcessSalaries();
        Assert.Equal(150m, wallet.Balance);

        // Antrą kartą tą pačią dieną nemoka
        world.ProcessSalaries();
        Assert.Equal(150m, wallet.Balance);
    }

    [Fact]
    public void Bank_CanTransferMoneyBetweenAgents()
    {
        var world = new WorldState();

        var jonasId = Guid.NewGuid();
        var petrasId = Guid.NewGuid();

        // Sukuriame agentų gyvenamąją vietą
        var home = new Location(
            "Namas", LocationType.Home, 2, 3);

        world.AddLocation(home);

        // Abu agentai atsiranda miestelyje
        world.SetAgentPosition(jonasId, 2, 3);
        world.SetAgentPosition(petrasId, 2, 3);

        // Jonas turi 100 €, Petras – 30 €
        var jonasWallet = world.GetOrCreateWallet(jonasId, 100m);
        var petrasWallet = world.GetOrCreateWallet(petrasId, 30m);

        // Jonas perveda Petrui 20 €
        bool success = world.TryTransferMoney(
            jonasId, petrasId, 20m);

        Assert.True(success);

        // Patikriname abiejų agentų balansus
        Assert.Equal(80m, jonasWallet.Balance);
        Assert.Equal(50m, petrasWallet.Balance);

        // Jonas bando pervesti daugiau, nei turi
        bool failedTransfer = world.TryTransferMoney(
            jonasId, petrasId, 200m);

        Assert.False(failedTransfer);

        // Po nesėkmingo pervedimo balansai nesikeičia
        Assert.Equal(80m, jonasWallet.Balance);
        Assert.Equal(50m, petrasWallet.Balance);
    }

}
