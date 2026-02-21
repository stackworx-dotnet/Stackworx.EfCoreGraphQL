namespace Sample.DesignTime;

using Microsoft.EntityFrameworkCore;
using Sample.DesignTime.Data;

public static class WebApplicationExtensions
{
    public static async Task SeedAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var db = await factory.CreateDbContextAsync();

        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();

        if (await db.Authors.AnyAsync())
        {
            return;
        }

        var author = new Author { Name = "Terry Pratchett" };
        db.Authors.Add(author);
        db.Books.AddRange(
            new Book { Title = "Small Gods", Author = author },
            new Book { Title = "Guards! Guards!", Author = author });

        await db.SaveChangesAsync();
    }
}
