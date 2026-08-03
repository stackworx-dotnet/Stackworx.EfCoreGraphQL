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

    /// <summary>
    /// True when the key property is a nullable value type (<c>int?</c>), so its value is reached through
    /// <c>.Value</c>. A nullable reference type (<c>string?</c>) is dereferenced directly.
    /// </summary>
    public bool KeyIsNullableValueType { get; init; }

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
            KeyIsNullableValueType = TypeUtils.TryUnwrapNullable(pkProp.ClrType, out _),
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
        var keyIsNullableValueType = TypeUtils.TryUnwrapNullable(keyType, out var inner);
        if (keyIsNullableValueType)
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
            KeyIsNullableValueType = keyIsNullableValueType,
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

    /// <summary>
    /// How the key is read off an entity. A nullable value type needs <c>.Value</c>; a nullable reference
    /// type only needs the null-forgiving operator, since it has no <c>.Value</c> member.
    /// </summary>
    private string KeyAccess
    {
        get
        {
            if (!this.Nullable)
            {
                return $"e.{this.ReferenceField}";
            }

            return this.KeyIsNullableValueType
                ? $"e.{this.ReferenceField}!.Value"
                : $"e.{this.ReferenceField}!";
        }
    }

    public string Emit(int version)
    {
        var sb = new StringBuilder();
        var keyType = TypeUtils.GetNestedQualifiedName(this.KeyType);
        var keyAccess = this.KeyAccess;

        sb.AppendLine($"    [DataLoader]");
        
        switch (this.Type)
        {
            case DataLoaderType.OneToMany:
            {
                sb.AppendLine(
                    $"    public static async Task<ILookup<{keyType}, {this.EntityType}>> {this.LoaderName}(");
                sb.AppendLine($"        IReadOnlyList<{keyType}> keys,");
                sb.AppendLine($"        {TypeUtils.GetNestedQualifiedName(this.DbContextType)} context,");
                sb.AppendLine($"        CancellationToken ct)");
                sb.AppendLine("    {");


                if (this.IsShadowProperty)
                {
                    sb.AppendLine($"        throw new ApplicationException(\"{this.ReferenceField} is a Shadow Property\");");
                    sb.AppendLine($"        /*");
                }
                
                sb.AppendLine($"        var items = await context.Set<{this.EntityType}>()");
                sb.AppendLine($"            .AsNoTracking()");
            
                sb.AppendLine($"            .Where(e => keys.Contains({keyAccess}))");
                sb.AppendLine($"            .ToListAsync(ct);");
                sb.AppendLine();
                sb.AppendLine($"        return items.ToLookup(e => {keyAccess});");
                
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
                sb.AppendLine($"        {TypeUtils.GetNestedQualifiedName(this.DbContextType)} context,");
                sb.AppendLine($"        CancellationToken ct)");
                sb.AppendLine("    {");
                
                sb.AppendLine($"        return await context.Set<{this.EntityType}>()");
                sb.AppendLine($"            .AsNoTracking()");

                sb.AppendLine($"            .Where(e => keys.Contains({keyAccess}))");
                sb.AppendLine($"            .ToDictionaryAsync(e => {keyAccess}, ct);");

                sb.AppendLine("    }");
                break;
            }
        }

        return sb.ToString();
    }
}