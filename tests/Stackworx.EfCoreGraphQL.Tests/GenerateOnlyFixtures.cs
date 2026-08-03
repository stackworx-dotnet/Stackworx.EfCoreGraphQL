// Fixtures for the generate-only route, which discovers what it needs by scanning an assembly: a model
// snapshot to name the output after, a design-time factory to build the model from, and IDesignTimeServices
// to read the options from. The namespace matches DesignTimeTests.SnapshotNamespace so both routes derive
// the same output namespace and can be compared byte for byte.
namespace Api.Migrations;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Stackworx.EfCoreGraphQL.Abstractions;
using Stackworx.EfCoreGraphQL.DesignTime;
using Stackworx.EfCoreGraphQL.Tests.Data;

[DbContext(typeof(AppDbContext))]
internal class AppDbContextModelSnapshot : ModelSnapshot
{
    // Only the type's name, namespace and [DbContext] are read; the recorded model is not, because it
    // carries no CLR types.
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
    }
}

internal sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
        => new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options);
}

internal sealed class DesignTimeServices : IDesignTimeServices
{
    public void ConfigureDesignTimeServices(IServiceCollection services)
        => services.AddEfCoreGraphQL(options => options.Mode = Mode.OptIn);
}
