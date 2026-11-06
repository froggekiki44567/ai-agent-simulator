
using System.Threading;

namespace AgentTown.World;

public sealed class WorldState
{
    public WorldMap Map { get; } = new();

    private long _currentTick = 0;

    public long CurrentTick =>
        Interlocked.Read(ref _currentTick);

    // Miestelio laikas prasideda 08:00
    // Vienas tick = viena virtuali minutė

    public const int MinutesPerTick = 5;


    private readonly List<Location> _locations = new();

    public IReadOnlyList<Location> Locations => _locations;


    public void AddLocation(Location location)
    {
        ArgumentNullException.ThrowIfNull(location);

        if (!Map.IsInside(location.X, location.Y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(location),
                $"Vieta '{location.Name}' yra už žemėlapio ribų.");
        }

        _locations.Add(location);
    }



    public TimeOnly CurrentTime =>
        new TimeOnly(8, 0).AddMinutes(CurrentTick * MinutesPerTick);

    public long CurrentDay =>
        (8 * 60 + CurrentTick * MinutesPerTick) / (24 * 60) + 1;


    public long AdvanceTick()
    {
        return Interlocked.Increment(ref _currentTick);
    }



    private readonly Dictionary<Guid, AgentPosition> _agentPositions = new();


    public void SetAgentPosition(Guid agentId, int x, int y)
    {
        if (!Map.IsInside(x, y))
        {
            throw new ArgumentOutOfRangeException(
                nameof(x),
                "Agentas negali būti už žemėlapio ribų.");
        }

        bool isRoad = Map.IsRoad(x, y);

        bool isLocation = _locations.Any(location =>
            location.X == x && location.Y == y);

        if (!isRoad && !isLocation)
        {
            throw new ArgumentException(
                "Agentas gali atsirasti tik ant kelio arba pastate.");
        }

        _agentPositions[agentId] = new AgentPosition(agentId, x, y);
    }

    public AgentPosition? GetAgentPosition(Guid agentId)
    {
        return _agentPositions.GetValueOrDefault(agentId);
    }



    public bool TryMoveAgent(Guid agentId, int x, int y)
    {
        // Patikriname, ar agentas egzistuoja pasaulyje
        if (!_agentPositions.TryGetValue(agentId, out var current))
            return false;

        // Neleidžiame išeiti už žemėlapio ribų
        if (!Map.IsInside(x, y))
            return false;

        // Leidžiame judėti tik vieną langelį
        int distance = Math.Abs(current.X - x)
                    + Math.Abs(current.Y - y);

        if (distance != 1)
            return false;

        // Patikriname, ar tai kelias arba pastatas
        bool isRoad = Map.IsRoad(x, y);

        bool isLocation = _locations.Any(location =>
            location.X == x && location.Y == y);

        if (!isRoad && !isLocation)
            return false;

        // Atnaujiname agento poziciją
        _agentPositions[agentId] = new AgentPosition(agentId, x, y);

        return true;
    }


    private readonly Dictionary<Guid, Queue<(int X, int Y)>> _agentRoutes = new();

    public bool TryPlanRoute(Guid agentId, Guid destinationId)
    {
        // Patikriname, ar agentas egzistuoja
        if (!_agentPositions.TryGetValue(agentId, out var position))
            return false;

        // Surandame norimą vietą
        var destination = _locations.FirstOrDefault(
            location => location.Id == destinationId);

        if (destination is null)
            return false;

        // Apskaičiuojame trumpiausią kelią
        var path = PathFinder.FindPath(
            Map,
            (position.X, position.Y),
            (destination.X, destination.Y));

        if (path.Count == 0)
            return false;

        // Išsaugome maršrutą be pradinės pozicijos
        _agentRoutes[agentId] = new Queue<(int X, int Y)>(
            path.Skip(1));

        return true;
    }


    public void AdvanceAgentRoutes()
    {
        // Pereiname per visus keliaujančius agentus
        foreach (var agentId in _agentRoutes.Keys.ToArray())
        {
            var route = _agentRoutes[agentId];

            // Jeigu maršrutas baigėsi
            if (route.Count == 0)
            {
                _agentRoutes.Remove(agentId);
                continue;
            }

            // Pasiimame kitą maršruto langelį
            var next = route.Peek();

            // Bandome pajudinti agentą
            if (!TryMoveAgent(agentId, next.X, next.Y))
            {
                // Jeigu žingsnis neįmanomas,
                // nutraukiame kelionę
                _agentRoutes.Remove(agentId);
                continue;
            }

            // Žingsnis atliktas – pašaliname jį iš eilės
            route.Dequeue();

            // Jeigu agentas pasiekė tikslą
            if (route.Count == 0)
            {
                _agentRoutes.Remove(agentId);
            }
        }
    }

}
