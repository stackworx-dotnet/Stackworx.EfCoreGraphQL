# Stackworx.EfCoreGraphQL.DesignTime

Generates HotChocolate DataLoaders and field extensions from your EF Core model whenever `dotnet ef`
scaffolds a migration, by registering a custom `IMigrationsCodeGenerator`. Output is written next to the
model snapshot as `{ModelSnapshotName}.DataLoaders.g.cs`.

## 1) Reference the package

Reference it from the project EF tooling loads design-time services from (commonly your startup project).

## 2) Register the generator

EF Core tooling discovers `IDesignTimeServices` automatically:

```csharp
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.DependencyInjection;
using Stackworx.EfCoreGraphQL.DesignTime;

public sealed class DesignTimeServices : IDesignTimeServices
{
    public void ConfigureDesignTimeServices(IServiceCollection services)
        => services.AddEfCoreGraphQL();
}
```

## 3) Configure generation

`AddEfCoreGraphQL` takes a `GenerateOptions`, or an `Action<GenerateOptions>`:

```csharp
using Stackworx.EfCoreGraphQL;
using Stackworx.EfCoreGraphQL.Abstractions;

public void ConfigureDesignTimeServices(IServiceCollection services)
    => services.AddEfCoreGraphQL(options =>
    {
        // Generate only for entities marked [EFCoreGraphQLInclude].
        options.Mode = Mode.OptIn;

        // Identity types are declared by the framework, so they cannot be annotated.
        options.Filter = EntityTypeFilters.AspNetIdentity;

        // Keep foreign-key scalars in an existing schema; clients may already select them.
        options.IgnoreForeignKeyFields = false;
    });
```

| Option                   | Default            | Effect                                                                                                                  |
|--------------------------|--------------------|-------------------------------------------------------------------------------------------------------------------------|
| `Mode`                   | `Mode.OptOut`      | `OptOut` generates for every entity except those marked `[EFCoreGraphQLIgnore]`; `OptIn` only for `[EFCoreGraphQLInclude]`. |
| `Filter`                 | none               | `Func<IEntityType, bool>`; return true to exclude an entity. For types you cannot annotate.                              |
| `Namespace`              | derived            | Defaults to `{modelSnapshotNamespace}.Generated.DataLoaders`. Set it to choose your own.                                 |
| `IgnoreForeignKeyFields` | `true`             | Hides foreign-key scalar fields via `ExtendObjectType(IgnoreFields = ...)` because the navigation replaces them.         |
| `CI`                     | `false`            | Runtime-only (`DataLoaderGenerator.Generate`); ignored during scaffolding, where the model is expected to change.        |

Registering the generator directly — `services.AddSingleton<IMigrationsCodeGenerator, EfCoreMigrationsCodeGenerator>()`
— still works and uses the defaults above.

### Adopting into an existing schema

`Mode.OptIn` with `IgnoreForeignKeyFields = false` adds nothing to the schema until you mark a type, and
does not remove fields clients already select:

```csharp
options.Mode = Mode.OptIn;
options.IgnoreForeignKeyFields = false;
```

```csharp
[EFCoreGraphQLInclude]
public class Author { /* ... */ }
```

## 4) Set the output directory (required)

During scaffolding EF Core's working directory is often **not** the migrations project, so the output
directory has to be explicit:

```zsh
export STACKWORX_EFCOREGRAPHQL_SIDECAR_OUTPUT_DIR="/absolute/path/to/YourProject/Migrations"
```

```powershell
$env:STACKWORX_EFCOREGRAPHQL_SIDECAR_OUTPUT_DIR = "C:\path\to\YourProject\Migrations"
```

It must point to an existing directory. Scaffolding fails with a clear error when it is unset or wrong.

## 5) Scaffold

```zsh
dotnet ef migrations add InitialCreate \
  --project ./src/Your.Migrations.Project \
  --startup-project ./src/Your.Api.Project
```

The sidecar is rewritten on every snapshot generation, so changes that don't affect the snapshot text
(e.g. adding `[EFCoreGraphQLIgnore]`) still take effect.
