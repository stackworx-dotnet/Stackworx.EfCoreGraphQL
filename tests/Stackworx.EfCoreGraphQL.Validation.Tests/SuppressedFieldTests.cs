namespace Stackworx.EfCoreGraphQL.Validation.Tests;

using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

/// <summary>
/// Two <c>ExtendObjectType</c> classes on one entity: a hand-written one hiding fields, and the generated
/// one resolving a navigation the hand-written one hides.
/// </summary>
/// <remarks>
/// HotChocolate applies <c>IgnoreFields</c> while merging that extension, against the fields present at
/// that moment, and a later extension re-adds what it resolves. Whichever merges last therefore wins, and
/// merge order is registration order — so the same pair of classes produces a schema with or without the
/// field depending only on the order they were added in.
/// </remarks>
public class SuppressedFieldTests
{
    public class AppDbContext(DbContextOptions options)
        : DbContext(options)
    {
        public DbSet<Party> Parties => this.Set<Party>();

        public DbSet<ApplicationUser> Users => this.Set<ApplicationUser>();
    }

    public class Party
    {
        public int Id { get; set; }

        public string Name { get; set; } = null!;
    }

    public class ApplicationUser
    {
        public int Id { get; set; }

        public string UserName { get; set; } = null!;

        public string PasswordHash { get; set; } = null!;

        public int PartyId { get; set; }

        [ForeignKey(nameof(PartyId))]
        public Party Party { get; set; } = null!;
    }

    [ExtendObjectType<ApplicationUser>(IgnoreFields = ["passwordHash", "party"])]
    public static class HandWrittenExtensions;

    [ExtendObjectType<ApplicationUser>]
    public static class GeneratedExtensions
    {
        public static Task<Party> GetPartyAsync([Parent] ApplicationUser parent)
            => Task.FromResult(new Party { Id = parent.PartyId, Name = "resolved" });
    }

    public class Query
    {
        public IList<ApplicationUser> GetUsers() => [];
    }

    [Fact]
    public async Task TestIgnoreFieldsOnAnotherExtensionMergingLastRemovesTheGeneratedField()
    {
        var (schema, model) = await BuildAsync(typeof(GeneratedExtensions), typeof(HandWrittenExtensions));

        FieldNames(schema).Should().NotContain("party");

        var errors = EvaluateSchema.Evaluate(schema, model);

        errors.Should().ContainSingle()
            .Which.Should().Match<EvaluateSchema.Error>(e =>
                e.Kind == EvaluateSchema.ErrorKind.SuppressedField
                && e.FieldName == "party"
                && e.Field == null
                && e.ObjectType.Name == "ApplicationUser");

        errors[0].Message.Should().Contain("is dead");
    }

    [Fact]
    public async Task TestGeneratedExtensionMergingLastKeepsTheField()
    {
        var (schema, model) = await BuildAsync(typeof(HandWrittenExtensions), typeof(GeneratedExtensions));

        // The generated extension re-adds what the hand-written one removed, so the ignore only wins when
        // it merges after.
        FieldNames(schema).Should().Contain("party");

        EvaluateSchema.Evaluate(schema, model).Should().BeEmpty();
    }

    [Fact]
    public async Task TestForeignKeyIgnoreIsUnaffectedByMergeOrder()
    {
        // partyId is a field of the base type, so no extension re-adds it and every order removes it.
        // Only fields an extension contributes are order-sensitive.
        FieldNames((await BuildAsync(typeof(GeneratedExtensions), typeof(HandWrittenFkIgnore))).Schema)
            .Should().NotContain("partyId");

        FieldNames((await BuildAsync(typeof(HandWrittenFkIgnore), typeof(GeneratedExtensions))).Schema)
            .Should().NotContain("partyId");
    }

    [Fact]
    public async Task TestNavigationIgnoredOnTheEntityIsNotReported()
    {
        // [GraphQLIgnore] on the navigation stops generation, so an absent field is what was asked for.
        var (schema, model) = await BuildAsync<IgnoredNavigationContext, IgnoredNavigationContext.Query>();

        FieldNames(schema).Should().NotContain("party");

        EvaluateSchema.Evaluate(schema, model).Should().BeEmpty();
    }

    [Fact]
    public async Task TestFilteredEntityIsNotReported()
    {
        // An entity the generation filter excludes has no generated resolver to be dead.
        var (schema, model) = await BuildAsync(typeof(GeneratedExtensions), typeof(HandWrittenExtensions));

        EvaluateSchema
            .Evaluate(schema, model, Abstractions.Mode.OptOut, filter: e => e.ClrType == typeof(ApplicationUser))
            .Should().BeEmpty();
    }

    [Fact]
    public async Task TestOptInReportsOnlyIncludedEntities()
    {
        // Nothing is marked [EFCoreGraphQLInclude], so Mode.OptIn generates nothing and there is nothing
        // to be suppressed.
        var (schema, model) = await BuildAsync(typeof(GeneratedExtensions), typeof(HandWrittenExtensions));

        EvaluateSchema.Evaluate(schema, model, Abstractions.Mode.OptIn).Should().BeEmpty();
    }

    [ExtendObjectType<ApplicationUser>(IgnoreFields = ["partyId"])]
    public static class HandWrittenFkIgnore;

    public class IgnoredNavigationContext(DbContextOptions options)
        : DbContext(options)
    {
        public DbSet<Party> Parties => this.Set<Party>();

        public DbSet<User> Users => this.Set<User>();

        public class User
        {
            public int Id { get; set; }

            public int PartyId { get; set; }

            [GraphQLIgnore]
            [ForeignKey(nameof(PartyId))]
            public Party Party { get; set; } = null!;
        }

        public class Query
        {
            public IList<User> GetUsers() => [];
        }
    }

    private static IEnumerable<string> FieldNames(ISchema schema)
        => schema.Types.OfType<ObjectType>()
            .Single(t => t.Name is "ApplicationUser" or "User")
            .Fields
            .Select(f => f.Name);

    private static Task<(ISchema Schema, Microsoft.EntityFrameworkCore.Metadata.IModel Model)> BuildAsync(
        params Type[] extensionsInRegistrationOrder)
        => BuildAsync<AppDbContext, Query>(extensionsInRegistrationOrder);

    private static async Task<(ISchema Schema, Microsoft.EntityFrameworkCore.Metadata.IModel Model)> BuildAsync<TContext, TQuery>(
        params Type[] extensionsInRegistrationOrder)
        where TContext : DbContext
        where TQuery : class
    {
        var builder = WebApplication.CreateBuilder([]);

        var graphQL = builder.AddGraphQL();
        foreach (var extension in extensionsInRegistrationOrder)
        {
            graphQL.AddTypeExtension(extension);
        }

        graphQL.AddQueryType<TQuery>();

        builder.Services.AddDbContext<TContext>(options => options.UseSqlite("Data Source=:memory:"));
        var app = builder.Build();

        return (await app.GetSchema(), app.GetEfCoreModel<TContext>());
    }
}
