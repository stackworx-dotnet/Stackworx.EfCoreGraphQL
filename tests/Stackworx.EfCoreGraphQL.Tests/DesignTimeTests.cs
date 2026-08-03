namespace Stackworx.EfCoreGraphQL.Tests;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations.Design;
using Microsoft.EntityFrameworkCore.Sqlite.Design.Internal;
using Microsoft.Extensions.DependencyInjection;
using Stackworx.EfCoreGraphQL.Abstractions;
using Stackworx.EfCoreGraphQL.DesignTime;
using Stackworx.EfCoreGraphQL.Tests.Data;

public class DesignTimeTests
{
    private const string OutputDirEnvVar = "STACKWORX_EFCOREGRAPHQL_SIDECAR_OUTPUT_DIR";

    private const string SnapshotNamespace = "Api.Migrations";

    [Fact]
    public async Task TestConfiguredOptionsReachTheGenerator()
    {
        await AppDbContext.WithSqliteInMemoryAsync(db =>
        {
            var source = GenerateSidecar(db, services => services.AddEfCoreGraphQL(options =>
            {
                options.Mode = Mode.OptIn;
                options.IgnoreForeignKeyFields = false;
            }));

            source.Should().Contain("class AuthorExtensions");
            source.Should().NotContain("class UserExtensions");
            source.Should().NotContain("IgnoreFields");

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TestFilterReachesTheGenerator()
    {
        await AppDbContext.WithSqliteInMemoryAsync(db =>
        {
            var source = GenerateSidecar(
                db,
                services => services.AddEfCoreGraphQL(new GenerateOptions { Filter = e => e.ClrType == typeof(User) }));

            source.Should().NotContain("class UserExtensions");
            source.Should().Contain("class AuthorExtensions");

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TestNamespaceIsDerivedFromSnapshotUnlessOverridden()
    {
        await AppDbContext.WithSqliteInMemoryAsync(db =>
        {
            GenerateSidecar(db, services => services.AddEfCoreGraphQL())
                .Should().Contain($"namespace {SnapshotNamespace}.{GenerateOptions.DefaultNamespace};");

            GenerateSidecar(db, services => services.AddEfCoreGraphQL(options => options.Namespace = "Api.Loaders"))
                .Should().Contain("namespace Api.Loaders;");

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TestRegisteringTheGeneratorWithoutOptionsUsesDefaults()
    {
        await AppDbContext.WithSqliteInMemoryAsync(db =>
        {
            var source = GenerateSidecar(
                db,
                services => services.AddSingleton<IMigrationsCodeGenerator, EfCoreMigrationsCodeGenerator>());

            source.Should().Contain("class UserExtensions");
            source.Should().Contain("IgnoreFields = [\"authorId\"]");
            source.Should().Contain($"namespace {SnapshotNamespace}.{GenerateOptions.DefaultNamespace};");

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TestConfiguredOptionsAreNotMutated()
    {
        await AppDbContext.WithSqliteInMemoryAsync(db =>
        {
            var options = new GenerateOptions();

            GenerateSidecar(db, services => services.AddEfCoreGraphQL(options));

            // The derived namespace belongs to the run, not to the options the consumer registered.
            options.Namespace.Should().BeNull();

            return Task.CompletedTask;
        });
    }

    private static string GenerateSidecar(AppDbContext db, Action<IServiceCollection> configure)
    {
        var outputDir = Directory.CreateTempSubdirectory("efcoregraphql-designtime");
        var previousOutputDir = Environment.GetEnvironmentVariable(OutputDirEnvVar);
        Environment.SetEnvironmentVariable(OutputDirEnvVar, outputDir.FullName);

        try
        {
            var services = new ServiceCollection()
                .AddEntityFrameworkDesignTimeServices();
            new SqliteDesignTimeServices().ConfigureDesignTimeServices(services);
            configure(services);

            // The snapshot generator needs the design-time model, which is what EF tooling passes it.
            var model = db.GetService<IDesignTimeModel>().Model;

            services.BuildServiceProvider()
                .GetRequiredService<IMigrationsCodeGenerator>()
                .GenerateSnapshot(SnapshotNamespace, typeof(AppDbContext), "AppDbContextModelSnapshot", model);

            return File.ReadAllText(Path.Combine(outputDir.FullName, "AppDbContextModelSnapshot.DataLoaders.g.cs"));
        }
        finally
        {
            Environment.SetEnvironmentVariable(OutputDirEnvVar, previousOutputDir);
            outputDir.Delete(recursive: true);
        }
    }
}
