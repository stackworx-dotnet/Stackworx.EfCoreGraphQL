namespace Stackworx.EfCoreGraphQL.Tests.Data;

public class Site
{
    public int Id { get; set; }

    public required string Name { get; set; }

    // optional FK that is a nullable reference type, not a nullable value type
    public string? TenantId { get; set; }

    public Tenant? Tenant { get; set; }
}
