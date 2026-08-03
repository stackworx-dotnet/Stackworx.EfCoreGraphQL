namespace Sample.DesignTime;

using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Stackworx.EfCoreGraphQL.DesignTime;

/// <summary>
/// EF Core tooling will discover this type automatically (when the assembly is referenced)
/// and call it during design-time operations (e.g. migrations scaffolding).
/// </summary>
public sealed class DesignTimeServices : IDesignTimeServices
{
    // AddEfCoreGraphQL also takes a GenerateOptions or an Action<GenerateOptions>; see the README.
    public void ConfigureDesignTimeServices(IServiceCollection services)
        => services.AddEfCoreGraphQL();
}