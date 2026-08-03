namespace Stackworx.EfCoreGraphQL.Tests.Data;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// An ASP.NET Core Identity model with entities of our own alongside it, for the
/// <see cref="EntityTypeFilters.AspNetIdentity"/> tests.
/// </summary>
public class AuthDbContext : IdentityDbContext<ApplicationUser>
{
    public DbSet<Widget> Widgets => this.Set<Widget>();

    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Only the model is needed, so the connection is never opened.
    /// </summary>
    public static AuthDbContext CreateModelOnly()
        => new(new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlite("DataSource=:memory:")
            .Options);
}

/// <summary>
/// Derives from an Identity type but is declared here, so it can be annotated and is not filtered.
/// </summary>
public class ApplicationUser : IdentityUser;

public class Widget
{
    public int Id { get; set; }

    public required string Name { get; set; }
}
