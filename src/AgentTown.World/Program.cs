
using AgentTown.World;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSingleton<WorldState>();
builder.Services.AddHostedService<WorldTickService>();

var host = builder.Build();

// Gauname bendrą miestelio būseną
var world = host.Services.GetRequiredService<WorldState>();

// Sukuriame pirmąsias miestelio vietas
world.AddLocation(new Location(
    "Gyvenamasis namas", LocationType.Home, 2, 3));

world.AddLocation(new Location(
    "Parduotuvė", LocationType.Shop, 7, 3));

world.AddLocation(new Location(
    "Biuras", LocationType.Workplace, 7, 8));

world.AddLocation(new Location(
    "Miesto parkas", LocationType.Park, 3, 8));

// Horizontalūs keliai
for (int x = 2; x <= 7; x++)
{
    world.Map.AddRoad(x, 4);
    world.Map.AddRoad(x, 7);
}

// Vertikalūs keliai
for (int y = 4; y <= 7; y++)
{
    world.Map.AddRoad(2, y);
    world.Map.AddRoad(7, y);
}

Console.WriteLine("=== AgentTown miestelio vietos ===");

foreach (var location in world.Locations)
{
    Console.WriteLine(
        $"{location.Name} | {location.Type} | X: {location.X}, Y: {location.Y}");
}


Console.WriteLine("=== AgentTown žemėlapis ===");

for (int y = 0; y < world.Map.Height; y++)
{
    for (int x = 0; x < world.Map.Width; x++)
    {
        var location = world.Locations
            .FirstOrDefault(l => l.X == x && l.Y == y);

        char symbol = location?.Type switch
        {
            LocationType.Home => 'H',
            LocationType.Shop => 'S',
            LocationType.Workplace => 'W',
            LocationType.Park => 'P',
            _ => world.Map.IsRoad(x, y) ? '#' : '.'
        };

        Console.Write($"{symbol} ");
    }

    Console.WriteLine();
}


host.Run();
