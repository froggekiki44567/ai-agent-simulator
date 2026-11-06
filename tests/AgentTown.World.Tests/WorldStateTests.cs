
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

}
