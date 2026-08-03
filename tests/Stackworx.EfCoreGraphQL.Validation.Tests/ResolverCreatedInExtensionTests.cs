namespace Stackworx.EfCoreGraphQL.Validation.Tests;

using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using HotChocolate;
using HotChocolate.Types;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

public class ResolverCreatedInExtensionTests
{
    public class AppDbContext(DbContextOptions options)
        : DbContext(options)
    {
        public DbSet<Author> Authors => this.Set<Author>();
        public DbSet<Book> Books => this.Set<Book>();
    }

    public class Author
    {
        public int Id { get; set; }

        public string Name { get; set; }
    };

    public class Book
    {
        public int Id { get; set; }

        public string Title { get; set; }

        public int AuthorId { get; set; }

        [ForeignKey(nameof(AuthorId))]
        public Author Author { get; set; } = null!;
    }

    [ExtendObjectType<Book>]
    public class BookExtensions
    {
        public Task<Author> GetAuthorAsync([Parent] Book _) => Task.FromResult(new Author());
    }

    public class Query
    {
        public IList<Book> GetBooks() => [];   
    }

    [Fact]
    public async Task Test()
    {
        // given
        var builder = WebApplication.CreateBuilder([]);

        builder.AddGraphQL()
            .AddTypeExtension<BookExtensions>()
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