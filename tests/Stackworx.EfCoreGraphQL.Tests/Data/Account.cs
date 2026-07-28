namespace Stackworx.EfCoreGraphQL.Tests.Data;

public class Account
{
    public int Id { get; set; }

    public required string Name { get; set; }

    // optional navigation to the dependent that shares this entity's primary key
    public AccountBalance? Balance { get; set; }
}
