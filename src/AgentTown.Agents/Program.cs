using AgentTown.Agents;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAgentServices(builder.Configuration);

// Day 3: subscribe AgentLoop.OnTickAsync to town.tick and OnActionResult to town.actions.result.*
// as soon as B's Messaging (INatsBus) is available.

builder.Build().Run();
