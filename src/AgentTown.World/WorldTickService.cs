
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
            TimeSpan.FromSeconds(5));

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                long tick = worldState.AdvanceTick();

                logger.LogInformation(
                    "AgentTown: pasaulio ciklas #{Tick}",
                    tick);
            }
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
            // Programa stabdoma.
        }
    }
}
