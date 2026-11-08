namespace AgentTown.Contracts.Model;

/// <summary>Well-known values for <see cref="Events.TransactionCompleted.Kind"/>.</summary>
public static class TransactionKinds
{
    public const string Wage = "Wage";
    public const string Purchase = "Purchase";
    public const string Sale = "Sale";
    public const string Deposit = "Deposit";
    public const string Withdrawal = "Withdrawal";
    public const string Transfer = "Transfer";
    public const string Loan = "Loan";
    public const string LoanInterest = "LoanInterest";
}
