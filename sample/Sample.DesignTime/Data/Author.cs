namespace Sample.DesignTime.Data;

public class Author
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;

    public List<Book> Books { get; set; } = [];
}
