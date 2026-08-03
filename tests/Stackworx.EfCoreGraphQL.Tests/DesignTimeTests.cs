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

    /// <summary>
    /// The generate-only route has to produce the file the migrations hook would have produced, or moving
    /// between the two churns the sidecar.
    /// </summary>
    [Fact]
    public async Task TestGenerateOnlyMatchesScaffoldingOutput()
    {
        await AppDbContext.WithSqliteInMemoryAsync(db =>
        {
            var options = new GenerateOptions { Mode = Mode.OptIn };

            var scaffolded = GenerateSidecar(db, services => services.AddEfCoreGraphQL(options));
            var generateOnly = GenerateOnly(options);

            generateOnly.Should().Be(scaffolded);

            return Task.CompletedTask;
        });
    }

    [Fact]
    public void TestGenerateOnlyReadsOptionsFromDesignTimeServices()
    {
        // The fixture's IDesignTimeServices configures Mode.OptIn, so options stay declared in the one
        // place the scaffolding route already reads them from.
        var source = GenerateOnly(options: null);

        source.Should().Contain("class AuthorExtensions");
        source.Should().NotContain("class UserExtensions");
        source.Should().Contain($"namespace {SnapshotNamespace}.{GenerateOptions.DefaultNamespace};");
    }

    [Fact]
    public void TestGenerateOnlyReportsWhetherOutputChanged()
    {
        var outputDir = Directory.CreateTempSubdirectory("efcoregraphql-generate-only");

        try
        {
            var first = SidecarGenerator.Generate(typeof(DesignTimeTests).Assembly, outputDir.FullName);
            first.Should().ContainSingle().Which.Changed.Should().BeTrue();

            var second = SidecarGenerator.Generate(typeof(DesignTimeTests).Assembly, outputDir.FullName);
            second.Should().ContainSingle().Which.Changed.Should().BeFalse();

            second[0].Path.Should().Be(Path.Combine(outputDir.FullName, "AppDbContextModelSnapshot.DataLoaders.g.cs"));
            second[0].ContextType.Should().Be(typeof(AppDbContext));
        }
        finally
        {
            outputDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void TestGenerateOnlyRequiresAnOutputDirectory()
    {
        var previous = Environment.GetEnvironmentVariable(OutputDirEnvVar);
        Environment.SetEnvironmentVariable(OutputDirEnvVar, null);

        try
        {
            var generate = () => SidecarGenerator.Generate(typeof(DesignTimeTests).Assembly);

            generate.Should().Throw<InvalidOperationException>().WithMessage($"*{OutputDirEnvVar}*");
        }
        finally
        {
            Environment.SetEnvironmentVariable(OutputDirEnvVar, previous);
        }
    }

    private static string GenerateOnly(GenerateOptions? options)
    {
        var outputDir = Directory.CreateTempSubdirectory("efcoregraphql-generate-only");

        try
        {
            var results = SidecarGenerator.Generate(typeof(DesignTimeTests).Assembly, outputDir.FullName, options);

            return File.ReadAllText(results.Should().ContainSingle().Subject.Path);
        }
        finally
        {
            outputDir.Delete(recursive: true);
        }
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
