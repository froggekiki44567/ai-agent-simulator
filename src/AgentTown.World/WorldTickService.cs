
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

                worldState.ProcessSalaries();

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

                    if (status == TravelStatus.Arrived)
                    {
                        // Patikriname, ar agentas atvyko į parduotuvę
                        var shopLocation = worldState.Locations.FirstOrDefault(
                            l => l.Type == LocationType.Shop &&
                                l.X == agent.X &&
                                l.Y == agent.Y);

                        if (shopLocation != null)
                        {
                            var shop = worldState.GetShop(shopLocation.Id);

                            // Surandame duoną
                            var bread = shop?.Products.FirstOrDefault(
                                p => p.Name == "Duona");

                            if (bread != null)
                            {
                                var inventory = worldState.GetOrCreateInventory(
                                    agent.AgentId);

                                // Demonstraciją atliekame tik vieną kartą
                                if (inventory.GetQuantity(bread.Id) == 0)
                                {
                                    bool bought = worldState.TryBuyProduct(
                                        agent.AgentId, shopLocation.Id, bread.Id, 2);

                                    var wallet = worldState.GetOrCreateWallet(
                                        agent.AgentId);

                                    logger.LogInformation(
                                        "Pirkimas: {Success} | Balansas: {Balance} € | Duona inventoriuje: {Quantity} | Likutis parduotuvėje: {Stock}",
                                        bought,
                                        wallet.Balance,
                                        inventory.GetQuantity(bread.Id),
                                        shop!.GetStock(bread.Id));
                                }
                            }
                        }
                    }

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
