namespace Stackworx.EfCoreGraphQL;

using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

public record ManyToMany
{
    public required Type DbContextType { get; init; }

    public required string ParentPropertyName { get; init; }
    
    public required string ParentKeyName { get; init; }
    
    public required Type ParentType { get; init; }
    
    public required Type ParentKeyType { get; init; }
    
    public required string ChildPropertyName { get; init; }
    
    public required Type ChildType { get; init; }
    
    public required string ChildKeyName { get; init; }
    
    public required Type ChildKeyType { get; init; }
    
    public required string LoaderName { get; init; }

    public string? LoaderNotes { get; set; }
    
    public string? FieldNotes { get; set; }
    
    public static ManyToMany FromNavigation(Type dbContextClassType, ISkipNavigation nav)
    {
        var inverse = nav.Inverse;

        // TODO: nullable?
        TypeUtils.TryUnwrapCollectionType(nav.ClrType, out var parentType);
        ArgumentNullException.ThrowIfNull(parentType);
        TypeUtils.TryUnwrapCollectionType(inverse.ClrType, out var childType);
        ArgumentNullException.ThrowIfNull(childType);

        // TODO: handle case where property not defined (shadow)
        var fieldNotes =
            $"GraphQL Field Override for <see cref=\"{TypeUtils.GetNestedQualifiedName(nav.DeclaringEntityType.ClrType)}.{nav.Name}\"/>";

        var loaderNotes =
            $"Skip Navigation Data Loader for <see cref=\"{TypeUtils.GetNestedQualifiedName(parentType)}.{nav.Inverse.Name}\"/>";

        return new ManyToMany
        {
            LoaderName = LoaderNames.GroupLoaderName(nav),
            DbContextType = dbContextClassType,
            ChildPropertyName = nav.Name,
            ChildKeyName = nav.ForeignKey.PrincipalKey.Properties.Single().Name,
            ChildKeyType = nav.ForeignKey.PrincipalKey.Properties.Single().ClrType,
            ChildType = parentType,
            ParentPropertyName = inverse.Name,
            ParentKeyName = inverse.ForeignKey.PrincipalKey.Properties.Single().Name,
            ParentKeyType = inverse.ForeignKey.PrincipalKey.Properties.Single().ClrType,
            ParentType = childType,
            FieldNotes = fieldNotes,
            LoaderNotes = loaderNotes,
        };
    }

    public string EmitDataLoader()
    {
        var sb = new StringBuilder();
        var parentKeyType = TypeUtils.GetNestedQualifiedName(this.ParentKeyType);
        var childKeyType = TypeUtils.GetNestedQualifiedName(this.ChildKeyType);
        var childType = TypeUtils.GetNestedQualifiedName(this.ChildType);
        
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// {this.LoaderNotes}");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine($"    [DataLoader]");
        
        sb.AppendLine(
            $"    public static async Task<ILookup<{childKeyType}, {childType}>> {this.LoaderName}(");
        sb.AppendLine($"        IReadOnlyList<{parentKeyType}> keys,");
        sb.AppendLine($"        {TypeUtils.CsDisplay(this.DbContextType)} context,");
        sb.AppendLine($"        CancellationToken ct)");
        sb.AppendLine("    {");
        sb.AppendLine($"        var pairs = await context.Set<{childType}>()");
            
        
        sb.AppendLine($"            .Where(e => e.{this.ParentPropertyName}.Any(p => keys.Contains(p.{this.ParentKeyName})))");
        sb.AppendLine($"            .SelectMany(child => child.{this.ParentPropertyName}.Select(parent => new {{ parent.{this.ParentKeyName}, Child = child }}))");
        sb.AppendLine($"            .AsNoTracking()");
        sb.AppendLine($"            .ToListAsync(ct);");
        sb.AppendLine();

        sb.AppendLine($"        return pairs.ToLookup(e => e.{this.ParentKeyName}, x => x.Child);");
        sb.AppendLine("    }");

        return sb.ToString();
    }
    
    public string EmitFieldExtension()
    {
        var sb = new StringBuilder();
        var parentType = TypeUtils.GetNestedQualifiedName(this.ParentType);
        var childType = TypeUtils.GetNestedQualifiedName(this.ChildType);

        // sb.AppendLine($"    // {this.Notes}");

        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// {this.FieldNotes}");
        sb.AppendLine($"    /// </summary>");
        sb.AppendLine(
            $"    public static async Task<{childType}[]> Get{this.ChildPropertyName}Async(");
        sb.AppendLine($"        [Parent] {parentType} parent,");
        sb.AppendLine($"        I{this.LoaderName}DataLoader loader,");
        sb.AppendLine($"        CancellationToken ct)");
        sb.AppendLine("    {");

        // if (this.ReferenceFieldNullable)
        // {
        //     sb.AppendLine($"        if (parent.{this.ReferenceField} is not null)");
        //     sb.AppendLine($"        {{");
        //     // TODO: this wont always be appropriate
        //     sb.AppendLine($"            return await loader.LoadAsync(parent.{this.ReferenceField}.Value, ct);");
        //     sb.AppendLine($"        }}");
        //     sb.AppendLine();
        //     sb.AppendLine($"        return null;");
        // }
        // else
        // {
            sb.AppendLine($"        return await loader.LoadAsync(parent.{this.ParentKeyName}, ct);");
        // }
            
        sb.AppendLine("    }");

        return sb.ToString();
    }

    public static bool CanGenerate(ISkipNavigation nav)
    {
        // TODO: check if the foreign key meets requirements
        return true;
    }
}