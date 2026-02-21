namespace Stackworx.EfCoreGraphQL.DesignTime;

using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.Extensions.DependencyInjection;

/// <summary>
/// EF Core tooling will discover this type automatically (when the assembly is referenced)
/// and call it during design-time operations (e.g. migrations scaffolding).
/// </summary>
public sealed class DesignTimeServices : IDesignTimeServices
{
    public void ConfigureDesignTimeServices(IServiceCollection services)
        => services.AddSingleton<IMigrationsCodeGenerator, MyMigrationsCodeGenerator>();
}