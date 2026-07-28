namespace Stackworx.EfCoreGraphQL.Tests.Data;

public class AccountBalance
{
    // shared primary key: this is both the PK and the FK to Account
    public int AccountId { get; set; }

    public decimal Amount { get; set; }

    public Account Account { get; set; } = null!;
}
