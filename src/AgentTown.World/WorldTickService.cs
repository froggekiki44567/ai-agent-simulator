
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentTown.World;

public sealed class WorldTickService(
    ILogger<WorldTickService> logger,
    WorldState worldState) : BackgroundService
{
    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(
            TimeSpan.FromSeconds(1));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {

                long tick = worldState.AdvanceTick();

                worldState.AdvanceAgentRoutes();

                logger.LogInformation(
                    "AgentTown | Diena {Day} | Laikas {Time} | Tick #{Tick}",
                    worldState.CurrentDay,
                    worldState.CurrentTime.ToString("HH:mm"),
                    tick);


                foreach (var agent in worldState.GetAgentPositions())
                {
                    var status = worldState.GetTravelStatus(agent.AgentId);

                    logger.LogInformation(
                        "Agentas {AgentId} | Pozicija: ({X}, {Y}) | Busena: {Status}",
                        agent.AgentId.ToString("N")[..8],
                        agent.X,
                        agent.Y,
                        status);
                }
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Programa stabdoma.
        }
    }
}
