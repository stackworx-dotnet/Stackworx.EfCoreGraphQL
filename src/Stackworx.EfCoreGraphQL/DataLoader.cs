namespace Stackworx.EfCoreGraphQL;

using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

public record DataLoader
{
    public required string LoaderName { get; init; }

    public required string EntityType { get; init; }

    public required Type KeyType { get; init; }

    public required Type DbContextType { get; init; }

    public required string ReferenceField { get; init; }
    
    public required bool IsShadowProperty { get; init; }

    public bool Nullable { get; init; }

    public DataLoaderType Type { get; init; }

    public string? Notes { get; set; }

    public enum DataLoaderType
    {
        OneToOne,
        OneToMany,
        ManyToMany,
    }

    public static DataLoader FromEntity(Type dbContextClass, IEntityType entityType)
    {
        var pk = entityType.FindPrimaryKey()
                 ?? throw new NotSupportedException($"Entity '{entityType.Name}' has no primary key.");

        if (pk.Properties.Count != 1)
        {
            throw new NotSupportedException($"Composite primary keys are not supported for '{entityType.Name}'.");
        }

        var pkProp = pk.Properties.Single();
        var keyType = pkProp.ClrType;
        var keyPropName = pkProp.Name;

        return new DataLoader
        {
            LoaderName = LoaderNames.BatchLoaderName(entityType, pkProp),
            Nullable = false,
            Type = DataLoader.DataLoaderType.OneToOne,
            KeyType = keyType,
            ReferenceField = keyPropName,
            IsShadowProperty = pkProp.IsShadowProperty(),
            EntityType = TypeUtils.GetNestedQualifiedName(entityType.ClrType),
            DbContextType = dbContextClass,
            Notes = $"Primary Key Data Loader for <see cref=\"{TypeUtils.GetNestedQualifiedName(entityType.ClrType)}\"/>",
        };
    }
    
    public static DataLoader FromNavigation(Type dbContextClass, INavigation nav)
    {
        var fk = nav.ForeignKey;
        IProperty prop;

        // Single-key only (per your instruction to ignore composites)
        if (nav.DeclaringType == fk.PrincipalEntityType)
        {
            prop = fk.Properties.Single();
        }
        else
        {
            prop = fk.PrincipalKey.Properties.Single();
        }
        
        var keyType = prop.ClrType;
        if (TypeUtils.TryUnwrapNullable(keyType, out var inner))
        {
            keyType = inner;
        }

        var type = nav.IsCollection
            ? DataLoader.DataLoaderType.OneToMany
            : DataLoader.DataLoaderType.OneToOne;

        var nullable = !fk.IsRequired;
        var entityType = nav.TargetEntityType;

        return new DataLoader
        {
            // TODO: what about different objects mapped to the same name?
            // TODO: provide an override
            LoaderName = nav.IsCollection
                ? LoaderNames.GroupLoaderName(nav.TargetEntityType, prop)
                : LoaderNames.BatchLoaderName(nav.TargetEntityType, prop),
            Nullable = nullable,
            Type = type,
            KeyType = keyType,
            ReferenceField = prop.Name,
            IsShadowProperty = prop.IsShadowProperty(),
            EntityType = TypeUtils.GetNestedQualifiedName(entityType.ClrType),
            DbContextType = dbContextClass,
            Notes = $"Navigation Data Loader for <see cref=\"{TypeUtils.GetNestedQualifiedName(entityType.ClrType)}.{nav.Inverse?.Name}\"/>",
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

    public string Emit(int version)
    {
        var sb = new StringBuilder();
        var keyType = TypeUtils.GetNestedQualifiedName(this.KeyType);

        sb.AppendLine($"    [DataLoader]");
        
        switch (this.Type)
        {
            case DataLoaderType.OneToMany:
            {
                sb.AppendLine(
                    $"    public static async Task<ILookup<{keyType}, {this.EntityType}>> {this.LoaderName}(");
                sb.AppendLine($"        IReadOnlyList<{keyType}> keys,");
                sb.AppendLine($"        {TypeUtils.CsDisplay(this.DbContextType)} context,");
                sb.AppendLine($"        CancellationToken ct)");
                sb.AppendLine("    {");


                if (this.IsShadowProperty)
                {
                    sb.AppendLine($"        throw new ApplicationException(\"{this.ReferenceField} is a Shadow Property\");");
                    sb.AppendLine($"        /*");
                }
                
                sb.AppendLine($"        var items = await context.Set<{this.EntityType}>()");
                sb.AppendLine($"            .AsNoTracking()");
            
                if (this.Nullable)
                {
                    sb.AppendLine($"            .Where(e => keys.Contains(e.{this.ReferenceField}!.Value))");
                }
                else
                {
                    sb.AppendLine($"            .Where(e => keys.Contains(e.{this.ReferenceField}))");
                }

                sb.AppendLine($"            .ToListAsync(ct);");
                sb.AppendLine();

                if (this.Nullable)
                {
                    sb.AppendLine($"        return items.ToLookup(e => e.{this.ReferenceField}!.Value);");
                }
                else
                {
                    sb.AppendLine($"        return items.ToLookup(e => e.{this.ReferenceField});");
                }
                
                if (this.IsShadowProperty)
                {
                    sb.AppendLine($"        */");
                }

                sb.AppendLine("    }");
                break;
            }
            case DataLoaderType.ManyToMany:
            {
                throw new NotImplementedException("Use ManyToMany");
            }
            // One to One
            default:
            {
                // HotChocolate 13 only works with Dictionary not IDictionary
                if (version <= 13) {
                    sb.AppendLine(
                        $"    public static async Task<Dictionary<{keyType}, {this.EntityType}>> {this.LoaderName}(");
                } else
                {
                    sb.AppendLine(
                        $"    public static async Task<IDictionary<{keyType}, {this.EntityType}>> {this.LoaderName}(");
                }

                sb.AppendLine($"        IReadOnlyList<{keyType}> keys,");
                sb.AppendLine($"        {TypeUtils.CsDisplay(this.DbContextType)} context,");
                sb.AppendLine($"        CancellationToken ct)");
                sb.AppendLine("    {");
                
                sb.AppendLine($"        return await context.Set<{this.EntityType}>()");
                sb.AppendLine($"            .AsNoTracking()");

                if (this.Nullable)
                {
                    sb.AppendLine($"            .Where(e => keys.Contains(e.{this.ReferenceField}!.Value))");
                    sb.AppendLine($"            .ToDictionaryAsync(e => e.{this.ReferenceField}!.Value, ct);");
                }
                else
                {
                    sb.AppendLine($"            .Where(e => keys.Contains(e.{this.ReferenceField}))");
                    sb.AppendLine($"            .ToDictionaryAsync(e => e.{this.ReferenceField}, ct);");
                }

                sb.AppendLine("    }");
                break;
            }
        }

        return sb.ToString();
    }
}