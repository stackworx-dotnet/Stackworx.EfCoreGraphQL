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

The symbols live in four different namespaces:

```csharp
using Microsoft.Extensions.DependencyInjection;    // IServiceCollection
using Stackworx.EfCoreGraphQL;                     // EntityTypeFilters, GenerateOptions
using Stackworx.EfCoreGraphQL.Abstractions;        // Mode, EFCoreGraphQLIncludeAttribute
using Stackworx.EfCoreGraphQL.DesignTime;          // AddEfCoreGraphQL

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

Adopting into a schema that already hides fields by hand has one trap worth reading before you start: a
pre-existing `ExtendObjectType(IgnoreFields = [...])` can delete a generated field with no warning and no
schema diff. See [A pre-existing `IgnoreFields` can silently delete a generated
field](https://github.com/stackworx-dotnet/Stackworx.EfCoreGraphQL#a-pre-existing-ignorefields-can-silently-delete-a-generated-field).

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

## 6) Regenerate without a migration

An annotation-only change doesn't move the EF model, so scaffolding a migration to pick it up produces one
with an empty `Up`/`Down`. `SidecarGenerator` writes the same file without touching migration history:

```csharp
using Stackworx.EfCoreGraphQL.DesignTime;

if (args.Contains("--generate-dataloaders"))
{
    foreach (var result in SidecarGenerator.Generate(typeof(Program).Assembly))
    {
        Console.WriteLine($"{(result.Changed ? "updated" : "unchanged")} {result.Path}");
    }

    return;
}
```

```zsh
dotnet run --project ./src/Your.Api.Project -- --generate-dataloaders
```

It reads the output name and namespace from the `ModelSnapshot`, the model from your
`IDesignTimeDbContextFactory<TContext>`, and the options from your `IDesignTimeServices` — so nothing is
configured twice, and both routes write identical bytes. `Changed` is the up-to-date check for CI.

Full detail, including the caveats:
[Regenerate without a migration](https://github.com/stackworx-dotnet/Stackworx.EfCoreGraphQL#6-regenerate-without-a-migration).
