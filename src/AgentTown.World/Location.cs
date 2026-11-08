
namespace AgentTown.World;

public enum LocationType
{
    Home,
    Shop,
    Workplace,
    Park
}

public sealed class Location
{
    public Guid Id { get; } = Guid.NewGuid();

    public string Name { get; }

    public LocationType Type { get; }

    public int X { get; }
    public int Y { get; }

    public Location(string name, LocationType type, int x, int y)
    {
        Name = name;
        Type = type;
        X = x;
        Y = y;
    }
}
