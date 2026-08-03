namespace Stackworx.EfCoreGraphQL.Validation.Tests;

using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class OwnedEntityTests
{
    public class AppDbContext(DbContextOptions options)
        : DbContext(options)
    {
        public DbSet<Person> Persons => this.Set<Person>();
    }

    public class Person
    {
        public int Id { get; set; }

        public string Name { get; set; }
    }

    [Owned]
    public class Address
    {
        public int Id { get; set; }
        
        public string Street { get; set; }
    }

    public class Query
    {
        public IList<Person> GetPeople() => [];   
    }

    [Fact]
    public async Task Test()
    {
        // given
        var builder = WebApplication.CreateBuilder([]);

        builder.AddGraphQL()
            .AddQueryType<Query>();

        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite("Data Source=:memory:"));
        var app = builder.Build();

        var schema = await app.GetSchema();
        var model = app.GetEfCoreModel<AppDbContext>();

        // when
        var errors = EvaluateSchema
            .Evaluate(schema, model)
            .Select(e => new
            {
                EntityName = e.EntityType.ClrType.Name,
                e.FieldName,
                e.Message,
                ObjectName = e.ObjectType.Name,
            });

        // then
        errors.Should().BeEmpty();
    }
}