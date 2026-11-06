
namespace AgentTown.World;

public class Shop
{
    public Guid LocationId { get; }

    // Parduotuvės prekės
    private readonly Dictionary<Guid, Product> _products = new();

    // Kiek kiekvienos prekės turime
    private readonly Dictionary<Guid, int> _stock = new();

    public IReadOnlyList<Product> Products =>
        _products.Values.ToArray();

    public Shop(Guid locationId)
    {
        if (locationId == Guid.Empty)
            throw new ArgumentException("Parduotuvės vieta privaloma.");

        LocationId = locationId;
    }

    // Papildome parduotuvės atsargas
    public void AddStock(Product product, int quantity)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        _products[product.Id] = product;
        _stock[product.Id] = checked(
            _stock.GetValueOrDefault(product.Id) + quantity);
    }

    // Patikriname prekės likutį
    public int GetStock(Guid productId)
    {
        return _stock.GetValueOrDefault(productId);
    }

    // Surandame prekę
    public Product? GetProduct(Guid productId)
    {
        return _products.GetValueOrDefault(productId);
    }

    // Sumažiname atsargas
    public bool TryRemoveStock(Guid productId, int quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        if (GetStock(productId) < quantity)
            return false;

        _stock[productId] -= quantity;
        return true;
    }
}
