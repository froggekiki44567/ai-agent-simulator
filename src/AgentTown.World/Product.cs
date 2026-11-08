
namespace AgentTown.World;

public class Product
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; }
    public decimal Price { get; }

    public Product(string name, decimal price)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException(
                "Prekės pavadinimas privalomas.");

        if (price <= 0)
            throw new ArgumentOutOfRangeException(nameof(price));

        Name = name;
        Price = price;
    }
}
