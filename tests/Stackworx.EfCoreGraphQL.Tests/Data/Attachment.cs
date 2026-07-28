namespace Stackworx.EfCoreGraphQL.Tests.Data;

public class Attachment
{
    public int Id { get; set; }

    public required string FileName { get; set; }

    // The foreign key really is a shadow property: there is no CommentId member on this type.
    public Comment? Comment { get; set; }
}
