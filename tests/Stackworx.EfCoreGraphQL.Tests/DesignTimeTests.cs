namespace Stackworx.EfCoreGraphQL.Tests;

using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Design.Internal;
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

    private const string SnapshotName = "AppDbContextModelSnapshot";

    private const string SidecarFileName = SnapshotName + ".DataLoaders.g.cs";

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

    [Fact]
    public void TestASnapshotRebuiltWithoutClrTypesLeavesTheSidecarUntouched()
    {
        const string existing = "// the sidecar written for the real model";

        var sidecar = GenerateSidecar(
            BuildSnapshotModelWithoutClrTypes(),
            services => services.AddEfCoreGraphQL(),
            existingSidecar: existing);

        sidecar.Should().Be(existing);
    }

    [Fact]
    public void TestASnapshotRebuiltWithoutClrTypesIsReported()
    {
        var reporter = new RecordingOperationReporter();

        GenerateSidecar(
            BuildSnapshotModelWithoutClrTypes(),
            services => services.AddSingleton<IOperationReporter>(reporter).AddEfCoreGraphQL());

        reporter.Warnings.Should().ContainSingle()
            .Which.Should().Contain(SidecarFileName).And.Contain("migrations add");
    }

    [Fact]
    public async Task TestAJoinEntityDoesNotMakeAModelLookLikeItHasNoClrTypes()
    {
        await AppDbContext.WithSqliteInMemoryAsync(db =>
        {
            var model = db.GetService<IDesignTimeModel>().Model;

            // Post and Tag are joined by an implicit many-to-many, whose join entity is a property bag
            // just like every entity type in a snapshot rebuilt without CLR types.
            model.GetEntityTypes().Should().Contain(e => e.ClrType == typeof(Dictionary<string, object>));

            GenerateSidecar(model, services => services.AddEfCoreGraphQL())
                .Should().Contain("class PostExtensions");

            return Task.CompletedTask;
        });
    }

    private static IModel BuildSnapshotModelWithoutClrTypes()
    {
        using var db = new SnapshotOnlyDbContext();
        return db.GetService<IDesignTimeModel>().Model;
    }

    private static string? GenerateSidecar(AppDbContext db, Action<IServiceCollection> configure)
        // The snapshot generator needs the design-time model, which is what EF tooling passes it.
        => GenerateSidecar(db.GetService<IDesignTimeModel>().Model, configure);

    private static string? GenerateSidecar(
        IModel model,
        Action<IServiceCollection> configure,
        string? existingSidecar = null)
    {
        var outputDir = Directory.CreateTempSubdirectory("efcoregraphql-designtime");
        var sidecarPath = Path.Combine(outputDir.FullName, SidecarFileName);
        var previousOutputDir = Environment.GetEnvironmentVariable(OutputDirEnvVar);
        Environment.SetEnvironmentVariable(OutputDirEnvVar, outputDir.FullName);

        try
        {
            if (existingSidecar is not null)
            {
                File.WriteAllText(sidecarPath, existingSidecar);
            }

            var services = new ServiceCollection()
                .AddEntityFrameworkDesignTimeServices();
            new SqliteDesignTimeServices().ConfigureDesignTimeServices(services);
            configure(services);

            services.BuildServiceProvider()
                .GetRequiredService<IMigrationsCodeGenerator>()
                .GenerateSnapshot(SnapshotNamespace, typeof(AppDbContext), SnapshotName, model);

            return File.Exists(sidecarPath) ? File.ReadAllText(sidecarPath) : null;
        }
        finally
        {
            Environment.SetEnvironmentVariable(OutputDirEnvVar, previousOutputDir);
            outputDir.Delete(recursive: true);
        }
    }

    /// <summary>
    /// Declares its entity types by name, the way a migration's <c>BuildTargetModel</c> does, so none of
    /// them carries a CLR type. This is the model EF reverts a snapshot to on <c>migrations remove</c>.
    /// </summary>
    private sealed class SnapshotOnlyDbContext : DbContext
    {
        private const string Author = "Stackworx.EfCoreGraphQL.Tests.Data.Author";

        private const string Book = "Stackworx.EfCoreGraphQL.Tests.Data.Book";

        protected override void OnConfiguring(DbContextOptionsBuilder builder)
            => builder.UseSqlite("DataSource=:memory:");

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity(Author, b =>
            {
                b.Property<int>("Id").ValueGeneratedOnAdd();
                b.Property<string>("Name").IsRequired();
                b.HasKey("Id");
                b.ToTable("Authors");
            });

            builder.Entity(Book, b =>
            {
                b.Property<int>("Id").ValueGeneratedOnAdd();
                b.Property<int>("AuthorId");
                b.Property<string>("Title").IsRequired();
                b.HasKey("Id");
                b.HasIndex("AuthorId");
                b.ToTable("Books");
            });

            builder.Entity(Book, b => b.HasOne(Author, "Author")
                .WithMany("Books")
                .HasForeignKey("AuthorId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired());
        }
    }

    private sealed class RecordingOperationReporter : IOperationReporter
    {
        public List<string> Warnings { get; } = [];

        public void WriteError(string message)
        {
        }

        public void WriteWarning(string message) => this.Warnings.Add(message);

        public void WriteInformation(string message)
        {
        }

        public void WriteVerbose(string message)
        {
        }
    }
}
