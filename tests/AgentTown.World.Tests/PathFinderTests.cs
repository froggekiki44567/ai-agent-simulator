
using AgentTown.World;
using Xunit;

namespace AgentTown.World.Tests;

public class PathFinderTests
{
    [Fact]
    public void FindPath_FromHomeToShop_ReturnsShortestPath()
    {
        // Sukuriame žemėlapį
        var map = new WorldMap();

        // Sukuriame kelią tarp namo ir parduotuvės
        for (int x = 2; x <= 7; x++)
        {
            map.AddRoad(x, 4);
        }

        var home = new Location(
            "Namas", LocationType.Home, 2, 3);

        var shop = new Location(
            "Parduotuvė", LocationType.Shop, 7, 3);

        // Ieškome trumpiausio kelio
        var path = PathFinder.FindPath(map, home, shop);

        // Tikriname rezultatą
        Assert.Equal(8, path.Count);
        Assert.Equal((2, 3), path[0]);
        Assert.Equal((7, 3), path[^1]);

        // Visi tarpiniai langeliai turi būti keliai
        foreach (var position in path.Skip(1).SkipLast(1))
        {
            Assert.True(map.IsRoad(position.X, position.Y));
        }
    }


    [Fact]
    public void FindPath_WhenNoRoadExists_ReturnsEmptyPath()
    {
        // Žemėlapis be kelių
        var map = new WorldMap();

        var home = new Location(
            "Namas", LocationType.Home, 2, 3);

        var shop = new Location(
            "Parduotuvė", LocationType.Shop, 7, 3);

        // Bandome rasti kelią
        var path = PathFinder.FindPath(map, home, shop);

        // Kelio nėra, todėl sąrašas turi būti tuščias
        Assert.Empty(path);
    }

}
