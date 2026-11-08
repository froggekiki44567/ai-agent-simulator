
namespace AgentTown.World;

public class Job
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Title { get; }
    public Guid WorkplaceId { get; }
    public decimal SalaryPerShift { get; }

    public Job(
        string title,
        Guid workplaceId,
        decimal salaryPerShift)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Darbo pavadinimas privalomas.");

        if (workplaceId == Guid.Empty)
            throw new ArgumentException("Darbo vieta privaloma.");

        if (salaryPerShift <= 0)
            throw new ArgumentOutOfRangeException(nameof(salaryPerShift));

        Title = title;
        WorkplaceId = workplaceId;
        SalaryPerShift = salaryPerShift;
    }
}
