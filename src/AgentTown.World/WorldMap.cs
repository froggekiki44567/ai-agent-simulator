
namespace AgentTown.World;

public sealed class WorldMap
{
    public int Width { get; } = 12;
    public int Height { get; } = 12;

    private readonly HashSet<(int X, int Y)> _roads = new();

    public bool IsInside(int x, int y)
    {
        return x >= 0 && x < Width &&
               y >= 0 && y < Height;
    }

    public void AddRoad(int x, int y)
    {
        if (!IsInside(x, y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                "Kelias negali būti už žemėlapio ribų.");
        }

        _roads.Add((x, y));
    }

    public bool IsRoad(int x, int y)
    {
        return _roads.Contains((x, y));
    }
}
