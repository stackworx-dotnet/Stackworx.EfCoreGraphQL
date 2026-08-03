namespace Stackworx.EfCoreGraphQL.Tests.Data;

public class Book
{
    public int Id { get; set; }
    public required string Title { get; set; }

    // explicit FK
    public int AuthorId { get; set; }

    public Author Author { get; set; } = null!;

    public List<Revision<Book>> Revisions { get; set; } = [];
}