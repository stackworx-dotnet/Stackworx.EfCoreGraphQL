using Microsoft.EntityFrameworkCore;
using Sample.DesignTime;
using Sample.DesignTime.Data;
using Sample.DesignTime.Types;

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
    // .AddSam()
    .InitializeOnStartup();

var app = builder.Build();

app.MapGraphQL();
app.MapGet("/", () => Results.Redirect("/graphql"));

await app.SeedAsync();

app.Run();
