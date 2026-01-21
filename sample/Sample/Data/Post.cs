namespace Stackworx.EfCoreGraphQL.Tests.Data;

public class Post
{
    public int Id { get; set; }
    public required string Title { get; set; }

    public List<Tag> Tags { get; set; } = new();

    public List<Tag> TagsShadow { get; set; } = new();
    
    public List<Comment> Comments { get; set; } = new();
}