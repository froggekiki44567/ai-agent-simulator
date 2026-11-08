
using System.Threading;

namespace AgentTown.World;

public sealed class WorldState
{
    private readonly Bank _bank = new();

    private readonly Dictionary<Guid, AgentInventory> _agentInventories = new();

    private readonly Dictionary<Guid, Shop> _shops = new();

    private readonly HashSet<(Guid AgentId, long Day)> _paidShifts = new();

    private readonly Dictionary<Guid, Guid> _agentJobs = new();

    private readonly List<Job> _jobs = new();

    public IReadOnlyList<Job> Jobs => _jobs.AsReadOnly();

    public WorldMap Map { get; } = new();

    private long _currentTick = 0;

    public long CurrentTick =>
        Interlocked.Read(ref _currentTick);

    // Miestelio laikas prasideda 08:00
    // Vienas tick = viena virtuali minutė

    public const int MinutesPerTick = 5;

    private readonly Dictionary<Guid, AgentWallet> _agentWallets = new();

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

    public IReadOnlyList<AgentPosition> GetAgentPositions()
    {
        return _agentPositions.Values.ToArray();
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
        {
            // Kelio iki tikslo nėra
            _travelStatuses[agentId] = TravelStatus.Failed;

            // Pašaliname ankstesnį maršrutą, jei toks buvo
            _agentRoutes.Remove(agentId);

            return false;
        }

        // Išsaugome maršrutą be pradinės pozicijos

        _agentRoutes[agentId] = new Queue<(int X, int Y)>(
            path.Skip(1));

        // Pažymime, kad agentas pradėjo kelionę
        _travelStatuses[agentId] = path.Count > 1
            ? TravelStatus.Traveling
            : TravelStatus.Arrived;

        return true;
    }



    public void AdvanceAgentRoutes()
    {
        foreach (var agentId in _agentRoutes.Keys.ToArray())
        {
            var route = _agentRoutes[agentId];

            // Agentas jau pasiekė tikslą
            if (route.Count == 0)
            {
                _travelStatuses[agentId] = TravelStatus.Arrived;
                _agentRoutes.Remove(agentId);
                continue;
            }

            var next = route.Peek();

            // Bandome žengti kitą žingsnį
            if (!TryMoveAgent(agentId, next.X, next.Y))
            {
                _travelStatuses[agentId] = TravelStatus.Failed;
                _agentRoutes.Remove(agentId);
                continue;
            }

            // Sėkmingai pajudėjome
            route.Dequeue();

            // Patikriname, ar kelionė baigta
            if (route.Count == 0)
            {
                _travelStatuses[agentId] = TravelStatus.Arrived;
                _agentRoutes.Remove(agentId);
            }
        }
    }


    private readonly Dictionary<Guid, TravelStatus> _travelStatuses = new();

    public TravelStatus? GetTravelStatus(Guid agentId)
    {
        // Jeigu agento pasaulyje nėra
        if (!_agentPositions.ContainsKey(agentId))
            return null;

        // Jeigu agentas neturi kelionės, jis yra Idle
        return _travelStatuses.GetValueOrDefault(
            agentId,
            TravelStatus.Idle);
    }


    public AgentWallet GetOrCreateWallet(
        Guid agentId,
        decimal startingBalance = 0)
    {
        // Patikriname, ar agentas egzistuoja miestelyje
        if (!_agentPositions.ContainsKey(agentId))
            throw new InvalidOperationException("Agentas neegzistuoja.");

        // Jei agentas jau turi piniginę, ją grąžiname
        if (_agentWallets.TryGetValue(agentId, out var wallet))
            return wallet;

        // Jei piniginės nėra, sukuriame naują
        wallet = new AgentWallet(agentId, startingBalance);
        _agentWallets[agentId] = wallet;

        return wallet;
    }


    public void AddJob(Job job)
    {
        ArgumentNullException.ThrowIfNull(job);

        // Darbas turi būti tikroje miestelio darbovietėje
        bool workplaceExists = _locations.Any(location =>
            location.Id == job.WorkplaceId &&
            (location.Type == LocationType.Workplace ||
            location.Type == LocationType.Shop));

        if (!workplaceExists)
            throw new InvalidOperationException(
                "Darbo vieta neegzistuoja.");

        // Neleidžiame to paties darbo pridėti du kartus
        if (_jobs.Any(existing => existing.Id == job.Id))
            throw new InvalidOperationException(
                "Darbas jau egzistuoja.");

        _jobs.Add(job);
    }


    public bool TryAssignJob(Guid agentId, Guid jobId)
    {
        // Patikriname, ar agentas egzistuoja
        if (!_agentPositions.ContainsKey(agentId))
            return false;

        // Patikriname, ar toks darbas egzistuoja
        if (!_jobs.Any(job => job.Id == jobId))
            return false;

        // Priskiriame darbą agentui
        _agentJobs[agentId] = jobId;

        return true;
    }

    public Job? GetAgentJob(Guid agentId)
    {
        // Patikriname, ar agentas turi darbą
        if (!_agentJobs.TryGetValue(agentId, out var jobId))
            return null;

        // Surandame ir grąžiname jo darbą
        return _jobs.FirstOrDefault(job => job.Id == jobId);
    }


