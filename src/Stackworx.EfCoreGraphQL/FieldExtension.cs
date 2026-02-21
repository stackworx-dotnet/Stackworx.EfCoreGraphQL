namespace Stackworx.EfCoreGraphQL;

using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

public record FieldExtension
{
    public required Type ParentType { get; init; }
    
    public required Type ChildType { get; init; }
    
    public required bool ChildTypeNullable { get; init; }
    
    public required string NavigationName { get; init; }
    
    public required string ReferenceField { get; init; }
    
    public required bool ReferenceFieldNullable { get; init; }

    public required Type DbContextType { get; init; }
    
    // TODO: Data Loader?
    public required string LoaderName { get; init; }
    
    public bool Collection { get; init; }
    
    public string? Notes { get; set; }

    public required bool IsShadowProperty { get; init; }
    
    public static FieldExtension FromNavigation(Type dbContextClass, INavigation nav)
    {
        var declaringType = nav.DeclaringEntityType.ClrType;
        var targetType = nav.TargetEntityType.ClrType;

        // We expect only a single property
        var fk = nav.ForeignKey;
        var notes =
            $"GraphQL Field Override for <see cref=\"{TypeUtils.GetNestedQualifiedName(nav.DeclaringEntityType.ClrType)}.{nav.Name}\"/>";

        var isCollection = nav.IsCollection;
        IProperty prop;
        bool childTypeNullable;

        // Build the loader name
        string loaderName;
        if (isCollection)
        {
            // No reason for GroupedDataLoader to return null in the list
            childTypeNullable = false;
            
            var parentProp = fk.PrincipalKey.Properties.Single();
            var childProp = fk.Properties.Single();
            var loaderProp = nav.DeclaringType == fk.PrincipalEntityType ? childProp : parentProp;
            
            prop = nav.DeclaringType == fk.PrincipalEntityType ? parentProp : childProp;
            loaderName = $"I{LoaderNames.GroupLoaderName(nav.TargetEntityType    , loaderProp)}DataLoader";
        }
        else
        {
            childTypeNullable = !nav.ForeignKey.IsRequired;
            
            var parentProp = fk.PrincipalKey.Properties.Single();
            var childProp = fk.Properties.Single();

            if (nav.IsOnDependent)
            {
                prop = childProp;
                loaderName = $"I{LoaderNames.BatchLoaderName(nav.TargetEntityType, parentProp)}DataLoader";
            }
            else
            {
                prop = parentProp;
                loaderName = $"I{LoaderNames.BatchLoaderName(nav.TargetEntityType, childProp)}DataLoader";
            }
        }

        return new FieldExtension
        {
            ParentType = declaringType,
            ChildType = targetType,
            ChildTypeNullable = childTypeNullable,
            NavigationName = nav.Name,
            ReferenceField = prop.Name,
            ReferenceFieldNullable = prop.IsNullable,
            IsShadowProperty = prop.IsShadowProperty(),
            DbContextType = dbContextClass,
            LoaderName = loaderName,
            Collection = nav.IsCollection,
            Notes = notes,
        };
    }

    public string EmitComment()
    {
        var sb = new StringBuilder();
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// {this.Notes}");
        sb.AppendLine($"    /// </summary>");
        return sb.ToString();
    }
    
    public string Emit()
    {
        var sb = new StringBuilder();
        var parentType = TypeUtils.GetNestedQualifiedName(this.ParentType);
        var childType = TypeUtils.GetNestedQualifiedName(this.ChildType);

        if (this.ChildTypeNullable)
        {
            childType += "?";
        }

        if (this.Collection) // GroupLoader
        {
            sb.AppendLine(
                $"    public static async Task<IList<{childType}>> Get{this.NavigationName}Async(");
            sb.AppendLine($"        [Parent] {parentType} parent,");
            sb.AppendLine($"        {this.LoaderName} loader,");
            sb.AppendLine($"        CancellationToken ct)");
            sb.AppendLine("    {");
            sb.AppendLine($"        return await loader.LoadAsync(parent.{this.ReferenceField}, ct);");
            sb.AppendLine("    }");
        }
        else // Batch Loader
        {
            sb.AppendLine(
                $"    public static async Task<{childType}> Get{this.NavigationName}Async(");
            sb.AppendLine($"        [Parent] {parentType} parent,");
            sb.AppendLine($"        {this.LoaderName} loader,");
            sb.AppendLine($"        CancellationToken ct)");
            sb.AppendLine("    {");
            
            if (this.IsShadowProperty)
            {
                sb.AppendLine($"        throw new ApplicationException(\"{this.ReferenceField} is a Shadow Property\");");
                sb.AppendLine($"        /*");
            }

            if (this.ReferenceFieldNullable)
            {
                sb.AppendLine($"        if (parent.{this.ReferenceField} is not null)");
                sb.AppendLine($"        {{");
                // TODO: this wont always be appropriate
                sb.AppendLine($"            return await loader.LoadAsync(parent.{this.ReferenceField}.Value, ct);");
                sb.AppendLine($"        }}");
                sb.AppendLine();
                sb.AppendLine($"        return null;");
            }
            else
            {
                sb.AppendLine($"        return await loader.LoadAsync(parent.{this.ReferenceField}, ct);");
            }

            if (this.IsShadowProperty)
            {
                sb.AppendLine($"        */");
            }
            
            sb.AppendLine("    }");
        }

        return sb.ToString();
    }
}