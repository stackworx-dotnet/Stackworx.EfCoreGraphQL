namespace Stackworx.EfCoreGraphQL.Tests;

using FluentAssertions;
using Stackworx.EfCoreGraphQL.Abstractions;
using Stackworx.EfCoreGraphQL.Tests.Data;

public class GenerateOptionsTests
{
    [Fact]
    public async Task TestOptInGeneratesOnlyIncludedEntities()
    {
        await AppDbContext.WithSqliteInMemoryAsync(db =>
        {
            var source = DataLoaderGenerator.GenerateString(
                db.Model,
                typeof(AppDbContext),
                new GenerateOptions { Mode = Mode.OptIn });

            source.Should().Contain("class AuthorExtensions");
            source.Should().NotContain("class UserExtensions");
            source.Should().NotContain("class BookExtensions");

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TestFilterExcludesEntities()
    {
        await AppDbContext.WithSqliteInMemoryAsync(db =>
        {
            var source = DataLoaderGenerator.GenerateString(
                db.Model,
                typeof(AppDbContext),
                new GenerateOptions { Filter = e => e.ClrType == typeof(User) });

            source.Should().NotContain("class UserExtensions");
            source.Should().Contain("class AuthorExtensions");

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TestAspNetIdentityFilterExcludesFrameworkTypesOnly()
    {
        await using var db = AuthDbContext.CreateModelOnly();

        var filtered = DataLoaderGenerator.GenerateString(
            db.Model,
            typeof(AuthDbContext),
            new GenerateOptions { Filter = EntityTypeFilters.AspNetIdentity });

        filtered.Should().NotContain("Microsoft.AspNetCore.Identity");

        // ApplicationUser derives from IdentityUser but is declared by the application, so it stays.
        filtered.Should().Contain("class ApplicationUserExtensions");
        filtered.Should().Contain("class WidgetExtensions");

        var unfiltered = DataLoaderGenerator.GenerateString(db.Model, typeof(AuthDbContext));
        unfiltered.Should().Contain("class IdentityRoleExtensions");

        // Identity is where generic entity types show up in practice, so it is also the model that
        // keeps their naming honest.
        unfiltered.Should().Contain("class IdentityUserClaimOfStringExtensions");
        unfiltered.Should().Contain(
            "[ExtendObjectType<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>(IgnoreFields = [\"userId\"])]");
        unfiltered.Should().NotContain("`");
    }

    [Fact]
    public async Task TestNamespaceDefaultsWhenUnset()
    {
        await AppDbContext.WithSqliteInMemoryAsync(db =>
        {
            DataLoaderGenerator.GenerateString(db.Model, typeof(AppDbContext))
                .Should().Contain($"namespace {GenerateOptions.DefaultNamespace};");

            DataLoaderGenerator.GenerateString(
                    db.Model,
                    typeof(AppDbContext),
                    new GenerateOptions { Namespace = "Api.Loaders" })
                .Should().Contain("namespace Api.Loaders;");

            return Task.CompletedTask;
        });
    }
}
