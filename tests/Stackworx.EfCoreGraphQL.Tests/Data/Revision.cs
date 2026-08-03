namespace Stackworx.EfCoreGraphQL.Tests.Data;

/// <summary>
/// A generic entity type. Its reflection name is not a legal C# identifier, and its type argument has to
/// survive into the emitted <c>ExtendObjectType</c> for the attribute to bind to the right type.
/// </summary>
public class Revision<T>
    where T : class
{
    public int Id { get; set; }

    public required string Summary { get; set; }

    public int BookId { get; set; }

    public Book Book { get; set; } = null!;

    /// <summary>
    /// Not generic itself, but nested inside a generic type, so reflection reports the declaring type's
    /// type arguments as its own.
    /// </summary>
    public class Note
    {
        public int Id { get; set; }

        public required string Text { get; set; }
    }
}
