namespace Sample.DesignTime.Types;

using Microsoft.EntityFrameworkCore;
using Sample.DesignTime.Data;

public class Query
{
    public IQueryable<Book> GetBooks(AppDbContext dbContext)
        => dbContext.Books.AsQueryable();

    public IQueryable<Author> GetAuthors(AppDbContext dbContext)
        => dbContext.Authors.AsQueryable();
}
