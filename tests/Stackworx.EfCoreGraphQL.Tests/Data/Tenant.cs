namespace Stackworx.EfCoreGraphQL.Tests.Data;

public class Tenant
{
    // string key, so dependents hold a nullable reference type rather than a Nullable<T>
    public required string Id { get; set; }

    public required string Name { get; set; }

    public ICollection<Site> Sites { get; set; } = [];
}
