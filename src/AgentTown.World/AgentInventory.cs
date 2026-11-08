
namespace AgentTown.World;

public class AgentInventory
{
    public Guid AgentId { get; }

    private readonly Dictionary<Guid, int> _items = new();

    public AgentInventory(Guid agentId)
    {
        if (agentId == Guid.Empty)
            throw new ArgumentException("Agent ID privalomas.");

        AgentId = agentId;
    }

    // Pridedame įsigytas prekes
    public void AddItem(Guid productId, int quantity)
    {
        if (productId == Guid.Empty)
            throw new ArgumentException("Product ID privalomas.");

        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        _items[productId] = checked(
            _items.GetValueOrDefault(productId) + quantity);
    }

    // Patikriname, kiek agentas turi šios prekės
    public int GetQuantity(Guid productId)
    {
        return _items.GetValueOrDefault(productId);
    }

    // Bandome panaudoti arba pašalinti prekę
    public bool TryRemoveItem(Guid productId, int quantity)
    {
        if (quantity <= 0)
            return false;

        if (GetQuantity(productId) < quantity)
            return false;

        _items[productId] -= quantity;
        return true;
    }
}
