namespace Stackworx.EfCoreGraphQL.Tests.Data;

using Stackworx.EfCoreGraphQL.Abstractions;

// The only entity opted in, so OptIn mode has something to emit and something to leave out.
[EFCoreGraphQLInclude]
public class Author
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public List<Book> Books { get; set; } = new();
}