    public bool TryPaySalaryForShift(Guid agentId)
    {
        // Patikriname, ar agentas turi darbą
        var job = GetAgentJob(agentId);

        if (job == null)
            return false;

        // Patikriname agento poziciją
        if (!_agentPositions.TryGetValue(agentId, out var position))
            return false;

        // Surandame darbovietę
        var workplace = _locations.FirstOrDefault(
            location => location.Id == job.WorkplaceId);

        if (workplace == null)
            return false;

        // Atlyginimas mokamas tik esant darbovietėje
        if (position.X != workplace.X || position.Y != workplace.Y)
            return false;

        // Vienas atlyginimas per miestelio dieną
        if (!_paidShifts.Add((agentId, CurrentDay)))
            return false;

        // Pervedame atlyginimą į agento piniginę
        var wallet = GetOrCreateWallet(agentId);
        wallet.AddMoney(job.SalaryPerShift);

        return true;
    }


    public void AddShop(Shop shop)
    {
        ArgumentNullException.ThrowIfNull(shop);

        // Patikriname, ar tokia parduotuvės vieta egzistuoja
        bool locationExists = _locations.Any(location =>
            location.Id == shop.LocationId &&
            location.Type == LocationType.Shop);

        if (!locationExists)
            throw new InvalidOperationException(
                "Parduotuvės vieta neegzistuoja.");

        // Neleidžiame toje pačioje vietoje sukurti dviejų parduotuvių
        if (_shops.ContainsKey(shop.LocationId))
            throw new InvalidOperationException(
                "Parduotuvė šioje vietoje jau egzistuoja.");

        _shops.Add(shop.LocationId, shop);
    }

    public Shop? GetShop(Guid locationId)
    {
        return _shops.GetValueOrDefault(locationId);
    }


    public bool TryBuyProduct(
        Guid agentId,
        Guid shopLocationId,
        Guid productId,
        int quantity = 1)
    {
        // Negalima pirkti neigiamo ar nulinio kiekio
        if (quantity <= 0)
            return false;

        // Patikriname agento poziciją
        var position = GetAgentPosition(agentId);
        if (position == null)
            return false;

        // Surandame parduotuvę
        var shop = GetShop(shopLocationId);
        if (shop == null)
            return false;

        // Surandame parduotuvės vietą žemėlapyje
        var location = _locations.FirstOrDefault(
            l => l.Id == shopLocationId);

        if (location == null)
            return false;

        // Agentas turi būti parduotuvėje
        if (position.X != location.X || position.Y != location.Y)
            return false;

        // Patikriname prekę ir jos likutį
        var product = shop.GetProduct(productId);
        if (product == null || shop.GetStock(productId) < quantity)
            return false;

        // Apskaičiuojame bendrą kainą
        decimal totalPrice = checked(product.Price * quantity);

        // Patikriname, ar pakanka pinigų
        var wallet = GetOrCreateWallet(agentId);
        if (wallet.Balance < totalPrice)
            return false;

        // Nuskaitome pinigus
        if (!wallet.TrySpend(totalPrice))
            return false;


        // Sumažiname parduotuvės atsargas
        if (!shop.TryRemoveStock(productId, quantity))
        {
            // Jei atsargų pašalinti nepavyko,
            // grąžiname agentui pinigus
            wallet.AddMoney(totalPrice);
            return false;
        }

        // Pridedame prekes į agento inventorių
        var inventory = GetOrCreateInventory(agentId);
        inventory.AddItem(productId, quantity);

        return true;
    }


    public AgentInventory GetOrCreateInventory(Guid agentId)
    {
        // Patikriname, ar agentas egzistuoja miestelyje
        if (!_agentPositions.ContainsKey(agentId))
            throw new InvalidOperationException(
                "Agentas neegzistuoja.");

        // Jeigu inventorius jau egzistuoja, jį grąžiname
        if (_agentInventories.TryGetValue(agentId, out var inventory))
            return inventory;

        // Sukuriame naują inventorių
        inventory = new AgentInventory(agentId);
        _agentInventories[agentId] = inventory;

        return inventory;
    }

    public void ProcessSalaries()
    {
        // Pereiname per visus dirbančius agentus
        foreach (var agentId in _agentJobs.Keys.ToArray())
        {
            // Patikriname, ar agentui galima išmokėti atlyginimą
            TryPaySalaryForShift(agentId);
        }
    }


    public bool TryTransferMoney(
        Guid senderId,
        Guid receiverId,
        decimal amount)
    {
        // Patikriname, ar abu agentai egzistuoja
        if (!_agentPositions.ContainsKey(senderId) ||
            !_agentPositions.ContainsKey(receiverId))
            return false;

        // Negalima pervesti pinigų sau ar netinkamos sumos
        if (senderId == receiverId || amount <= 0)
            return false;

        // Gauname abiejų agentų pinigines
        var senderWallet = GetOrCreateWallet(senderId);
        var receiverWallet = GetOrCreateWallet(receiverId);

        // Atliekame pervedimą
        return _bank.TryTransfer(
            senderWallet,
            receiverWallet,
            amount);
    }

}
