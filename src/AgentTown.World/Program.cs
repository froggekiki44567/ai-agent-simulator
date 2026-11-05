
var builder = Host.CreateApplicationBuilder(args);

// Miestelio būsena
builder.Services.AddSingleton<AgentTown.World.WorldState>();

// Miestelio veikimo ciklas
builder.Services.AddHostedService<AgentTown.World.WorldTickService>();

builder.Build().Run();
