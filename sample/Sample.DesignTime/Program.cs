using Microsoft.EntityFrameworkCore;
using Sample.DesignTime;
using Sample.DesignTime.Data;
using Sample.DesignTime.Types;
using Stackworx.EfCoreGraphQL.DesignTime;

// Regenerates the sidecar without scaffolding a migration, for changes the EF model does not see
// (adding or removing [EFCoreGraphQLInclude], say). See the README.
if (args.Contains("--generate-dataloaders"))
{
    foreach (var result in SidecarGenerator.Generate(typeof(Program).Assembly))
    {
        Console.WriteLine($"{(result.Changed ? "updated" : "unchanged")} {result.Path}");
    }

    return;
}

var builder = WebApplication.CreateBuilder();

builder.Services.AddDbContextFactory<AppDbContext>(opts =>
{
    // opts.UseSqlite("DataSource=:memory:");
    opts.UseSqlite("DataSource=db/app.db");
});

builder
    .AddGraphQL()
    .AddQueryType<Query>()
    .RegisterDbContextFactory<AppDbContext>()
    .AddDesignTimeTypes()
    .InitializeOnStartup();

var app = builder.Build();

app.MapGraphQL();
app.MapGet("/", () => Results.Redirect("/graphql"));

await app.SeedAsync();

app.Run();
