// ReSharper disable once CheckNamespace
namespace HotChocolate;

using Microsoft.EntityFrameworkCore.Metadata;
using Stackworx.EfCoreGraphQL.Abstractions;
using Stackworx.EfCoreGraphQL.Validation;
using Xunit;

public static class SchemaExtensions
{
    public static void ValidateDbContext(
        this ISchema schema,
        IModel model,
        Mode mode = Mode.OptOut,
        params IEntityType[] entityTypesToIgnore)
        => schema.ValidateDbContext(model, mode, filter: null, entityTypesToIgnore);

    /// <param name="filter">
    /// The <c>GenerateOptions.Filter</c> generation ran with. Entities it excludes have nothing generated
    /// for them, so passing it keeps validation from reporting their fields as suppressed.
    /// </param>
    public static void ValidateDbContext(
        this ISchema schema,
        IModel model,
        Mode mode,
        Func<IEntityType, bool>? filter,
        params IEntityType[] entityTypesToIgnore)
    {
        var groupedErrors = EvaluateSchema
            .Evaluate(schema, model, mode, filter)
            .GroupBy(e => e.EntityType)
            .ToDictionary(g => g.Key, g => g.ToList());

        var actions = new List<Action>();

        foreach (var (entityType, errors) in groupedErrors)
        {
            if (entityTypesToIgnore.Contains(entityType))
            {
                continue;
            }

            actions.Add(() => Assert.Empty(
                errors.Select(e => $"GraphQL: {e.EntityType.Name}, DB: {e.ObjectType.Name}, Field: {e.FieldName}, Message: {e.Message}")));
        }

        Assert.Multiple(actions.ToArray());
    }
}
